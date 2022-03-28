using NaniteConstructionSystem.Entities.Effects;
using NaniteConstructionSystem.Extensions;
using ParallelTasks;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Concurrent;
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
        public enum DetectorStates
        {
            Disabled,
            Enabled,
            Scanning,
            ScanComplete
        }

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
        public IMyOreDetector Block
        {
            get { return m_block; }
        }

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

        public float ScanningUpgradesNum
        {
            get { return m_block.UpgradeValues["Scanning"]; }
        }

        protected bool supportFilter = true;
        protected int maxScanningLevel = 0;
        protected float minRange = 0f;
        protected float basePower = 0f;
        internal NaniteOreDetectorSettings Settings;
        internal float m_scanProgress;
        private DetectorStates m_lastDetectorState;
        internal DetectorStates m_detectorState;
        internal bool m_tooCloseToOtherDetector;
        bool m_tooCloseOld;
        public DetectorStates DetectorState
        {
            get { return m_detectorState; }
        }

        public ConcurrentBag<Vector3D> minedPositions = new ConcurrentBag<Vector3D>();

        private static readonly List<MyVoxelBase> m_inRangeCache = new List<MyVoxelBase>();
        private static readonly List<MyVoxelBase> m_notInRangeCache = new List<MyVoxelBase>();
        private ConcurrentDictionary<MyVoxelBase, OreDeposit> m_depositGroupsByEntity = new ConcurrentDictionary<MyVoxelBase, OreDeposit>();
        public ConcurrentDictionary<MyVoxelBase, OreDeposit> DepositGroup
        {
            get { return m_depositGroupsByEntity; }
        }
        private readonly List<OreDetectorEffect> m_effects = new List<OreDetectorEffect>();

        public NaniteOreDetector(IMyFunctionalBlock entity)
        {
            m_block = entity as IMyOreDetector;
            m_lastUpdate = DateTime.MinValue;
            m_scanStart = DateTime.MinValue;
            m_scanEnd = DateTime.MinValue;
            m_lock = new FastResourceLock();
            m_oreListCache = new StringBuilder();
            m_detectorState = DetectorStates.Disabled;
            m_lastDetectorState = DetectorStates.Disabled;

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

            m_effects.Add(new OreDetectorEffect((MyCubeBlock)m_block));

            if (!NaniteConstructionManager.OreDetectors.ContainsKey(entity.EntityId))
                NaniteConstructionManager.OreDetectors.Add(entity.EntityId, this);
        }

        public void ClearMinedPositions()
        {
            Vector3D removedItem = new Vector3D(1.0, 1.0, 1.0);
            while (!minedPositions.IsEmpty)
                minedPositions.TryTake(out removedItem);
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

            MyAPIGateway.Parallel.Start(() =>
            {
                try
                    { UpdatePower(); }
                catch (Exception e)
                    {VRage.Utils.MyLog.Default.WriteLineAndConsole($"NaniteOreDetector.UpdatePower() exception: {e.ToString()}");}
            });

            if (Sync.IsClient)
                MessageHub.SendMessageToServer(new MessageOreDetectorSettings()
                {
                    EntityId = m_block.EntityId
                });
        }

        public virtual void Close()
        {
            m_effects.Clear();

            NaniteConstructionManager.OreDetectors.Remove(m_block.EntityId);
        }

        public void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder sb)
        {
            sb.Append("Type: Nanite Ore Detector\n");
            sb.Append($"Current Input: {Power} MW\n");

            if (m_tooCloseToOtherDetector)
            {
                sb.Append("WARNING: Unit was too close to\nanother active Nanite Ore Detector\n and was disabled.\n");
                sb.Append("Turn the unit back on\nwhen the problem is resolved.");
                return;
            }
                
            sb.Append($"Frequency:\n");
            foreach (var freq in GetScanningFrequencies())
                sb.Append($" - [{freq}]\n");

            // TODO remove debug only
            sb.Append($"Range: {Range}\n"); 
            sb.Append($"Scan: {(m_scanProgress * 100).ToString("0.00")}%\n");
            sb.Append($"Ores:\n");
            sb.Append(m_oreListCache);
        }

        public void UpdateStatus()
        {
            if (!m_block.Enabled || !m_block.IsFunctional || !Sink.IsPoweredByType(MyResourceDistributorComponent.ElectricityId) || m_tooCloseToOtherDetector)
                m_detectorState = DetectorStates.Disabled;

            else if (m_depositGroupsByEntity.Count == 0)
                m_detectorState = DetectorStates.Enabled;

            if (m_detectorState != m_lastDetectorState || m_tooCloseToOtherDetector != m_tooCloseOld)
            {
                m_tooCloseOld = m_tooCloseToOtherDetector;
                m_lastDetectorState = m_detectorState;
                MessageHub.SendToPlayerInSyncRange(new MessageOreDetectorStateChange()
                {
                    EntityId = m_block.EntityId,
                    State = m_lastDetectorState,
                    TooClose = m_tooCloseToOtherDetector
                }, m_block.GetPosition());
            }
        }

        public void DrawStatus()
        {
            foreach (var item in m_effects)
            {
                if (m_detectorState == DetectorStates.Enabled)
                    item.ActiveUpdate();
                else if (m_detectorState == DetectorStates.Scanning)
                    item.ScanningUpdate();
                else if (m_detectorState == DetectorStates.ScanComplete)
                    item.ScanCompleteUpdate();
                else
                    item.InactiveUpdate();
            }
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
            MyAPIGateway.Parallel.Start(() =>
            {
                try
                    { UpdatePower(); }
                catch (Exception e)
                    {Logging.Instance.WriteLine($"NaniteOreDetector.UpdatePower() exception: {e.ToString()}");}
            });
                
            UpdateStatus();
        }

        private void UpdatePower()
        {
            if (!m_block.Enabled || !m_block.IsFunctional)
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                {
                    Sink.Update();
                    return;
                });

            float upgradeRangeAddition = 0f;
            float upgradeRangeMultiplicator = 1;

            float rangeToAdd = NaniteConstructionManager.Settings != null
              ? NaniteConstructionManager.Settings.OreDetectorRangePerUpgrade : RANGE_PER_UPGRADE;

            for (int i = 1; i <= (int)m_block.UpgradeValues["Range"]; i++)
            {
                upgradeRangeAddition += rangeToAdd;

                if (upgradeRangeMultiplicator == 1f)
                    upgradeRangeMultiplicator = 0.7f;
                else if (upgradeRangeMultiplicator > 0f)
                    upgradeRangeMultiplicator -= 0.1f;
            }

            _maxRange = minRange + upgradeRangeAddition;

            // this piece is resetting the range, the module check is delayed and it breaks saved values
            // however it also assigns the Range below the config values so I am keeping it in
            Range = _maxRange;

            float powerPerRange = NaniteConstructionManager.Settings != null
              ? NaniteConstructionManager.Settings.OreDetectorPowerIncreasePerRangeUpgrade : POWER_PER_RANGE_UPGRADE;

            float powerPerFilter = NaniteConstructionManager.Settings != null
              ? NaniteConstructionManager.Settings.OreDetectorPowerIncreasePerFilterUpgrade : POWER_PER_FILTER_UPGRADE;

            float powerPerScanning = NaniteConstructionManager.Settings != null
              ? NaniteConstructionManager.Settings.OreDetectorPowerPercentIncreasedPerScanningUpgrade : POWER_PER_SCANNING_UPGRADE;

            float powerPerEfficiency = NaniteConstructionManager.Settings != null
              ? NaniteConstructionManager.Settings.OreDetectorPowerPercentReducedPerEfficiencyUpgrade : POWER_PER_POWEREFFICIENCY_UPGRADE;

            _power = basePower;
            _power += m_block.UpgradeValues["Range"] * powerPerRange;
            _power += m_block.UpgradeValues["Filter"] * powerPerFilter;
            _power *= 1 + (m_block.UpgradeValues["Scanning"] * powerPerScanning);
            _power *= 1 - (m_block.UpgradeValues["PowerEfficiency"] * powerPerEfficiency);

            if (NaniteConstructionManager.Settings != null)
                _power *= NaniteConstructionManager.Settings.OreDetectorPowerMultiplicator;

            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                { Sink.Update(); });

            Logging.Instance.WriteLine($"Updated Nanite Ore Detector power {_power}", 1);
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
                if (m_block.UpgradeValues["Filter"] < 1f && (stringId.String == "Uranium" || stringId.String == "Platinum" || stringId.String == "Deuterium" || stringId.String == "Silver" || stringId.String == "Gold"))
                    continue;
                if (m_block.UpgradeValues["Filter"] < 2f && (stringId.String == "Uranium" || stringId.String == "Platinum" || stringId.String == "Deuterium" || stringId.String == "Ravinium" || stringId.String == "Gardinium"))
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

        private void CheckIsTooCloseToOtherDetector()
        {
            MyAPIGateway.Parallel.Start(() =>
            {
                float distance = NaniteConstructionManager.Settings != null ? NaniteConstructionManager.Settings.OreDetectorToNaniteFactoryCommunicationDistance : 300f;
                bool result = false;
                foreach (var detector in NaniteConstructionManager.OreDetectors)
                    if (detector.Key != m_block.EntityId && detector.Value.Block != null
                      && Vector3D.Distance(m_block.GetPosition(), detector.Value.Block.GetPosition()) < distance
                      && detector.Value.DetectorState != DetectorStates.Disabled && detector.Value.Block.IsFunctional)
                    {
                        result = true;
                        break;                            
                    }

                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                    { m_tooCloseToOtherDetector = result; });
            });
        }

        #region Voxel/Ore detection
        public void CheckScan()
        { // Invoked on a parallel thread in LargeNaniteOreDetecotrLogic
            if (!m_block.Enabled || !m_block.IsFunctional || !Sink.IsPoweredByType(MyResourceDistributorComponent.ElectricityId))
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                {
                    m_depositGroupsByEntity.Clear();
                    m_inRangeCache.Clear();
                });
                
                return;
            }

            CheckIsTooCloseToOtherDetector();

            if (m_tooCloseToOtherDetector)
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                {
                    m_depositGroupsByEntity.Clear();
                    m_inRangeCache.Clear();
                    m_block.Enabled = false;
                });
                return;
            }

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
                scanProgress = (float)totalProcessedTasks / (float)totalInitialTasks;

            if (scanProgress != m_scanProgress)
            {
                m_scanProgress = scanProgress;
                MessageHub.SendToPlayerInSyncRange(new MessageOreDetectorScanProgress()
                {
                    EntityId = m_block.EntityId,
                    Progress = m_scanProgress
                }, m_block.GetPosition());
            }

            StringBuilder oreListCache = new StringBuilder();

            foreach (var item in m_depositGroupsByEntity.SelectMany((x) => x.Value.Materials.GetMaterialList()).GroupBy((x) => x.Material.MinedOre))
                oreListCache.Append($"- {item.Key}: {item.Sum((x) => x.Count)}\n");
            
            if (oreListCache != m_oreListCache)
            {
                m_oreListCache = oreListCache;
                MessageHub.SendToPlayerInSyncRange(new MessageOreDetectorScanComplete()
                {
                    EntityId = m_block.EntityId,
                    OreListCache = m_oreListCache.ToString()
                }, m_block.GetPosition());
            }
        }

        private void UpdateDeposits(ref BoundingSphereD sphere)
        { // Invoked in a parallel task
            foreach (OreDeposit value in m_depositGroupsByEntity.Values)
                value.UpdateDeposits(ref sphere, m_block.EntityId, this);

            var initialTasks = m_depositGroupsByEntity.Sum((x) => x.Value.InitialTasks);

            if (initialTasks != 0)
            {
                var processedTasks = m_depositGroupsByEntity.Sum((x) => x.Value.ProcessedTasks);

                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                {
                    if (processedTasks == initialTasks)
                        m_detectorState = DetectorStates.ScanComplete;

                    else
                         m_detectorState = DetectorStates.Scanning;

                    if (m_detectorState != m_lastDetectorState)
                    {
                        m_lastDetectorState = m_detectorState;
                        MessageHub.SendToPlayerInSyncRange(new MessageOreDetectorStateChange()
                        {
                            EntityId = m_block.EntityId,
                            State = m_lastDetectorState,
                        }, m_block.GetPosition());
                    }
                });
            }
        }

        private void AddVoxelMapsInRange()
        {
            foreach (MyVoxelBase item in m_inRangeCache.ToList())
                m_depositGroupsByEntity.TryAdd(item, new OreDeposit(item));

            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                { m_inRangeCache.Clear(); });
        }

        private void RemoveVoxelMapsOutOfRange()
        { // invoked in a parallel task
            foreach (MyVoxelBase key in m_depositGroupsByEntity.Keys)
                if (!m_inRangeCache.Contains(key.GetTopMostParent() as MyVoxelBase))
                    MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                        { m_notInRangeCache.Add(key); });

            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                MyAPIGateway.Parallel.Start(() =>
                {
                    try
                    {
                        OreDeposit outOreDeposit = null;

                        foreach (MyVoxelBase item in m_notInRangeCache)
                            m_depositGroupsByEntity.TryRemove(item, out outOreDeposit);

                        m_notInRangeCache.Clear();
                    }
                    catch (Exception e)
                        {VRage.Utils.MyLog.Default.WriteLineAndConsole($"NaniteOreDetector.RemoveVoxelMapsOutOfRange() exception: {e.ToString()}");}
                });
            });
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
        public int InitialTasks { get { return m_initialTasks; } }
        private int m_processedTasks;
        public int ProcessedTasks { get { return m_processedTasks; } }
        private MyConcurrentQueue<Vector3I> m_taskQueue;
        private bool HasFilterUpgrade;
        private List<string> OreListSelected;
        private int m_tasksTimeout;
        private int m_OldprocessedTasks;

        public readonly OreDepositMaterials Materials;

        public OreDeposit(MyVoxelBase voxelMap)
        {
            m_voxelMap = voxelMap;
            m_taskQueue = new MyConcurrentQueue<Vector3I>();
            Materials = new OreDepositMaterials(voxelMap);
        }

        public void UpdateDeposits(ref BoundingSphereD sphere, long detectorId, NaniteOreDetector detectorComponent)
        { // Invoked from parallel task
            Logging.Instance.WriteLine($"UpdateDeposits Tasks: Running:{m_tasksRunning} Initial tasks:{m_initialTasks} Processed tasks:{m_processedTasks} Timeout:{m_tasksTimeout}", 1);

            if (m_tasksRunning > 0)
            {
                if (m_OldprocessedTasks == m_processedTasks && m_processedTasks != m_initialTasks)
                {
                    if (m_tasksTimeout > 100)
                    {
                        Logging.Instance.WriteLine($"Mining scan task timeout. Clearing ore deposits and restarting.");
                        MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                        {
                            detectorComponent.DepositGroup.Clear();
                            m_tasksTimeout = 0;
                        });
                    }
                    else
                        MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                            { m_tasksTimeout++; });
                }
                else
                {
                    MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                    {
                        m_OldprocessedTasks = m_processedTasks;
                        m_tasksTimeout = 0;
                    });
                }
                return;
            }

            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                m_tasksTimeout = 0;
                HasFilterUpgrade = detectorComponent.HasFilterUpgrade;
                OreListSelected = detectorComponent.OreListSelected;
            });

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

            if (m_lastDetectionMin == null || m_lastDetectionMax == null)
            { // First scan
                Logging.Instance.WriteLine($"UpdateDeposits First scan", 1);

                detectorComponent.ClearMinedPositions();
                
                m_lastDetectionMax = minCorner;
                m_lastDetectionMin = maxCorner;
            }
            else if (m_lastDetectionMin == minCorner && m_lastDetectionMax == maxCorner)
            { // sphere still at some position
                Logging.Instance.WriteLine($"UpdateDeposits sphere still at some position", 1);
                
                if (m_tasksRunning < 1)
                    SpawnQueueWorker(detectorComponent);
                
                return;
            }
            else if (m_lastDetectionMin != minCorner || m_lastDetectionMax != maxCorner)
            { // sphere moved, RESET QUEUES
                Logging.Instance.WriteLine($"UpdateDeposits sphere moved", 1);
                m_lastDetectionMin = minCorner;
                m_lastDetectionMax = maxCorner;
                Materials.Clear();
                m_taskQueue.Clear();
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                {
                    m_initialTasks = 0;
                    m_processedTasks = 0;
                });
            }

            Vector3I cell = default(Vector3I);
            cell.Z = minCorner.Z;
            while (cell.Z <= maxCorner.Z)
            {
                cell.Y = minCorner.Y;
                while (cell.Y <= maxCorner.Y)
                {
                    cell.X = minCorner.X;
                    while (cell.X <= maxCorner.X)
                    {
                        m_taskQueue.Enqueue(cell);
                        cell.X++;
                    }
                    cell.Y++;
                }
                cell.Z++;
            }

            Logging.Instance.WriteLine($"UpdateDeposits setup queue {m_taskQueue.Count}", 1);
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                { m_initialTasks = m_taskQueue.Count; });
        }

        private void SpawnQueueWorker(NaniteOreDetector detectorComponent)
        {
            int scanningSpeed = NaniteConstructionManager.Settings != null ? NaniteConstructionManager.Settings.OreDetectorScanningSpeed : 10;

            scanningSpeed += (int)detectorComponent.ScanningUpgradesNum * 2;

            // Logging.Instance.WriteLine($"scanningSpeed sphere moved {scanningSpeed}");

            // Logging.Instance.WriteLine($"SpawnQueueWorker {Math.Min(m_taskQueue.Count, scanningSpeed)}", 1);
            for (int i = 0; i < Math.Min(m_taskQueue.Count, scanningSpeed); i++)
            {
                Vector3I vector;
                if (!m_taskQueue.TryDequeue(out vector))
                    return;

                OreDepositWork.Start(vector, vector + 1, m_voxelMap, Materials, QueueWorkerDone, HasFilterUpgrade, OreListSelected);

                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                    { m_tasksRunning++; });
                
                MyAPIGateway.Parallel.Sleep(100);
            }
        }

        private void QueueWorkerDone()
        {
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                m_tasksRunning--;
                m_processedTasks++;
            });
        }
    }

    public class OreDepositWork : IWork
    {
        public WorkOptions Options => new WorkOptions
            { MaximumThreads = 1 };

        public MyVoxelBase VoxelMap { get; set; }
        public Vector3I Min { get; set; }
        public Vector3I Max { get; set; }
        public OreDepositMaterials Materials { get; set; }
        public Action Callback { get; set; }
        private bool HasFilterUpgrade;
        private List<string> OreListSelected;
        private int sleepTimer = 1;

        private static MyStorageData m_cache;
        private static MyStorageData Cache
        {
            get
            {
                if (m_cache == null)
                    m_cache = new MyStorageData();

                return m_cache;
            }
        }

        public static void Start(Vector3I min, Vector3I max, MyVoxelBase voxelMap, OreDepositMaterials materials, Action completionCallback, bool FilterUpgrade, List<string> OreList)
        {
            MyAPIGateway.Parallel.StartBackground(new OreDepositWork
            {
                OreListSelected = OreList,
                HasFilterUpgrade = FilterUpgrade,
                VoxelMap = voxelMap,
                Min = min,
                Max = max,
                Materials = materials,
                Callback = completionCallback,
            });
        }

        public void DoWork(WorkData workData = null)
        {
            try
            {
                // LOD above is 5 we decrease it by 2 so our LOD now is 3
                Min <<= 2;
                Max <<= 2;

                MyStorageData cache = Cache;
                cache.Resize(new Vector3I(8));

                for (int x = Min.X; x <= Max.X; x++)
                    for (int y = Min.Y; y <= Max.Y; y++)
                        for (int z = Min.Z; z <= Max.Z; z++)
                            ProcessCell(cache, VoxelMap.Storage, new Vector3I(x, y, z), 0);

                Callback();
            }
            catch (Exception e)
            {
                Logging.Instance.WriteLine($"{e}");
                Callback();
            }
        }

        private void ProcessCell(MyStorageData cache, IMyStorage storage, Vector3I cell, long detectorId)
        {
            //bool m_miningDebug = false;

            if (cache == null || storage == null)
                return;

            Vector3I vector3I = cell << 3;
            Vector3I lodVoxelRangeMax = vector3I + 7;

            // Advice cache because of performance issues
            var flag = MyVoxelRequestFlags.ContentCheckedDeep;

            Stopwatch stopwatch = Stopwatch.StartNew();
            storage.ReadRange(cache, MyStorageDataTypeFlags.Content, 0, vector3I, lodVoxelRangeMax, ref flag);
            stopwatch.Stop();

            int readingTime = (int)((stopwatch.ElapsedTicks * 1000000)/Stopwatch.Frequency);
            if (readingTime > 1000)
            {
                int changeAmount = (int)(readingTime / 1000);

                if (MyAPIGateway.Physics.ServerSimulationRatio < 1.00f)
                    sleepTimer += changeAmount * 100;
                else
                    sleepTimer = Math.Max(sleepTimer - 1, 1);
                
                MyAPIGateway.Parallel.Sleep(sleepTimer);                    
            }
                
            //if (m_miningDebug)
                //Logging.Instance.WriteLine($"ProcessCell.ReadRange(1) took {(stopwatch.ElapsedTicks * 1000000)/Stopwatch.Frequency} microseconds");

            if (cache.ContainsVoxelsAboveIsoLevel())
            {
                //Stopwatch stopwatch2 = Stopwatch.StartNew();
                storage.ReadRange(cache, MyStorageDataTypeFlags.Material, 0, vector3I, lodVoxelRangeMax, ref flag);
                //stopwatch2.Stop();

                //if (m_miningDebug)
                    //Logging.Instance.WriteLine($"ProcessCell.ReadRange(2) took {(stopwatch2.ElapsedTicks * 1000000)/Stopwatch.Frequency} microseconds");

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

                                if (HasFilterUpgrade)
                                {
                                    var voxelDefinition = MyDefinitionManager.Static.GetVoxelMaterialDefinition(b);

                                    if (voxelDefinition != null && voxelDefinition.MinedOre != null)
                                    {
                                        foreach (string mat in OreListSelected)
                                            if (voxelDefinition.MinedOre.ToLower() == mat.ToLower())
                                            {
                                                Materials.AddMaterial(b, vector3I + p);
                                                break;
                                            }
                                    }
                                    else
                                        Materials.AddMaterial(b, vector3I + p);  
                                }
                                else
                                    Materials.AddMaterial(b, vector3I + p);
                            }
                            p.X++;
                        }
                        p.Y++;
                    }
                    p.Z++;
                }
            }
        }
    }

    public class OreDepositMaterials
    {
        public struct MaterialPositionData
        {
            public List<Vector3I> VoxelPosition;
            public List<Vector3D> WorldPosition;
            public int Count;
        }

        private readonly MaterialPositionData[] m_materials;

        private readonly FastResourceLock m_lock = new FastResourceLock();

        private readonly MyVoxelBase m_voxelMap;

        public OreDepositMaterials(MyVoxelBase voxelMap)
        {
            m_materials = new MaterialPositionData[256];
            m_voxelMap = voxelMap;
            for (int i = 0; i < 256; i++)
            {
                m_materials[i].VoxelPosition = new List<Vector3I>(1000);
                m_materials[i].WorldPosition = new List<Vector3D>(1000);
            }
        }

        public void AddMaterial(byte material, Vector3I pos)
        {
            using (m_lock.AcquireExclusiveUsing())
            {
               var count = m_materials[material].Count++;
                if (count <= 1000)
                {
                    m_materials[material].VoxelPosition.Add(pos);

                    Vector3D worldPositon;
                    MyVoxelCoordSystems.VoxelCoordToWorldPosition(m_voxelMap.PositionLeftBottomCorner, ref pos, out worldPositon);
                    m_materials[material].WorldPosition.Add(worldPositon);
                }
            }
        }

        public void Clear()
        {
            using (m_lock.AcquireExclusiveUsing())
            {
                for (int i = 0; i < 256; i++)
                {
                    m_materials[i].Count = 0;
                    m_materials[i].VoxelPosition.Clear();
                }
            }
        }

        public struct MiningMaterial
        {
            public long EntityId;
            public List<Vector3I> VoxelPosition;
            public List<Vector3D> WorldPosition;
            public int Count;
            public byte Material;
            public MyVoxelMaterialDefinition Definition;
        }

        public List<MiningMaterial> MiningMaterials()
        {
            List<MiningMaterial> materials = new List<MiningMaterial>();

            using (m_lock.AcquireSharedUsing())
            {
                for (int i = 0; i < 256; i++)
                {
                    if (m_materials[i].Count == 0)
                        continue;

                    var voxelDefinition = MyDefinitionManager.Static.GetVoxelMaterialDefinition((byte)i);
                    if (voxelDefinition == null)
                        continue;

                    materials.Add(new MiningMaterial()
                    {
                        EntityId = m_voxelMap.EntityId,
                        VoxelPosition = m_materials[i].VoxelPosition,
                        WorldPosition = m_materials[i].WorldPosition,
                        Count = m_materials[i].Count,
                        Material = (byte)i,
                        Definition = voxelDefinition,
                    });
                }
            }

            return materials;
        }

        public struct MaterialList
        {
            public MyVoxelMaterialDefinition Material;
            public int Count;
        }

        public List<MaterialList> GetMaterialList()
        {
            List<MaterialList> list = new List<MaterialList>();

            using (m_lock.AcquireSharedUsing())
            {
                for (int i = 0; i < 256; i++)
                {
                    if (m_materials[i].Count == 0)
                        continue;

                    var voxelDefinition = MyDefinitionManager.Static.GetVoxelMaterialDefinition((byte)i);
                    if (voxelDefinition == null)
                        continue;

                    list.Add(new MaterialList()
                    {
                        Count = m_materials[i].Count,
                        Material = voxelDefinition,
                    });
                }
            }

            return list;
        }
    }
    #endregion
}
