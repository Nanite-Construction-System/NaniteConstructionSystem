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

using NaniteConstructionSystem.Particles;
using NaniteConstructionSystem.Extensions;
using NaniteConstructionSystem.Entities.Detectors;
using VRage.Voxels;

namespace NaniteConstructionSystem.Entities.Targets
{
    public class NaniteMiningTarget
    {
        public int ParticleCount { get; set; }
        public double StartTime { get; set; }
        public double CarryTime { get; set; }
        public double LastUpdate { get; set; }
    }

    public class NaniteMiningTargets : NaniteTargetBlocksBase
    {
        public override string TargetName
        {
            get { return "Mining"; }
        }

        private List<NaniteMiningItem> m_potentialMiningTargets = new List<NaniteMiningItem>();
        private Dictionary<long, IMyEntity> voxelEntities = new Dictionary<long, IMyEntity>();
        private ConcurrentBag<NaniteMiningItem> finalAddList = new ConcurrentBag<NaniteMiningItem>();
        private float m_maxDistance = 500f;
        private Dictionary<NaniteMiningItem, NaniteMiningTarget> m_targetTracker;
        private static HashSet<Vector3D> m_globalPositionList;
        private Random rnd;
        private int m_oldMinedPositionsCount;
        private int m_scannertimeout;
        private int m_minedPositionsCount;

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
            if (!((IMyFunctionalBlock)factory.ConstructionBlock).Enabled
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

        public override void ParallelUpdate(List<IMyCubeGrid> gridList, List<BlockTarget> gridBlocks)
        {
            DateTime start = DateTime.Now;

            if (!IsEnabled(m_constructionBlock))
            {
                m_potentialMiningTargets.Clear();
                return;
            }

            List<string> allowedMats = new List<string>();
                
            foreach (var oreDetector in NaniteConstructionManager.OreDetectors.Where( x => IsInRange( x.Value.Block.GetPosition() ) ) )
            {
                if (oreDetector.Value.DetectorState == NaniteOreDetector.DetectorStates.Disabled)
                    continue;
                
                if (m_potentialMiningTargets.Count > 0)
                {
                    if (oreDetector.Value.HasFilterUpgrade)
                    {
                        foreach (string mat in oreDetector.Value.OreListSelected)
                            if (!allowedMats.Contains(mat))
                                allowedMats.Add(mat);
                    }  
                    else if (!allowedMats.Contains("all"))
                        allowedMats.Add("all");
                    
                    break;
                }
                
                IMyCubeBlock item = oreDetector.Value.Block;
      
                if (!MyRelationsBetweenPlayerAndBlockExtensions.IsFriendly(item.GetUserRelationToOwner(m_constructionBlock.ConstructionBlock.OwnerId)))
                    continue;

                var materialList = oreDetector.Value.DepositGroup.SelectMany((x) => x.Value.Materials.MiningMaterials());
                if (materialList.Count() == 0 && oreDetector.Value.minedPositions.Count > 0)
                {
                    Logging.Instance.WriteLine("Clearing deposit groups due to no new minable targets.");
                    oreDetector.Value.ClearMinedPositions();
                    oreDetector.Value.DepositGroup.Clear();
                    continue;
                }

                foreach (var material in materialList)
                {
                    try
                    {
                        for (int i = 0; i < material.WorldPosition.Count; i++)
                        {
                            bool alreadyMined = false;
                            Vector3D removePos = Vector3D.Zero;
                            foreach (var minedPos in m_globalPositionList)
                            {
                                if (material.WorldPosition[i] == minedPos)
                                {
                                    alreadyMined = true;
                                    material.WorldPosition.RemoveAt(i);
                                    removePos = minedPos;
                                    //Logging.Instance.WriteLine($"Found an already mined position {minedPos}");
                                    break;
                                }
                            }

                            if (alreadyMined)
                            {
                                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                                    { m_globalPositionList.Remove(removePos); });
                                continue;
                            }
                                
                            NaniteMiningItem target = new NaniteMiningItem();
                            target.Position = material.WorldPosition[i];
                            target.VoxelPosition = material.VoxelPosition[i];
                            target.Definition = material.Definition;
                            target.VoxelMaterial = material.Material;
                            target.VoxelId = material.EntityId;
                            target.Amount = 1f;// * 3.9f;
                            target.OreDetectorId = ((MyEntity)item).EntityId;

                            if (voxelEntities.ContainsKey(material.EntityId))
                                PrepareTarget(voxelEntities[material.EntityId], target);
                            else
                                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                                {
                                    IMyEntity entity; // For whatever reason, TryGetEntityById only works on the game thread
                                    if (!MyAPIGateway.Entities.TryGetEntityById(material.EntityId, out entity))
                                        return;

                                    if (!voxelEntities.ContainsKey(material.EntityId))
                                        voxelEntities.Add(material.EntityId, entity);

                                    MyAPIGateway.Parallel.Start(() =>
                                        { PrepareTarget(entity, target); });
                                });

                            MyAPIGateway.Parallel.Sleep(1);
                        }
                    }
                    catch (Exception ex) when (ex.ToString().Contains("ArgumentOutOfRangeException")) 
                    { // because Keen thinks we shouldn't have access to this exception^ ...
                        Logging.Instance.WriteLine("Caught an ArgumentOutOfRangeException while processing mining targets. Forcing the Nanite Ore Detector to rescan.");
                        oreDetector.Value.DepositGroup.Clear();
                        break;
                    }
                }

                if (m_oldMinedPositionsCount == m_minedPositionsCount && m_minedPositionsCount > 0)
                {
                    if (m_scannertimeout++ > 20)
                    {
                        m_scannertimeout = 0;
                        oreDetector.Value.DepositGroup.Clear(); //we've mined all the scanned stuff. Try a rescan.
                        Logging.Instance.WriteLine("Clearing deposit groups due to mining target timeout.");
                        m_minedPositionsCount = 0;
                    }
                }
                else
                {
                    m_oldMinedPositionsCount = m_minedPositionsCount;
                    m_scannertimeout = 0;
                }
            }

            if (allowedMats.Count > 0 && !allowedMats.Contains("all"))
            {
                List<NaniteMiningItem> removeList = new List<NaniteMiningItem>();
                foreach (var target in m_potentialMiningTargets)
                {
                    bool allow = false;

                    foreach (string mat in allowedMats)
                        if (target.Definition.MinedOre.ToLower() == mat.ToLower())
                        {
                            allow = true;
                            break;
                        }

                    if (!allow)
                        removeList.Add(target);

                    MyAPIGateway.Parallel.Sleep(1);
                }

                foreach (var target in removeList)
                    m_potentialMiningTargets.Remove(target);
            }

            //m_potentialMiningTargets.Clear();

            while(finalAddList.Count > 0)
            {
                NaniteMiningItem miningTarget;
                if (finalAddList.TryTake(out miningTarget))
                    m_potentialMiningTargets.Add(miningTarget);
            }

            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                {PotentialTargetListCount = m_potentialMiningTargets.Count;});
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
            maxVoxel += voxelBase.StorageMin;// + 4;

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
                Logging.Instance.WriteLine("Content is empty!");
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                    {AddMinedPosition(target);});
                return false;
            }

            Logging.Instance.WriteLine($"Material: SizeLinear: {cache.SizeLinear}, Size3D: {cache.Size3D}, AboveISO: {cache.ContainsVoxelsAboveIsoLevel()}");
            cache.Content(0, 0);

            var voxelMat = target.Definition;
            target.Amount = CalculateAmount(voxelMat, original * 8f);

            Logging.Instance.WriteLine($"Removing: {target.Position} ({material2} {amount})");

            if (material2 == 0)
            {
                Logging.Instance.WriteLine(string.Format("Material is 0", target.VoxelId));
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                    {AddMinedPosition(target);});
                return false;
            }

            if (target.Amount == 0f)
            {
                Logging.Instance.WriteLine(string.Format("Amount is 0", target.VoxelId));
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
                if (item == null || TargetList.Contains(item))
                {
                    removeList.Add(item);
                    continue;
                }
                if (m_globalPositionList.Contains(item.Position) || usedPositions.Contains(item.Position))
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
                var nearestFactory = GetNearestFactory(TargetName, item.Position);
                if (Vector3D.DistanceSquared(nearestFactory.ConstructionBlock.GetPosition(), item.Position) < m_maxDistance * m_maxDistance)
                {
                    Logging.Instance.WriteLine(string.Format("ADDING Mining Target: conid={0} pos={1} type={2}", 
                        m_constructionBlock.ConstructionBlock.EntityId, item.Position, MyDefinitionManager.Static.GetVoxelMaterialDefinition(item.VoxelMaterial).MinedOre));

                    removeList.Add(item);
                    usedPositions.Add(item.Position);

                    MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                    {
                        if (m_constructionBlock.IsUserDefinedLimitReached())
                            InvalidTargetReason("User defined maximum nanite limit reached");
                        else if (item != null)
                        {
                            removeList.Add(item);
                            TargetList.Add(item);
                        } 
                    });
                    
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
            foreach(var item in TargetList.ToList())
                ProcessItem(item);         
        }

        private void ProcessItem(object miningTarget)
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
                        TransferFromTarget(target);
                        trackedItem.LastUpdate = MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
                    }
                }
            }

            CreateMiningParticles(target);
        }

        private void CreateMiningParticles(NaniteMiningItem target)
        {
            if (!m_targetTracker.ContainsKey(target))
                CreateTrackerItem(target);

            // Create Particle
            MyAPIGateway.Parallel.Start(() =>
            {
                try
                {
                    Vector4 startColor = new Vector4(0.7f, 0.2f, 0.0f, 1f);
                    Vector4 endColor = new Vector4(0.2f, 0.05f, 0.0f, 0.35f);

                    var nearestFactory = GetNearestFactory(TargetName, target.Position);

                    if (nearestFactory.ParticleManager.Particles.Count < NaniteParticleManager.MaxTotalParticles)
                        MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                        {
                            nearestFactory.ParticleManager.AddParticle(startColor, endColor, GetMinTravelTime() * 1000f, GetSpeed(), target, null);
                        });
                }
                catch (Exception e)
                    {VRage.Utils.MyLog.Default.WriteLineAndConsole($"NaniteMiningTargets.CreateMiningParticles() exception: {e.ToString()}");}
            }); 
        }

        private void CreateTrackerItem(NaniteMiningItem target)
        {
            var nearestFactory = GetNearestFactory(TargetName, target.Position);
            double distance = Vector3D.Distance(nearestFactory.ConstructionBlock.GetPosition(), target.Position);
            int time = (int)Math.Max(GetMinTravelTime() * 1000f, (distance / GetSpeed()) * 1000f);

            NaniteMiningTarget miningTarget = new NaniteMiningTarget();
            miningTarget.ParticleCount = 0;
            miningTarget.StartTime = MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
            miningTarget.LastUpdate = MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
            miningTarget.CarryTime = time - 1000;
            m_targetTracker.Add(target, miningTarget);
        }

        private void TransferFromTarget(NaniteMiningItem target)
        { // Must be invoked from game thread
            IMyEntity entity;
            if (!MyAPIGateway.Entities.TryGetEntityById(target.VoxelId, out entity))
            {
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
                            { CancelTarget(target); });

                        return;
                    }

                    MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                    {
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
                                    CancelTarget(target);
                                    return;
                                }

                                var ownerName = targetInventory.Owner as IMyTerminalBlock;
                                if (ownerName != null)
                                    Logging.Instance.WriteLine($"TRANSFER Adding {target.Amount} {item.GetId().SubtypeName} to {ownerName.CustomName}");

                                targetInventory.AddItems((MyFixedPoint)(target.Amount), item);
                            
                                IMyVoxelBase voxel = entity as IMyVoxelBase;
                                MyVoxelBase voxelBase = voxel as MyVoxelBase;

                                voxelBase.RequestVoxelOperationSphere(target.Position, 1f, target.VoxelMaterial, MyVoxelBase.OperationType.Cut);
                
                                AddMinedPosition(target);
                                CompleteTarget(target);
                                return;
                            }

                            Logging.Instance.WriteLine("Mined materials could not be moved. No free cargo space (probably)!");
                            CancelTarget(target);
                        }
                        catch (Exception e)
                            { Logging.Instance.WriteLine($"{e}"); }
                    });
                }
                catch (Exception e)
                    { Logging.Instance.WriteLine($"{e}"); }
            });
        }

        private void AddMinedPosition(NaniteMiningItem target)
        {
            m_minedPositionsCount++;
            m_globalPositionList.Add(target.Position);

            foreach (var oreDetector in NaniteConstructionManager.OreDetectors)
                if ((long)target.OreDetectorId == (long)((MyEntity)oreDetector.Value.Block).EntityId)
                {
                    Logging.Instance.WriteLine($"Adding a mined position{target.Position}");
                    oreDetector.Value.minedPositions.Add(target.Position);
                }
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
            Logging.Instance.WriteLine(string.Format("CANCELLED Mining Target: {0} - {1} (VoxelID={2},Position={3})",
              m_constructionBlock.ConstructionBlock.EntityId, obj.GetType().Name, target.VoxelId, target.Position));

            if (Sync.IsServer)
                m_constructionBlock.SendCompleteTarget((NaniteMiningItem)obj);

            m_constructionBlock.ParticleManager.CancelTarget(target);
            if (m_targetTracker.ContainsKey(target))
                m_targetTracker.Remove(target);

            Remove(obj);
        }

        public override void CompleteTarget(object obj)
        {
            var target = obj as NaniteMiningItem;
            Logging.Instance.WriteLine(string.Format("COMPLETED Mining Target: {0} - {1} (VoxelID={2},Position={3})",
              m_constructionBlock.ConstructionBlock.EntityId, obj.GetType().Name, target.VoxelId, target.Position));

            if (Sync.IsServer)
                m_constructionBlock.SendCompleteTarget((NaniteMiningItem)obj);

            m_constructionBlock.ParticleManager.CompleteTarget(target);
            if (m_targetTracker.ContainsKey(target))
                m_targetTracker.Remove(target);

            Remove(obj);
        }
    }
}
