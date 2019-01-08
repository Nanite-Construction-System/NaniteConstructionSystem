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
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace NaniteConstructionSystem.Entities.Targets
{
    public class NaniteConstructionTarget
    {
        public int ParticleCount { get; set; }
        public NaniteAreaBeacon AreaBeacon { get; set; }
    }

    public class NaniteConstructionTargets : NaniteTargetBlocksBase
    {
        public override string TargetName
            {get {return "Construction";}}

        private Dictionary<IMySlimBlock, int> m_targetBlocks;
        private Dictionary<IMySlimBlock, NaniteAreaBeacon> m_areaTargetBlocks;
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
            m_areaTargetBlocks = new Dictionary<IMySlimBlock, NaniteAreaBeacon>();
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

            foreach (IMySlimBlock item in m_potentialTargetList.ToList())
            {
                if (item == null || TargetList.Contains(item)) 
                    continue;

                missing.Clear();
                item.GetMissingComponents(missing);
                if (missing == null && !MyAPIGateway.Session.CreativeMode) 
                    continue;

                bool foundMissingComponents = true;

                if (missing.Count > 0) 
                    foundMissingComponents = m_constructionBlock.InventoryManager.CheckComponentsAvailable(ref missing, ref available);

                if (foundMissingComponents && m_constructionBlock.HasRequiredPowerForNewTarget(this))
                {
                    bool found = false;
                    foreach (var block in blockList.ToList())
                    {
                        if (block != null && block.Targets.First(y => y is NaniteConstructionTargets).TargetList.Contains(item as IMySlimBlock))
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
                    Logging.Instance.WriteLine(string.Format("ADDING Construction/Repair Target: conid={0} subtype={1} entityID={2} position={3}", 
                        m_constructionBlock.ConstructionBlock.EntityId, def.Id.SubtypeId, item.FatBlock != null ? item.FatBlock.EntityId : 0, item.Position), 1);

                    if (++targetListCount >= maxTargets) 
                        break;
                }
                else if (!foundMissingComponents)
                    LastInvalidTargetReason = "Missing components";

                else if (!m_constructionBlock.HasRequiredPowerForNewTarget(this))
                {
                    LastInvalidTargetReason = "Insufficient power for another target.";
                    break;
                }
                    
            }
            if (LastInvalidTargetReason != "")
                InvalidTargetReason(LastInvalidTargetReason);
        }

        private int GetMissingComponentCount(NaniteConstructionInventory inventoryManager, IMySlimBlock block)
        {
            Dictionary<string, int> missing = new Dictionary<string, int>();
            block.GetMissingComponents(missing);
            if (missing.Count == 0)
                return 0;

            Dictionary<string, int> available = new Dictionary<string, int>();
            inventoryManager.GetAvailableComponents(ref available);
            for (int r = missing.Count - 1; r >= 0; r--)
            {
                var item = missing.ElementAt(r);
                if (available.ContainsKey(item.Key))
                {
                    int amount = Math.Min(item.Value, available[item.Key]);
                    available[item.Key] -= amount;
                    missing[item.Key] -= amount;
                }
            }

            return missing.Sum(x => x.Value);
        }

        public override void Update()
        {
            MyAPIGateway.Parallel.Start(() =>
            {
                try
                {
                    foreach (var item in m_targetList.ToList())
                    {
                        var block = item as IMySlimBlock;
                        if (block != null)
                            ProcessConstructionItem(block);
                    }
                }
                catch (Exception e)
                    {Logging.Instance.WriteLine($"{e}");}
            });
        }

        private void ProcessConstructionItem(IMySlimBlock target)
        {
            if (Sync.IsServer)
            {
                if (m_constructionBlock.FactoryState != NaniteConstructionBlock.FactoryStates.Active)
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

                if (m_areaTargetBlocks.ContainsKey(target))
                {
                    BoundingBoxD bb;
                    target.GetWorldBoundingBox(out bb, true);
                    if (!m_areaTargetBlocks[target].IsInsideBox(bb))
                    {
                        MyAPIGateway.Utilities.InvokeOnGameThread(() => 
                        {
                            if (target != null)
                                CancelTarget(target);
                        });
                        return;
                    }
                }

                if (target.IsDestroyed || target.IsFullyDismounted || (target.FatBlock != null && target.FatBlock.Closed))
                {
                    Logging.Instance.WriteLine("CANCELLING Construction/Repair Target due to target being destroyed", 1);
                    MyAPIGateway.Utilities.InvokeOnGameThread(() => 
                    {
                        if (target != null)
                            CancelTarget(target);
                    });
                    return;
                }

                if (welder != null && MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - welder.StartTime >= welder.WaitTime)
                {
                    MyAPIGateway.Utilities.InvokeOnGameThread(() => 
                    {
                        if (target == null)
                            return;

                        target.MoveItemsToConstructionStockpile(((MyEntity)m_constructionBlock.ConstructionBlock).GetInventory());

                        if (!target.HasDeformation && !target.CanContinueBuild( ((MyEntity)m_constructionBlock.ConstructionBlock).GetInventory() ) && !MyAPIGateway.Session.CreativeMode)
                        {
                            Logging.Instance.WriteLine("CANCELLING Construction/Repair Target due to missing components", 1);

                            CancelTarget(target);
                        }
                    });

                    return;
                }
                // NEW 12-1-2018 To save on performance, once a target is started, use SyncDistance only so we dont have to check each slave factory
                if (m_remoteTargets.Contains(target) 
                  && EntityHelper.GetDistanceBetweenBlockAndSlimblock((IMyCubeBlock)m_constructionBlock.ConstructionBlock, target) > MyAPIGateway.Session.SessionSettings.SyncDistance)
                {
                    Logging.Instance.WriteLine("CANCELLING Repair Target due to target being out of range", 1);
                    MyAPIGateway.Utilities.InvokeOnGameThread(() => 
                        { CancelTarget(target); });
                    return;
                }
            }

            CreateConstructionParticle(target);
        }

        private void CreateConstructionParticle(IMySlimBlock target)
        {
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

            var nearestFactory = GetNearestFactory(TargetName, targetPosition);

            if (nearestFactory.ParticleManager.Particles.Count < NaniteParticleManager.MaxTotalParticles)
                MyAPIGateway.Utilities.InvokeOnGameThread(() => 
                {
                    nearestFactory.ParticleManager.AddParticle(startColor, endColor, GetMinTravelTime() * 1000f, GetSpeed(), target);
                });
        }

        public void CompleteTarget(IMySlimBlock obj)
        {
            Logging.Instance.WriteLine(string.Format("COMPLETING Construction/Repair Target: {0} - {1} (EntityID={2},Position={3})", 
              m_constructionBlock.ConstructionBlock.EntityId, obj.GetType().Name, obj.FatBlock != null ? obj.FatBlock.EntityId : 0, obj.Position), 1);

            if (Sync.IsServer)
                m_constructionBlock.SendCompleteTarget(obj, TargetTypes.Construction);

            m_constructionBlock.ParticleManager.CompleteTarget(obj);
            m_constructionBlock.ToolManager.Remove(obj);
            Remove(obj);
            m_remoteTargets.Remove(obj);
            m_areaTargetBlocks.Remove(obj);
        }

        public void CancelTarget(IMySlimBlock obj)
        {
            Logging.Instance.WriteLine(string.Format("CANCELLING Construction/Repair Target: {0} - {1} (EntityID={2},Position={3})", 
              m_constructionBlock.ConstructionBlock.EntityId, obj.GetType().Name, obj.FatBlock != null ? obj.FatBlock.EntityId : 0, obj.Position), 1);

            if (Sync.IsServer)
                m_constructionBlock.SendCancelTarget(obj, TargetTypes.Construction);

            m_constructionBlock.ParticleManager.CancelTarget(obj);
            m_constructionBlock.ToolManager.Remove(obj);
            Remove(obj);
            m_remoteTargets.Remove(obj);
            m_areaTargetBlocks.Remove(obj);
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
                    AddPotentialBlock(block.Block, block.IsRemote, block.AreaBeacon);
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
                  || !IsInRange( item.GetPosition() ) )
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

        public override void CheckAreaBeacons()
        {
            CheckConstructionOrProjectionAreaBeacons();
        }

        private bool AddPotentialBlock(IMySlimBlock block, bool remote = false, NaniteAreaBeacon beacon = null)
        {
            if (PotentialTargetList.Contains(block))
                return false;

            if (!remote && block.FatBlock != null && block.FatBlock is IMyTerminalBlock && block.FatBlock.OwnerId != 0
              && !MyRelationsBetweenPlayerAndBlockExtensions.IsFriendly(block.FatBlock.GetUserRelationToOwner(m_constructionBlock.ConstructionBlock.OwnerId)))
                return false;

            if (!block.IsFullIntegrity || block.HasDeformation)
            {
                if (beacon != null)
                {
                    if (!m_areaTargetBlocks.ContainsKey(block))
                        m_areaTargetBlocks.Add(block, beacon);
                    else
                        m_areaTargetBlocks[block] = beacon;
                }

                PotentialTargetList.Add(block);
                return true;
            }

            return false;
        }
    }
}
