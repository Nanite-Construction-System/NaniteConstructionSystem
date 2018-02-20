using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRageMath;
using VRage.Utils;
using VRage.ObjectBuilders;
using Ingame = VRage.Game.ModAPI.Ingame;
//using Ingame = VRage.ModAPI.Ingame;
using VRage.Game.ModAPI;
using Sandbox.Definitions;

using NaniteConstructionSystem.Extensions;
using NaniteConstructionSystem.Particles;
using NaniteConstructionSystem.Entities.Tools;
using NaniteConstructionSystem.Entities.Beacons;

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
        {
            get { return "Construction"; }
        }

        private Dictionary<IMySlimBlock, int> m_targetBlocks;
        private Dictionary<IMySlimBlock, NaniteAreaBeacon> m_areaTargetBlocks;
        private float m_maxDistance = 300f;
        private HashSet<IMySlimBlock> m_remoteTargets;
        private FastResourceLock m_remoteLock;

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
            MyCubeBlock block = (MyCubeBlock)m_constructionBlock.ConstructionBlock;            
            return (int)Math.Min(NaniteConstructionManager.Settings.ConstructionNanitesNoUpgrade + (block.UpgradeValues["ConstructionNanites"] * NaniteConstructionManager.Settings.ConstructionNanitesPerUpgrade), NaniteConstructionManager.Settings.ConstructionMaxStreams);
        }

        public override float GetPowerUsage()
        {
            MyCubeBlock block = (MyCubeBlock)m_constructionBlock.ConstructionBlock;
            return Math.Max(1, NaniteConstructionManager.Settings.ConstructionPowerPerStream - (int)(block.UpgradeValues["PowerNanites"] * NaniteConstructionManager.Settings.PowerDecreasePerUpgrade));
        }

        public override float GetMinTravelTime()
        {
            MyCubeBlock block = (MyCubeBlock)m_constructionBlock.ConstructionBlock;
            return Math.Max(1f, NaniteConstructionManager.Settings.ConstructionMinTravelTime - (block.UpgradeValues["SpeedNanites"] * NaniteConstructionManager.Settings.MinTravelTimeReductionPerUpgrade));
        }

        public override float GetSpeed()
        {
            MyCubeBlock block = (MyCubeBlock)m_constructionBlock.ConstructionBlock;
            return NaniteConstructionManager.Settings.ConstructionDistanceDivisor + (block.UpgradeValues["SpeedNanites"] * (float)NaniteConstructionManager.Settings.SpeedIncreasePerUpgrade);
        }

        public override bool IsEnabled()
        {
            bool result = true;
            if (!((IMyFunctionalBlock)m_constructionBlock.ConstructionBlock).Enabled ||
                !((IMyFunctionalBlock)m_constructionBlock.ConstructionBlock).IsFunctional ||
                m_constructionBlock.ConstructionBlock.CustomName.ToLower().Contains("NoConstruction".ToLower()))
                result = false;

            if(NaniteConstructionManager.TerminalSettings.ContainsKey(m_constructionBlock.ConstructionBlock.EntityId))
            {
                if (!NaniteConstructionManager.TerminalSettings[m_constructionBlock.ConstructionBlock.EntityId].AllowRepair)
                    return false;
            }

            return result;
        }

        public override void FindTargets(ref Dictionary<string, int> available)
        {
            if (!IsEnabled())
                return;

            ComponentsRequired.Clear();
            if (m_targetList.Count >= GetMaximumTargets())
            {
                if (PotentialTargetList.Count > 0)
                    m_lastInvalidTargetReason = "Maximum targets reached.  Add more upgrades!";

                return;
            }

            NaniteConstructionInventory inventoryManager = m_constructionBlock.InventoryManager;
            Vector3D sourcePosition = m_constructionBlock.ConstructionBlock.GetPosition();

            Dictionary<string, int> missing = new Dictionary<string, int>();
            using (Lock.AcquireExclusiveUsing())
            {
                var potentialList = m_potentialTargetList.OrderByDescending(x => GetMissingComponentCount(inventoryManager, (IMySlimBlock)x)).ToList();
                for (int r = potentialList.Count - 1; r >= 0; r--)
                {
                    if (m_constructionBlock.IsUserDefinedLimitReached())
                    {
                        m_lastInvalidTargetReason = "User defined maximum nanite limit reached";
                        return;
                    }

                    var item = (IMySlimBlock)potentialList[r];
                    if (TargetList.Contains(item))
                        continue;

                    missing.Clear();
                    item.GetMissingComponents(missing);
                    bool foundMissingComponents = true;

                    if (MyAPIGateway.Session.CreativeMode)
                        foundMissingComponents = true;
                    else if (missing.Count > 0)
                    {
                        foundMissingComponents = inventoryManager.CheckComponentsAvailable(ref missing, ref available);
                    }

                    if (foundMissingComponents && NaniteConstructionPower.HasRequiredPowerForNewTarget((IMyFunctionalBlock)m_constructionBlock.ConstructionBlock, this))
                    {
                        // Check to see if another block targetting this for construction
                        var blockList = NaniteConstructionManager.GetConstructionBlocks((IMyCubeGrid)m_constructionBlock.ConstructionBlock.CubeGrid);
                        bool found = false;
                        foreach (var block in blockList)
                        {
                            if (block.Targets.First(y => y is NaniteConstructionTargets).TargetList.Contains(item as IMySlimBlock))
                            {
                                found = true;
                                break;
                            }
                        }

                        if (found)
                        {
                            m_lastInvalidTargetReason = "Another factory has this block as a target";
                            continue;
                        }

                        m_potentialTargetList.RemoveAt(r);
                        m_targetList.Add(item);
                        var def = item.BlockDefinition as MyCubeBlockDefinition;
                        Logging.Instance.WriteLine(string.Format("ADDING Construction/Repair Target: conid={0} subtype={1} entityID={2} position={3}", m_constructionBlock.ConstructionBlock.EntityId, def.Id.SubtypeId, item.FatBlock != null ? item.FatBlock.EntityId : 0, item.Position));
                        if (m_targetList.Count >= GetMaximumTargets())
                            break;
                    }
                    else if (!foundMissingComponents)
                    {
                        foreach (var component in missing)
                        {
                            if (!ComponentsRequired.ContainsKey(component.Key))
                                ComponentsRequired.Add(component.Key, component.Value);
                            else
                                ComponentsRequired[component.Key] += component.Value;
                        }

                        m_lastInvalidTargetReason = "Missing components";
                    }
                    else if (!NaniteConstructionPower.HasRequiredPowerForNewTarget((IMyFunctionalBlock)m_constructionBlock.ConstructionBlock, this))
                    {
                        m_lastInvalidTargetReason = "Insufficient power for another target.";
                    }
                }
            }
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
            foreach (var item in m_targetList.ToList())
            {
                var block = item as IMySlimBlock;
                if (block != null)
                    ProcessConstructionItem(block);
            }
        }

        private void ProcessConstructionItem(IMySlimBlock target)
        {
            if (Sync.IsServer)
            {
                if (!IsEnabled())
                {
                    Logging.Instance.WriteLine("CANCELLING Construction/Repair Target due to being disabled");
                    CancelTarget(target);
                    return;
                }

                if (!NaniteConstructionPower.HasRequiredPowerForCurrentTarget((IMyFunctionalBlock)m_constructionBlock.ConstructionBlock))
                {
                    Logging.Instance.WriteLine("CANCELLING Construction/Repair Target due to power shortage");
                    CancelTarget(target);
                    return;
                }

                if (m_constructionBlock.FactoryState != NaniteConstructionBlock.FactoryStates.Active)
                    return;

                if (!m_targetBlocks.ContainsKey(target))
                    m_targetBlocks.Add(target, 0);

                NaniteWelder welder = (NaniteWelder)m_constructionBlock.ToolManager.Tools.FirstOrDefault(x => x.TargetBlock == target && x is NaniteWelder);
                if (welder == null)
                {
                    double distance = EntityHelper.GetDistanceBetweenBlockAndSlimblock((IMyCubeBlock)m_constructionBlock.ConstructionBlock, target);
                    int time = (int)Math.Max(GetMinTravelTime() * 1000f, (distance / GetSpeed()) * 1000f);
                    welder = new NaniteWelder(m_constructionBlock, target, (int)(time / 2.5f), false);
                    m_constructionBlock.ToolManager.Tools.Add(welder);
                    m_constructionBlock.SendAddTarget(target, TargetTypes.Construction);
                }

                if (target.IsFullIntegrity && !target.HasDeformation)
                {
                    CompleteTarget(target);
                    return;
                }

                if (m_areaTargetBlocks.ContainsKey(target))
                {
                    BoundingBoxD bb;
                    target.GetWorldBoundingBox(out bb, true);
                    if (!m_areaTargetBlocks[target].IsInsideBox(bb))
                    {
                        CancelTarget(target);
                        return;
                    }
                }

                if (target.IsDestroyed || target.IsFullyDismounted || (target.FatBlock != null && target.FatBlock.Closed))
                {
                    Logging.Instance.WriteLine("CANCELLING Construction/Repair Target due to target being destroyed");
                    CancelTarget(target);
                    return;
                }

                if (welder != null && MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - welder.StartTime >= welder.WaitTime)
                {
                    target.MoveItemsToConstructionStockpile(((MyEntity)m_constructionBlock.ConstructionBlock).GetInventory());
                    Dictionary<string, int> missing = new Dictionary<string, int>();
                    target.GetMissingComponents(missing);

                    if (!target.HasDeformation && !target.CanContinueBuild(((MyEntity)m_constructionBlock.ConstructionBlock).GetInventory()))
                    {
                        if (!MyAPIGateway.Session.CreativeMode)
                        {
                            Logging.Instance.WriteLine("CANCELLING Construction/Repair Target due to missing components");
                            foreach (var item in missing)
                            {
                                Logging.Instance.WriteLine(string.Format("Missing component: {0} - {1}", item.Value, item.Key));
                            }
                            CancelTarget(target);
                        }
                    }

                    return;
                }

                bool isRemote = false;
                using (m_remoteLock.AcquireExclusiveUsing())
                {
                    isRemote = m_remoteTargets.Contains(target);
                }

                if (isRemote && EntityHelper.GetDistanceBetweenBlockAndSlimblock((IMyCubeBlock)m_constructionBlock.ConstructionBlock, target) > m_maxDistance)
                {
                    Logging.Instance.WriteLine("CANCELLING Repair Target due to target being out of range");
                    CancelTarget(target);
                    return;
                }
            }

            CreateConstructionParticle(target);
        }

        private void CreateConstructionParticle(IMySlimBlock target)
        {
            if (!m_targetBlocks.ContainsKey(target))
                m_targetBlocks.Add(target, 0);

            if (NaniteParticleManager.TotalParticleCount > NaniteParticleManager.MaxTotalParticles)
                return;

            m_targetBlocks[target]++;
            int size = (int)Math.Max(60f, NaniteParticleManager.TotalParticleCount);
            if ((float)m_targetBlocks[target] / size < 1f)
                return;

            m_targetBlocks[target] = 0;
            Vector4 startColor = new Vector4(0.55f, 0.55f, 0.95f, 0.75f);
            Vector4 endColor = new Vector4(0.05f, 0.05f, 0.35f, 0.75f);
            m_constructionBlock.ParticleManager.AddParticle(startColor, endColor, GetMinTravelTime() * 1000f, GetSpeed(), target);
        }

        public void CompleteTarget(IMySlimBlock obj)
        {
            Logging.Instance.WriteLine(string.Format("COMPLETING Construction/Repair Target: {0} - {1} (EntityID={2},Position={3})", m_constructionBlock.ConstructionBlock.EntityId, obj.GetType().Name, obj.FatBlock != null ? obj.FatBlock.EntityId : 0, obj.Position));
            if (Sync.IsServer)
                m_constructionBlock.SendCompleteTarget(obj, TargetTypes.Construction);

            m_constructionBlock.ParticleManager.CompleteTarget(obj);
            m_constructionBlock.ToolManager.Remove(obj);
            Remove(obj);
            m_remoteTargets.Remove(obj);

            if(m_areaTargetBlocks.ContainsKey(obj))
                m_areaTargetBlocks.Remove(obj);
        }

        public void CancelTarget(IMySlimBlock obj)
        {
            Logging.Instance.WriteLine(string.Format("CANCELLING Construction/Repair Target: {0} - {1} (EntityID={2},Position={3})", m_constructionBlock.ConstructionBlock.EntityId, obj.GetType().Name, obj.FatBlock != null ? obj.FatBlock.EntityId : 0, obj.Position));
            if (Sync.IsServer)
                m_constructionBlock.SendCancelTarget(obj, TargetTypes.Construction);

            m_constructionBlock.ParticleManager.CancelTarget(obj);
            m_constructionBlock.ToolManager.Remove(obj);
            Remove(obj);
            m_remoteTargets.Remove(obj);

            if (m_areaTargetBlocks.ContainsKey(obj))
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

        public override void ParallelUpdate(List<IMyCubeGrid> gridList, List<IMySlimBlock> blocks)
        {
            using (Lock.AcquireExclusiveUsing())
            {
                PotentialTargetList.Clear();
            }

            var remoteList = new HashSet<IMySlimBlock>();

            if (!IsEnabled())
                return;

            foreach (var block in blocks)
            {
                AddPotentialBlock(block);
            }

            foreach (var beaconBlock in NaniteConstructionManager.BeaconList.Where(x => x is NaniteBeaconConstruct && Vector3D.DistanceSquared(m_constructionBlock.ConstructionBlock.GetPosition(), x.BeaconBlock.GetPosition()) < m_maxDistance * m_maxDistance)) {
				
				IMyCubeBlock item = (IMyCubeBlock)beaconBlock.BeaconBlock;

				if (!((IMyFunctionalBlock)item).Enabled || !((IMyFunctionalBlock)item).IsFunctional)
					continue;

                MyRelationsBetweenPlayerAndBlock relation = item.GetUserRelationToOwner(m_constructionBlock.ConstructionBlock.OwnerId);
                if (!(relation == MyRelationsBetweenPlayerAndBlock.Owner || relation == MyRelationsBetweenPlayerAndBlock.FactionShare || (MyAPIGateway.Session.CreativeMode && relation == MyRelationsBetweenPlayerAndBlock.NoOwnership)))
                    continue;

                List<IMyCubeGrid> beaconGridList = GridHelper.GetGridGroup((IMyCubeGrid)item.CubeGrid);
                List<IMySlimBlock> beaconBlocks = new List<IMySlimBlock>();
                foreach (var grid in beaconGridList)
                {
                    grid.GetBlocks(beaconBlocks);
                }

                foreach (var block in beaconBlocks)
                {
                    if(AddPotentialBlock(block, true))
                    {
                        remoteList.Add(block);
                    }
                }
            }

            CheckAreaBeacons();

            using (m_remoteLock.AcquireExclusiveUsing())
            {
                m_remoteTargets = remoteList;
            }
        }

        private void CheckAreaBeacons()
        {
            foreach(var beaconBlock in NaniteConstructionManager.BeaconList.Where(x => x is NaniteAreaBeacon))
            {
                IMyCubeBlock cubeBlock = beaconBlock.BeaconBlock;
                //MyRelationsBetweenPlayerAndBlock relation = cubeBlock.GetUserRelationToOwner(m_constructionBlock.ConstructionBlock.OwnerId);
                //if (!(relation == MyRelationsBetweenPlayerAndBlock.Owner || relation == MyRelationsBetweenPlayerAndBlock.FactionShare || (MyAPIGateway.Session.CreativeMode && relation == MyRelationsBetweenPlayerAndBlock.NoOwnership)))
                //    continue;

                if (!((IMyFunctionalBlock)cubeBlock).Enabled || !((IMyFunctionalBlock)cubeBlock).IsFunctional)
                    continue;

                var item = beaconBlock as NaniteAreaBeacon;
                if (!item.Settings.AllowRepair)
                    continue;

                HashSet<IMyEntity> entities = new HashSet<IMyEntity>();
                MyAPIGateway.Entities.GetEntities(entities);
                foreach(var entity in entities)
                {
                    var grid = entity as IMyCubeGrid;
                    if (grid == null)
                        continue;

                    if((grid.GetPosition() - cubeBlock.GetPosition()).LengthSquared() < m_maxDistance * m_maxDistance)
                    {
                        foreach(IMySlimBlock block in ((MyCubeGrid)grid).GetBlocks())
                        {
                            BoundingBoxD blockbb;
                            block.GetWorldBoundingBox(out blockbb);
                            if (item.IsInsideBox(blockbb))
                            {
                                AddPotentialBlock(block, true, item);
                            }
                        }
                    }
                }
            }
        }

        private bool AddPotentialBlock(IMySlimBlock block, bool remote = false, NaniteAreaBeacon beacon = null)
        {
            if (TargetList.Contains(block))
                return false;

            if (!remote && block.FatBlock != null && block.FatBlock is IMyTerminalBlock && block.FatBlock.OwnerId != 0)
            {
                IMyTerminalBlock terminal = (IMyTerminalBlock)block.FatBlock;
                MyRelationsBetweenPlayerAndBlock relation = terminal.GetUserRelationToOwner(m_constructionBlock.ConstructionBlock.OwnerId);

                if (relation == MyRelationsBetweenPlayerAndBlock.Neutral ||
                    relation == MyRelationsBetweenPlayerAndBlock.Enemies)
                {
                    return false;
                }
            }
            else if(remote)
            {
                //if (block.CubeGrid.BigOwners.Count < 1)
                //    return false;

                foreach (var item in block.CubeGrid.BigOwners)
                {
                    MyRelationsBetweenPlayerAndBlock relation = m_constructionBlock.ConstructionBlock.GetUserRelationToOwner(item);

                    if (relation == MyRelationsBetweenPlayerAndBlock.Neutral ||
                        relation == MyRelationsBetweenPlayerAndBlock.Enemies)
                    {
                        return false;
                    }
                }
            }

            if (!block.IsFullIntegrity || block.HasDeformation)
            {
                using (Lock.AcquireExclusiveUsing())
                {
                    if(beacon != null)
                    {
                        if (!m_areaTargetBlocks.ContainsKey(block))
                            m_areaTargetBlocks.Add(block, beacon);
                        else
                            m_areaTargetBlocks[block] = beacon;
                    }

                    PotentialTargetList.Add(block);
                    return true;
                }
            }

            return false;
        }
    }
}
