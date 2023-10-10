using NaniteConstructionSystem.Entities.Beacons;
using NaniteConstructionSystem.Entities.Tools;
using NaniteConstructionSystem.Extensions;
using NaniteConstructionSystem.Particles;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using NaniteConstructionSystem.Integration;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace NaniteConstructionSystem.Entities.Targets
{
    public class NaniteConstructionTarget
    {
        public int ParticleCount { get; set; }
    }

    public class NaniteConstructionTargets : NaniteTargetBlocksBase
    {
        public override string TargetName
            {get {return "Construction";}}

        private Dictionary<IMySlimBlock, int> m_targetBlocks;
        private float m_maxDistance = 300f;
        private HashSet<IMySlimBlock> m_remoteTargets;
        private FastResourceLock m_remoteLock;
        private List<IMySlimBlock> beaconBlocks = new List<IMySlimBlock>();
        private int GetBeaconBlocksRetryCounter;

        public NaniteConstructionTargets(NaniteConstructionBlock constructionBlock) : base(constructionBlock)
        {
            m_targetBlocks = new Dictionary<IMySlimBlock, int>();
            m_maxDistance = NaniteConstructionManager.Settings.ConstructionMaxBeaconDistance;
            m_remoteTargets = new HashSet<IMySlimBlock>();
            m_remoteLock = new FastResourceLock();
        }

        public override int GetMaximumTargets()
        {
            return (int)Math.Min((NaniteConstructionManager.Settings.ConstructionNanitesNoUpgrade * m_constructionBlock.FactoryGroup.Count)
              + m_constructionBlock.UpgradeValue("ConstructionNanites"), NaniteConstructionManager.Settings.ConstructionMaxStreams);
        }

        public override float GetPowerUsage()
        {
            return Math.Max(1, NaniteConstructionManager.Settings.ConstructionPowerPerStream
              - (int)m_constructionBlock.UpgradeValue("PowerNanites"));
        }

        public override float GetMinTravelTime()
        {
            return Math.Max(1f, NaniteConstructionManager.Settings.ConstructionMinTravelTime
              - m_constructionBlock.UpgradeValue("MinTravelTime"));
        }

        public override float GetSpeed()
        {
            return NaniteConstructionManager.Settings.ConstructionDistanceDivisor
              + m_constructionBlock.UpgradeValue("SpeedNanites");
        }

        public override bool IsEnabled(NaniteConstructionBlock factory)
        {
            if (!((IMyFunctionalBlock)factory.ConstructionBlock).Enabled
              || !((IMyFunctionalBlock)factory.ConstructionBlock).IsFunctional
              || (NaniteConstructionManager.TerminalSettings.ContainsKey(factory.ConstructionBlock.EntityId)
              && !NaniteConstructionManager.TerminalSettings[factory.ConstructionBlock.EntityId].AllowRepair))
            {
                factory.EnabledParticleTargets[TargetName] = false;
                return false;
            }

            factory.EnabledParticleTargets[TargetName] = true;
            return true;
        }

        public override void FindTargets(ref Dictionary<string, int> available, List<NaniteConstructionBlock> blockList)
        {
            InvalidTargetReason("");

            var maxTargets = GetMaximumTargets();

            if (m_targetList.Count >= maxTargets)
            {
                if (PotentialTargetList.Count > 0)
                    InvalidTargetReason("Maximum targets reached. Add more upgrades!");

                return;
            }

            Dictionary<string, int> missing = new Dictionary<string, int>();
            string LastInvalidTargetReason = "";

            int targetListCount = m_targetList.Count;
            List<object> localTargetList = m_potentialTargetList.ToList();
            localTargetList.Shuffle();

            foreach (IMySlimBlock item in localTargetList.ToList())
            {
                if (item == null || TargetList.Contains(item) || PotentialIgnoredList.Contains(item))
                    continue;

                missing.Clear();
                item.GetMissingComponents(missing);
                if (missing == null && !MyAPIGateway.Session.CreativeMode) {
                    AddToIgnoreList(item);
                    continue;
                }

                bool foundMissingComponents = true;

                if (missing.Count > 0) {
                    foundMissingComponents = m_constructionBlock.InventoryManager.CheckComponentsAvailable(ref missing, ref available);
                }

                if (foundMissingComponents && m_constructionBlock.HasRequiredPowerForNewTarget(this))
                {
                    bool found = false;
                    foreach (var block in blockList.ToList())
                    {
                        if (block != null && block != m_constructionBlock && !m_constructionBlock.Slaves.Contains(block)
                          && block.Targets.First(y => y is NaniteConstructionTargets).TargetList.Contains(item as IMySlimBlock))
                        {
                            found = true;
                            LastInvalidTargetReason = "Another factory has this block as a target";
                            break;
                        }
                    }

                    if (found)
                        continue;

                    AddTarget(item);

                    var def = item.BlockDefinition as MyCubeBlockDefinition;
                    Logging.Instance.WriteLine(string.Format("[Construction] Adding Construction/Repair Target: conid={0} subtype={1} entityID={2} position={3}",
                        m_constructionBlock.ConstructionBlock.EntityId, def.Id.SubtypeId, item.FatBlock != null ? item.FatBlock.EntityId : 0, item.Position), 1);

                    if (++targetListCount >= maxTargets)
                        break;
                }
                else if (!foundMissingComponents) {
                    LastInvalidTargetReason = "Missing components";
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
            if (LastInvalidTargetReason != "")
                InvalidTargetReason(LastInvalidTargetReason);
        }

        public override void Update()
        {
            try
            {
                foreach (var item in m_targetList.ToList()) {
                    var block = item as IMySlimBlock;
                    if (block != null) {
                        ProcessConstructionItem(block);
                    }
                }
            }
            catch (Exception e)
            {
                Logging.Instance.WriteLine($"{e}");
            }
        }

        private void ProcessConstructionItem(IMySlimBlock target)
        {
            try {
                if (Sync.IsServer)
                {
                    if (!((m_constructionBlock.FactoryState == NaniteConstructionBlock.FactoryStates.Active || m_constructionBlock.FactoryState == NaniteConstructionBlock.FactoryStates.MissingParts) && (TargetList.Count > 0 || PotentialTargetList.Count > 0)))
                        return;

                    if (!m_targetBlocks.ContainsKey(target))
                        m_targetBlocks.Add(target, 0);

                    NaniteWelder welder = (NaniteWelder)m_constructionBlock.ToolManager.Tools.FirstOrDefault(x => x.TargetBlock == target && x is NaniteWelder);
                    if (welder == null)
                    {
                        double distance = EntityHelper.GetDistanceBetweenBlockAndSlimblock((IMyCubeBlock)m_constructionBlock.ConstructionBlock, target);
                        int time = (int)Math.Max(GetMinTravelTime() * 1000f, (distance / GetSpeed()) * 1000f);
                        MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                        {
                            if (target == null)
                                return;

                            welder = new NaniteWelder(m_constructionBlock, target, (int)(time / 2.5f), false);
                            m_constructionBlock.ToolManager.Tools.Add(welder);
                            m_constructionBlock.SendAddTarget(target, TargetTypes.Construction);
                        });
                    }

                    if (target.IsFullIntegrity && !target.HasDeformation)
                    {
                        MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                        {
                            if (target != null)
                                CompleteTarget(target);
                        });
                        return;
                    }

                    if (target.IsDestroyed || target.IsFullyDismounted || (target.FatBlock != null && target.FatBlock.Closed))
                    {
                        Logging.Instance.WriteLine("[Construction] Cancelling Construction/Repair Target due to target being destroyed", 1);
                        MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                        {
                            if (target != null) {
                                AddToIgnoreList(target);
                                CancelTarget(target);
                            }
                        });
                        return;
                    }

                    if (welder != null && MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - welder.StartTime >= welder.WaitTime)
                    {
                        MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                        {
                            if (target == null)
                                return;

                            var blockDefinition = target.BlockDefinition as MyCubeBlockDefinition;
                            var localShipWelder = m_constructionBlock.ConstructionBlock as IMyShipWelder;

                            m_constructionBlock.UpdateOverLimit = false;

                            if (NaniteConstructionManager.ProjectorBlocks != null) {
                                foreach(var item in NaniteConstructionManager.ProjectorBlocks)
                                {
                                    var projector = item.Value as IMyProjector;
                                    if (projector == null)
                                        continue;

                                    int subgridIndex;
                                    if (!ProjectorIntegration.TryGetSubgridIndex(projector, target, out subgridIndex))
                                        return;

                                    if (localShipWelder != null && blockDefinition != null) {
                                        var validator = localShipWelder.IsWithinWorldLimits(projector, blockDefinition.BlockPairName, blockDefinition.PCU);
                                        if (!validator) {
                                            m_constructionBlock.UpdateOverLimit = true;
                                            break;
                                        }
                                    }
                                }
                            }

                            if (m_constructionBlock.UpdateOverLimit) {
                                AddToIgnoreList(target);
                                CancelTarget(target);
                                return;
                            }

                            target.MoveItemsToConstructionStockpile(((MyEntity)m_constructionBlock.ConstructionBlock).GetInventory());

                            if (!target.HasDeformation && !target.CanContinueBuild( ((MyEntity)m_constructionBlock.ConstructionBlock).GetInventory() ) && !MyAPIGateway.Session.CreativeMode)
                            {
                                Logging.Instance.WriteLine("[Construction] Cancelling Construction/Repair Target due to missing components", 1);

                                AddToIgnoreList(target);
                                CancelTarget(target);
                            }
                        });

                        return;
                    }
                    // NEW 12-1-2018 To save on performance, once a target is started, use SyncDistance only so we dont have to check each slave factory
                    if (m_remoteTargets.Contains(target)
                      && !IsInRange(target, m_maxDistance))
                    {
                        Logging.Instance.WriteLine("[Construction] Cancelling Repair Target due to being out of range", 1);
                        MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                            {
                                AddToIgnoreList(target);
                                CancelTarget(target);
                            });
                        return;
                    }
                }

                CreateConstructionParticle(target);
            } catch(Exception exc) {
                MyLog.Default.WriteLineAndConsole($"##MOD: nanites, ERROR: {exc}");
            }
        }

        private void CreateConstructionParticle(IMySlimBlock target)
        {
            try {
                if (!m_targetBlocks.ContainsKey(target))
                    m_targetBlocks.Add(target, 0);

                Vector4 startColor = new Vector4(0.55f, 0.55f, 0.95f, 0.75f);
                Vector4 endColor = new Vector4(0.05f, 0.05f, 0.35f, 0.75f);

                Vector3D targetPosition = default(Vector3D);

                if (target.FatBlock != null)
                    targetPosition = target.FatBlock.GetPosition();
                else
                {
                    var size = target.CubeGrid.GridSizeEnum == MyCubeSize.Small ? 0.5f : 2.5f;
                    var destinationPosition = new Vector3D(target.Position * size);
                    targetPosition = Vector3D.Transform(destinationPosition, target.CubeGrid.WorldMatrix);
                }

                var nearestFactory = m_constructionBlock;

                if (nearestFactory.ParticleManager.Particles.Count < NaniteParticleManager.MaxTotalParticles)
                    MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                    {
                        nearestFactory.ParticleManager.AddParticle(startColor, endColor, GetMinTravelTime() * 1000f, GetSpeed(), target);
                    });

            } catch (Exception e) {
                Logging.Instance.WriteLine($"{e}");
            }
        }

        public void CompleteTarget(IMySlimBlock obj)
        {
            try {
                Logging.Instance.WriteLine(string.Format("[Construction] Completing Construction/Repair Target: {0} - {1} (EntityID={2},Position={3})",
                  m_constructionBlock.ConstructionBlock.EntityId, obj.GetType().Name, obj.FatBlock != null ? obj.FatBlock.EntityId : 0, obj.Position), 1);

                var localBlockBuiltBy = (MyCubeBlock) m_constructionBlock.ConstructionBlock;
                var ownerId = m_constructionBlock.ConstructionBlock.OwnerId;

                // no defined owner
                if (ownerId == 0) {
                    if (obj != null && obj.CubeGrid != null && obj.CubeGrid.BigOwners[0] != null) {
                        ownerId = obj.CubeGrid.BigOwners[0];
                    }

                    if (ownerId == 0 && localBlockBuiltBy != null && localBlockBuiltBy.BuiltBy != null) {
                        ownerId = localBlockBuiltBy.BuiltBy;
                    }
                }

                if (obj != null && ownerId > 0) {
                    var cubeBlock = (MyCubeBlock) obj.FatBlock;
                    if (cubeBlock != null) {
                        cubeBlock.ChangeOwner(ownerId, MyOwnershipShareModeEnum.Faction);
                    }
                }

                if (Sync.IsServer) {
                    m_constructionBlock.SendCompleteTarget(obj, TargetTypes.Construction);
                }

                m_constructionBlock.ParticleManager.CompleteTarget(obj);
                m_constructionBlock.ToolManager.Remove(obj);
                Remove(obj);
                m_remoteTargets.Remove(obj);

            } catch (Exception e) {
                Logging.Instance.WriteLine($"{e}");
            }
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

        public void CancelTarget(IMySlimBlock obj)
        {
            Logging.Instance.WriteLine(string.Format("[Construction] Cancelling Construction/Repair Target: {0} - {1} (EntityID={2},Position={3})",
              m_constructionBlock.ConstructionBlock.EntityId, obj.GetType().Name, obj.FatBlock != null ? obj.FatBlock.EntityId : 0, obj.Position), 1);

            if (Sync.IsServer)
                m_constructionBlock.SendCancelTarget(obj, TargetTypes.Construction);

            m_constructionBlock.ParticleManager.CancelTarget(obj);
            m_constructionBlock.ToolManager.Remove(obj);
            Remove(obj);
            m_remoteTargets.Remove(obj);
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

        public override void ParallelUpdate(List<IMyCubeGrid> gridList, List<BlockTarget> blocks)
        {
            foreach (var block in blocks.ToList())
            {
                if (block == null)
                    continue;

                if (block.IsRemote && AddPotentialBlock(block.Block, true))
                    m_remoteTargets.Add(block.Block);
                else
                    AddPotentialBlock(block.Block, block.IsRemote);
            }
        }

        public override void CheckBeacons()
        {
            m_remoteTargets.Clear();

            // Find beacons in range
            foreach (var beaconBlock in (NaniteConstructionManager.BeaconList.ToList()).Where(x => (x.Value is NaniteBeaconConstruct || x.Value is NaniteBeaconProjection)))
            {
                var item = beaconBlock.Value.BeaconBlock;

                if (item == null || !item.Enabled || !item.IsFunctional
                  || !MyRelationsBetweenPlayerAndBlockExtensions.IsFriendly(item.GetUserRelationToOwner(m_constructionBlock.ConstructionBlock.OwnerId))
                  || !IsInRange(item.GetPosition(), m_maxDistance) )
                    continue;

                GetBeaconBlocks((IMyCubeGrid)item.CubeGrid);
                GetBeaconBlocksRetryCounter = 0;

                foreach (var block in beaconBlocks)
                    m_constructionBlock.ScanBlocksCache.Add(new BlockTarget(block, true));
            }
        }

        private void GetBeaconBlocks(IMyCubeGrid BeaconBlockGrid)
        {
            try
            {
                beaconBlocks.Clear();
                foreach (var grid in MyAPIGateway.GridGroups.GetGroup(BeaconBlockGrid, GridLinkTypeEnum.Physical))
                    grid.GetBlocks(beaconBlocks);
            }
            catch (InvalidOperationException ex)
            {
                if (GetBeaconBlocksRetryCounter++ > 60)
                {
                    Logging.Instance.WriteLine("NaniteConstructionTargets.GetBeaconBlocks caused an infinite loop. Aborting.");
                    return;
                }
                Logging.Instance.WriteLine("NaniteConstructionTargets.GetBeaconBlocks: Grid group was modified. Retrying.");
                GetBeaconBlocks(BeaconBlockGrid);
            }
            catch (Exception ex)
                { Logging.Instance.WriteLine($"NaniteConstructionTargets.GetBeaconBlocks:\n{ex.ToString()}"); }
        }

        private bool AddPotentialBlock(IMySlimBlock block, bool remote = false)
        {
            if (PotentialTargetList.Contains(block))
                return false;

            if (!remote && block.FatBlock != null && block.FatBlock is IMyTerminalBlock && block.FatBlock.OwnerId != 0
              && !MyRelationsBetweenPlayerAndBlockExtensions.IsFriendly(block.FatBlock.GetUserRelationToOwner(m_constructionBlock.ConstructionBlock.OwnerId)))
                return false;

            if (PotentialIgnoredList.Contains(block))
                return false;

            if (!block.IsFullIntegrity || block.HasDeformation)
            {
                PotentialTargetList.Add(block);
                return true;
            }

            return false;
        }
    }
}
