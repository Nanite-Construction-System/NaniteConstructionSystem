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
        public const float POWER_PER_UPGRADE = 0.125f;

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

        private Dictionary<MyVoxelMaterialDefinition, List<NaniteMiningItem>> m_oreList;
        public Dictionary<MyVoxelMaterialDefinition, List<NaniteMiningItem>> OreList
        {
            get { return m_oreList; }
        }

        private FastResourceLock m_lock;
        public FastResourceLock Lock
        {
            get { return m_lock; }
        }

        public MyOreDetectorDefinition BlockDefinition => (m_block as MyCubeBlock).BlockDefinition as MyOreDetectorDefinition;
        public MyModStorageComponentBase Storage { get; set; }

        internal MyResourceSinkInfo ResourceInfo;
        internal MyResourceSinkComponent Sink;
        static readonly MyDefinitionId gId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");
        private IMyOreDetector m_block;
        private bool m_busy;
        private DateTime m_lastUpdate;
        private StringBuilder m_oreListCache;
        private readonly List<MyVoxelBase> m_oreInRangeCache = new List<MyVoxelBase>();
        private float _maxRange = 0f;
        public float MaxRange
        {
            get { return _maxRange; }
        }

        private float _power = 0f;
        public float Power
        {
            get { return Sink.CurrentInputByType(gId); }
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
            m_lock = new FastResourceLock();
            m_oreListCache = new StringBuilder();

            m_block.Components.TryGet(out Sink);
            ResourceInfo = new MyResourceSinkInfo()
            {
                ResourceTypeId = gId,
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

            if (!ShowScanRadius || !m_block.Enabled || !m_block.IsFunctional || !Sink.IsPoweredByType(gId))
                return;

            var matrix = m_block.PositionComp.WorldMatrix;
            Color color = Color.LightGoldenrodYellow;
            MySimpleObjectDraw.DrawTransparentSphere(ref matrix, Range, ref color, MySimpleObjectRasterizer.SolidAndWireframe, 20);
        }

        public void Update100()
        {
            DateTime start = DateTime.Now;

            ScanVoxelTargets();

            //if (Sync.IsServer && !m_busy && (m_oreList == null || DateTime.Now - m_lastUpdate > TimeSpan.FromSeconds(15)))
            //{
            //    m_lastUpdate = DateTime.Now;
            //    m_busy = true;
            //    MyAPIGateway.Parallel.StartBackground(ScanVoxelWork, ScanVoxelComplete);
            //}

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
            _power += m_block.UpgradeValues["Range"] * POWER_PER_UPGRADE;
            _power += m_block.UpgradeValues["Filter"] * 0.1f;
            _power *= 1 + m_block.UpgradeValues["Scanning"];
            //_power *= m_block.UpgradeValues["PowerEfficiency"] / 10;
            //_power -= m_block.UpgradeValues["PowerEfficiency"] * 1f;

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

        private void ScanVoxelTargets()
        {
            if (!m_block.Enabled || !m_block.IsFunctional || !Sink.IsPoweredByType(gId))
                return;

            Logging.Instance.WriteLine($"Range: {Range}");

            m_oreInRangeCache.Clear();
            Vector3D position = m_block.GetPosition();
            BoundingSphereD sphere = new BoundingSphereD(position, Range);
            MyGamePruningStructure.GetAllVoxelMapsInSphere(ref sphere, m_oreInRangeCache);

            var allowedOreList = new HashSet<string>(OreListSelected);
            MyConcurrentDictionary<Vector3D, NaniteMiningItem> miningItems = new MyConcurrentDictionary<Vector3D, NaniteMiningItem>(100000);
            foreach (var voxelMap in m_oreInRangeCache)
            {
                Logging.Instance.WriteLine($"Item: {voxelMap.GetType().Name}:{voxelMap.EntityId}");

                if (!(voxelMap is IMyVoxelMap))
                    continue;

                Vector3I min, max;
                {
                    var worldMin = sphere.Center - sphere.Radius;
                    var worldMax = sphere.Center + sphere.Radius;
                    Logging.Instance.WriteLine($"worldMin: {worldMin} worldMax: {worldMax}");
                    MyVoxelCoordSystems.WorldPositionToVoxelCoord(voxelMap.PositionLeftBottomCorner, ref worldMin, out min);
                    MyVoxelCoordSystems.WorldPositionToVoxelCoord(voxelMap.PositionLeftBottomCorner, ref worldMax, out max);
                    Logging.Instance.WriteLine($"min: {min} max: {max}");
                    // mk:TODO Get rid of this computation. Might require a mechanism to figure out whether MyVoxelMap is subpart of MyPlanet or not. (Maybe third class for subparts?)
                    min += voxelMap.StorageMin;
                    max += voxelMap.StorageMin;
                    Logging.Instance.WriteLine($"min: {min} max: {max}");

                    (voxelMap as IMyVoxelMap).ClampVoxelCoord(ref min);
                    (voxelMap as IMyVoxelMap).ClampVoxelCoord(ref max);
                    Logging.Instance.WriteLine($"min: {min} max: {max}");
                    min >>= (CELL_SIZE_IN_VOXELS_BITS + QUERY_LOD);
                    max >>= (CELL_SIZE_IN_VOXELS_BITS + QUERY_LOD);
                }

                Logging.Instance.WriteLine($"min: {min} max: {max}");
            }
        }











        private void ScanVoxelWork()
        {
            if (!m_block.Enabled || !m_block.IsFunctional || !Sink.IsPoweredByType(gId))
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
                foreach (var voxelMap in m_oreInRangeCache)
                {
                    Logging.Instance.WriteLine($"Item: {voxelMap.GetType().Name}:{voxelMap.EntityId}");
                    //var direction = Vector3D.Normalize(item.GetPosition() - position);
                    var direction = Vector3D.Normalize(-m_block.PositionComp.WorldMatrix.Up);
                    //ReadVoxel(voxelMap, position + (direction * r), miningItems, allowedOreList);
                }

                Logging.Instance.WriteLine(string.Format("MINING Hammer Read Voxel Complete: {0}ms", (DateTime.Now - start).TotalMilliseconds));

                Dictionary<MyVoxelMaterialDefinition, List<NaniteMiningItem>> oreList = new Dictionary<MyVoxelMaterialDefinition, List<NaniteMiningItem>>();
                Dictionary<Vector3D, NaniteMiningItem> oreLocations = new Dictionary<Vector3D, NaniteMiningItem>();

                foreach (var item in miningItems.Values)
                {
                    var def = MyDefinitionManager.Static.GetVoxelMaterialDefinition(item.VoxelMaterial);
                    if (!oreList.ContainsKey(def))
                        oreList.Add(def, new List<NaniteMiningItem>());

                    //if (oreList[def].Count >= 1000)
                    //    continue;

                    oreList[def].Add(item);
                    //oreLocations.Add(item.Position, item);
                }

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
            m_lastUpdate = DateTime.Now;
            m_busy = false;
        }

        private void ReadVoxel(IMyVoxelBase voxel, Vector3D position, MyConcurrentDictionary<Vector3D, NaniteMiningItem> targets, HashSet<string> allowedOreList = null)
        {
            var m_cache = new MyStorageData();
            NaniteShapeSphere shapeSphere = new NaniteShapeSphere();
            shapeSphere.Center = position;
            shapeSphere.Radius = NaniteConstructionManager.Settings.MiningRadius / 2;

            //NaniteShapeCapsule shapeCapsule = new NaniteShapeCapsule();
            //shapeCapsule.A = positionA;
            //shapeCapsule.B = positionB;
            //shapeCapsule.Radius = NaniteConstructionManager.Settings.MiningRadius;

            Vector3I minCorner, maxCorner, numCells;
            GetVoxelShapeDimensions(voxel, shapeSphere, out minCorner, out maxCorner, out numCells);

            var cacheMin = minCorner - 1;
            var cacheMax = maxCorner + 1;

            //bool bRareOnly = true;
            //if (allowedOreList != null && allowedOreList.Contains("Stone"))
            //    bRareOnly = false;

            m_cache.Resize(cacheMin, cacheMax);
            m_cache.ClearContent(0);
            m_cache.ClearMaterials(0);
            var flags = MyVoxelRequestFlags.AdviseCache;
            voxel.Storage.ReadRange(m_cache, MyStorageDataTypeFlags.ContentAndMaterial, 0, cacheMin, cacheMax, ref flags);

            //voxel.Storage.PinAndExecute(() =>
            {
                Vector3I pos;
                for (pos.X = minCorner.X; pos.X <= maxCorner.X; ++pos.X)
                    for (pos.Y = minCorner.Y; pos.Y <= maxCorner.Y; ++pos.Y)
                        for (pos.Z = minCorner.Z; pos.Z <= maxCorner.Z; ++pos.Z)
                        {
                            // get original amount
                            var relPos = pos - cacheMin;
                            var lin = m_cache.ComputeLinear(ref relPos);

                            //var relPos = pos - cacheMin; // Position of voxel in local space
                            var original = m_cache.Content(lin); // Content at this position

                            if (original == MyVoxelConstants.VOXEL_CONTENT_EMPTY)
                                continue;

                            var material = m_cache.Material(lin); // Material at this position
                            Vector3D vpos;
                            MyVoxelCoordSystems.VoxelCoordToWorldPosition(voxel.PositionLeftBottomCorner, ref pos, out vpos);
                            if (targets.ContainsKey(vpos))
                                continue;

                            /*
                            var volume = shapeSphere.GetVolume(ref vpos);
                            if (volume == 0f) // Shape and voxel do not intersect at this position, so continue
                                continue;
                            */

                            // Pull information about voxel required for later processing
                            var voxelMat = MyDefinitionManager.Static.GetVoxelMaterialDefinition(material);
                            //if ((bRareOnly && voxelMat.IsRare) || !bRareOnly)
                            //if(voxelMat.IsRare)
                            if (allowedOreList != null)// || (allowedOreList == null && bRareOnly && voxelMat.IsRare))
                            {
                                if (allowedOreList != null && !allowedOreList.Contains(voxelMat.MinedOre))
                                    continue;


                                NaniteMiningItem miningItem = new NaniteMiningItem();
                                miningItem.Position = vpos;
                                miningItem.VoxelMaterial = material;
                                miningItem.VoxelId = voxel.EntityId;
                                miningItem.Amount = original; // * 3.9f;
                                //miningItem.MiningHammer = this;
                                targets.Add(vpos, miningItem);
                                //count++;
                            }

                            //m_cache.Content(lin, 0);
                            //m_cache.Material(lin, 0);
                        }


                //voxel.Storage.WriteRange(m_cache, MyStorageDataTypeFlags.ContentAndMaterial, cacheMin, cacheMax);
            };

            /*
            int count = 0;
            for (var itCells = new Vector3I_RangeIterator(ref Vector3I.Zero, ref numCells); itCells.IsValid(); itCells.MoveNext())
            {
                Vector3I cellMinCorner, cellMaxCorner;
                GetCellCorners(ref minCorner, ref maxCorner, ref itCells, out cellMinCorner, out cellMaxCorner);

                var cacheMin = cellMinCorner - 1;
                var cacheMax = cellMaxCorner + 1;
                voxel.Storage.ClampVoxel(ref cacheMin);
                voxel.Storage.ClampVoxel(ref cacheMax);

                m_cache.Resize(cacheMin, cacheMax);
                voxel.Storage.ReadRange(m_cache, MyStorageDataTypeFlags.ContentAndMaterial, 0, cacheMin, cacheMax);

                for (var it = new Vector3I_RangeIterator(ref cellMinCorner, ref cellMaxCorner); it.IsValid(); it.MoveNext())
                {
                    var relPos = it.Current - cacheMin; // Position of voxel in local space
                    var original = m_cache.Content(ref relPos); // Content at this position
                    var material = m_cache.Material(ref relPos); // Material at this position

                    if (original == MyVoxelConstants.VOXEL_CONTENT_EMPTY)
                        continue;

                    Vector3D vpos;
                    MyVoxelCoordSystems.VoxelCoordToWorldPosition(voxel.PositionLeftBottomCorner, ref it.Current, out vpos);
                    if (targets.ContainsKey(vpos))
                        continue;

                    var volume = shapeSphere.GetVolume(ref vpos);
                    if (volume == 0f) // Shape and voxel do not intersect at this position, so continue
                        continue;

                    // Pull information about voxel required for later processing
                    var voxelMat = MyDefinitionManager.Static.GetVoxelMaterialDefinition(m_cache.Material(ref relPos));
                    //if ((bRareOnly && voxelMat.IsRare) || !bRareOnly)
                    //if(voxelMat.IsRare)
                    if (allowedOreList != null)// || (allowedOreList == null && bRareOnly && voxelMat.IsRare))
                    {
                        if (allowedOreList != null && !allowedOreList.Contains(voxelMat.MinedOre))
                            continue;

                        NaniteMiningItem miningItem = new NaniteMiningItem();
                        miningItem.Position = vpos;
                        miningItem.VoxelMaterial = material;
                        miningItem.VoxelId = voxel.EntityId;
                        miningItem.Amount = original; // * 3.9f;
                        miningItem.MiningHammer = this;
                        targets.Add(vpos, miningItem);
                        count++;
                    }                   
                }                
            }
            */

            //Logging.Instance.WriteLine(string.Format("Voxels Read: {0} - {1}", voxel.GetType().Name, count));
        }

        public static bool CheckVoxelContent(long voxelId, Vector3D position)
        {
            IMyEntity entity;
            if (!MyAPIGateway.Entities.TryGetEntityById(voxelId, out entity))
                return false;

            var voxel = entity as IMyVoxelBase;
            var targetMin = position;
            var targetMax = position;
            Vector3I minVoxel, maxVoxel;
            MyVoxelCoordSystems.WorldPositionToVoxelCoord(voxel.PositionLeftBottomCorner, ref targetMin, out minVoxel);
            MyVoxelCoordSystems.WorldPositionToVoxelCoord(voxel.PositionLeftBottomCorner, ref targetMax, out maxVoxel);

            MyVoxelBase voxelBase = voxel as MyVoxelBase;
            minVoxel += voxelBase.StorageMin;
            maxVoxel += voxelBase.StorageMin;

            voxel.Storage.ClampVoxel(ref minVoxel);
            voxel.Storage.ClampVoxel(ref maxVoxel);

            MyStorageData cache = new MyStorageData();
            cache.Resize(minVoxel, maxVoxel);
            voxel.Storage.ReadRange(cache, MyStorageDataTypeFlags.Content, 0, minVoxel, maxVoxel);

            // Grab content and material
            var original = cache.Content(0);
            //var material = cache.Material(0);

            if (original == MyVoxelConstants.VOXEL_CONTENT_EMPTY)
            {
                //Logging.Instance.WriteLine(string.Format("Content is empty"));
                return false;
            }

            return true;
        }


        public abstract class NaniteShape
        {
            protected MatrixD m_transformation = MatrixD.Identity;
            protected MatrixD m_inverse = MatrixD.Identity;
            protected bool m_inverseIsDirty = false;

            public MatrixD Transformation
            {
                get { return m_transformation; }
                set
                {
                    m_transformation = value;
                    m_inverseIsDirty = true;
                }
            }

            public abstract BoundingBoxD GetWorldBoundaries();

            /// <summary>
            /// Gets volume of intersection of shape and voxel
            /// </summary>
            /// <param name="voxelPosition">Left bottom point of voxel</param>
            /// <returns>Normalized volume of intersection</returns>
            public abstract float GetVolume(ref Vector3D voxelPosition);

            /// <returns>Recomputed density value from signed distance</returns>
            protected float SignedDistanceToDensity(float signedDistance)
            {
                const float TRANSITION_SIZE = MyVoxelConstants.VOXEL_SIZE_IN_METRES;
                const float NORMALIZATION_CONSTANT = 1 / (2 * MyVoxelConstants.VOXEL_SIZE_IN_METRES);
                return MathHelper.Clamp(-signedDistance, -TRANSITION_SIZE, TRANSITION_SIZE) * NORMALIZATION_CONSTANT + 0.5f;
            }
        }

        public partial class NaniteShapeCapsule : NaniteShape
        {
            public Vector3D A;
            public Vector3D B;
            public float Radius;

            public override BoundingBoxD GetWorldBoundaries()
            {
                var bbox = new BoundingBoxD(A - Radius, B + Radius);
                return bbox.TransformSlow(Transformation);
            }

            public override float GetVolume(ref Vector3D voxelPosition)
            {
                if (m_inverseIsDirty)
                {
                    m_inverse = MatrixD.Invert(m_transformation);
                    m_inverseIsDirty = false;
                }

                voxelPosition = Vector3D.Transform(voxelPosition, m_inverse);

                var pa = voxelPosition - A;
                var ba = B - A;
                var h = MathHelper.Clamp(pa.Dot(ref ba) / ba.LengthSquared(), 0.0, 1.0);
                var sd = (float)((pa - ba * h).Length() - Radius);
                return SignedDistanceToDensity(sd);
            }
        }

        public class NaniteShapeSphere : NaniteShape
        {
            public Vector3D Center; // in World space
            public float Radius;

            public override BoundingBoxD GetWorldBoundaries()
            {
                var bbox = new BoundingBoxD(Center - Radius, Center + Radius);
                return bbox.TransformSlow(Transformation);
            }

            public override float GetVolume(ref Vector3D voxelPosition)
            {
                if (m_inverseIsDirty) { MatrixD.Invert(ref m_transformation, out m_inverse); m_inverseIsDirty = false; }
                Vector3D.Transform(ref voxelPosition, ref m_inverse, out voxelPosition);
                float dist = (float)(voxelPosition - Center).Length();
                float diff = dist - Radius;
                return SignedDistanceToDensity(diff);
            }
        }

        private static void ComputeShapeBounds(IMyVoxelBase voxelMap, ref BoundingBoxD shapeAabb, Vector3D voxelMapMinCorner, Vector3I storageSize, out Vector3I voxelMin, out Vector3I voxelMax)
        {
            MyVoxelCoordSystems.WorldPositionToVoxelCoord(voxelMapMinCorner, ref shapeAabb.Min, out voxelMin);
            MyVoxelCoordSystems.WorldPositionToVoxelCoord(voxelMapMinCorner, ref shapeAabb.Max, out voxelMax);

            MyVoxelBase voxelBase = voxelMap as MyVoxelBase;

            voxelMin += voxelBase.StorageMin;
            voxelMax += voxelBase.StorageMin + 1;

            storageSize -= 1;
            Vector3I.Clamp(ref voxelMin, ref Vector3I.Zero, ref storageSize, out voxelMin);
            Vector3I.Clamp(ref voxelMax, ref Vector3I.Zero, ref storageSize, out voxelMax);
        }

        private static void GetVoxelShapeDimensions(IMyVoxelBase voxelMap, NaniteShape shape, out Vector3I minCorner, out Vector3I maxCorner, out Vector3I numCells)
        {
            {
                var bbox = shape.GetWorldBoundaries();
                ComputeShapeBounds(voxelMap, ref bbox, voxelMap.PositionLeftBottomCorner, voxelMap.Storage.Size, out minCorner, out maxCorner);
            }
            numCells = new Vector3I((maxCorner.X - minCorner.X) / CELL_SIZE, (maxCorner.Y - minCorner.Y) / CELL_SIZE, (maxCorner.Z - minCorner.Z) / CELL_SIZE);
        }

        private static void GetCellCorners(ref Vector3I minCorner, ref Vector3I maxCorner, ref Vector3I_RangeIterator it, out Vector3I cellMinCorner, out Vector3I cellMaxCorner)
        {
            cellMinCorner = new Vector3I(minCorner.X + it.Current.X * CELL_SIZE, minCorner.Y + it.Current.Y * CELL_SIZE, minCorner.Z + it.Current.Z * CELL_SIZE);
            cellMaxCorner = new Vector3I(Math.Min(maxCorner.X, cellMinCorner.X + CELL_SIZE), Math.Min(maxCorner.Y, cellMinCorner.Y + CELL_SIZE), Math.Min(maxCorner.Z, cellMinCorner.Z + CELL_SIZE));
        }

        private bool IsInVoxel(IMyTerminalBlock block)
        {
            BoundingBoxD blockWorldAABB = block.PositionComp.WorldAABB;
            List<MyVoxelBase> voxelList = new List<MyVoxelBase>();
            MyGamePruningStructure.GetAllVoxelMapsInBox(ref blockWorldAABB, voxelList);

            var cubeSize = block.CubeGrid.GridSize;

            BoundingBoxD localAABB = new BoundingBoxD(cubeSize * ((Vector3D)block.Min - 1), cubeSize * ((Vector3D)block.Max + 1));
            var gridWorldMatrix = block.CubeGrid.WorldMatrix;

            //Logging.Instance.WriteLine($"Total Voxels: {voxelList.Count}.  CubeSize: {cubeSize}.  localAABB: {localAABB}");

            foreach (var map in voxelList)
            {
                if (map.IsAnyAabbCornerInside(ref gridWorldMatrix, localAABB))
                {
                    return true;
                }
            }

            return false;
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
