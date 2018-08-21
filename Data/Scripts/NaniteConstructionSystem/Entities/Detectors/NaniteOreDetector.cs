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
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
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
            sb.Append($"Scan: {(m_scanProgress * 100).ToString("0.0")}%\n");
            sb.Append($"Ores:\n");
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

            int totalInitialTasks = m_depositGroupsByEntity.Sum((x) => x.Value.InitialTasks);
            int totalProcessedTasks = m_depositGroupsByEntity.Sum((x) => x.Value.ProcessedTasks);
            float scanProgress = 0f;
            if (totalInitialTasks != 0)
            {
                scanProgress = (float)totalProcessedTasks / (float)totalInitialTasks;
            }
            if (scanProgress != m_scanProgress)
            {
                m_scanProgress = scanProgress;
                MessageHub.SendMessageToAllPlayers(new MessageOreDetectorScanProgress()
                {
                    EntityId = m_block.EntityId,
                    Progress = m_scanProgress
                });
            }

            //StringBuilder oreListCache = new StringBuilder();
            //foreach (var item in m_depositGroupsByEntity.SelectMany((x) => x.Value.Materials).GroupBy((x) => x.Material.MinedOre))
            //{
            //    oreListCache.Append($"- {item.Key}: {item.Count()}\n");
            //}
            //if (oreListCache != m_oreListCache)
            //{
            //    m_oreListCache = oreListCache;
            //    MessageHub.SendMessageToAllPlayers(new MessageOreDetectorScanComplete()
            //    {
            //        EntityId = m_block.EntityId,
            //        OreListCache = m_oreListCache.ToString()
            //    });
            //}
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
                    m_depositGroupsByEntity.Add(item, new OreDeposit(item));
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
        public readonly MyConcurrentList<OreDepositWork.MaterialPositionData> Materials = new MyConcurrentList<OreDepositWork.MaterialPositionData>();
        public int InitialTasks { get { return m_initialTasks; } }
        public int ProcessedTasks { get { return m_processedTasks; } }

        public OreDeposit(MyVoxelBase voxelMap)
        {
            m_voxelMap = voxelMap;
            m_taskQueue = new MyConcurrentQueue<Vector3I>();
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
                CheckQueue();
                return;
            }
            // sphere moved
            else if (m_lastDetectionMin != minCorner || m_lastDetectionMax != maxCorner)
            {
                Logging.Instance.WriteLine($"UpdateDeposits sphere moved");
                m_lastDetectionMin = minCorner;
                m_lastDetectionMax = maxCorner;
                Materials.Clear();
                m_taskQueue.Clear();
                m_initialTasks = 0;
                m_processedTasks = 0;
                // RESET QUEUES
            }

            //int num = Math.Max((maxCorner.X - minCorner.X) / 2, 1);
            //int num2 = Math.Max((maxCorner.Y - minCorner.Y) / 2, 1);
            //Vector3I min = default(Vector3I);
            //min.Z = minCorner.Z;
            //Vector3I max = default(Vector3I);
            //max.Z = maxCorner.Z;
            //for (int i = 0; i < 2; i++)
            //{
            //    for (int j = 0; j < 2; j++)
            //    {
            //        min.X = minCorner.X + i * num;
            //        min.Y = minCorner.Y + j * num2;
            //        max.X = min.X + num;
            //        max.Y = min.Y + num2;
            //        OreDepositWork.Start(min, max, m_voxelMap, Materials, QueueWorkerDone);
            //        m_tasksRunning++;
            //        m_initialTasks++;
            //    }
            //}

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

        private void CheckQueue()
        {
            //if (m_taskQueue.Count > 0)
            //{
            SpawnQueueWorker();
            //    return;
            //}

            //foreach (var item in m_materials.GroupBy((x) => x.Value.Material.MinedOre))
            //{

            //}
        }

        private void SpawnQueueWorker()
        {
            Logging.Instance.WriteLine($"SpawnQueueWorker {Math.Min(m_taskQueue.Count, 100)}");
            for (int i = 0; i < Math.Min(m_taskQueue.Count, 100); i++)
            {
                Vector3I vector;
                if (!m_taskQueue.TryDequeue(out vector))
                    return;

                OreDepositWork.Start(vector, vector + 1, m_voxelMap, Materials, QueueWorkerDone);

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
        public Vector3I Min { get; set; }
        public Vector3I Max { get; set; }
        public MyConcurrentList<MaterialPositionData> Materials { get; set; }
        public Action Callback { get; set; }

        public static void Start(Vector3I min, Vector3I max, MyVoxelBase voxelMap, MyConcurrentList<OreDepositWork.MaterialPositionData> materials, Action completionCallback)
        {
            MyAPIGateway.Parallel.StartBackground(new OreDepositWork
            {
                VoxelMap = voxelMap,
                Min = min,
                Max = max,
                Materials = materials,
                Callback = completionCallback,
            });
        }

        public void DoWork(WorkData workData = null)
        {
            // LOD above is 5 we decrease it by 2 so our LOD now is 3
            Min <<= 2;
            Max <<= 2;

            MyStorageData cache = new MyStorageData(); // TODO: Outsource because of memory allocations
            cache.Resize(new Vector3I(8));
            for (int x = Min.X; x <= Max.X; x++)
                for (int y = Min.Y; y <= Max.Y; y++)
                    for (int z = Min.Z; z <= Max.Z; z++)
                        ProcessCell(cache, VoxelMap.Storage, new Vector3I(x, y, z), 0);

            Callback();
        }

        private void ProcessCell(MyStorageData cache, IMyStorage storage, Vector3I cell, long detectorId)
        {
            Vector3I vector3I = cell << 3;
            Vector3I lodVoxelRangeMax = vector3I + 7;
            storage.ReadRange(cache, MyStorageDataTypeFlags.Content, 0, vector3I, lodVoxelRangeMax);
            if (cache.ContainsVoxelsAboveIsoLevel())
            {
                storage.ReadRange(cache, MyStorageDataTypeFlags.Material, 0, vector3I, lodVoxelRangeMax);
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
                                //Materials.Add(new MaterialPositionData
                                //{
                                //    Material = voxelMaterialDefinition,
                                //    VoxelPosition = p
                                //});
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
