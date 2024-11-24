using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;
using Sandbox.Game.Entities;
using Sandbox.Definitions;
using VRage.Game;
using VRage.Utils;

using NaniteConstructionSystem.Particles;
using NaniteConstructionSystem.Extensions;
using NaniteConstructionSystem.Entities.Beacons;
using NaniteConstructionSystem.Integration;
using Sandbox.Game;

namespace NaniteConstructionSystem.Entities.Targets
{
    public class NaniteProjectionTarget
    {
        public int ParticleCount { get; set; }
        public int StartTime { get; set; }
        public bool CheckInventory { get; set; }
    }

    public class NaniteProjectionTargets : NaniteTargetBlocksBase
    {
        public override string TargetName
        {
            get { return "Projection"; }
        }

        private Dictionary<IMySlimBlock, NaniteProjectionTarget> m_targetBlocks;
        private float m_orientationAngle = 0.0f;
        private Vector3 m_dirUp = new Vector3(1.0f, 0.0f, 0.0f);
        private Vector3 m_dirForward = new Vector3(0.0f, 1.0f, 0.0f);
        private int m_count;
        private float m_maxDistance = 300f;
        private int m_oldTargetListCount;
        private int m_oldTargetListCounter;
        private int m_boostTargetCountCounter;

        public NaniteProjectionTargets(NaniteConstructionBlock constructionBlock) : base(constructionBlock)
        {
            m_count = 0;
            m_targetBlocks = new Dictionary<IMySlimBlock, NaniteProjectionTarget>();
            m_maxDistance = NaniteConstructionManager.Settings.ProjectionMaxBeaconDistance;
        }
        
        public override void ClearInternalTargetList()
        {
            m_targetBlocks.Clear();
        }

        public override int GetMaximumTargets()
        {
            return (int)Math.Min((NaniteConstructionManager.Settings.ProjectionNanitesNoUpgrade * m_constructionBlock.FactoryGroup.Count)
              + m_constructionBlock.UpgradeValue("ProjectionNanites"), NaniteConstructionManager.Settings.ProjectionMaxStreams);
        }

        public override float GetPowerUsage()
        {
            return Math.Max(1, NaniteConstructionManager.Settings.ProjectionPowerPerStream
              - (int)m_constructionBlock.UpgradeValue("PowerNanites"));
        }

        public override float GetMinTravelTime()
        {
            return Math.Max(1f, NaniteConstructionManager.Settings.ProjectionMinTravelTime
              - m_constructionBlock.UpgradeValue("MinTravelTime"));
        }

        public override float GetSpeed()
        {
            return NaniteConstructionManager.Settings.ProjectionDistanceDivisor
              + m_constructionBlock.UpgradeValue("SpeedNanites");
        }

        public override bool IsEnabled(NaniteConstructionBlock factory)
        {
            if (!factory.ConstructionBlock.Enabled
              || !factory.ConstructionBlock.IsFunctional
              || (NaniteConstructionManager.TerminalSettings.ContainsKey(factory.ConstructionBlock.EntityId)
              && !NaniteConstructionManager.TerminalSettings[factory.ConstructionBlock.EntityId].AllowProjection))
            {
                factory.EnabledParticleTargets[TargetName] = false;
                return false;
            }

            factory.EnabledParticleTargets[TargetName] = true;
            return true;
        }

        public override void FindTargets(ref Dictionary<string, int> available, List<NaniteConstructionBlock> blockList)
        {
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                m_lastInvalidTargetReason = "";
            });

            var maxTargets = GetMaximumTargets();
            int targetListCount = TargetList.Count;
            
            // if (MyAPIGateway.Session.LocalHumanPlayer != null)
            //     MyVisualScriptLogicProvider.SendChatMessage("Targets: " + targetListCount + " - " + maxTargets,"Nanite", MyAPIGateway.Session.LocalHumanPlayer.IdentityId, "White");

            if (targetListCount == m_oldTargetListCount)
            {
                m_oldTargetListCounter++;
                m_oldTargetListCount = targetListCount;
            }
            else
            {
                m_oldTargetListCounter = 0;
            }

            if (m_oldTargetListCounter >= 10)
            {
                // if (MyAPIGateway.Session.LocalHumanPlayer != null)
                //     MyVisualScriptLogicProvider.SendChatMessage("Probably stuck, fixing","Nanite", MyAPIGateway.Session.LocalHumanPlayer.IdentityId, "White");
                
                InvalidTargetReason("Probably stuck, re-setting projection targets!");

                // maxTargets = 100;
                TargetList.Clear();
                PotentialTargetList.Clear();
                PotentialIgnoredList.Clear();
                PotentialTargetListCount = 0;
                IgnoredCheckedTimes.Clear();
                m_oldTargetListCounter = 0;
                return;
            }

            if (targetListCount >= maxTargets)
            {
                if (PotentialTargetList.Count > 0)
                    InvalidTargetReason("Maximum targets reached. Add more upgrades!");
                return;
            }
            
            string lastInvalidTargetReason = "";
            StringBuilder chatMessageLogging = new StringBuilder();
            
            var invalidCount = 0;
            var missingCount = 0;
            var ignoredCount = 0;

            foreach (var item in PotentialTargetList.ToList())
            {
                if (item == null || TargetList.Contains(item))
                {
                    invalidCount++;
                    continue;
                }

                if (PotentialIgnoredList.Contains(item))
                {
                    ignoredCount++;
                    continue;
                }

                IMySlimBlock slimBlock = (IMySlimBlock)item;

                if (slimBlock == null)
                {
                    missingCount++;
                    continue;
                }

                // check obstructions
                var isObstructed = false;
                foreach (var possibleProjector in NaniteConstructionManager.ProjectorBlocks)
                {
                    var cubeGridProjected = slimBlock.CubeGrid as MyCubeGrid;
                    if (cubeGridProjected == null) break;
                    
                    var projector = possibleProjector.Value as IMyProjector;
                    if (projector == null)
                        continue;

                    int subgridIndex;
                    if (!ProjectorIntegration.TryGetSubgridIndex(projector, slimBlock, out subgridIndex))
                        continue;

                    var cubeGrid = cubeGridProjected.Projector.CubeGrid;
                    Vector3I blockPos = cubeGrid.WorldToGridInteger(cubeGridProjected.GridIntegerToWorld(slimBlock.Position));
                    var obstBlock = cubeGrid.GetCubeBlock(blockPos) as IMySlimBlock;
                    if (obstBlock != null)
                    {

                        if (obstBlock.BlockDefinition.Id.SubtypeName != slimBlock.BlockDefinition.Id.SubtypeName ||
                            obstBlock.Integrity < obstBlock.MaxIntegrity)
                        {
                            // this should be ok, we need to finish/repair blocks
                            break;
                        }

                        if (obstBlock.BlockDefinition.Id.SubtypeName != slimBlock.BlockDefinition.Id.SubtypeName ||
                            obstBlock.IsFullIntegrity)
                            isObstructed = true;

                        chatMessageLogging.AppendLine("Obstruction found: " +
                                                      slimBlock.BlockDefinition.DisplayNameText + ";" +
                                                      obstBlock.BlockDefinition.DisplayNameText + " : " +
                                                      obstBlock.IsFullIntegrity + ";" + obstBlock.Integrity + ";" +
                                                      obstBlock.BuildIntegrity + ";" + obstBlock.MaxIntegrity);
                        
                        break;
                    }
                }

                if (isObstructed)
                {
                    if (IgnoredCheckedTimes.ContainsKey(item))
                    {
                        IgnoredCheckedTimes[item]++;
                        if (IgnoredCheckedTimes[item] > 4)
                        {
                            lastInvalidTargetReason = "Target is obstructed by another block";
                            AddToIgnoreList(item);
                            CancelTarget(item);
                            continue;
                        }
                    }
                    else
                    {
                        IgnoredCheckedTimes.Add(item, 1);
                    }
                }
                
                // check if it is fully built if from projection, if not, give warning and skip
                // if (slimBlock.CubeGrid.Physics == null)
                // {
                //     if (slimBlock.Integrity < slimBlock.MaxIntegrity ||
                //         slimBlock.BuildIntegrity < slimBlock.MaxIntegrity || !slimBlock.IsFullIntegrity)
                //     {
                //         chatMessageLogging.AppendLine("Unfinished block: " + slimBlock.BlockDefinition.DisplayNameText +
                //                                       " missing " +
                //                                       Math.Abs(slimBlock.Integrity - slimBlock.MaxIntegrity));
                //         lastInvalidTargetReason = "Unfinished or damaged block in projection";
                //         AddToIgnoreList(item);
                //         CancelTarget(item);
                //         continue;
                //     }
                // }
                
                // check components
                var missing = m_constructionBlock.InventoryManager.GetProjectionComponents(slimBlock, true);
                bool haveComponents = m_constructionBlock.InventoryManager.CheckComponentsAvailable(ref missing, ref available);
                
                if ((MyAPIGateway.Session.CreativeMode || haveComponents) && m_constructionBlock.HasRequiredPowerForNewTarget(this)
                    && (slimBlock.CubeGrid.GetPosition() != Vector3D.Zero))
                {
                    bool found = false;
                    foreach (var block in blockList)
                    {
                        if (block != null && block.GetTarget<NaniteProjectionTargets>().TargetList.Contains(item))
                        {
                            found = true;
                            lastInvalidTargetReason = "Another factory has this block as a target";
                            break;
                        }
                    }

                    if (found)
                        continue;

                    AddTarget(item);
                    
                    var def = slimBlock.BlockDefinition as MyCubeBlockDefinition;
                    Logging.Instance.WriteLine(string.Format("[Projection] Adding Projection Target: conid={0} subtypeid={1} entityID={2} position={3}",
                        m_constructionBlock.ConstructionBlock.EntityId, def.Id.SubtypeId, slimBlock.FatBlock != null ? slimBlock.FatBlock.EntityId : 0, slimBlock.Position), 1);
                    
                    if (++targetListCount >= maxTargets && m_boostTargetCountCounter < 10)
                    {
                        m_boostTargetCountCounter++;
                        chatMessageLogging.AppendLine("Too many possible targets > maxTargets, checked " + m_boostTargetCountCounter + " times");
                        break;
                    }
                }
                else
                {
                    lastInvalidTargetReason = !haveComponents ? "Missing components" : "Insufficient power for another target";
                    if (!haveComponents)
                    {
                        if (IgnoredCheckedTimes.ContainsKey(item))
                        {
                            IgnoredCheckedTimes[item]++;
                            if (IgnoredCheckedTimes[item] > 4)
                                AddToIgnoreList(item);
                        }
                        else
                        {
                            IgnoredCheckedTimes.Add(item, 1);
                        }
                    }
                }
            }
            
            if (m_boostTargetCountCounter >= 10)
            {
                m_boostTargetCountCounter = 0;
            }
            
            chatMessageLogging.AppendLine("Targets: " + targetListCount + " - " + maxTargets);
            
            if (invalidCount > 0)
                chatMessageLogging.AppendLine("In TargetList: " + invalidCount);
            
            if (ignoredCount > 0)
                chatMessageLogging.AppendLine("Ignored: " + ignoredCount);
            
            if (missingCount > 0)
                chatMessageLogging.AppendLine("Missing SlimBlock: " + missingCount);
            
            // if (chatMessageLogging.Length > 0 && MyAPIGateway.Session.LocalHumanPlayer != null)
            //     MyVisualScriptLogicProvider.SendChatMessage(chatMessageLogging.ToString(), "Nanite", MyAPIGateway.Session.LocalHumanPlayer.IdentityId, "White");

            if (lastInvalidTargetReason != "")
                InvalidTargetReason(lastInvalidTargetReason);
        }

        public override void Update()
        {
            foreach (var item in TargetList.ToList())
            {
                var block = item as IMySlimBlock;
                if (block != null)
                    ProcessProjectedItem(block);
            }
        }

        private void ProcessProjectedItem(IMySlimBlock target)
        {
            try
            {
                if (Sync.IsServer)
                {
                    if (target.CubeGrid.GetPosition() == Vector3D.Zero)
                    {
                        Logging.Instance.WriteLine("[Projection] Cancelling Projection Target due to invalid position", 1);
                        AddToIgnoreList(target);
                        CancelTarget(target);
                        return;
                    }

                    if (!((m_constructionBlock.FactoryState == NaniteConstructionBlock.FactoryStates.Active || m_constructionBlock.FactoryState == NaniteConstructionBlock.FactoryStates.MissingParts) 
                          && (TargetList.Count > 0 || PotentialTargetList.Count > 0)))
                        return;

                    if (!IsInRange(target, m_maxDistance))
                    {
                        Logging.Instance.WriteLine("[Projection] Cancelling Projection Target due to being out of range", 1);
                        AddToIgnoreList(target);
                        CancelTarget(target);
                        return;
                    }

                    double distance = EntityHelper.GetDistanceBetweenBlockAndSlimblock((IMyCubeBlock)m_constructionBlock.ConstructionBlock, target);
                    int time = (int)Math.Max(GetMinTravelTime() * 1000f, (distance / GetSpeed()) * 1000f);

                    if (!m_targetBlocks.ContainsKey(target))
                    {
                        var projectionTarget = new NaniteProjectionTarget
                        {
                            ParticleCount = 0,
                            StartTime = (int)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds
                        };
                        m_targetBlocks.Add(target, projectionTarget);
                        int subgridIndex;
                        var projectorId = GetProjectorAndSubgridByBlock(target, out subgridIndex);
                        m_constructionBlock.SendAddTarget(target, TargetTypes.Projection, projectorId, subgridIndex);
                    }

                    var targetBlock = m_targetBlocks[target];

                    if (MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - targetBlock.StartTime >= time / 2.5)
                    {
                        var availableComponents = new Dictionary<string, int>();
                        m_constructionBlock.InventoryManager.GetAvailableComponents(ref availableComponents);
                        var missing = m_constructionBlock.InventoryManager.GetProjectionComponents(target, true);
                        bool haveComponents = m_constructionBlock.InventoryManager.CheckComponentsAvailable(ref missing, ref availableComponents);
                        
                        if (!haveComponents)
                        {
                            AddToIgnoreList(target);
                            CancelTarget(target);
                            return;
                        }
                    }

                    if (MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - targetBlock.StartTime >= time / 2)
                    {
                        ProcessBuildBlock(target);
                        CompleteTarget(target);
                        return;
                    }
                }

                if (IsInRange(target, m_maxDistance))
                    CreateProjectionParticle(target);
            }
            catch (Exception e)
            {
                Logging.Instance.WriteLine($"{e}");
            }
        }

        public void AddToIgnoreList(IMySlimBlock obj){
            if (PotentialIgnoredList.Contains(obj) == false) {
                PotentialIgnoredList.Add(obj);
                
                if (PotentialTargetList.Contains(obj)) 
                    PotentialTargetList.Remove(obj);

                if (TargetList.Contains(obj))
                    TargetList.Remove(obj);
            }
        }

        public override void AddToIgnoreList(object obj){
            if (PotentialIgnoredList.Contains(obj) == false) {
                PotentialIgnoredList.Add(obj);
                
                if (PotentialTargetList.Contains(obj)) 
                    PotentialTargetList.Remove(obj);

                if (TargetList.Contains(obj))
                    TargetList.Remove(obj);
            }
        }

        public void CancelTarget(IMySlimBlock target)
        {
            Logging.Instance.WriteLine(string.Format("[Projection] Cancelling Projection Target: {0} - {1} (EntityID={2},Position={3})",
              m_constructionBlock.ConstructionBlock.EntityId, target.GetType().Name, target.FatBlock != null ? target.FatBlock.EntityId : 0, target.Position), 1);

            if (Sync.IsServer)
                m_constructionBlock.SendCancelTarget(target, TargetTypes.Projection, GetProjectorByBlock(target));

            m_constructionBlock.ParticleManager.CancelTarget(target);
            m_constructionBlock.ToolManager.Remove(target);
            Remove(target);
            m_targetBlocks.Remove(target);
        }

        public override void CancelTarget(object obj)
        {
            var target = obj as IMySlimBlock;
            if (target == null)
                return;

            CancelTarget(target);
        }

        public override void CompleteTarget(object obj)
        {
            var target = obj as IMySlimBlock;
            if (target == null)
                return;

            CompleteTarget(target);
        }

        public void CompleteTarget(IMySlimBlock target)
        {
            Logging.Instance.WriteLine(string.Format("[Projection] Completing Projection Target: {0} - {1} (EntityID={2},Position={3})",
              m_constructionBlock.ConstructionBlock.EntityId, target.GetType().Name, target.FatBlock != null ? target.FatBlock.EntityId : 0, target.Position), 1);

            if (Sync.IsServer)
                m_constructionBlock.SendCompleteTarget(target, TargetTypes.Projection, GetProjectorByBlock(target));

            m_constructionBlock.ParticleManager.CompleteTarget(target);
            m_constructionBlock.ToolManager.Remove(target);
            Remove(target);
            m_targetBlocks.Remove(target);
        }

        private void CreateProjectionParticle(IMySlimBlock target)
        {
            try
            {
                if (!m_targetBlocks.ContainsKey(target))
                {
                    Logging.Instance.WriteLine($"[Projection] Adding ProjectionParticle Target: {target.Position}", 1);
                    NaniteProjectionTarget projectionTarget = new NaniteProjectionTarget
                    {
                        ParticleCount = 0,
                        StartTime = (int)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds
                    };
                    m_targetBlocks.Add(target, projectionTarget);
                }

                Vector3D targetPosition;
                if (target.FatBlock != null)
                {
                    targetPosition = target.FatBlock.GetPosition();
                }
                else
                {
                    var size = target.CubeGrid.GridSizeEnum == MyCubeSize.Small ? 0.5f : 2.5f;
                    var destinationPosition = new Vector3D(target.Position * size);
                    targetPosition = Vector3D.Transform(destinationPosition, target.CubeGrid.WorldMatrix);
                }

                NaniteConstructionBlock nearestFactory =  GetNearestFactory(TargetName, targetPosition);
                Vector4 startColor = new Vector4(0.95f, 0.0f, 0.95f, 0.75f);
                Vector4 endColor = new Vector4(0.035f, 0.0f, 0.35f, 0.75f);

                if (nearestFactory.ParticleManager.Particles.Count < NaniteParticleManager.MaxTotalParticles)
                {
                    nearestFactory.ParticleManager.AddParticle(startColor, endColor, GetMinTravelTime() * 1000f, GetSpeed(), target);
                }
            }
            catch (Exception e)
            {
                Logging.Instance.WriteLine($"{e}");
            }
        }

        public override void ParallelUpdate(List<IMyCubeGrid> gridList, ConcurrentBag<BlockTarget> blocks)
        {
            if (!IsEnabled(m_constructionBlock))
            {
                PotentialTargetList.Clear();
                return;
            }

            foreach (var block in blocks)
                CheckBlockProjection(block.Block);
        }

        public override void CheckBeacons()
        {
            var checkedGridIds = new List<long>();
            
            foreach (var beaconBlock in NaniteConstructionManager.BeaconList.Where(x => x.Value is NaniteBeaconProjection))
            {
                IMyCubeBlock item = (IMyCubeBlock)beaconBlock.Value.BeaconBlock;

				if (item == null || !((IMyFunctionalBlock)item).Enabled || !((IMyFunctionalBlock)item).IsFunctional
                  || !MyRelationsBetweenPlayerAndBlockExtensions.IsFriendly(item.GetUserRelationToOwner(m_constructionBlock.ConstructionBlock.OwnerId)) || !IsInRange(item.GetPosition(), m_maxDistance) )
					continue;

                List<IMySlimBlock> beaconBlocks = new List<IMySlimBlock>();

                foreach (var grid in MyAPIGateway.GridGroups.GetGroup((IMyCubeGrid)item.CubeGrid,
                             GridLinkTypeEnum.Physical))
                {
                    if (grid == null || checkedGridIds.Contains(grid.EntityId))
                        continue;
                    
                    grid.GetBlocks(beaconBlocks);
                    checkedGridIds.Add(grid.EntityId);
                }

                foreach (var block in beaconBlocks)
                {
                    m_constructionBlock.ScanBlocksCache.Add(new BlockTarget(block));
                }
            }
        }

        public static long GetProjectorByBlock(IMySlimBlock block)
        {
            int subgridIndex;
            return GetProjectorAndSubgridByBlock(block, out subgridIndex);
        }

        public static long GetProjectorAndSubgridByBlock(IMySlimBlock block, out int subgridIndex)
        {
            foreach(var item in NaniteConstructionManager.ProjectorBlocks)
            {
                var projector = item.Value as IMyProjector;
                if (projector == null)
                    continue;

                if (ProjectorIntegration.TryGetSubgridIndex(projector, block, out subgridIndex))
                    return projector.EntityId;
            }

            subgridIndex = 0;
            return 0;
        }

        private void CheckBlockProjection(IMySlimBlock item)
        {
            if (item.FatBlock == null || !(item.FatBlock is IMyProjector))
                return;

            IMyProjector projector = item.FatBlock as IMyProjector;
            if (projector.Enabled && projector.ProjectedGrid != null && projector.BuildableBlocksCount > 0)
                ProcessProjector(projector);
        }

        private void ProcessProjector(IMyProjector projector)
        {
            foreach (IMySlimBlock block in ProjectorIntegration.IterBuildableBlocks(projector))
                if (!PotentialTargetList.Contains(block) && !PotentialIgnoredList.Contains(block))
                    PotentialTargetList.Add(block);
        }

        private void ProcessBuildBlock(IMySlimBlock block)
        {
            try {
                var blockDefinition = block.BlockDefinition as MyCubeBlockDefinition;
                var localShipWelder = m_constructionBlock.ConstructionBlock as IMyShipWelder;

                if (block.FatBlock != null && block.FatBlock.Closed)
                {
                    CancelTarget(block);
                    return;
                }

                foreach(var item in NaniteConstructionManager.ProjectorBlocks)
                {
                    var projector = item.Value as IMyProjector;
                    if (projector == null)
                        continue;

                    int subgridIndex;
                    if (!ProjectorIntegration.TryGetSubgridIndex(projector, block, out subgridIndex))
                        continue;

                    if (localShipWelder != null && blockDefinition != null) {
                        var validator = localShipWelder.IsWithinWorldLimits(projector, blockDefinition.BlockPairName, blockDefinition.PCU);
                        if (!validator) {
                            CancelTarget(block);
                            m_constructionBlock.UpdateOverLimit = true;
                            break;
                        }


                        m_constructionBlock.UpdateOverLimit = false;

                        var localBlockBuiltBy = (MyCubeBlock) m_constructionBlock.ConstructionBlock;
                        var ownerId = m_constructionBlock.ConstructionBlock.OwnerId;

                        // no defined owner
                        if (ownerId == 0) {
                            if (block.CubeGrid != null && block.CubeGrid.BigOwners[0] != null) {
                                ownerId = block.CubeGrid.BigOwners[0];
                            }

                            if (ownerId == 0 && localBlockBuiltBy != null && localBlockBuiltBy.BuiltBy != null) {
                                ownerId = localBlockBuiltBy.BuiltBy;
                            }
                        }
                        
                        // do not build without owner
                        if (ownerId == 0)
                            break;
                        
                        // Fully build the block if integrity checks are passed
                        projector.Build(block, ownerId, m_constructionBlock.ConstructionBlock.EntityId, false, ownerId);
                        
                        break;
                    }
                }
            } catch(Exception exc) {
                MyLog.Default.WriteLine($"##MOD: nanites, ERROR: {exc}");
            }
        }
    }
}
