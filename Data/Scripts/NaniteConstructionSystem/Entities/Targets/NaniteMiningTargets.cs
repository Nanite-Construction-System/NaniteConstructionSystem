using System;
using System.Collections.Generic;
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
            get
            {
                return "Mining";
            }
        }

        private float m_maxDistance = 500f;
        private Dictionary<NaniteMiningItem, NaniteMiningTarget> m_targetTracker;
        private static HashSet<Vector3D> m_globalPositionList;
        private Random rnd;
        private int oldMinedPositionsCount;
        private int scannertimeout;

        public NaniteMiningTargets(NaniteConstructionBlock constructionBlock) : base(constructionBlock)
        {
            m_maxDistance = NaniteConstructionManager.Settings.MiningMaxDistance;
            m_targetTracker = new Dictionary<NaniteMiningItem, NaniteMiningTarget>();
            m_globalPositionList = new HashSet<Vector3D>();
            rnd = new Random();
        }

        public override int GetMaximumTargets()
        {
            MyCubeBlock block = (MyCubeBlock)m_constructionBlock.ConstructionBlock;
            return (int)Math.Min(NaniteConstructionManager.Settings.MiningNanitesNoUpgrade + (block.UpgradeValues["MiningNanites"] 
              * NaniteConstructionManager.Settings.MiningNanitesPerUpgrade), NaniteConstructionManager.Settings.MiningMaxStreams);
        }

        public override float GetPowerUsage()
        {
            MyCubeBlock block = (MyCubeBlock)m_constructionBlock.ConstructionBlock;
            return Math.Max(1, NaniteConstructionManager.Settings.MiningPowerPerStream - (int)(block.UpgradeValues["PowerNanites"] * NaniteConstructionManager.Settings.PowerDecreasePerUpgrade));
        }

        public override float GetMinTravelTime()
        {
            MyCubeBlock block = (MyCubeBlock)m_constructionBlock.ConstructionBlock;
            return Math.Max(1f, NaniteConstructionManager.Settings.MiningMinTravelTime - (block.UpgradeValues["SpeedNanites"] * NaniteConstructionManager.Settings.MinTravelTimeReductionPerUpgrade));
        }

        public override float GetSpeed()
        {
            MyCubeBlock block = (MyCubeBlock)m_constructionBlock.ConstructionBlock;
            return NaniteConstructionManager.Settings.MiningDistanceDivisor + (block.UpgradeValues["SpeedNanites"] * (float)NaniteConstructionManager.Settings.SpeedIncreasePerUpgrade);
        }

        public override bool IsEnabled()
        {
            bool result = true;
            if (!((IMyFunctionalBlock)m_constructionBlock.ConstructionBlock).Enabled ||
                !((IMyFunctionalBlock)m_constructionBlock.ConstructionBlock).IsFunctional ||
                m_constructionBlock.ConstructionBlock.CustomName.ToLower().Contains("NoMining".ToLower()))
                result = false;

            if (NaniteConstructionManager.TerminalSettings.ContainsKey(m_constructionBlock.ConstructionBlock.EntityId))
            {
                if (!NaniteConstructionManager.TerminalSettings[m_constructionBlock.ConstructionBlock.EntityId].AllowMining)
                    return false;
            }

            return result;
        }

        public override void ParallelUpdate(List<IMyCubeGrid> gridList, List<IMySlimBlock> gridBlocks)
        {
            using (Lock.AcquireExclusiveUsing())
                PotentialTargetList.Clear();

            DateTime start = DateTime.Now;
            List<object> finalAddList = new List<object>();

            foreach (var oreDetector in NaniteConstructionManager.OreDetectors.Where((x) => Vector3D.DistanceSquared(m_constructionBlock.ConstructionBlock.GetPosition(), x.Value.Block.GetPosition()) < m_maxDistance * m_maxDistance).OrderBy(x => rnd.Next(100)))
            {
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
                    for (int i = 0; i < material.WorldPosition.Count; i++)
                    {
                        bool alreadyMined = false;
                        foreach (var minedPos in oreDetector.Value.minedPositions)
                        {
                            if (material.WorldPosition[i] == minedPos)
                            {
                                alreadyMined = true;
                                Logging.Instance.WriteLine($"Found an already mined position {minedPos}");
                                break;
                            }
                        }
                        if (alreadyMined)
                            continue;

                        NaniteMiningItem miningItem = new NaniteMiningItem();
                        miningItem.Position = material.WorldPosition[i];
                        miningItem.VoxelPosition = material.VoxelPosition[i];
                        miningItem.Definition = material.Definition;
                        miningItem.VoxelMaterial = material.Material;
                        miningItem.VoxelId = material.EntityId;
                        miningItem.Amount = 1f; // * 3.9f;
                        miningItem.OreDetectorId = ((MyEntity)item).EntityId;
                        finalAddList.Add(miningItem);
                    }
                }
                if (oldMinedPositionsCount == oreDetector.Value.minedPositions.Count && oreDetector.Value.minedPositions.Count > 0)
                {
                    if (scannertimeout++ > 5)
                    {
                        scannertimeout = 0;
                        oreDetector.Value.DepositGroup.Clear(); //we've mined all the scanned stuff. Try a rescan.
                    }
                }
                else
                {
                    oldMinedPositionsCount = oreDetector.Value.minedPositions.Count;
                    scannertimeout = 0;
                }
            }
            var listToAdd = finalAddList.Take(1000).ToList();
            listToAdd.Reverse();
            PotentialTargetList.AddRange(listToAdd);
        }

        private void DistributeList(List<object> listToAdd, List<object> finalList, int count)
        {
            if (count < 1)
            {
                finalList.AddRange(listToAdd);
                return;
            }

            for(int r = listToAdd.Count - 1; r >= 0; r--)
            {
                var item = listToAdd[r];
                var realPos = r * (count + 1);
                if (realPos >= finalList.Count)
                    realPos = finalList.Count - 1;

                finalList.Insert(realPos, item);
            }
        }

        public override void FindTargets(ref Dictionary<string, int> available, List<NaniteConstructionBlock> blockList)
        {
            if (!IsEnabled())
                return;

            if (TargetList.Count >= GetMaximumTargets())
            {
                if (PotentialTargetList.Count > 0)
                    InvalidTargetReason("Maximum targets reached. Add more upgrades!");

                return;
            }

            string LastInvalidTargetReason = "";

            int targetListCount = m_targetList.Count;

            foreach(NaniteMiningItem item in m_potentialTargetList.ToList())
            {
                if (item == null || TargetList.Contains(item))
                    continue;

                if (m_globalPositionList.Contains(item.Position))
                {
                    LastInvalidTargetReason = "Another factory has this voxel as a target";
                    continue;
                }
                else if (!m_constructionBlock.HasRequiredPowerForNewTarget(this))
                {
                    LastInvalidTargetReason = "Insufficient power for another target.";
                    break;
                }
                    
                bool found = false;
                foreach (var block in blockList.ToList())
                {
                    // This can be sped up if necessary by indexing items by position
                    if (block != null && block.GetTarget<NaniteMiningTargets>().TargetList.FirstOrDefault(x => ((NaniteMiningItem)x).Position == item.Position) != null)
                    {
                        found = true;
                        LastInvalidTargetReason = "Another factory has this voxel as a target";
                        break;
                    }
                }

                if (found)
                    continue;

                if (Vector3D.DistanceSquared(m_constructionBlock.ConstructionBlock.GetPosition(), item.Position) < m_maxDistance * m_maxDistance)
                {
                    Logging.Instance.WriteLine(string.Format("ADDING Mining Target: conid={0} pos={1} type={2}", 
                        m_constructionBlock.ConstructionBlock.EntityId, item.Position, MyDefinitionManager.Static.GetVoxelMaterialDefinition(item.VoxelMaterial).MinedOre));

                    MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                    {
                        if (m_constructionBlock.IsUserDefinedLimitReached())
                            InvalidTargetReason("User defined maximum nanite limit reached");
                        else if (item != null)
                        {
                            TargetList.Add(item);
                            m_globalPositionList.Add(item.Position);
                        }
                    });
                    
                    if (targetListCount++ >= GetMaximumTargets())
                        break;
                }
            }
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
                if (!IsEnabled())
                {
                    Logging.Instance.WriteLine("CANCELLING Mining Target due to being disabled");
                    CancelTarget(target);
                    return;
                }

                if (m_constructionBlock.FactoryState != NaniteConstructionBlock.FactoryStates.Active)
                    return;

                if (!m_constructionBlock.IsPowered())
                {
                    Logging.Instance.WriteLine("CANCELLING Mining Target due to power shortage");
                    CancelTarget(target);
                    return;
                }

                if (!m_targetTracker.ContainsKey(target))
                    m_constructionBlock.SendAddTarget(target);

                if (m_targetTracker.ContainsKey(target))
                {
                    var trackedItem = m_targetTracker[target];
                    if (MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - trackedItem.StartTime >= trackedItem.CarryTime &&
                        MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - trackedItem.LastUpdate > 2000)
                    {
                        trackedItem.LastUpdate = MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;

                        if (!TransferFromTarget(target))
                            CancelTarget(target);
                        else
                            CompleteTarget(target);
                    }
                }
            }

            CreateMiningParticles(target);
        }

        private void CreateMiningParticles(NaniteMiningItem target)
        {
            if (!m_targetTracker.ContainsKey(target))
                CreateTrackerItem(target);

            if (NaniteParticleManager.TotalParticleCount > NaniteParticleManager.MaxTotalParticles)
                return;

            // Create Particle
            Vector4 startColor = new Vector4(0.7f, 0.2f, 0.0f, 1f);
            Vector4 endColor = new Vector4(0.2f, 0.05f, 0.0f, 0.35f);
            m_constructionBlock.ParticleManager.AddParticle(startColor, endColor, GetMinTravelTime() * 1000f, GetSpeed(), target, null);
        }

        private void CreateTrackerItem(NaniteMiningItem target)
        {
            double distance = Vector3D.Distance(m_constructionBlock.ConstructionBlock.GetPosition(), target.Position);
            int time = (int)Math.Max(GetMinTravelTime() * 1000f, (distance / GetSpeed()) * 1000f);

            NaniteMiningTarget miningTarget = new NaniteMiningTarget();
            miningTarget.ParticleCount = 0;
            miningTarget.StartTime = MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
            miningTarget.LastUpdate = MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
            miningTarget.CarryTime = time - 1000;
            m_targetTracker.Add(target, miningTarget);
        }

        private bool TransferFromTarget(NaniteMiningItem target)
        {
            byte material = 0;
            float amount = 0;

            IMyEntity entity;
            if (!MyAPIGateway.Entities.TryGetEntityById(target.VoxelId, out entity))
                return false;

            IMyVoxelBase voxel = entity as IMyVoxelBase;
            Vector3D targetMin = target.Position;
            Vector3D targetMax = target.Position;
            Vector3I minVoxel, maxVoxel;
            MyVoxelCoordSystems.WorldPositionToVoxelCoord(voxel.PositionLeftBottomCorner, ref targetMin, out minVoxel);
            MyVoxelCoordSystems.WorldPositionToVoxelCoord(voxel.PositionLeftBottomCorner, ref targetMax, out maxVoxel);

            MyVoxelBase voxelBase = voxel as MyVoxelBase;

            minVoxel += voxelBase.StorageMin;
            maxVoxel += voxelBase.StorageMin + 4;

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
            material = cache.Material(0);

            if (original == MyVoxelConstants.VOXEL_CONTENT_EMPTY)
            {
                Logging.Instance.WriteLine("Content is empty!");
                AddMinedPosition(target);
                
                return false;
            }

            Logging.Instance.WriteLine($"Material: SizeLinear: {cache.SizeLinear}, Size3D: {cache.Size3D}, AboveISO: {cache.ContainsVoxelsAboveIsoLevel()}");
            cache.Content(0, 0);

            var voxelMat = target.Definition;
            amount = CalculateAmount(voxelMat, original * 3.9f);

            Logging.Instance.WriteLine($"Removing: {target.Position} ({material} {amount})");

            if (material == 0)
            {
                Logging.Instance.WriteLine(string.Format("Material is 0", target.VoxelId));
                AddMinedPosition(target);
                return false;
            }

            if (amount == 0f)
            {
                Logging.Instance.WriteLine(string.Format("Amount is 0", target.VoxelId));
                AddMinedPosition(target);
                return false;
            }

            var def = MyDefinitionManager.Static.GetVoxelMaterialDefinition(target.VoxelMaterial);
            var item = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Ore>(def.MinedOre);
            var inventory = ((MyCubeBlock)m_constructionBlock.ConstructionBlock).GetInventory();
            MyInventory targetInventory = ((MyCubeBlock)m_constructionBlock.ConstructionBlock).GetInventory();

            if (targetInventory != null && targetInventory.CanItemsBeAdded((MyFixedPoint)amount, item.GetId()))
            {
                var ownerName = targetInventory.Owner as IMyTerminalBlock;
                if (ownerName != null)
                    Logging.Instance.WriteLine($"TRANSFER Adding {amount} {item.GetId().SubtypeName} to {ownerName.CustomName}");

                targetInventory.AddItems((MyFixedPoint)amount, item);

                voxelBase.PerformCutOutSphereFast(target.Position, 3.9f, true);
                //Eventually change this to voxelBase.CutOutShapeWithProperties to only remove the material the user wants, rather than wasting unwanted material.
                
                AddMinedPosition(target);

                return true;
            }
            Logging.Instance.WriteLine(string.Format("Mined materials could not be moved. No free cargo space!"));
            return false;
        }

        private void AddMinedPosition(NaniteMiningItem target)
        {
            foreach (var oreDetector in NaniteConstructionManager.OreDetectors)
            {
                //Logging.Instance.WriteLine($"{target.OreDetectorId} | {((MyEntity)oreDetector.Value.Block).EntityId}");
                if((long)target.OreDetectorId == (long)((MyEntity)oreDetector.Value.Block).EntityId)
                {
                    Logging.Instance.WriteLine($"Adding a mined position{target.Position}");
                    oreDetector.Value.minedPositions.Add(target.Position);
                }
            }
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

        public override void CancelTarget(object obj)
        {
            var target = obj as NaniteMiningItem;
            Logging.Instance.WriteLine(string.Format("CANCELLED Mining Target: {0} - {1} (VoxelID={2},Position={3})", m_constructionBlock.ConstructionBlock.EntityId, obj.GetType().Name, target.VoxelId, target.Position));
            if (Sync.IsServer)
            {
                m_constructionBlock.SendCompleteTarget((NaniteMiningItem)obj);
            }

            m_constructionBlock.ParticleManager.CancelTarget(target);
            if (m_targetTracker.ContainsKey(target))
                m_targetTracker.Remove(target);

            m_globalPositionList.Remove(target.Position);
            Remove(obj);
        }

        public override void CompleteTarget(object obj)
        {
            var target = obj as NaniteMiningItem;
            Logging.Instance.WriteLine(string.Format("COMPLETED Mining Target: {0} - {1} (VoxelID={2},Position={3})", m_constructionBlock.ConstructionBlock.EntityId, obj.GetType().Name, target.VoxelId, target.Position));
            if (Sync.IsServer)
            {
                m_constructionBlock.SendCompleteTarget((NaniteMiningItem)obj);
            }

            m_constructionBlock.ParticleManager.CompleteTarget(target);
            if (m_targetTracker.ContainsKey(target))
                m_targetTracker.Remove(target);

            m_globalPositionList.Remove(target.Position);
            Remove(obj);
        }
    }
}
