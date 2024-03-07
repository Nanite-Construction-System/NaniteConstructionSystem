using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using Sandbox.Common.ObjectBuilders;
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
using NaniteConstructionSystem.Integration;

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
        private bool allowAllNextTime = false;

        public NaniteProjectionTargets(NaniteConstructionBlock constructionBlock) : base(constructionBlock)
        {
            m_count = 0;
            m_targetBlocks = new Dictionary<IMySlimBlock, NaniteProjectionTarget>();
            m_maxDistance = NaniteConstructionManager.Settings.ProjectionMaxBeaconDistance;
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
                ComponentsRequired.Clear();
            });

            var maxTargets = GetMaximumTargets();

            if (TargetList.Count >= maxTargets)
            {
                if(PotentialTargetList.Count > 0)
                    InvalidTargetReason("Maximum targets reached. Add more upgrades!");

                return;
            }

            NaniteConstructionInventory inventoryManager = m_constructionBlock.InventoryManager;
            Vector3D sourcePosition = m_constructionBlock.ConstructionBlock.GetPosition();
            Dictionary<string, int> missing = new Dictionary<string, int>();
            string LastInvalidTargetReason = "";

            int TargetListCount = TargetList.Count;
            var orderedList = PotentialTargetList.OrderBy(x => Vector3D.Distance(sourcePosition, EntityHelper.GetBlockPosition((IMySlimBlock)x))).ToList();
            var ignoredCount = 0;
            var ignoreBlockCheck = false;

            if (allowAllNextTime) {
                ignoreBlockCheck = true;
            }

            foreach (var item in orderedList)
            {
                if (item == null || TargetList.Contains(item) || PotentialIgnoredList.Contains(item))
                    continue;

                missing = inventoryManager.GetProjectionComponents((IMySlimBlock)item, true);
                bool haveComponents = inventoryManager.CheckComponentsAvailable(ref missing, ref available);

                if ((MyAPIGateway.Session.CreativeMode || haveComponents) && m_constructionBlock.HasRequiredPowerForNewTarget(this)
                  && ((IMySlimBlock)item).CubeGrid.GetPosition() != Vector3D.Zero)
                {
                    bool found = false;
                    foreach (var block in blockList.ToList())
                    {
                        if (block != null && block.GetTarget<NaniteProjectionTargets>().TargetList.Contains(item))
                        {
                            found = true;
                            LastInvalidTargetReason = "Another factory has this block as a target";
                            break;
                        }
                    }

                    if (found) {
                        continue;
                    }

                    // item position has a block on it
                    var localSlimBlock = item as IMySlimBlock;
                    if (!ignoreBlockCheck && localSlimBlock != null && localSlimBlock.CubeGrid.GridSizeEnum != MyCubeSize.Small) {
                        var size = 2.5f;
                        var blockPosition = new Vector3D(localSlimBlock.Position * size);
                        var targetPosition = Vector3D.Transform(blockPosition, localSlimBlock.CubeGrid.WorldMatrix);

                        var addToCanceled = false;
                        var sphere = new BoundingSphereD(targetPosition, 1f);
                        var entityList = new List<MyEntity>();
                        MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref sphere, entityList);

                        foreach (var entity in entityList) {
                            var CubeGrid = entity as IMyCubeGrid;

                            if (CubeGrid == null) {
                                continue;
                            }

                            IMySlimBlock localBlock = CubeGrid.GetCubeBlock((Vector3I)blockPosition);

                            if (localBlock == null) {
                                continue;
                            }

                            if (localBlock.FatBlock == null || localBlock.CubeGrid.GridSizeEnum == MyCubeSize.Small) {
                                continue;
                            }

                            addToCanceled = true;
                            break;
                        }
                        if (addToCanceled) {
                            // MyLog.Default.WriteLine($"Target RESET FindTargets");
                            ignoredCount++;
                            continue;
                        }
                    }

                    AddTarget(item);

                    IMySlimBlock slimBlock = (IMySlimBlock)item;
                    var def = slimBlock.BlockDefinition as MyCubeBlockDefinition;
                    Logging.Instance.WriteLine(string.Format("[Projection] Adding Projection Target: conid={0} subtypeid={1} entityID={2} position={3}",
                        m_constructionBlock.ConstructionBlock.EntityId, def.Id.SubtypeId, slimBlock.FatBlock != null ? slimBlock.FatBlock.EntityId : 0, slimBlock.Position), 1);

                    if (++TargetListCount >= maxTargets)
                        break;
                }
                else if (!haveComponents) {
                    LastInvalidTargetReason = "Missing components to start projected block";
                    if (IgnoredCheckedTimes.ContainsKey(item)) {
                        IgnoredCheckedTimes[item]++;
                        if (IgnoredCheckedTimes[item] > 4) {
                            AddToIgnoreList(item);
                        }
                    } else {
                        IgnoredCheckedTimes.Add(item, 1);
                    }
                }

                else if (!m_constructionBlock.HasRequiredPowerForNewTarget(this))
                {
                    LastInvalidTargetReason = "Insufficient power for another target.";
                    break;
                }

            }
            if (ignoredCount > 0 && !allowAllNextTime) {
                allowAllNextTime = true;
            } else {
                allowAllNextTime = false;
            }
            if (LastInvalidTargetReason != "") {
                InvalidTargetReason(LastInvalidTargetReason);
            }
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
            if (Sync.IsServer)
            {
                if (target.CubeGrid.GetPosition() == Vector3D.Zero)
                {
                    Logging.Instance.WriteLine("[Projection] Cancelling Projection Target due to invalid position", 1);
                    AddToIgnoreList(target);
                    CancelTarget(target);
                    return;
                }

                if (!((m_constructionBlock.FactoryState == NaniteConstructionBlock.FactoryStates.Active || m_constructionBlock.FactoryState == NaniteConstructionBlock.FactoryStates.MissingParts) && (TargetList.Count > 0 || PotentialTargetList.Count > 0))) {
                    return;
                }

                if (!IsInRange(target, m_maxDistance))
                {
                    Logging.Instance.WriteLine("[Projection] Cancelling Projection Target due to being out of range", 1);
                    AddToIgnoreList(target);
                    CancelTarget(target);
                }

                double distance = EntityHelper.GetDistanceBetweenBlockAndSlimblock((IMyCubeBlock)m_constructionBlock.ConstructionBlock, target);
                int time = (int)Math.Max(GetMinTravelTime() * 1000f, (distance / GetSpeed()) * 1000f);

                if (!m_targetBlocks.ContainsKey(target))
                {
                    NaniteProjectionTarget projectionTarget = new NaniteProjectionTarget();
                    projectionTarget.ParticleCount = 0;
                    projectionTarget.StartTime = (int)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
                    m_targetBlocks.Add(target, projectionTarget);
                    int subgridIndex;
                    var projectorId = GetProjectorAndSubgridByBlock(target, out subgridIndex);
                    m_constructionBlock.SendAddTarget(target, TargetTypes.Projection, projectorId, subgridIndex);
                }

                if (MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - m_targetBlocks[target].StartTime >= time / 2.5 && !m_targetBlocks[target].CheckInventory)
                {
                    m_targetBlocks[target].CheckInventory = true;
                    /*if (!m_constructionBlock.InventoryManager.ProcessMissingComponents(target) && !MyAPIGateway.Session.CreativeMode)
                    {
                        AddToIgnoreList(target);
                        CancelTarget(target);
                        return;
                    }*/
                }

                if (MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - m_targetBlocks[target].StartTime >= time / 2)
                {
                    ProcessBuildBlock(target);
                    CompleteTarget(target);
                    return;
                }
            }
            CreateProjectionParticle(target);
        }

        public void AddToIgnoreList(IMySlimBlock target){

            object obj = target as object;

            if (PotentialIgnoredList.Contains(obj) == false) {
                PotentialIgnoredList.Add(obj);
                if (PotentialTargetList.Contains(obj)) {
                    PotentialTargetList.Remove(obj);
                }
            }
        }

        public override void AddToIgnoreList(object obj){
            if (PotentialIgnoredList.Contains(obj) == false) {
                PotentialIgnoredList.Add(obj);
                if (PotentialTargetList.Contains(obj)) {
                    PotentialTargetList.Remove(obj);
                }
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
            if (!m_targetBlocks.ContainsKey(target))
            {
                Logging.Instance.WriteLine($"[Projection] Adding ProjectionParticle Target: {target.Position}", 1);
                NaniteProjectionTarget projectionTarget = new NaniteProjectionTarget();
                projectionTarget.ParticleCount = 0;
                projectionTarget.StartTime = (int)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
                m_targetBlocks.Add(target, projectionTarget);
            }

            try {
                Vector3D targetPosition = default(Vector3D);

                if (target.FatBlock != null) {
                    targetPosition = target.FatBlock.GetPosition();
                } else {
                    var size = target.CubeGrid.GridSizeEnum == MyCubeSize.Small ? 0.5f : 2.5f;
                    var destinationPosition = new Vector3D(target.Position * size);
                    targetPosition = Vector3D.Transform(destinationPosition, target.CubeGrid.WorldMatrix);
                }

                NaniteConstructionBlock nearestFactory = GetNearestFactory(TargetName, targetPosition);

                Vector4 startColor = new Vector4(0.95f, 0.0f, 0.95f, 0.75f);
                Vector4 endColor = new Vector4(0.035f, 0.0f, 0.35f, 0.75f);

                if (nearestFactory.ParticleManager.Particles.Count < NaniteParticleManager.MaxTotalParticles) {
                    MyAPIGateway.Utilities.InvokeOnGameThread(() => {
                        nearestFactory.ParticleManager.AddParticle(startColor, endColor, GetMinTravelTime() * 1000f, GetSpeed(), target);
                    });
                }
            } catch (Exception e) {
                Logging.Instance.WriteLine($"{e}");
            }
        }

        public override void ParallelUpdate(List<IMyCubeGrid> gridList, List<BlockTarget> blocks)
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
            foreach (var beaconBlock in NaniteConstructionManager.BeaconList.Where(x => x.Value is NaniteBeaconProjection))
            {
                IMyCubeBlock item = (IMyCubeBlock)beaconBlock.Value.BeaconBlock;

				if (item == null || !((IMyFunctionalBlock)item).Enabled || !((IMyFunctionalBlock)item).IsFunctional
                  || !MyRelationsBetweenPlayerAndBlockExtensions.IsFriendly(item.GetUserRelationToOwner(m_constructionBlock.ConstructionBlock.OwnerId)) || !IsInRange(item.GetPosition(), m_maxDistance) )
					continue;

                List<IMySlimBlock> beaconBlocks = new List<IMySlimBlock>();

                foreach (var grid in MyAPIGateway.GridGroups.GetGroup((IMyCubeGrid)item.CubeGrid, GridLinkTypeEnum.Physical))
                    grid.GetBlocks(beaconBlocks);

                foreach (var block in beaconBlocks)
                    m_constructionBlock.ScanBlocksCache.Add(new BlockTarget(block));
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
                        if (ownerId > 0) {
                            projector.Build(block, ownerId, m_constructionBlock.ConstructionBlock.EntityId, false, ownerId);
                        }
                        break;
                    }
                }
            } catch(Exception exc) {
                MyLog.Default.WriteLineAndConsole($"##MOD: nanites, ERROR: {exc}");
            }
        }
    }
}
