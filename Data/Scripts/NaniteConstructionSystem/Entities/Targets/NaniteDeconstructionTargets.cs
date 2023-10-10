using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRageMath;
using Ingame = Sandbox.ModAPI.Ingame;
using Sandbox.Definitions;

using NaniteConstructionSystem.Particles;
using NaniteConstructionSystem.Extensions;
using NaniteConstructionSystem.Entities.Beacons;
using NaniteConstructionSystem.Entities.Tools;

namespace NaniteConstructionSystem.Entities.Targets
{
    public class NaniteDeconstructionGrid
    {
        private OrderedSet<IMySlimBlock> m_removeList;
        public OrderedSet<IMySlimBlock> RemoveList
        {
            get { return m_removeList; }
        }

        private OrderedSet<IMySlimBlock> m_addingList;
        public OrderedSet<IMySlimBlock> AddingList
        {
            get { return m_addingList; }
        }

        private OrderedSet<IMySlimBlock> m_addingGridList;
        public OrderedSet<IMySlimBlock> AddingGridList
        {
            get { return m_addingGridList; }
        }

        private HashSet<IMyCubeGrid> m_gridsProcessed;
        public HashSet<IMyCubeGrid> GridsProcessed
        {
            get { return m_gridsProcessed; }
        }

        private IMyCubeGrid m_mainGrid;
        public IMyCubeGrid MainGrid
        {
            get { return m_mainGrid; }
        }

        private DateTime m_lastUpdate;

        public NaniteDeconstructionGrid(IMyCubeGrid mainGrid)
        {
            m_removeList = new OrderedSet<IMySlimBlock>();
            m_addingList = new OrderedSet<IMySlimBlock>();
            m_addingGridList = new OrderedSet<IMySlimBlock>();
            m_gridsProcessed = new HashSet<IMyCubeGrid>();
            m_lastUpdate = DateTime.Now;
            m_mainGrid = mainGrid;
        }
    }

    public class NaniteDeconstructionTargets : NaniteTargetBlocksBase
    {
        private Dictionary<IMyCubeGrid, DateTime> m_tempPhysicless;
        public Dictionary<IMyCubeGrid, DateTime> TempPhysicless
        {
            get { return m_tempPhysicless; }
        }

        private HashSet<NaniteDeconstructionGrid> m_validBeaconedGrids;
        private Dictionary<IMySlimBlock, int> m_targetBlocks;
        private float m_maxDistance = 300f;

        public NaniteDeconstructionTargets(NaniteConstructionBlock block) : base(block)
        {
            m_validBeaconedGrids = new HashSet<NaniteDeconstructionGrid>();
            m_targetBlocks = new Dictionary<IMySlimBlock, int>();
            m_tempPhysicless = new Dictionary<IMyCubeGrid, DateTime>();
            m_maxDistance = NaniteConstructionManager.Settings.DeconstructionMaxDistance;

            MyAPIGateway.Entities.OnEntityRemove += OnEntityRemove;
        }

        public override string TargetName
        {
            get { return "Deconstruction"; }
        }

        public override int GetMaximumTargets()
        {
            return (int)Math.Min((NaniteConstructionManager.Settings.DeconstructionNanitesNoUpgrade * m_constructionBlock.FactoryGroup.Count)
              + m_constructionBlock.UpgradeValue("DeconstructionNanites"), NaniteConstructionManager.Settings.DeconstructionMaxStreams);
        }

        public override float GetPowerUsage()
        {
            return Math.Max(1, NaniteConstructionManager.Settings.DeconstructionPowerPerStream
              - (int)m_constructionBlock.UpgradeValue("PowerNanites"));
        }

        public override float GetMinTravelTime()
        {
            return Math.Max(1f, NaniteConstructionManager.Settings.DeconstructionMinTravelTime
              - m_constructionBlock.UpgradeValue("MinTravelTime"));
        }

        public override float GetSpeed()
        {
            return NaniteConstructionManager.Settings.DeconstructionDistanceDivisor
              + m_constructionBlock.UpgradeValue("SpeedNanites");
        }

        public override bool IsEnabled(NaniteConstructionBlock factory)
        {
            if (!((IMyFunctionalBlock)factory.ConstructionBlock).Enabled
              || !((IMyFunctionalBlock)factory.ConstructionBlock).IsFunctional
              || (NaniteConstructionManager.TerminalSettings.ContainsKey(factory.ConstructionBlock.EntityId)
              && !NaniteConstructionManager.TerminalSettings[factory.ConstructionBlock.EntityId].AllowDeconstruct))
            {
                factory.EnabledParticleTargets[TargetName] = false;
                return false;
            }

            factory.EnabledParticleTargets[TargetName] = true;
            return true;
        }

        public override void ParallelUpdate(List<IMyCubeGrid> NaniteGridGroup, List<BlockTarget> gridBlocks)
        {
            try
            {
                // Add
                foreach (var beaconBlock in NaniteConstructionManager.BeaconList.Where(x => x.Value is NaniteBeaconDeconstruct))
                {

                    IMyCubeBlock item = (IMyCubeBlock)beaconBlock.Value.BeaconBlock;

                    if (item == null
                        || item.CubeGrid == null
                        || !((IMyFunctionalBlock)item).Enabled
                        || !((IMyFunctionalBlock)item).IsFunctional
                        || NaniteGridGroup.Contains(item.CubeGrid)
                        || !MyRelationsBetweenPlayerAndBlockExtensions.IsFriendly(item.GetUserRelationToOwner(m_constructionBlock.ConstructionBlock.OwnerId))
                        || m_validBeaconedGrids.FirstOrDefault(x => x.GridsProcessed.Contains(item.CubeGrid)) != null
                        || !IsInRange(item.GetPosition(), m_maxDistance)
                        )
						continue;

                    NaniteDeconstructionGrid deconstruct = new NaniteDeconstructionGrid(item.CubeGrid);

                    CreateGridStack(NaniteGridGroup, deconstruct, (MyCubeGrid)item.CubeGrid, (MyCubeBlock)item);
                    m_validBeaconedGrids.Add(deconstruct);

                    Logging.Instance.WriteLine($"[Deconstruction] Grid {item.CubeGrid.CustomName} queued for deconstruction", 1);

                    foreach (var slimBlock in deconstruct.RemoveList)
                    {
                        if (slimBlock != null)
                            PotentialTargetList.Add(slimBlock);
                    }

                    deconstruct.RemoveList.Clear();
                }

                if (PotentialTargetList.Count > 0)
                {
                    foreach (IMySlimBlock item in PotentialTargetList.ToList())
                    {
                        if (item == null || item.CubeGrid == null || item.CubeGrid.Closed || item.IsDestroyed || item.IsFullyDismounted
                          || (item.FatBlock != null && item.FatBlock.Closed)
                          || EntityHelper.GetDistanceBetweenBlockAndSlimblock((IMyCubeBlock)m_constructionBlock.ConstructionBlock, item) > m_maxDistance)
                            PotentialTargetList.Remove(item);
                    }
                }
                else if (TargetList.Count == 0 && PotentialTargetList.Count == 0)
                    m_validBeaconedGrids.Clear();
            }
            catch (Exception e)
                { Logging.Instance.WriteLine($"Exception in NaniteDeconstructionTargets.ParallelUpdate:\n{e}"); }
        }

        public override void FindTargets(ref Dictionary<string, int> available, List<NaniteConstructionBlock> blockList)
        {
            InvalidTargetReason("");

            var maxTargets = GetMaximumTargets();

            if (TargetList.Count >= maxTargets)
            {
                if (PotentialTargetList.Count > 0)
                    InvalidTargetReason("Maximum targets reached. Add more upgrades!");

                return;
            }

            int TargetListCount = TargetList.Count;
            string LastInvalidTargetReason = "";

            foreach(IMySlimBlock item in PotentialTargetList.ToList())
            {
                if (item == null || TargetList.Contains(item))
                    continue;

                if (!m_constructionBlock.HasRequiredPowerForNewTarget(this))
                {
                    LastInvalidTargetReason = "Insufficient power for another target.";
                    break;
                }

                if (item.CubeGrid.Closed || item.IsDestroyed || item.IsFullyDismounted || (item.FatBlock != null && item.FatBlock.Closed))
                {
                    LastInvalidTargetReason = "Potential target is destroyed";
                    continue;
                }

                bool found = false;
                foreach (var block in blockList.ToList())
                {
                    if (block != null && block.Targets.First(x => x is NaniteDeconstructionTargets).TargetList.Contains(item as IMySlimBlock))
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
                Logging.Instance.WriteLine(string.Format("[Deconstruction] Adding Deconstruction Target: conid={0} subtypeid={1} entityID={2} position={3}",
                  m_constructionBlock.ConstructionBlock.EntityId, def.Id.SubtypeId, item.FatBlock != null ? item.FatBlock.EntityId : 0, item.Position), 1);

                if (++TargetListCount >= maxTargets)
                    break;
            }

            if (LastInvalidTargetReason != "")
                InvalidTargetReason(LastInvalidTargetReason);
        }

        public override void Update()
        {
            foreach(var item in TargetList.ToList())
            {
                var block = item as IMySlimBlock;
                if (block != null)
                    ProcessItem(block);
            }

            foreach (var item in TempPhysicless.ToList())
            {
                if (DateTime.Now - item.Value > TimeSpan.FromSeconds(10))
                {
                    if (item.Key.Closed)
                    {
                        TempPhysicless.Remove(item.Key);
                        continue;
                    }

                    if (!item.Key.Closed && item.Key.Physics != null)
                    {
                        item.Key.Physics.LinearVelocity = Vector3.Zero;
                        item.Key.Physics.AngularVelocity = Vector3.Zero;
                        item.Key.Physics.LinearDamping = 0.0f;
                        item.Key.Physics.AngularDamping = 0.0f;
                    }

                    TempPhysicless.Remove(item.Key);
                }
            }
        }

        private void ProcessItem(IMySlimBlock target)
        {
            if (Sync.IsServer)
            {
                if (!((m_constructionBlock.FactoryState == NaniteConstructionBlock.FactoryStates.Active || m_constructionBlock.FactoryState == NaniteConstructionBlock.FactoryStates.MissingParts) && (TargetList.Count > 0 || PotentialTargetList.Count > 0)))
                    return;

                NaniteGrinder grinder = (NaniteGrinder)m_constructionBlock.ToolManager.Tools.FirstOrDefault(x => x.TargetBlock == target && x is NaniteGrinder);

                if (grinder == null)
                {
                    double distance = EntityHelper.GetDistanceBetweenBlockAndSlimblock((IMyCubeBlock)m_constructionBlock.ConstructionBlock, target);
                    int time = (int)Math.Max(GetMinTravelTime() * 1000f, (distance / GetSpeed()) * 1000f);
                    grinder = new NaniteGrinder(m_constructionBlock, target, (int)(time / 2.5f), NaniteConstructionManager.Settings.DeconstructionPerformanceFriendly);
                    m_constructionBlock.ToolManager.Tools.Add(grinder);
                    m_constructionBlock.SendAddTarget(target, TargetTypes.Deconstruction);
                }

                if (target.IsDestroyed || target.IsFullyDismounted || target.CubeGrid.GetCubeBlock(target.Position) == null || (target.FatBlock != null && target.FatBlock.Closed))
                {
                    CompleteTarget(target);
                    return;
                }

                if (target.CubeGrid.Closed)
                {
                    Logging.Instance.WriteLine("[Deconstruction] Cancelling Deconstruction Target due to grid being closed", 1);
                    CancelTarget(target);
                    return;
                }

                if (!IsInRange(target, m_maxDistance))
                {
                    Logging.Instance.WriteLine("[Deconstruction] Cancelling Deconstruction Target due to being out of range", 1);
                    CancelTarget(target);
                    return;
                }
            }

            CreateDeconstructionParticle(target);
        }

        private void RemoveGridTarget(IMyCubeGrid grid)
        {
            foreach (var item in m_validBeaconedGrids)
                if (item.MainGrid == grid)
                {
                    foreach (var block in item.RemoveList)
                    {
                        PotentialTargetList.Remove(block);
                        TargetList.Remove(block);
                    }
                    m_validBeaconedGrids.Remove(item);
                    break;
                }

        }

        private void CreateDeconstructionParticle(IMySlimBlock target)
        {
            if (!m_targetBlocks.ContainsKey(target))
                m_targetBlocks.Add(target, 0);

            m_targetBlocks[target] = 0;

            try {
                Vector3D targetPosition = default(Vector3D);

                if (target.FatBlock != null) {
                    targetPosition = target.FatBlock.GetPosition();
                } else {
                    var size = target.CubeGrid.GridSizeEnum == MyCubeSize.Small ? 0.5f : 2.5f;
                    var destinationPosition = new Vector3D(target.Position * size);
                    targetPosition = Vector3D.Transform(destinationPosition, target.CubeGrid.WorldMatrix);
                }

                var nearestFactory = m_constructionBlock;

                Vector4 startColor = new Vector4(0.55f, 0.95f, 0.95f, 0.75f);
                Vector4 endColor = new Vector4(0.05f, 0.35f, 0.35f, 0.75f);

                if (nearestFactory.ParticleManager.Particles.Count < NaniteParticleManager.MaxTotalParticles) {
                    MyAPIGateway.Utilities.InvokeOnGameThread(() => {
                        nearestFactory.ParticleManager.AddParticle(startColor, endColor, GetMinTravelTime() * 1000f, GetSpeed(), target);
                    });
                }
            } catch (Exception e) {
                Logging.Instance.WriteLine($"{e}");
            }

        }

        public void CompleteTarget(IMySlimBlock obj)
        {
            Logging.Instance.WriteLine(string.Format("[Deconstruction] Completing Deconstruction Target: {0} - {1} (EntityID={2},Position={3})",
            m_constructionBlock.ConstructionBlock.EntityId, obj.GetType().Name, obj.FatBlock != null ? obj.FatBlock.EntityId : 0, obj.Position), 1);

            if (Sync.IsServer) {
                m_constructionBlock.SendCompleteTarget(obj, TargetTypes.Deconstruction);
            }

            m_constructionBlock.ParticleManager.CompleteTarget(obj);
            m_constructionBlock.ToolManager.Remove(obj);
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

        public void CancelTarget(IMySlimBlock obj)
        {
            Logging.Instance.WriteLine(string.Format("[Deconstruction] Cancelling Deconstruction Target: {0} - {1} (EntityID={2},Position={3})",
              m_constructionBlock.ConstructionBlock.EntityId, obj.GetType().Name, obj.FatBlock != null ? obj.FatBlock.EntityId : 0, obj.Position), 1);

            if (Sync.IsServer)
                m_constructionBlock.SendCancelTarget(obj, TargetTypes.Deconstruction);

            m_constructionBlock.ParticleManager.CancelTarget(obj);
            m_constructionBlock.ToolManager.Remove(obj);
            Remove(obj);

            using (Lock.AcquireExclusiveUsing())
                PotentialTargetList.Add(obj);
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

        private void OnEntityRemove(IMyEntity obj)
        {
            var grid = obj as IMyCubeGrid;
            if (grid == null)
                return;

            foreach (var item in m_validBeaconedGrids.ToList())
            {
                if (item.GridsProcessed.Contains(grid))
                    item.GridsProcessed.Remove(grid);

                if (item.MainGrid == grid)
                    m_validBeaconedGrids.Remove(item);
            }

        }

        private int GetGridGroupBlockCount(IMyCubeGrid grid)
        {
            int count = 0;
            foreach (var item in MyAPIGateway.GridGroups.GetGroup(grid, GridLinkTypeEnum.Physical))
                count += ((MyCubeGrid)item).GetBlocks().Count;

            return count;
        }

        private long GetGridGroupOwner(IMyCubeGrid grid)
        {
            if(grid.BigOwners.Count > 0)
                return grid.BigOwners.First();

            foreach (IMyCubeGrid item in MyAPIGateway.GridGroups.GetGroup(grid, GridLinkTypeEnum.Physical))
                if (item.BigOwners.Count > 0)
                    return item.BigOwners.First();

            return 0;
        }

        private void addNeighboursDeconstruct(ref NaniteDeconstructionGrid deconstruct, IMySlimBlock currentBlock, bool clear = true)
        {
            if (clear)
            {
                deconstruct.AddingList.Clear();
                deconstruct.AddingGridList.Clear();
            }

            MyCubeBlockDefinition blockDefinition = (MyCubeBlockDefinition)currentBlock.BlockDefinition;

            // Get real block max
            Vector3I Max = Vector3I.Zero;
            Vector3I Min = currentBlock.Min;
            ComputeMax(blockDefinition, currentBlock.Orientation, ref Min, out Max);

            AddNeighbours(deconstruct, currentBlock, Min, new Vector3I(Min.X, Max.Y, Max.Z), -Vector3I.UnitX);
            AddNeighbours(deconstruct, currentBlock, Min, new Vector3I(Max.X, Min.Y, Max.Z), -Vector3I.UnitY);
            AddNeighbours(deconstruct, currentBlock, Min, new Vector3I(Max.X, Max.Y, Min.Z), -Vector3I.UnitZ);
            AddNeighbours(deconstruct, currentBlock, new Vector3I(Max.X, Min.Y, Min.Z), Max, Vector3I.UnitX);
            AddNeighbours(deconstruct, currentBlock, new Vector3I(Min.X, Max.Y, Min.Z), Max, Vector3I.UnitY);
            AddNeighbours(deconstruct, currentBlock, new Vector3I(Min.X, Min.Y, Max.Z), Max, Vector3I.UnitZ);
        }

        private int GetBlockConnections(NaniteDeconstructionGrid deconstruct, IMySlimBlock currentBlock)
        {
            addNeighboursDeconstruct(ref deconstruct, currentBlock);
            return deconstruct.AddingList.Count + deconstruct.AddingGridList.Count;
        }

        private int GetBlockConnections(IMySlimBlock currentBlock)
        {
            if (currentBlock.CubeGrid.Closed)
                return 0;

            NaniteDeconstructionGrid deconstruct = new NaniteDeconstructionGrid(currentBlock.CubeGrid);

            addNeighboursDeconstruct(ref deconstruct, currentBlock);

            int additional = 0;
            if (currentBlock.FatBlock != null && currentBlock.FatBlock.BlockDefinition.SubtypeName.Contains("NaniteBeaconDeconstruct"))
                additional += 10;

            AddConnectedGridBlock(deconstruct, currentBlock);

            return deconstruct.AddingList.Count + deconstruct.AddingGridList.Count + additional;
        }

        private void CreateGridStack(List<IMyCubeGrid> NaniteGridGroup, NaniteDeconstructionGrid deconstruct, MyCubeGrid grid, MyCubeBlock beacon)
        {
            DateTime start = DateTime.Now;
            IMyCubeGrid mainGrid = MyAPIGateway.GridGroups.GetGroup((IMyCubeGrid)grid, GridLinkTypeEnum.Physical).SkipWhile(x => NaniteGridGroup.Contains(x)).OrderByDescending(x => ((MyCubeGrid)x).GetBlocks().Count).FirstOrDefault();

            if (mainGrid == null)
                return;

            List<IMySlimBlock> blocks = new List<IMySlimBlock>();
            mainGrid.GetBlocks(blocks);
            IMySlimBlock block = blocks.OrderBy(x => GetBlockConnections(deconstruct, x)).FirstOrDefault();

            if (block == null)
                return;

            if (beacon != null && mainGrid == beacon.CubeGrid)
                block = (IMySlimBlock)beacon.SlimBlock;

            CreateRemovalOrder(deconstruct, block);
            DateTime end = DateTime.Now;
            deconstruct.AddingGridList.Clear();
            deconstruct.AddingList.Clear();
            Logging.Instance.WriteLine($"[Deconstruction] Creating Grid Stack. Total Process Time: {(end - start).TotalMilliseconds}ms", 1);
        }

        private void CreateRemovalOrder(NaniteDeconstructionGrid deconstruct, IMySlimBlock currentBlock)
        {
            deconstruct.AddingList.Clear();
            deconstruct.GridsProcessed.Clear();

            while (true)
            {
                if (!deconstruct.GridsProcessed.Contains(currentBlock.CubeGrid))
                {
                    ((MyCubeGrid)currentBlock.CubeGrid).OnGridSplit += OnGridSplit;
                    deconstruct.GridsProcessed.Add(currentBlock.CubeGrid);
                }

                addNeighboursDeconstruct(ref deconstruct, currentBlock, false);

                // Check if currentBlock is a connector of some kind, then follow it
                AddConnectedGridBlock(deconstruct, currentBlock);

                // Add to removal list
                if(!deconstruct.RemoveList.Contains(currentBlock))
                    deconstruct.RemoveList.AddStart(currentBlock);

                if (deconstruct.AddingList.Count < 1 && deconstruct.AddingGridList.Count < 1)
                    break;

                if (deconstruct.AddingList.Count < 1 && deconstruct.AddingGridList.Count > 0)
                {
                    currentBlock = deconstruct.AddingGridList[0];
                    deconstruct.AddingGridList.Remove(currentBlock);
                }
                else
                {
                    currentBlock = deconstruct.AddingList[0];
                    deconstruct.AddingList.Remove(currentBlock);
                }
            }

            // Find user defined priority blocks for deconstruction.
            FindPriorityBlocks(deconstruct, currentBlock);

            Logging.Instance.WriteLine($"[Deconstruction] Block Count: {deconstruct.RemoveList.Count}", 1);
            Logging.Instance.WriteLine($"[Deconstruction] Grid Count: {deconstruct.GridsProcessed.Count}", 1);
        }

        private void FindPriorityBlocks(NaniteDeconstructionGrid deconstruct, IMySlimBlock startBlock)
        {
            foreach (var grid in MyAPIGateway.GridGroups.GetGroup(startBlock.CubeGrid, GridLinkTypeEnum.Physical))
            {
                List<IMySlimBlock> blocks = new List<IMySlimBlock>();
                grid.GetBlocks(blocks);
                foreach (var block in blocks)
                {
                    if (block.FatBlock == null)
                        continue;

                    var functional = block.FatBlock as IMyFunctionalBlock;
                    if (functional == null)
                        continue;

                    bool priority = functional.CustomName.ToLower().Contains("deconstruct priority") || functional.CustomData.ToLower().Contains("deconstruct priority");
                    if (!priority)
                        continue;

                    if (deconstruct.RemoveList.Contains(block))
                        deconstruct.RemoveList.Remove(block);

                    deconstruct.RemoveList.AddStart(block);
                }
            }
        }

        private void AddNeighbours(NaniteDeconstructionGrid deconstruct, IMySlimBlock block, Vector3I min, Vector3I max, Vector3I normalDirection)
        {
            Vector3I temp;
            for (temp.X = min.X; temp.X <= max.X; temp.X++)
                for (temp.Y = min.Y; temp.Y <= max.Y; temp.Y++)
                    for (temp.Z = min.Z; temp.Z <= max.Z; temp.Z++)
                        AddNeighbour(deconstruct, block, temp, normalDirection);
        }

        private void AddNeighbour(NaniteDeconstructionGrid deconstruct, IMySlimBlock block, Vector3I pos, Vector3I dir)
        {
            var otherBlock = (IMySlimBlock)block.CubeGrid.GetCubeBlock(pos + dir);
            if (otherBlock != null && otherBlock != block && !deconstruct.AddingList.Contains(otherBlock) && !deconstruct.RemoveList.Contains(otherBlock))
                deconstruct.AddingList.Add(otherBlock);
        }

        private void AddConnectedGridBlock(NaniteDeconstructionGrid deconstruct, IMySlimBlock slimBlock)
        {
            if (slimBlock.FatBlock == null)
                return;

            IMyCubeBlock cubeBlock = (IMyCubeBlock)slimBlock.FatBlock;
            IMySlimBlock otherBlock = null;
            if (cubeBlock is IMyPistonBase)
            {
                IMyPistonBase pistonBase = (IMyPistonBase)cubeBlock;
                if (pistonBase.Top != null)
                {
                    MyCubeBlock cubeOther = (MyCubeBlock)pistonBase.Top;

                    if (deconstruct.GridsProcessed.Contains(cubeOther.CubeGrid))
                        return;

                    if (!deconstruct.AddingGridList.Contains(cubeOther.SlimBlock))
                    {
                        deconstruct.AddingGridList.Add(cubeOther.SlimBlock);
                        otherBlock = cubeOther.SlimBlock;
                    }
                }
            }
            else if (cubeBlock is Ingame.IMyShipConnector)
            {
                Ingame.IMyShipConnector connector = (Ingame.IMyShipConnector)cubeBlock;
                if (connector.Status == Sandbox.ModAPI.Ingame.MyShipConnectorStatus.Connected)
                {
                    MyCubeBlock cubeOther = (MyCubeBlock)connector.OtherConnector;

                    if (deconstruct.GridsProcessed.Contains(cubeOther.CubeGrid))
                        return;

                    if (!deconstruct.AddingGridList.Contains(cubeOther.SlimBlock))
                    {
                        deconstruct.AddingGridList.Add(cubeOther.SlimBlock);
                        otherBlock = cubeOther.SlimBlock;
                    }
                }
            }
            else if (cubeBlock is IMyAttachableTopBlock)
            {
                var motorRotor = cubeBlock as IMyAttachableTopBlock;
                if (motorRotor.Base != null)
                {
                    MyCubeBlock cubeOther = motorRotor.Base as MyCubeBlock;

                    if (deconstruct.GridsProcessed.Contains(cubeOther.CubeGrid))
                        return;

                    if (!deconstruct.AddingGridList.Contains(cubeOther.SlimBlock))
                    {
                        deconstruct.AddingGridList.Add(cubeOther.SlimBlock);
                        otherBlock = cubeOther.SlimBlock;
                    }
                }
            }
            else if (cubeBlock is IMyMechanicalConnectionBlock)
            {
                var motorBase = cubeBlock as IMyMechanicalConnectionBlock;
                if (motorBase.TopGrid != null)
                {
                    var cubeOther = motorBase.Top as MyCubeBlock;

                    if (deconstruct.GridsProcessed.Contains(cubeOther.CubeGrid))
                        return;

                    if (!deconstruct.AddingGridList.Contains(cubeOther.SlimBlock))
                    {
                        deconstruct.AddingGridList.Add(cubeOther.SlimBlock);
                        otherBlock = cubeOther.SlimBlock;
                    }
                }
            }
        }

        private void OnGridSplit(MyCubeGrid original, MyCubeGrid newGrid)
        {
            Logging.Instance.WriteLine(string.Format("[Deconstruction] Warning - Split detected: {0} - {1} ({2})",
              original.EntityId, newGrid.EntityId, newGrid.GetBlocks().Count), 1);

            ((IMyCubeGrid)original).Physics.LinearVelocity = Vector3.Zero;
            ((IMyCubeGrid)original).Physics.AngularVelocity = Vector3.Zero;
            ((IMyCubeGrid)newGrid).Physics.LinearVelocity = Vector3.Zero;
            ((IMyCubeGrid)newGrid).Physics.AngularVelocity = Vector3.Zero;
            ((IMyCubeGrid)newGrid).Physics.LinearDamping = 100.0f;
            ((IMyCubeGrid)newGrid).Physics.AngularDamping = 100.0f;
            AddPhysicless((IMyCubeGrid)newGrid);

            newGrid.OnGridSplit += OnGridSplit;
        }

        private void AddPhysicless(IMyCubeGrid grid)
        {
            if (!TempPhysicless.ContainsKey((IMyCubeGrid)grid))
                TempPhysicless.Add((IMyCubeGrid)grid, DateTime.Now);
            else
                TempPhysicless[(IMyCubeGrid)grid] = DateTime.Now;
        }

        private void ComputeMax(MyCubeBlockDefinition definition, MyBlockOrientation orientation, ref Vector3I min, out Vector3I max)
        {
            Vector3I size = definition.Size - 1;
            MatrixI localMatrix = new MatrixI(orientation);
            Vector3I.TransformNormal(ref size, ref localMatrix, out size);
            Vector3I.Abs(ref size, out size);
            max = min + size;
        }
    }
}
