using NaniteConstructionSystem.Extensions;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ModAPI;
using VRage.Utils;
using VRage.Voxels;
using VRageMath;

namespace NaniteConstructionSystem.Entities.Detectors
{
    public abstract class NaniteOreDetector
    {
        public const int CELL_SIZE = 16;
        public const int VOXEL_CLAMP_BORDER_DISTANCE = 2;
        public const float RANGE_PER_UPGRADE = 50f;
        public const float POWER_PER_RANGE_UPGRADE = 0.125f;
        public const float POWER_PER_FILTER_UPGRADE = 0.1f;
        public const float POWER_PER_SCANNING_UPGRADE = 1f;
        public const float POWER_PER_POWEREFFICIENCY_UPGRADE = 0.1f;

        public const int QUERY_LOD = 1;
        public const int CELL_SIZE_IN_VOXELS_BITS = 3;
        public const int CELL_SIZE_IN_LOD_VOXELS = 1 << CELL_SIZE_IN_VOXELS_BITS;
        public const float CELL_SIZE_IN_METERS = MyVoxelConstants.VOXEL_SIZE_IN_METRES * (1 << (CELL_SIZE_IN_VOXELS_BITS + QUERY_LOD));
        public const float CELL_SIZE_IN_METERS_HALF = CELL_SIZE_IN_METERS * 0.5f;

        public float Range {
            get { return Settings.Settings.Range; }
            set
            {
                Settings.Settings.Range = value;
                Settings.Save();
            }
        }
        public List<string> OreListSelected
        {
            get
            {
                if (Settings.Settings.OreList == null)
                    Settings.Settings.OreList = new List<string>();
                return Settings.Settings.OreList;
            }
            set
            {
                Settings.Settings.OreList = value;
                Settings.Save();
            }
        }
        public bool ShowScanRadius
        {
            get { return Settings.Settings.ShowScanRadius; }
            set
            {
                Settings.Settings.ShowScanRadius = value;
                Settings.Save();
            }
        }

        private MyConcurrentDictionary<MyVoxelMaterialDefinition, List<NaniteMiningItem>> m_oreList;
        public MyConcurrentDictionary<MyVoxelMaterialDefinition, List<NaniteMiningItem>> OreList
        {
            get { return m_oreList; }
        }

        private StringBuilder m_oreListCache;
        public StringBuilder OreListCache
        {
            set { m_oreListCache = value; }
        }

        private FastResourceLock m_lock;
        public FastResourceLock Lock
        {
            get { return m_lock; }
        }

        private DateTime m_scanStart;
        private DateTime m_scanEnd;

        public TimeSpan ScanDuration
        {
            get { return m_scanEnd - m_scanStart; }
        }

        public MyOreDetectorDefinition BlockDefinition => (m_block as MyCubeBlock).BlockDefinition as MyOreDetectorDefinition;
        public MyModStorageComponentBase Storage { get; set; }

        internal MyResourceSinkInfo ResourceInfo;
        internal MyResourceSinkComponent Sink;
        private IMyOreDetector m_block;
        private bool m_busy;
        private DateTime m_lastUpdate;
        private readonly List<MyVoxelBase> m_oreInRangeCache = new List<MyVoxelBase>();
        private float _maxRange = 0f;
        public float MaxRange
        {
            get { return _maxRange; }
        }

        private float _power = 0f;
        public float Power
        {
            get { return Sink.CurrentInputByType(MyResourceDistributorComponent.ElectricityId); }
        }

        public bool HasFilterUpgrade
        {
            get { return supportFilter && m_block.UpgradeValues["Filter"] > 0f; }
        }

        protected bool supportFilter = false;
        protected int maxScanningLevel = 0;
        protected float minRange = 0f;
        protected float basePower = 0f;
        internal NaniteOreDetectorSettings Settings;


        public NaniteOreDetector(IMyFunctionalBlock entity)
        {
            m_block = entity as IMyOreDetector;
            m_busy = false;
            m_lastUpdate = DateTime.MinValue;
            m_scanStart = DateTime.MinValue;
            m_scanEnd = DateTime.MinValue;
            m_lock = new FastResourceLock();
            m_oreListCache = new StringBuilder();

            m_block.Components.TryGet(out Sink);
            ResourceInfo = new MyResourceSinkInfo()
            {
                ResourceTypeId = MyResourceDistributorComponent.ElectricityId,
                MaxRequiredInput = 0f,
                RequiredInputFunc = () => (m_block.Enabled && m_block.IsFunctional) ? _power : 0f
            };
            Sink.RemoveType(ref ResourceInfo.ResourceTypeId);
            Sink.Init(MyStringHash.GetOrCompute("Utility"), ResourceInfo);
            Sink.AddType(ref ResourceInfo);
        }

        public void Init()
        {
            StorageSetup();
            m_block.UpgradeValues.Add("Range", 0f);
            m_block.UpgradeValues.Add("Scanning", 0f);
            m_block.UpgradeValues.Add("Filter", 0f);
            m_block.UpgradeValues.Add("PowerEfficiency", 0f);

            m_block.BroadcastUsingAntennas = false;

            m_block.OnUpgradeValuesChanged += UpdatePower;
            m_block.EnabledChanged += EnabledChanged;
            UpdatePower();

            if (Sync.IsClient)
                MessageHub.SendMessageToServer(new MessageOreDetectorSettings()
                {
                    EntityId = m_block.EntityId
                });
        }

        public void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder sb)
        {
            sb.Append("Type: Nanite Ore Detector\n");
            sb.Append($"Current Input: {Power} MW\n");
            sb.Append($"Frequency:\n");
            foreach (var freq in GetScanningFrequencies())
                sb.Append($" - [{freq}]\n");
            sb.Append($"Range: {Range}"); // TODO remove debug only
            sb.Append(m_oreListCache);
        }

        public void DrawScanningSphere()
        {
            if (Sync.IsDedicated)
                return;

            // Client is not synced yet
            if (Sync.IsClient && NaniteConstructionManager.Settings == null)
                return;

            if (!ShowScanRadius || !m_block.Enabled || !m_block.IsFunctional || !Sink.IsPoweredByType(MyResourceDistributorComponent.ElectricityId))
                return;

            var matrix = m_block.PositionComp.WorldMatrix;
            Color color = Color.LightGoldenrodYellow;
            MySimpleObjectDraw.DrawTransparentSphere(ref matrix, Range, ref color, MySimpleObjectRasterizer.SolidAndWireframe, 20);
        }

        public void Update100()
        {
            DateTime start = DateTime.Now;

            if (Sync.IsServer && !m_busy && (m_oreList == null || DateTime.Now - m_lastUpdate > TimeSpan.FromSeconds(15)))
            {
                m_lastUpdate = DateTime.Now;
                m_busy = true;
                MyAPIGateway.Parallel.StartBackground(ScanVoxelWork, ScanVoxelComplete);
            }

            Logging.Instance.WriteLine($"Update100 {m_block.EntityId}: {(DateTime.Now - start).TotalMilliseconds}ms");
        }

        private void EnabledChanged(IMyTerminalBlock obj)
        {
            UpdatePower();
        }

        private void UpdatePower()
        {
            if (!m_block.Enabled || !m_block.IsFunctional)
            {
                Sink.Update();
                return;
            }

            float upgradeRangeAddition = 0f;
            float upgradeRangeMultiplicator = 1;
            for (int i = 1; i <= (int)m_block.UpgradeValues["Range"]; i++)
            {
                upgradeRangeAddition += RANGE_PER_UPGRADE * upgradeRangeMultiplicator;

                if (upgradeRangeMultiplicator == 1f)
                    upgradeRangeMultiplicator = 0.7f;
                else if (upgradeRangeMultiplicator > 0f)
                    upgradeRangeMultiplicator -= 0.1f;
            }
            _maxRange = minRange + upgradeRangeAddition;
            if (Range > _maxRange)
                Range = _maxRange;

            _power = basePower;
            _power += m_block.UpgradeValues["Range"] * POWER_PER_RANGE_UPGRADE;
            _power += m_block.UpgradeValues["Filter"] * POWER_PER_FILTER_UPGRADE;
            _power *= 1 + (m_block.UpgradeValues["Scanning"] * POWER_PER_SCANNING_UPGRADE);
            _power *= 1 - (m_block.UpgradeValues["PowerEfficiency"] * POWER_PER_POWEREFFICIENCY_UPGRADE);

            if (NaniteConstructionManager.Settings != null)
                _power *= NaniteConstructionManager.Settings.OreDetectorPowerMultiplicator;

            Sink.Update();

            Logging.Instance.WriteLine($"Updated power {_power}");
        }

        public List<string> GetScanningFrequencies()
        {
            List<string> frequencies = new List<string>();
            if (m_block.UpgradeValues["Scanning"] >= 2f)
                frequencies.Add("8kHz-2MHz");
            if (m_block.UpgradeValues["Scanning"] >= 1f)
                frequencies.Add("15MHz-40MHz");

            frequencies.Add("75MHz-310MHz");

            return frequencies;
        }

        public List<MyTerminalControlListBoxItem> GetOreList()
        {
            List<MyTerminalControlListBoxItem> list = new List<MyTerminalControlListBoxItem>();
            foreach (var item in MyDefinitionManager.Static.GetVoxelMaterialDefinitions().Select(x => x.MinedOre).Distinct())
            {
                MyStringId stringId = MyStringId.GetOrCompute(item);

                // Filter upgrade
                if (m_block.UpgradeValues["Scanning"] < 1f && (stringId.String == "Uranium" || stringId.String == "Platinum" || stringId.String == "Silver" || stringId.String == "Gold"))
                    continue;
                if (m_block.UpgradeValues["Scanning"] < 2f && (stringId.String == "Uranium" || stringId.String == "Platinum"))
                    continue;

                MyTerminalControlListBoxItem listItem = new MyTerminalControlListBoxItem(stringId, stringId, null);
                list.Add(listItem);
            }
            return list;
        }

        private void StorageSetup()
        {
            if (Settings == null)
                Settings = new NaniteOreDetectorSettings(m_block, minRange);

            Settings.Load();
        }

        struct MaterialPositionData
        {
            public Vector3 Sum;
            public int Count;
        }

        private void ScanVoxelWork()
        {
            m_scanEnd = DateTime.MinValue;
            m_scanStart = DateTime.Now;

            if (!m_block.Enabled || !m_block.IsFunctional || !Sink.IsPoweredByType(MyResourceDistributorComponent.ElectricityId))
                return;

            try
            {
                m_oreInRangeCache.Clear();
                Vector3D position = m_block.GetPosition();
                BoundingSphereD sphere = new BoundingSphereD(position, Range);
                MyGamePruningStructure.GetAllVoxelMapsInSphere(ref sphere, m_oreInRangeCache);

                var allowedOreList = new HashSet<string>(OreListSelected);

                DateTime start = DateTime.Now;
                Logging.Instance.WriteLine("MINING Hammer Start Scan");
                MyConcurrentDictionary<Vector3D, NaniteMiningItem> miningItems = new MyConcurrentDictionary<Vector3D, NaniteMiningItem>(100000);
                MyAPIGateway.Parallel.ForEach(m_oreInRangeCache, (voxelMap) =>
                {
                    Logging.Instance.WriteLine($"Item: {voxelMap.GetType().Name}:{voxelMap.EntityId}");

                    if (!(voxelMap is IMyVoxelMap))
                        return;

                    Vector3I minCorner, maxCorner;
                    {
                        var worldMin = sphere.Center - sphere.Radius - MyVoxelConstants.VOXEL_SIZE_IN_METRES;
                        var worldMax = sphere.Center + sphere.Radius + MyVoxelConstants.VOXEL_SIZE_IN_METRES;
                        MyVoxelCoordSystems.WorldPositionToVoxelCoord(voxelMap.PositionLeftBottomCorner, ref worldMin, out minCorner);
                        MyVoxelCoordSystems.WorldPositionToVoxelCoord(voxelMap.PositionLeftBottomCorner, ref worldMax, out maxCorner);
                        minCorner += voxelMap.StorageMin;
                        maxCorner += voxelMap.StorageMin;
                    }
                    Logging.Instance.WriteLine($"asd: {minCorner} {maxCorner}");

                    var m_cache = new MyStorageData();

                    var cacheMin = minCorner - 1;
                    var cacheMax = maxCorner + 1;

                    //bool bRareOnly = true;
                    //if (allowedOreList != null && allowedOreList.Contains("Stone"))
                    //    bRareOnly = false;

                    m_cache.Resize(cacheMin, cacheMax);
                    m_cache.ClearContent(0);
                    m_cache.ClearMaterials(0);
                    var flags = MyVoxelRequestFlags.AdviseCache;
                    voxelMap.Storage.PinAndExecute(() =>
                    {
                        voxelMap.Storage.ReadRange(m_cache, MyStorageDataTypeFlags.ContentAndMaterial, 0, cacheMin, cacheMax, ref flags);
                    });

                    MyAPIGateway.Parallel.For(minCorner.X, maxCorner.X, (x) =>
                    {
                        MyAPIGateway.Parallel.For(minCorner.Y, maxCorner.Y, (y) =>
                        {
                            MyAPIGateway.Parallel.For(minCorner.Z, maxCorner.Z, (z) =>
                            {
                                Logging.Instance.WriteLine($"dd: {x} {y} {z}");
                                Vector3I pos = new Vector3I(x,y,z);

                                // get original amount
                                var relPos = pos - cacheMin;
                                Logging.Instance.WriteLine($"relPos: {relPos}");
                                var lin = m_cache.ComputeLinear(ref relPos);
                                Logging.Instance.WriteLine($"lin: {lin}");

                                //var relPos = pos - cacheMin; // Position of voxel in local space
                                var original = m_cache.Content(lin); // Content at this position

                                if (original == MyVoxelConstants.VOXEL_CONTENT_EMPTY)
                                    return;

                                var material = m_cache.Material(lin); // Material at this position
                                Vector3D vpos;
                                MyVoxelCoordSystems.VoxelCoordToWorldPosition(voxelMap.PositionLeftBottomCorner, ref pos, out vpos);
                                if (miningItems.ContainsKey(vpos))
                                    return;

                                /*
                                var volume = shapeSphere.GetVolume(ref vpos);
                                if (volume == 0f) // Shape and voxel do not intersect at this position, so continue
                                    continue;
                                */

                                // Pull information about voxel required for later processing
                                var voxelMat = MyDefinitionManager.Static.GetVoxelMaterialDefinition(material);
                                
                                if (!HasFilterUpgrade || allowedOreList != null)
                                {
                                    // Skip if ore not in allowed ore list
                                    if (allowedOreList != null && !allowedOreList.Contains(voxelMat.MinedOre))
                                        return;

                                    NaniteMiningItem miningItem = new NaniteMiningItem();
                                    miningItem.Position = vpos;
                                    miningItem.VoxelMaterial = material;
                                    miningItem.VoxelId = voxelMap.EntityId;
                                    miningItem.Amount = original; // * 3.9f;
                                                                    //miningItem.MiningHammer = this;
                                    miningItems.Add(vpos, miningItem);
                                    //count++;
                                }
                            });
                        });
                    });
                });

                Logging.Instance.WriteLine(string.Format("MINING Hammer Read Voxel Complete: {0}ms", (DateTime.Now - start).TotalMilliseconds));

                MyConcurrentDictionary<MyVoxelMaterialDefinition, List<NaniteMiningItem>> oreList = new MyConcurrentDictionary<MyVoxelMaterialDefinition, List<NaniteMiningItem>>();
                Dictionary<Vector3D, NaniteMiningItem> oreLocations = new Dictionary<Vector3D, NaniteMiningItem>();

                MyAPIGateway.Parallel.ForEach(miningItems.Values, (item) =>
                {
                    var def = MyDefinitionManager.Static.GetVoxelMaterialDefinition(item.VoxelMaterial);
                    oreList.TryAdd(def, new List<NaniteMiningItem>());

                    //if (oreList[def].Count >= 1000)
                    //    continue;

                    oreList[def].Add(item);
                    //oreLocations.Add(item.Position, item);
                });

                m_oreListCache.Clear();
                if (oreList != null)
                {
                    foreach (var item in oreList)
                    {
                        m_oreListCache.Append(string.Format("{0} - {1:N0} Kg\r\n", item.Key.MinedOre, CalculateAmount(item.Key, item.Value.Sum(x => x.Amount))));
                    }
                }

                //using (LocationLock.AcquireExclusiveUsing())
                //m_oreLocations = oreLocations;

                using (Lock.AcquireExclusiveUsing())
                    m_oreList = oreList;

                Logging.Instance.WriteLine(string.Format("MINING Hammer Scan Complete: {0}ms ({1} groups, {2} items)", (DateTime.Now - start).TotalMilliseconds, oreList.Count, oreList.Sum(x => x.Value.Count)));
            }
            catch (Exception ex)
            {
                Logging.Instance.WriteLine(string.Format("ScanVoxelWork() Error: {0}", ex.ToString()));
            }
        }

        private void ScanVoxelComplete()
        {
            m_scanEnd = DateTime.Now;
            MessageHub.SendMessageToAllPlayers(new MessageOreDetectorScanComplete()
            {
                EntityId = m_block.EntityId,
                OreListCache = m_oreListCache.ToString(),
            });
            m_lastUpdate = DateTime.Now;
            m_busy = false;
        }

        private static float CalculateAmount(MyVoxelMaterialDefinition material, float amount)
        {
            var oreObjBuilder = VRage.ObjectBuilders.MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Ore>(material.MinedOre);
            oreObjBuilder.MaterialTypeName = material.Id.SubtypeId;
            float amountCubicMeters = (float)(((float)amount / (float)MyVoxelConstants.VOXEL_CONTENT_FULL) * MyVoxelConstants.VOXEL_VOLUME_IN_METERS * Sandbox.Game.MyDrillConstants.VOXEL_HARVEST_RATIO);
            amountCubicMeters *= (float)material.MinedOreRatio;
            var physItem = MyDefinitionManager.Static.GetPhysicalItemDefinition(oreObjBuilder);
            MyFixedPoint amountInItemCount = (MyFixedPoint)(amountCubicMeters / physItem.Volume);
            return (float)amountInItemCount;
        }
    }
}
