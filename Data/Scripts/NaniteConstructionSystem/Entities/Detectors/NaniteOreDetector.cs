using NaniteConstructionSystem.Extensions;
using ParallelTasks;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
        public const float RANGE_PER_UPGRADE = 50f;
        public const float POWER_PER_RANGE_UPGRADE = 0.125f;
        public const float POWER_PER_FILTER_UPGRADE = 0.1f;
        public const float POWER_PER_SCANNING_UPGRADE = 1f;
        public const float POWER_PER_POWEREFFICIENCY_UPGRADE = 0.1f;

        public float Range
        {
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
        internal float m_scanProgress;

        private static readonly List<MyVoxelBase> m_inRangeCache = new List<MyVoxelBase>();
        private static readonly List<MyVoxelBase> m_notInRangeCache = new List<MyVoxelBase>();
        private readonly Dictionary<MyVoxelBase, OreDeposit> m_depositGroupsByEntity = new Dictionary<MyVoxelBase, OreDeposit>();
        private readonly MyConcurrentDictionary<MyVoxelBase, OreDepositWork.MaterialPositionData> m_voxelMaterials = new MyConcurrentDictionary<MyVoxelBase, OreDepositWork.MaterialPositionData>();

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

            // TODO remove debug only
            sb.Append($"Range: {Range}\n"); 
            sb.Append($"Scan: {m_scanProgress * 100}%");
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

        public List<MyTerminalControlListBoxItem> GetTerminalOreList()
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

        #region Voxel/Ore detection
        public void CheckScan()
        {
            Vector3D position = m_block.GetPosition();
            BoundingSphereD sphere = new BoundingSphereD(position, Range);
            MyGamePruningStructure.GetAllVoxelMapsInSphere(ref sphere, m_inRangeCache);
            RemoveVoxelMapsOutOfRange();
            AddVoxelMapsInRange();
            UpdateDeposits(ref sphere);
            m_inRangeCache.Clear();

            var totalInitialTasks = m_depositGroupsByEntity.Sum((x) => x.Value.InitialTasks * 1000);
            var scanProgress = 0f;
            if (totalInitialTasks != 0)
            {
                scanProgress = (m_depositGroupsByEntity.Sum((x) => x.Value.ProcessedTasks * 1000) / totalInitialTasks) / 1000;
            }
            Logging.Instance.WriteLine($"scan: {scanProgress} {totalInitialTasks}");
            if (scanProgress != m_scanProgress)
            {
                m_scanProgress = scanProgress;
                MessageHub.SendMessageToAllPlayers(new MessageOreDetectorScanProgress()
                {
                    Progress = m_scanProgress
                });
            }
        }

        private void UpdateDeposits(ref BoundingSphereD sphere)
        {
            Logging.Instance.WriteLine("UpdateDeposits");
            foreach (OreDeposit value in m_depositGroupsByEntity.Values)
            {
                value.UpdateDeposits(ref sphere, m_block.EntityId, this);
            }
        }

        private void AddVoxelMapsInRange()
        {
            foreach (MyVoxelBase item in m_inRangeCache)
            {
                if (!m_depositGroupsByEntity.ContainsKey(item.GetTopMostParent() as MyVoxelBase))
                {
                    Logging.Instance.WriteLine("AddVoxelMapsInRange");
                    m_depositGroupsByEntity.Add(item, new OreDeposit(item, m_voxelMaterials));
                }
            }
            m_inRangeCache.Clear();
        }

        private void RemoveVoxelMapsOutOfRange()
        {
            foreach (MyVoxelBase key in m_depositGroupsByEntity.Keys)
            {
                if (!m_inRangeCache.Contains(key.GetTopMostParent() as MyVoxelBase))
                {
                    Logging.Instance.WriteLine("RemoveVoxelMapsOutOfRange");
                    m_notInRangeCache.Add(key);
                }
            }
            foreach (MyVoxelBase item in m_notInRangeCache)
            {
                //OreDeposit value;
                //if (m_depositGroupsByEntity.TryGetValue(item, out value))
                //{
                //    //value.RemoveMarks();
                //}
                m_depositGroupsByEntity.Remove(item);
            }
            m_notInRangeCache.Clear();
        }
        #endregion
    }

    #region Ore Deposit & Worker
    public class OreDeposit
    {
        private readonly MyVoxelBase m_voxelMap;
        private Vector3I m_lastDetectionMin;
        private Vector3I m_lastDetectionMax;
        private FastResourceLock m_lock = new FastResourceLock();
        private int m_tasksRunning;
        private int m_initialTasks;
        private int m_processedTasks;
        private MyConcurrentQueue<Vector3I> m_taskQueue;
        private MyConcurrentDictionary<MyVoxelBase, OreDepositWork.MaterialPositionData> m_materials { get; set; }

        public int InitialTasks { get { return m_initialTasks; } }
        public int ProcessedTasks { get { return m_processedTasks; } }

        public OreDeposit(MyVoxelBase voxelMap, MyConcurrentDictionary<MyVoxelBase, OreDepositWork.MaterialPositionData> materials)
        {
            m_voxelMap = voxelMap;
            m_taskQueue = new MyConcurrentQueue<Vector3I>();
            m_materials = materials;
        }

        public void UpdateDeposits(ref BoundingSphereD sphere, long detectorId, NaniteOreDetector detectorComponent)
        {
            Logging.Instance.WriteLine($"UpdateDeposits Tasks: {m_tasksRunning} {m_initialTasks} {m_processedTasks}");
            if (m_tasksRunning > 0)
                return;

            Vector3I minCorner, maxCorner;
            {
                var sphereMin = sphere.Center - sphere.Radius;
                var sphereMax = sphere.Center + sphere.Radius;
                MyVoxelCoordSystems.WorldPositionToVoxelCoord(m_voxelMap.PositionLeftBottomCorner, ref sphereMin, out minCorner);
                MyVoxelCoordSystems.WorldPositionToVoxelCoord(m_voxelMap.PositionLeftBottomCorner, ref sphereMax, out maxCorner);
                minCorner += m_voxelMap.StorageMin;
                maxCorner += m_voxelMap.StorageMin;

                // LOD changes
                minCorner >>= 5;
                maxCorner >>= 5;
            }

            // First scan
            if (m_lastDetectionMin == null || m_lastDetectionMax == null)
            {
                Logging.Instance.WriteLine($"UpdateDeposits First scan");
                m_lastDetectionMax = minCorner;
                m_lastDetectionMin = maxCorner;
            }
            // sphere still at some position
            else if (m_lastDetectionMin == minCorner && m_lastDetectionMax == maxCorner)
            {
                Logging.Instance.WriteLine($"UpdateDeposits sphere still at some position");
                SpawnQueueWorker();
                return;
            }
            // sphere moved
            else if (m_lastDetectionMin != minCorner || m_lastDetectionMax != maxCorner)
            {
                Logging.Instance.WriteLine($"UpdateDeposits sphere moved");
                m_lastDetectionMin = minCorner;
                m_lastDetectionMax = maxCorner;
                m_taskQueue.Clear();
                m_initialTasks = 0;
                m_processedTasks = 0;
                m_materials.Remove(m_voxelMap);
                // RESET QUEUES
            }

            MyAPIGateway.Parallel.For(minCorner.X, maxCorner.X, (x) =>
            {
                MyAPIGateway.Parallel.For(minCorner.Y, maxCorner.Y, (y) =>
                {
                    MyAPIGateway.Parallel.For(minCorner.Z, maxCorner.Z, (z) =>
                    {
                        Vector3I pos = new Vector3I(x, y, z);
                        m_taskQueue.Enqueue(pos);
                    });
                });
            });
            Logging.Instance.WriteLine($"UpdateDeposits setup queue {m_taskQueue.Count}");
            m_initialTasks = m_taskQueue.Count;

            SpawnQueueWorker();
        }

        private void SpawnQueueWorker()
        {
            Logging.Instance.WriteLine($"SpawnQueueWorker {Math.Min(m_taskQueue.Count, 100)}");
            for (int i = 0; i < Math.Min(m_taskQueue.Count, 100); i++)
            {
                Vector3I vector;
                if (!m_taskQueue.TryDequeue(out vector))
                    return;

                MyAPIGateway.Parallel.StartBackground(new OreDepositWork
                {
                    VoxelMap = m_voxelMap,
                    Vector = vector,
                    Materials = m_materials,
                    Callback = QueueWorkerDone,
                });

                using (m_lock.AcquireExclusiveUsing())
                    m_tasksRunning++;
            }
        }

        private void QueueWorkerDone()
        {
            using (m_lock.AcquireExclusiveUsing())
            {
                m_tasksRunning--;
                m_processedTasks++;
            }
        }
    }

    public class OreDepositWork : IWork
    {
        public WorkOptions Options => new WorkOptions
        {
            MaximumThreads = 2
        };

        public struct MaterialPositionData
        {
            public MyVoxelMaterialDefinition Material;
            public Vector3I VoxelPosition;
        }

        public MyVoxelBase VoxelMap { get; set; }
        public Vector3I Vector { get; set; }
        public MyConcurrentDictionary<MyVoxelBase, MaterialPositionData> Materials { get; set; }
        public Action Callback { get; set; }

        public void DoWork(WorkData workData = null)
        {
            // LOD above is 5 we decrease it by 3 so our LOD now is 2
            var minCorner = Vector << 3;
            var maxCorner = (Vector + 1) << 3;

            MyStorageData cache = new MyStorageData(); // TODO: Outsource because of memory allocations
            cache.Resize(new Vector3I(8));
            for (int x = minCorner.X; x <= maxCorner.X; x++)
                for (int y = minCorner.Y; y <= maxCorner.Y; y++)
                    for (int z = minCorner.Z; z <= maxCorner.Z; z++)
                        ProcessCell(cache, VoxelMap.Storage, new Vector3I(x, y, z), 0);

            Callback();
        }

        private void ProcessCell(MyStorageData cache, IMyStorage storage, Vector3I cell, long detectorId)
        {
            Vector3I vector3I = cell << 3;
            Vector3I lodVoxelRangeMax = vector3I + 7;
            storage.ReadRange(cache, MyStorageDataTypeFlags.Content, 2, vector3I, lodVoxelRangeMax);
            if (cache.ContainsVoxelsAboveIsoLevel())
            {
                storage.ReadRange(cache, MyStorageDataTypeFlags.Material, 2, vector3I, lodVoxelRangeMax);
                Vector3I p = default(Vector3I);
                p.Z = 0;
                while (p.Z < 8)
                {
                    p.Y = 0;
                    while (p.Y < 8)
                    {
                        p.X = 0;
                        while (p.X < 8)
                        {
                            int linearIdx = cache.ComputeLinear(ref p);
                            if (cache.Content(linearIdx) > 127)
                            {
                                byte b = cache.Material(linearIdx);
                                MyVoxelMaterialDefinition voxelMaterialDefinition = MyDefinitionManager.Static.GetVoxelMaterialDefinition(b);
                                Materials.Add(VoxelMap, new MaterialPositionData
                                {
                                    Material = voxelMaterialDefinition,
                                    VoxelPosition = p
                                });
                            }
                            p.X++;
                        }
                        p.Y++;
                    }
                    p.Z++;
                }
                //MyEntityOreDeposit myEntityOreDeposit = null;
                //for (int i = 0; i < materialData.Length; i++)
                //{
                //    if (materialData[i].Count != 0)
                //    {
                //        MyVoxelMaterialDefinition voxelMaterialDefinition = MyDefinitionManager.Static.GetVoxelMaterialDefinition((byte)i);
                //        if (voxelMaterialDefinition != null && voxelMaterialDefinition.IsRare)
                //        {
                //            if (myEntityOreDeposit == null)
                //            {
                //                myEntityOreDeposit = new MyEntityOreDeposit(VoxelMap, cell, detectorId);
                //            }
                //            myEntityOreDeposit.Materials.Add(new MyEntityOreDeposit.Data
                //            {
                //                Material = voxelMaterialDefinition,
                //                AverageLocalPosition = Vector3D.Transform(materialData[i].Sum / (float)materialData[i].Count - VoxelMap.SizeInMetresHalf, Quaternion.CreateFromRotationMatrix(VoxelMap.WorldMatrix))
                //            });
                //        }
                //    }
                //}
                //if (myEntityOreDeposit != null)
                //{
                //    m_result.Add(myEntityOreDeposit);
                //}
                //else
                //{
                //    m_emptyCells.Add(cell);
                //}
                //Array.Clear(materialData, 0, materialData.Length);
            }
        }
    }
    #endregion
}
