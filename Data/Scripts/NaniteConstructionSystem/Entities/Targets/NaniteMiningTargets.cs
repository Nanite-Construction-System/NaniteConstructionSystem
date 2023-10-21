using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.ModAPI;
using VRageMath;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Definitions;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ObjectBuilders;
using VRage.Utils;

using NaniteConstructionSystem.Particles;
using NaniteConstructionSystem.Extensions;
using NaniteConstructionSystem.Entities.Beacons;
using VRage.Voxels;
using VRage.ModAPI;

namespace NaniteConstructionSystem.Entities.Targets
{
    public class NaniteMiningTarget
    {
        public int ParticleCount { get; set; }
        public double StartTime { get; set; }
        public double CarryTime { get; set; }
        public double LastUpdate { get; set; }
    }

    public class NaniteMiningItem
    {
        public byte VoxelMaterial { get; set; }
        public Vector3D Position { get; set; }
        public Vector3I VoxelPosition { get; set; }
        public MyVoxelMaterialDefinition Definition { get; set; }
        public long VoxelId { get; set; }
        public float Amount { get; set; }
    }

    public class NaniteMiningTargets : NaniteTargetBlocksBase
    {
        public override string TargetName
        {
            get { return "Mining"; }
        }

        private List<NaniteMiningItem> m_potentialMiningTargets = new List<NaniteMiningItem>();
        private List<NaniteMiningItem> alreadyCreatedMiningTarget = new List<NaniteMiningItem>();
        private Dictionary<long, IMyEntity> voxelEntities = new Dictionary<long, IMyEntity>();
        private List<NaniteMiningItem> finalAddList = new List<NaniteMiningItem>();
        private float m_maxDistance = 500f;
        private Dictionary<NaniteMiningItem, NaniteMiningTarget> m_targetTracker;
        private static HashSet<Vector3D> m_globalPositionList;
        private Random rnd;
        private int m_oldMinedPositionsCount;
        private int m_scannertimeout;
        private int m_minedPositionsCount;
        private string beaconDataCode = "";

        public NaniteMiningTargets(NaniteConstructionBlock constructionBlock) : base(constructionBlock)
        {
            m_maxDistance = NaniteConstructionManager.Settings.MiningMaxDistance;
            m_targetTracker = new Dictionary<NaniteMiningItem, NaniteMiningTarget>();
            m_globalPositionList = new HashSet<Vector3D>();
            rnd = new Random();
        }

        public override int GetMaximumTargets()
        {
             return (int)Math.Min((NaniteConstructionManager.Settings.MiningNanitesNoUpgrade * m_constructionBlock.FactoryGroup.Count)
              + m_constructionBlock.UpgradeValue("MiningNanites"), NaniteConstructionManager.Settings.MiningMaxStreams);
        }

        public override float GetPowerUsage()
        {
            return Math.Max(1, NaniteConstructionManager.Settings.MiningPowerPerStream
              - (int)m_constructionBlock.UpgradeValue("PowerNanites"));
        }

        public override float GetMinTravelTime()
        {
            return Math.Max(1f, NaniteConstructionManager.Settings.MiningMinTravelTime 
              - m_constructionBlock.UpgradeValue("MinTravelTime"));
        }

        public override float GetSpeed()
        {
            return NaniteConstructionManager.Settings.MiningDistanceDivisor
              + m_constructionBlock.UpgradeValue("SpeedNanites");
        }

        public override bool IsEnabled(NaniteConstructionBlock factory)
        {
            if ( ( (IMyFunctionalBlock)factory.ConstructionBlock ) == null || !( (IMyFunctionalBlock)factory.ConstructionBlock ).Enabled
              || !((IMyFunctionalBlock)factory.ConstructionBlock).IsFunctional 
              || (NaniteConstructionManager.TerminalSettings.ContainsKey(factory.ConstructionBlock.EntityId) 
              && !NaniteConstructionManager.TerminalSettings[factory.ConstructionBlock.EntityId].AllowMining))
            {
                factory.EnabledParticleTargets[TargetName] = false;
                return false;
            }
                
            factory.EnabledParticleTargets[TargetName] = true;
            return true;
        }

        private static void ClampVoxelCoord(IMyStorage storage, ref Vector3I voxelCoord, int distance = 1)
        {
            if (storage == null) return;
            Vector3I newSize = storage.Size - distance;
            Vector3I.Clamp(ref voxelCoord, ref Vector3I.Zero, ref newSize, out voxelCoord);
        }

        public override void ParallelUpdate(List<IMyCubeGrid> gridList, List<BlockTarget> gridBlocks)
        {
            try
            {
                DateTime start = DateTime.Now;

                if (!IsEnabled(m_constructionBlock))
                {
                    m_potentialMiningTargets.Clear();
                    return;
                }

                finalAddList.Clear();

                if (m_potentialMiningTargets.Count() < 500 && finalAddList.Count() < 300) {

                    // DATA Z DETECTORU, teď z mining beaconu a nasypat je do finalAddList
                    var newBeaconDataCode = "";
                    foreach (var beaconBlock in NaniteConstructionManager.BeaconList.Where(x => x.Value is NaniteBeaconMine))
                    {
                        var item = beaconBlock.Value.BeaconBlock;
                        int beaconData = 0;

                        if (!int.TryParse(item.CustomData, out beaconData)) {
                            item.CustomData = "";
                        }
                        if ((beaconData - 1) >= 0 && (beaconData - 1) < NaniteConstructionManager.OreList.Count) {
                            // pass
                        } else {
                            beaconData = 0;
                            item.CustomData = "";
                        }

                        newBeaconDataCode += item.CustomData.ToString();

                        if (item == null || !item.Enabled || !item.IsFunctional
                          || !MyRelationsBetweenPlayerAndBlockExtensions.IsFriendly(item.GetUserRelationToOwner(m_constructionBlock.ConstructionBlock.OwnerId))
                          || !IsInRange(item.GetPosition(), m_maxDistance) )
                            continue;

                        // if grid is not named, name it
                        IMyCubeGrid parentGrid = item.CubeGrid;
                        if (parentGrid == null)
                            continue;

                        string gridCustomName = parentGrid.CustomName;
                        if (gridCustomName.Contains("Large Grid") || gridCustomName.Contains("Small Grid") || gridCustomName.Contains("Static Grid"))
                        {
                            parentGrid.CustomName = "Nanite Mining Beacon";
                        }

                        // valid friendly mining beacon in range
                        //get all the materials if we have less than 500 targets

                        List<MyVoxelBase> detected = new List<MyVoxelBase>();
                        Vector3D position = item.GetPosition();
                        BoundingSphereD boundingSphereD = new BoundingSphereD(position, 20);
                        MyGamePruningStructure.GetAllVoxelMapsInSphere(ref boundingSphereD, detected);
                        float randomFloat = (float)(rnd.Next(5, 11) / 10.0);

                        foreach (MyVoxelBase voxelMap in detected)
                        {
                            // check voxel state
                            if (voxelMap.Closed || voxelMap.MarkedForClose || voxelMap.Storage == null)
                                continue;

                            // Voxel base detected within the sphere
                            // MyVisualScriptLogicProvider.ShowNotificationToAll($"PASS 1 : voxelBase {voxelMap.StorageName}", 4000);

                            // voxel entity id
                            var targetEntityId = voxelMap.EntityId;

                            // create storage cache
                            MyStorageData storageCache = new MyStorageData(MyStorageDataTypeFlags.ContentAndMaterial);
                            storageCache.Resize(new Vector3I(1));
                            var myVoxelRequestFlag = MyVoxelRequestFlags.ContentCheckedDeep;

                            // min max
                            Vector3I minVoxel;
                            Vector3I maxVoxel;
                            var min = boundingSphereD.Center - new Vector3D(boundingSphereD.Radius);
                            var max = boundingSphereD.Center + new Vector3D(boundingSphereD.Radius);
                            MyVoxelCoordSystems.WorldPositionToVoxelCoord(voxelMap.PositionLeftBottomCorner, ref min, out minVoxel);
                            MyVoxelCoordSystems.WorldPositionToVoxelCoord(voxelMap.PositionLeftBottomCorner, ref max, out maxVoxel);
                            ClampVoxelCoord(voxelMap.Storage, ref minVoxel);
                            ClampVoxelCoord(voxelMap.Storage, ref maxVoxel);
                            float minVoxelX = (float)minVoxel.X;
                            float maxVoxelX = (float)maxVoxel.X;
                            float minVoxelY = (float)minVoxel.Y;
                            float maxVoxelY = (float)maxVoxel.Y;
                            float minVoxelZ = (float)minVoxel.Z;
                            float maxVoxelZ = (float)maxVoxel.Z;

                            for (var x = minVoxelX; x <= maxVoxelX; x += randomFloat) {
                                if (finalAddList.Count() > 300) break;
                                for (var y = minVoxelY; y <= maxVoxelY; y += randomFloat) {
                                    if (finalAddList.Count() > 300) break;
                                    for (var z = minVoxelZ; z <= maxVoxelZ; z += randomFloat) {
                                        try
                                        {
                                            if (finalAddList.Count() > 300) break;

                                            // check that position is within the sphere
                                            var voxelPosition = new Vector3I(x, y, z);
                                            var worldPosition = new Vector3D(0);
                                            MyVoxelCoordSystems.VoxelCoordToWorldPosition(voxelMap.PositionLeftBottomCorner, ref voxelPosition, out worldPosition);

                                            var distance = Vector3D.Distance(worldPosition, boundingSphereD.Center);
                                            if (distance <= boundingSphereD.Radius) {
                                                // voxel position is within the sphere, read cache

                                                voxelMap.Storage.ReadRange(storageCache, MyStorageDataTypeFlags.ContentAndMaterial, 0, voxelPosition, voxelPosition, ref myVoxelRequestFlag);

                                                var content = storageCache.Content(0);
                                                if (content == MyVoxelConstants.VOXEL_CONTENT_EMPTY)
                                                    continue;

                                                var materialByte = storageCache.Material(0);
                                                MyVoxelMaterialDefinition materialDefinition = MyDefinitionManager.Static.GetVoxelMaterialDefinition(materialByte);

                                                if (materialDefinition != null && materialDefinition.MinedOre != null)
                                                {
                                                    var filteredOreName = "";

                                                    if (beaconData != null && beaconData != 0) {
                                                        var parseOreIdent = beaconData;
                                                        parseOreIdent -= 1;
                                                        if (parseOreIdent >= 0 && parseOreIdent < NaniteConstructionManager.OreList.Count) {
                                                            filteredOreName = NaniteConstructionManager.OreList[parseOreIdent];
                                                        }
                                                    }

                                                    if (filteredOreName != "" && filteredOreName != materialDefinition.MinedOre) {
                                                        continue;
                                                    }

                                                    NaniteMiningItem target = new NaniteMiningItem();
                                                    target.Position = worldPosition;
                                                    target.VoxelPosition = voxelPosition;
                                                    target.Definition = materialDefinition;
                                                    target.VoxelMaterial = materialByte;
                                                    target.VoxelId = targetEntityId;
                                                    target.Amount = 1f;

                                                    var ignored = false;
                                                    foreach (object ignoredItem in PotentialIgnoredList) {
                                                        var miningTarget = ignoredItem as NaniteMiningItem;

                                                        if (miningTarget == null)
                                                            continue;

                                                        if (miningTarget.VoxelId == target.VoxelId && miningTarget.Position == target.Position && miningTarget.VoxelMaterial == target.VoxelMaterial) {
                                                            ignored = true;
                                                            break;
                                                        }
                                                    }

                                                    if (!ignored && !alreadyCreatedMiningTarget.Contains(target) && !finalAddList.Contains(target) && !TargetList.Contains(target) && !m_globalPositionList.Contains(worldPosition)) {
                                                        finalAddList.Add(target);
                                                        alreadyCreatedMiningTarget.Add(target);
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            MyLog.Default.WriteLineAndConsole($"##MOD: Nanite Facility, for cycle ERROR: {e}");
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (newBeaconDataCode != beaconDataCode) {
                        beaconDataCode = newBeaconDataCode;

                        if (beaconDataCode != "") {
                            finalAddList.Clear();
                            m_potentialMiningTargets.Clear();
                        }
                    }
                }

                if (m_oldMinedPositionsCount == m_minedPositionsCount && m_minedPositionsCount > 0)
                {
                    // MyVisualScriptLogicProvider.ShowNotificationToAll($"m_scannertimeout: {m_scannertimeout}", 2000);
                    if (m_scannertimeout++ > 20)
                    {
                        m_scannertimeout = 0;
                        m_minedPositionsCount = 0;
                        ResetMiningTargetsAndRescan();
                    }
                }
                else
                {
                    m_oldMinedPositionsCount = m_minedPositionsCount;
                    m_scannertimeout = 0;
                }

                foreach (NaniteMiningItem miningTarget in finalAddList.ToList()) {
                    if (miningTarget != null) {
                        m_potentialMiningTargets.Add(miningTarget);
                    }
                }

                // MyVisualScriptLogicProvider.ShowNotificationToAll($"PASS 1 : {m_potentialMiningTargets.Count()};{finalAddList.Count()}", 4000);

                PotentialTargetListCount = m_potentialMiningTargets.Count;
            }
            catch (ArgumentException e)
            {
                MyLog.Default.WriteLineAndConsole($"##MOD: Nanite Facility, Argument ERROR: {e}");
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLineAndConsole($"##MOD: Nanite Facility, ERROR: {e}");
            }
        }

        private void ResetMiningTargetsAndRescan()
        {
            // MyVisualScriptLogicProvider.ShowNotificationToAll($"resetting targets", 4000);

            finalAddList = new List<NaniteMiningItem>();
            m_potentialMiningTargets = new List<NaniteMiningItem>();
            alreadyCreatedMiningTarget = new List<NaniteMiningItem>();
            m_targetTracker = new Dictionary<NaniteMiningItem, NaniteMiningTarget>();
            m_globalPositionList = new HashSet<Vector3D>();

            MyLog.Default.WriteLineAndConsole($"##MOD: Nanite Facility, RESET TARGETS");
        }

        private void TryAddNewVoxelEntity(long entityId)
        {
            try
            {
                IMyEntity entity; // For whatever reason, TryGetEntityById only works on the game thread
                if (!MyAPIGateway.Entities.TryGetEntityById(entityId, out entity))
                    return;
                                        
                if (!voxelEntities.ContainsKey(entityId))
                {
                    Logging.Instance.WriteLine("[Mining] Adding new voxel entity to storage list.", 1);
                    voxelEntities.Add(entityId, entity);
                }
            }
            catch (Exception e)
                { Logging.Instance.WriteLine($"{e}"); }
        }

        private void PrepareTarget(IMyEntity entity, NaniteMiningItem target)
        {
            try
            {
                if (entity == null)
                    return;

                if (IsValidVoxelTarget(target, entity))
                    finalAddList.Add(target);
            }
            catch (Exception e)
                {Logging.Instance.WriteLine($"{e}");}
        }

        private bool IsValidVoxelTarget(NaniteMiningItem target, IMyEntity entity)
        {
            if (entity == null)
                return false;

            byte material2 = 0;
            float amount = 0;                    
            IMyVoxelBase voxel = entity as IMyVoxelBase;
            Vector3D targetMin = target.Position;
            Vector3D targetMax = target.Position;
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
            var flag = MyVoxelRequestFlags.AdviseCache;
            cache.ClearContent(0);
            cache.ClearMaterials(0);

            byte original = 0;

            voxel.Storage.ReadRange(cache, MyStorageDataTypeFlags.ContentAndMaterial, 0, minVoxel, maxVoxel, ref flag);

            original = cache.Content(0);
            material2 = cache.Material(0);

            if (original == MyVoxelConstants.VOXEL_CONTENT_EMPTY)
            {
                Logging.Instance.WriteLine("[Mining] Content is empty!", 2);
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                    {AddMinedPosition(target);});
                return false;
            }

            Logging.Instance.WriteLine($"[Mining] Material: SizeLinear: {cache.SizeLinear}, Size3D: {cache.Size3D}, AboveISO: {cache.ContainsVoxelsAboveIsoLevel()}", 2);
            cache.Content(0, 0);

            var voxelMat = target.Definition;
            target.Amount = CalculateAmount(voxelMat, original * 8f);

            Logging.Instance.WriteLine($"[Mining] Removing: {target.Position} ({material2} {amount})", 2);

            if (material2 == 0)
            {
                Logging.Instance.WriteLine("[Mining] Material is 0", 2);
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                    {AddMinedPosition(target);});
                return false;
            }

            if (target.Amount == 0f)
            {
                Logging.Instance.WriteLine("[Mining] Amount is 0", 2);
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                    {AddMinedPosition(target);});
                return false;
            }

            return true;
        }

        public override void FindTargets(ref Dictionary<string, int> available, List<NaniteConstructionBlock> blockList)
        {
            var maxTargets = GetMaximumTargets();

            if (TargetList.Count >= maxTargets)
            {
                if (m_potentialMiningTargets.Count > 0)
                    InvalidTargetReason("Maximum targets reached. Add more upgrades!");

                return;
            }

            string LastInvalidTargetReason = "";

            int targetListCount = m_targetList.Count;

            HashSet<Vector3D> usedPositions = new HashSet<Vector3D>();
            List<NaniteMiningItem> removeList = new List<NaniteMiningItem>();

            foreach(NaniteMiningItem item in m_potentialMiningTargets.ToList())
            {
                if (item == null)
                {
                    LastInvalidTargetReason = "Mining position is invalid";
                    removeList.Add(item);
                    continue;
                }
                if (TargetList.Contains(item))
                {
                    LastInvalidTargetReason = "Mining position is already mined";
                    removeList.Add(item);
                    continue;
                }
                if (m_globalPositionList.Contains(item.Position))
                {
                    LastInvalidTargetReason = "Mining position was already mined";
                    removeList.Add(item);
                    continue;
                }
                if (usedPositions.Contains(item.Position))
                {
                    LastInvalidTargetReason = "Mining position was already targeted";
                    removeList.Add(item);
                    continue;
                }
                if (!m_constructionBlock.HasRequiredPowerForNewTarget(this))
                {
                    LastInvalidTargetReason = "Insufficient power for another target";
                    break;
                }
                    
                bool found = false;
                foreach (var block in blockList.ToList())
                    if (block != null && block.GetTarget<NaniteMiningTargets>().TargetList.FirstOrDefault(x => ((NaniteMiningItem)x).Position == item.Position) != null)
                    {
                        found = true;
                        LastInvalidTargetReason = "Another factory has this voxel as a target";
                        break;
                    }

                if (found)
                {
                    removeList.Add(item);
                    continue;
                }
                var nearestFactory = m_constructionBlock;
                if (IsInRange(nearestFactory, item.Position, m_maxDistance))
                {
                    /*Logging.Instance.WriteLine(string.Format("[Mining] Adding Mining Target: conid={0} pos={1} type={2}",
                    m_constructionBlock.ConstructionBlock.EntityId, item.Position, MyDefinitionManager.Static.GetVoxelMaterialDefinition(item.VoxelMaterial).MinedOre), 1);*/

                    removeList.Add(item);
                    usedPositions.Add(item.Position);

                    if (m_constructionBlock.IsUserDefinedLimitReached())
                    {
                        InvalidTargetReason("User defined maximum nanite limit reached");
                    }
                    else if (item != null)
                    {
                        removeList.Add(item);
                        TargetList.Add(item);
                    }
                    
                    if (targetListCount++ >= maxTargets)
                        break;
                }
                else
                    removeList.Add(item);
            }

            foreach (var item in removeList)
                m_potentialMiningTargets.Remove(item);

            if (LastInvalidTargetReason != "")
                InvalidTargetReason(LastInvalidTargetReason);
        }

        public override void Update()
        {
            try
            {
                foreach (var item in TargetList.ToList()) {
                    ProcessMiningItem(item);
                }
            }
            catch (Exception e)
            {
                Logging.Instance.WriteLine($"Exception in NaniteMiningTargets.Update:\n{e}");
            }
        }

        private void ProcessMiningItem(object miningTarget)
        {
            var target = miningTarget as NaniteMiningItem;

            if (target == null)
                return;

            if (Sync.IsServer)
            {
                if (!m_targetTracker.ContainsKey(target))
                    m_constructionBlock.SendAddTarget(target);

                if (m_targetTracker.ContainsKey(target))
                {
                    var trackedItem = m_targetTracker[target];
                    
                    if (MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - trackedItem.StartTime >= trackedItem.CarryTime &&
                      MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - trackedItem.LastUpdate > 2000)
                    {
                        MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                        {
                            try {
                                TransferFromTarget(target);
                                trackedItem.LastUpdate = MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
                            } catch (Exception e) {
                                Logging.Instance.WriteLine($"Exception in NaniteMiningTargets.ProcessMiningItem (Invocation 1):\n{e}");
                            }
                        });
                    }
                }
            }

            CreateMiningParticles(target);
        }

        private void CreateMiningParticles(NaniteMiningItem target)
        {
            try
            {
                if (!m_targetTracker.ContainsKey(target))
                    CreateTrackerItem(target);
                    
                Vector4 startColor = new Vector4(1.5f, 0.2f, 0.0f, 1f);
                Vector4 endColor = new Vector4(0.2f, 0.05f, 0.0f, 0.35f);

                var nearestFactory = m_constructionBlock;

                if (nearestFactory.ParticleManager.Particles.Count < NaniteParticleManager.MaxTotalParticles)
                    nearestFactory.ParticleManager.AddParticle(startColor, endColor, GetMinTravelTime() * 1000f, GetSpeed(), target, null);
            }
            catch (Exception e)
                {VRage.Utils.MyLog.Default.WriteLineAndConsole($"NaniteMiningTargets.CreateMiningParticles() exception: {e}");}
        }

        private void CreateTrackerItem(NaniteMiningItem target)
        {
            var nearestFactory = m_constructionBlock;
            double distance = Vector3D.Distance(nearestFactory.ConstructionBlock.GetPosition(), target.Position);
            int time = (int)Math.Max(GetMinTravelTime() * 1000f, (distance / GetSpeed()) * 1000f);

            NaniteMiningTarget miningTarget = new NaniteMiningTarget();
            miningTarget.ParticleCount = 0;
            miningTarget.StartTime = MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
            miningTarget.LastUpdate = MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
            miningTarget.CarryTime = time - 1000;

            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                if (!m_targetTracker.ContainsKey(target))
                    m_targetTracker.Add(target, miningTarget);
            });
        }

        private void TransferFromTarget(NaniteMiningItem target)
        { // Must be invoked from game thread
            IMyEntity entity;
            if (!MyAPIGateway.Entities.TryGetEntityById(target.VoxelId, out entity))
            {
                AddToIgnoreList(target);
                CancelTarget(target);
                return;
            }

            MyAPIGateway.Parallel.Start(() =>
            {
                try
                {
                    if (entity == null || !IsValidVoxelTarget(target, entity))
                    {
                        MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                            {
                                AddToIgnoreList(target);
                                CancelTarget(target);
                            });

                        return;
                    }

                    MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                    { // Invocation 0
                        try
                        {
                            var def = MyDefinitionManager.Static.GetVoxelMaterialDefinition(target.VoxelMaterial);
                            var item = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Ore>(def.MinedOre);
                            var inventory = ((MyCubeBlock)m_constructionBlock.ConstructionBlock).GetInventory();
                            MyInventory targetInventory = ((MyCubeBlock)m_constructionBlock.ConstructionBlock).GetInventory();

                            if (targetInventory != null && targetInventory.CanItemsBeAdded((MyFixedPoint)(target.Amount), item.GetId()))
                            {
                                if (entity == null)
                                {
                                    AddToIgnoreList(target);
                                    CancelTarget(target);
                                    return;
                                }

                                var ownerName = targetInventory.Owner as IMyTerminalBlock;
                                if (ownerName != null)
                                    Logging.Instance.WriteLine($"[Mining] Transfer - Adding {target.Amount} {item.GetId().SubtypeName} to {ownerName.CustomName}", 1);

                                if (!targetInventory.AddItems((MyFixedPoint)(target.Amount), item))
                                {
                                    Logging.Instance.WriteLine($"Error while transferring {target.Amount} {item.GetId().SubtypeName}! Aborting mining operation.");
                                    return;
                                }
                            
                                IMyVoxelBase voxel = entity as IMyVoxelBase;
                                MyVoxelBase voxelBase = voxel as MyVoxelBase;

                                voxelBase.RequestVoxelOperationSphere(target.Position, 1f, target.VoxelMaterial, MyVoxelBase.OperationType.Cut);
                
                                AddMinedPosition(target);
                                CompleteTarget(target);
                                return;
                            }

                            Logging.Instance.WriteLine("[Mining] Mined materials could not be moved. No free cargo space (probably)!", 1);
                            AddToIgnoreList(target);
                            CancelTarget(target);
                        }
                        catch (Exception e)
                            { Logging.Instance.WriteLine($"Exception in NaniteMiningTargets.TransferFromTarget (Invocation 0):\n{e}"); }
                    });
                }
                catch (Exception e)
                    { Logging.Instance.WriteLine($"Exception in NaniteMiningTargets.TransferFromTarget:\n{e}"); }
            });
        }

        private void AddMinedPosition(NaniteMiningItem target)
        {
            m_minedPositionsCount++;
            m_globalPositionList.Add(target.Position);
        }

        private static float CalculateAmount(MyVoxelMaterialDefinition material, float amount)
        {
            var oreObjBuilder = VRage.ObjectBuilders.MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Ore>(material.MinedOre);
            oreObjBuilder.MaterialTypeName = material.Id.SubtypeId;

            float amountCubicMeters = (float)(((float)amount / (float)MyVoxelConstants.VOXEL_CONTENT_FULL)
              * MyVoxelConstants.VOXEL_VOLUME_IN_METERS * Sandbox.Game.MyDrillConstants.VOXEL_HARVEST_RATIO);
            
            amountCubicMeters *= (float)material.MinedOreRatio;
            var physItem = MyDefinitionManager.Static.GetPhysicalItemDefinition(oreObjBuilder);
            MyFixedPoint amountInItemCount = (MyFixedPoint)(amountCubicMeters / physItem.Volume);
            return (float)amountInItemCount;
        }

        public override void CancelTarget(object obj)
        {
            var target = obj as NaniteMiningItem;
            Logging.Instance.WriteLine(string.Format("[Mining] Cancelled Mining Target: {0} - {1} (VoxelID={2},Position={3})",
              m_constructionBlock.ConstructionBlock.EntityId, obj.GetType().Name, target.VoxelId, target.Position), 1);

            if (Sync.IsServer)
                m_constructionBlock.SendCompleteTarget((NaniteMiningItem)obj);

            m_constructionBlock.ParticleManager.CancelTarget(target);
            if (m_targetTracker.ContainsKey(target))
                m_targetTracker.Remove(target);

            Remove(obj);
        }

        public override void AddToIgnoreList(object obj){
            if (PotentialIgnoredList.Contains(obj) == false) {
                PotentialIgnoredList.Add(obj);
                if (PotentialTargetList.Contains(obj)) {
                    PotentialTargetList.Remove(obj);
                }
            }
        }

        public override void CompleteTarget(object obj)
        {
            var target = obj as NaniteMiningItem;
            Logging.Instance.WriteLine(string.Format("[Mining] Completed Mining Target: {0} - {1} (VoxelID={2},Position={3})",
              m_constructionBlock.ConstructionBlock.EntityId, obj.GetType().Name, target.VoxelId, target.Position), 1);

            if (Sync.IsServer)
                m_constructionBlock.SendCompleteTarget((NaniteMiningItem)obj);

            m_constructionBlock.ParticleManager.CompleteTarget(target);
            if (m_targetTracker.ContainsKey(target))
                m_targetTracker.Remove(target);

            Remove(obj);
        }
    }
}
