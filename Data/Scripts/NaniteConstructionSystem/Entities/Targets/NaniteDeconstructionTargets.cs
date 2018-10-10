using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.ModAPI;
using VRageMath;
using Ingame = Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI;
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

        /*
        private List<Node<IMySlimBlock>> m_gridTree;
        public List<Node<IMySlimBlock>> GridTree
        {
            get { return m_gridTree; }
            set { m_gridTree = value; }
        }

        private int m_treePosition = 0;
        public int TreePosition
        {
            get { return m_treePosition; }
            set { m_treePosition = value; }
        }
        */

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
        private Dictionary<IMyCubeGrid, NaniteAreaBeacon> m_areaTargetBlocks;
        private Dictionary<IMySlimBlock, int> m_targetBlocks;
        private float m_maxDistance = 300f;

        public NaniteDeconstructionTargets(NaniteConstructionBlock block) : base(block)
        {
            m_validBeaconedGrids = new HashSet<NaniteDeconstructionGrid>();
            m_targetBlocks = new Dictionary<IMySlimBlock, int>();
            m_tempPhysicless = new Dictionary<IMyCubeGrid, DateTime>();
            m_areaTargetBlocks = new Dictionary<IMyCubeGrid, NaniteAreaBeacon>();
            m_maxDistance = NaniteConstructionManager.Settings.DeconstructionMaxDistance;

            MyAPIGateway.Entities.OnEntityRemove += OnEntityRemove;
        }

        public override string TargetName
        {
            get
            {
                return "Deconstruction";
            }
        }

        public override int GetMaximumTargets()
        {
            MyCubeBlock block = (MyCubeBlock)m_constructionBlock.ConstructionBlock;
            return (int)Math.Min(NaniteConstructionManager.Settings.DeconstructionNanitesNoUpgrade + (block.UpgradeValues["DeconstructionNanites"] * NaniteConstructionManager.Settings.DeconstructionNanitesPerUpgrade), NaniteConstructionManager.Settings.DeconstructionMaxStreams);
        }

        public override float GetPowerUsage()
        {
            MyCubeBlock block = (MyCubeBlock)m_constructionBlock.ConstructionBlock;
            return Math.Max(1, NaniteConstructionManager.Settings.DeconstructionPowerPerStream - (int)(block.UpgradeValues["PowerNanites"] * NaniteConstructionManager.Settings.PowerDecreasePerUpgrade));
        }

        public override float GetMinTravelTime()
        {
            MyCubeBlock block = (MyCubeBlock)m_constructionBlock.ConstructionBlock;
            return Math.Max(1f, NaniteConstructionManager.Settings.DeconstructionMinTravelTime - (block.UpgradeValues["SpeedNanites"] * NaniteConstructionManager.Settings.MinTravelTimeReductionPerUpgrade));
        }

        public override float GetSpeed()
        {
            MyCubeBlock block = (MyCubeBlock)m_constructionBlock.ConstructionBlock;
            return NaniteConstructionManager.Settings.DeconstructionDistanceDivisor + (block.UpgradeValues["SpeedNanites"] * (float)NaniteConstructionManager.Settings.SpeedIncreasePerUpgrade);
        }

        public override bool IsEnabled()
        {
            bool result = true;
            if (!((IMyFunctionalBlock)m_constructionBlock.ConstructionBlock).Enabled ||
                !((IMyFunctionalBlock)m_constructionBlock.ConstructionBlock).IsFunctional ||
                m_constructionBlock.ConstructionBlock.CustomName.ToLower().Contains("NoDeconstruction".ToLower()))
                result = false;

            if (NaniteConstructionManager.TerminalSettings.ContainsKey(m_constructionBlock.ConstructionBlock.EntityId))
            {
                if (!NaniteConstructionManager.TerminalSettings[m_constructionBlock.ConstructionBlock.EntityId].AllowDeconstruct)
                    return false;
            }

            return result;
        }

        public override void ParallelUpdate(List<IMyCubeGrid> gridList, List<IMySlimBlock> gridBlocks)
        {
            try
            {
                if (!IsEnabled())
                {
                    PotentialTargetList.Clear();
                    return;
                }

                // Add 
                foreach (var beaconBlock in NaniteConstructionManager.BeaconList.Where(x => x.Value is NaniteBeaconDeconstruct && Vector3D.DistanceSquared(m_constructionBlock.ConstructionBlock.GetPosition(), x.Value.BeaconBlock.GetPosition()) < m_maxDistance * m_maxDistance))
                {
                    IMyCubeBlock item = (IMyCubeBlock)beaconBlock.Value.BeaconBlock;

                    if (!((IMyFunctionalBlock)item).Enabled || !((IMyFunctionalBlock)item).IsFunctional)
						continue;

                    if (gridList.Contains(item.CubeGrid))
                        continue;

                    MyRelationsBetweenPlayerAndBlock relation = item.GetUserRelationToOwner(m_constructionBlock.ConstructionBlock.OwnerId);
                    if (!(relation == MyRelationsBetweenPlayerAndBlock.Owner || relation == MyRelationsBetweenPlayerAndBlock.FactionShare || (MyAPIGateway.Session.CreativeMode && relation == MyRelationsBetweenPlayerAndBlock.NoOwnership)))
                        continue;

                    if (m_validBeaconedGrids.FirstOrDefault(x => x.GridsProcessed.Contains(item.CubeGrid)) != null)
                        continue;

                    NaniteDeconstructionGrid deconstruct = new NaniteDeconstructionGrid(item.CubeGrid);
                    m_validBeaconedGrids.Add(deconstruct);
                    CreateGridStack(deconstruct, (MyCubeGrid)item.CubeGrid, (MyCubeBlock)item);

                    using (Lock.AcquireExclusiveUsing())
                    {
                        foreach (var slimBlock in deconstruct.RemoveList)
                            PotentialTargetList.Add(slimBlock);
                    }

                    deconstruct.RemoveList.Clear();
                }

                CheckAreaBeacons();
                if (PotentialTargetList.Count > 0)
                {
                    using (Lock.AcquireExclusiveUsing())
                    {
                        foreach (IMySlimBlock item in PotentialTargetList.ToList())
                        {
                            if (item.CubeGrid.Closed || item.IsDestroyed || item.IsFullyDismounted || (item.FatBlock != null && item.FatBlock.Closed))
                                PotentialTargetList.Remove(item);
                            
                            if (EntityHelper.GetDistanceBetweenBlockAndSlimblock((IMyCubeBlock)m_constructionBlock.ConstructionBlock, item) > m_maxDistance)
                                PotentialTargetList.Remove(item);
                        }

                        //m_potentialTargetList = m_potentialTargetList.OrderBy(x => GetBlockConnections((IMySlimBlock)(x))).ToList();
                    }
                }
                else if (TargetList.Count == 0 && PotentialTargetList.Count == 0)
                {
                    m_validBeaconedGrids.Clear();
                }
            }
            catch(Exception ex)
            {
                Logging.Instance.WriteLine(string.Format("Parallel Erorr: {0}", ex.ToString()));
            }
        }

        private void CheckAreaBeacons()
        {
            foreach (var beaconBlock in NaniteConstructionManager.BeaconList.Where(x => x.Value is NaniteAreaBeacon))
            {
                IMyCubeBlock cubeBlock = (IMyCubeBlock)beaconBlock.Value.BeaconBlock;

				if (!((IMyFunctionalBlock)cubeBlock).Enabled || !((IMyFunctionalBlock)cubeBlock).IsFunctional)
					continue;

				MyRelationsBetweenPlayerAndBlock relation = cubeBlock.GetUserRelationToOwner(m_constructionBlock.ConstructionBlock.OwnerId);
                if (!(relation == MyRelationsBetweenPlayerAndBlock.Owner || relation == MyRelationsBetweenPlayerAndBlock.FactionShare || (MyAPIGateway.Session.CreativeMode && relation == MyRelationsBetweenPlayerAndBlock.NoOwnership)))
                    continue;

                var item = beaconBlock.Value as NaniteAreaBeacon;
                if (!item.Settings.AllowDeconstruction)
                    continue;

                HashSet<IMyEntity> entities = new HashSet<IMyEntity>();
                MyAPIGateway.Entities.GetEntities(entities);
                foreach (var entity in entities)
                {
                    var grid = entity as IMyCubeGrid;
                    if (grid == null)
                        continue;

                    if (grid.Physics == null)
                        continue;

                    if (grid.Physics.AngularVelocity.Length() != 0f || grid.Physics.LinearVelocity.Length() != 0f)
                        continue;

                    if (m_validBeaconedGrids.FirstOrDefault(x => x.GridsProcessed.Contains(grid)) != null)
                        continue;

                    if (GridHelper.GetGridGroup(grid).Contains(cubeBlock.CubeGrid))
                        continue;

                    if ((grid.GetPosition() - cubeBlock.GetPosition()).LengthSquared() < m_maxDistance * m_maxDistance)
                    {
                        if (item.IsInsideBox(grid.WorldAABB, false))
                        {
                            NaniteDeconstructionGrid deconstruct = new NaniteDeconstructionGrid(grid);
                            m_validBeaconedGrids.Add(deconstruct);
                            CreateGridStack(deconstruct, (MyCubeGrid)grid, null);
                            if (!m_areaTargetBlocks.ContainsKey(grid))
                                m_areaTargetBlocks.Add(grid, item);
                            else
                                m_areaTargetBlocks[grid] = item;

                            foreach (var block in deconstruct.RemoveList)
                            {
                                if(!PotentialTargetList.Contains(block))
                                {
                                    PotentialTargetList.Add(block);
                                }
                            }
                        }
                    }
                }
            }
        }


        public override void FindTargets(ref Dictionary<string, int> available, List<NaniteConstructionBlock> blockList)
        {
            InvalidTargetReason("");

            if (!IsEnabled()) 
                return;

            if (TargetList.Count >= GetMaximumTargets())
            {
                if (PotentialTargetList.Count > 0) 
                    InvalidTargetReason("Maximum targets reached. Add more upgrades!");
                return;
            }

            int TargetListCount = TargetList.Count;
            string LastInvalidTargetReason = "";

            lock (PotentialTargetList)
            {
                foreach(IMySlimBlock item in PotentialTargetList.ToList())
                {
                    if (m_constructionBlock.IsUserDefinedLimitReached())
                    {
                        InvalidTargetReason("User defined maximum nanite limit reached");
                        return;
                    }

                    if (TargetList.Contains(item)) 
                        continue;

                    if (!NaniteConstructionPower.HasRequiredPowerForNewTarget((IMyFunctionalBlock)m_constructionBlock.ConstructionBlock, this))
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
                    foreach (var block in blockList)
                    {
                        if (block.Targets.First(x => x is NaniteDeconstructionTargets).TargetList.Contains(item as IMySlimBlock))
                        {
                            found = true;
                            LastInvalidTargetReason = "Another factory has this block as a target";
                            break;
                        }
                    }

                    if (found)
                        continue;
                    
                    MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                    {
                        if (item != null)
                            TargetList.Add(item);
                    });

                    var def = item.BlockDefinition as MyCubeBlockDefinition;
                    Logging.Instance.WriteLine(string.Format("ADDING Deconstruction Target: conid={0} subtypeid={1} entityID={2} position={3}", 
                      m_constructionBlock.ConstructionBlock.EntityId, def.Id.SubtypeId, item.FatBlock != null ? item.FatBlock.EntityId : 0, item.Position));

                    if (++TargetListCount >= GetMaximumTargets()) 
                        break;
                }
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

                        /*
                        MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                        {
                            item.Key.Physics.LinearVelocity = Vector3.Zero;
                            item.Key.Physics.AngularVelocity = Vector3.Zero;
                        });
                        */
                    }

                    TempPhysicless.Remove(item.Key);
                }
            }
        }

        private void ProcessItem(IMySlimBlock target)
        {
            if(Sync.IsServer)
            {
                if(!IsEnabled())
                {
                    Logging.Instance.WriteLine("CANCELLING Deconstruction Target due to being disabled");
                    CancelTarget(target);
                    return;
                }

                if(!NaniteConstructionPower.HasRequiredPowerForCurrentTarget((IMyFunctionalBlock)m_constructionBlock.ConstructionBlock))
                {
                    Logging.Instance.WriteLine("CANCELLING Deconstruction Target due to power shortage");
                    CancelTarget(target);
                    return;
                }

                if (m_constructionBlock.FactoryState != NaniteConstructionBlock.FactoryStates.Active)
                    return;

                NaniteGrinder grinder = (NaniteGrinder)m_constructionBlock.ToolManager.Tools.FirstOrDefault(x => x.TargetBlock == target && x is NaniteGrinder);
                if(grinder == null)
                {
                    double distance = EntityHelper.GetDistanceBetweenBlockAndSlimblock((IMyCubeBlock)m_constructionBlock.ConstructionBlock, target);
                    int time = (int)Math.Max(GetMinTravelTime() * 1000f, (distance / GetSpeed()) * 1000f);
                    grinder = new NaniteGrinder(m_constructionBlock, target, (int)(time / 2.5f), NaniteConstructionManager.Settings.DeconstructionPerformanceFriendly);
                    m_constructionBlock.ToolManager.Tools.Add(grinder);
                    m_constructionBlock.SendAddTarget(target, TargetTypes.Deconstruction);
                }

                if(m_areaTargetBlocks.ContainsKey(target.CubeGrid))
                {
                    if(!m_areaTargetBlocks[target.CubeGrid].IsInsideBox(target.CubeGrid.WorldAABB, false))                    
                    {
                        CancelTarget(target);
                        RemoveGridTarget(target.CubeGrid);
                        return;
                    }

                    if (target.CubeGrid.Physics.LinearVelocity.LengthSquared() != 0f || target.CubeGrid.Physics.AngularVelocity.LengthSquared() != 0f)
                    {
                        CancelTarget(target);
                        return;
                    }
                }

                if(target.IsDestroyed || target.IsFullyDismounted || target.CubeGrid.GetCubeBlock(target.Position) == null || (target.FatBlock != null && target.FatBlock.Closed))
                {
                    CompleteTarget(target);
                    return;
                }

                if(target.CubeGrid.Closed)
                {
                    Logging.Instance.WriteLine("CANCELLING Deconstruction Target due to grid being closed");
                    CancelTarget(target);
                    return;
                }

                if (EntityHelper.GetDistanceBetweenBlockAndSlimblock((IMyCubeBlock)m_constructionBlock.ConstructionBlock, target) > m_maxDistance)
                {
                    Logging.Instance.WriteLine("CANCELLING Deconstruction Target due to target being out of range");
                    CancelTarget(target);
                    return;
                }
            }

            CreateDeconstructionParticle(target);
        }

        private void RemoveGridTarget(IMyCubeGrid grid)
        {
            foreach(var item in m_validBeaconedGrids)
            {
                if(item.MainGrid == grid)
                {
                    using (Lock.AcquireExclusiveUsing())
                    {
                        foreach (var block in item.RemoveList)
                        {
                            PotentialTargetList.Remove(block);
                            TargetList.Remove(block);
                        }
                    }

                    m_validBeaconedGrids.Remove(item);
                    break;
                }
            }

            if(m_areaTargetBlocks.ContainsKey(grid))
                m_areaTargetBlocks.Remove(grid);
        }

        private void CreateDeconstructionParticle(IMySlimBlock target)
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
            Vector4 startColor = new Vector4(0.55f, 0.95f, 0.95f, 0.75f);
            Vector4 endColor = new Vector4(0.05f, 0.35f, 0.35f, 0.75f);
            m_constructionBlock.ParticleManager.AddParticle(startColor, endColor, GetMinTravelTime() * 1000f, GetSpeed(), target);

            var distance = EntityHelper.GetDistanceBetweenBlockAndSlimblock((IMyCubeBlock)m_constructionBlock.ConstructionBlock, target);
            if (distance > 1000f)
            {
                Logging.Instance.WriteLine(string.Format("PROBLEM Creating particle that has to travel {0}m: {1} - {2} - {3} ({4} {5} {6} {7})", distance, target.FatBlock != null ? target.FatBlock.GetType().Name : target.GetType().Name, m_constructionBlock.ConstructionBlock.EntityId, target.Position, target.FatBlock != null ? target.FatBlock.Physics == null : false, target.CubeGrid.Physics == null, target.CubeGrid.Closed, target.FatBlock != null ? target.FatBlock.Closed : target.IsDestroyed));
            }
        }

        public void CompleteTarget(IMySlimBlock obj)
        {
            Logging.Instance.WriteLine(string.Format("COMPLETING Deconstruction Target: {0} - {1} (EntityID={2},Position={3})", m_constructionBlock.ConstructionBlock.EntityId, obj.GetType().Name, obj.FatBlock != null ? obj.FatBlock.EntityId : 0, obj.Position));
            if (Sync.IsServer)
                m_constructionBlock.SendCompleteTarget(obj, TargetTypes.Deconstruction);

            m_constructionBlock.ParticleManager.CompleteTarget(obj);
            m_constructionBlock.ToolManager.Remove(obj);
            Remove(obj);
        }

        public void CancelTarget(IMySlimBlock obj)
        {
            Logging.Instance.WriteLine(string.Format("CANCELLING Deconstruction Target: {0} - {1} (EntityID={2},Position={3})", m_constructionBlock.ConstructionBlock.EntityId, obj.GetType().Name, obj.FatBlock != null ? obj.FatBlock.EntityId : 0, obj.Position));
            if (Sync.IsServer)
            {
                m_constructionBlock.SendCancelTarget(obj, TargetTypes.Deconstruction);
            }

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
            if(grid != null)
            {
                foreach(var item in m_validBeaconedGrids.ToList())
                {
                    if (item.GridsProcessed.Contains(grid))
                        item.GridsProcessed.Remove(grid);

                    if(item.MainGrid == grid)
                    {
                        m_validBeaconedGrids.Remove(item);
                    }
                }

                if (m_areaTargetBlocks.ContainsKey(grid))
                    m_areaTargetBlocks.Remove(grid);
            }
        }

        private int GetGridGroupBlockCount(IMyCubeGrid grid)
        {
            List<IMyCubeGrid> gridList = GridHelper.GetGridGroup(grid);
            int count = 0;
            foreach (var item in gridList)
            {
                count += ((MyCubeGrid)item).GetBlocks().Count;
            }
            return count;

            //return gridList.Sum(x => ((MyCubeGrid)x).GetBlocks().Count);
        }

        private long GetGridGroupOwner(IMyCubeGrid grid)
        {
            Ingame.IMyGridTerminalSystem system = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid);
            List<Ingame.IMyTerminalBlock> terminalBlocks = new List<Ingame.IMyTerminalBlock>();
            system.GetBlocks(terminalBlocks);
            foreach (var item in terminalBlocks)
            {
                if (((IMyCubeGrid)item.CubeGrid).BigOwners.Count > 0)
                    return ((IMyCubeGrid)item.CubeGrid).BigOwners.First();

                if (item is IMyPistonBase)
                {
                    IMyPistonBase pistonBase = (IMyPistonBase)item;
                    if (pistonBase.TopGrid != null)
                    {
                        if (pistonBase.TopGrid.BigOwners.Count > 0)
                            return pistonBase.TopGrid.BigOwners.First();
                    }
                }

                if (item is IMyMechanicalConnectionBlock)
                {
                    var motorBase = item as IMyMechanicalConnectionBlock;
                    if (motorBase.TopGrid != null)
                    {
                        if (motorBase.TopGrid.BigOwners.Count > 0)
                            return motorBase.TopGrid.BigOwners.First();
                    }
                }

                if (item is Ingame.IMyShipConnector)
                {
                    Ingame.IMyShipConnector connector = (Ingame.IMyShipConnector)item;
                    if (connector.Status == Sandbox.ModAPI.Ingame.MyShipConnectorStatus.Connected && connector.OtherConnector != null)
                    {
                        if (((IMyCubeGrid)connector.OtherConnector.CubeGrid).BigOwners.Count > 0)
                            return ((IMyCubeGrid)connector.OtherConnector.CubeGrid).BigOwners.First();
                    }
                }

                if (item is IMyAttachableTopBlock)
                {
                    var motorRotor = item as IMyAttachableTopBlock;
                    if (motorRotor.IsAttached && motorRotor.Base != null)
                    {
                        if (motorRotor.Base.CubeGrid.BigOwners.Count > 0)
                            return motorRotor.Base.CubeGrid.BigOwners.First();
                    }
                }
            }

            return 0;
        }

        private int GetBlockConnections(NaniteDeconstructionGrid deconstruct, IMySlimBlock currentBlock)
        {
            deconstruct.AddingList.Clear();
            deconstruct.AddingGridList.Clear();

            MyObjectBuilder_CubeBlock block = (MyObjectBuilder_CubeBlock)currentBlock.GetObjectBuilder();
            MyCubeBlockDefinition blockDefinition;
            MyDefinitionManager.Static.TryGetCubeBlockDefinition(block.GetId(), out blockDefinition);

            // Get real block max
            Vector3I Max = Vector3I.Zero;
            Vector3I Min = block.Min;
            ComputeMax(blockDefinition, block.BlockOrientation, ref Min, out Max);

            AddNeighbours(deconstruct, currentBlock, Min, new Vector3I(Min.X, Max.Y, Max.Z), -Vector3I.UnitX);
            AddNeighbours(deconstruct, currentBlock, Min, new Vector3I(Max.X, Min.Y, Max.Z), -Vector3I.UnitY);
            AddNeighbours(deconstruct, currentBlock, Min, new Vector3I(Max.X, Max.Y, Min.Z), -Vector3I.UnitZ);
            AddNeighbours(deconstruct, currentBlock, new Vector3I(Max.X, Min.Y, Min.Z), Max, Vector3I.UnitX);
            AddNeighbours(deconstruct, currentBlock, new Vector3I(Min.X, Max.Y, Min.Z), Max, Vector3I.UnitY);
            AddNeighbours(deconstruct, currentBlock, new Vector3I(Min.X, Min.Y, Max.Z), Max, Vector3I.UnitZ);

            // Check if currentBlock is a connector of some kind, then follow it
            // AddConnectedGridBlock(deconstruct, currentBlock);

            return deconstruct.AddingList.Count + deconstruct.AddingGridList.Count;
        }

        private int GetBlockConnections(IMySlimBlock currentBlock)
        {
            if (currentBlock.CubeGrid.Closed)
                return 0;

            NaniteDeconstructionGrid deconstruct = new NaniteDeconstructionGrid(currentBlock.CubeGrid);

            deconstruct.AddingList.Clear();
            deconstruct.AddingGridList.Clear();

            MyObjectBuilder_CubeBlock block = (MyObjectBuilder_CubeBlock)currentBlock.GetObjectBuilder();
            MyCubeBlockDefinition blockDefinition;
            MyDefinitionManager.Static.TryGetCubeBlockDefinition(block.GetId(), out blockDefinition);

            // Get real block max
            Vector3I Max = Vector3I.Zero;
            Vector3I Min = block.Min;
            ComputeMax(blockDefinition, block.BlockOrientation, ref Min, out Max);

            AddNeighbours(deconstruct, currentBlock, Min, new Vector3I(Min.X, Max.Y, Max.Z), -Vector3I.UnitX);
            AddNeighbours(deconstruct, currentBlock, Min, new Vector3I(Max.X, Min.Y, Max.Z), -Vector3I.UnitY);
            AddNeighbours(deconstruct, currentBlock, Min, new Vector3I(Max.X, Max.Y, Min.Z), -Vector3I.UnitZ);
            AddNeighbours(deconstruct, currentBlock, new Vector3I(Max.X, Min.Y, Min.Z), Max, Vector3I.UnitX);
            AddNeighbours(deconstruct, currentBlock, new Vector3I(Min.X, Max.Y, Min.Z), Max, Vector3I.UnitY);
            AddNeighbours(deconstruct, currentBlock, new Vector3I(Min.X, Min.Y, Max.Z), Max, Vector3I.UnitZ);

            int additional = 0;
            if (currentBlock.FatBlock != null && currentBlock.FatBlock.BlockDefinition.SubtypeName.Contains("NaniteBeaconDeconstruct"))
                additional += 10;

            /*
            if (currentBlock.FatBlock != null && 
                    (currentBlock.FatBlock is IMyPistonBase ||
                     currentBlock.FatBlock is IMyPistonTop ||
                     currentBlock.FatBlock is IMyMotorRotor ||
                     currentBlock.FatBlock is IMyMotorStator ||
                     currentBlock.FatBlock is IMyMotorBase ||
                     currentBlock.FatBlock is Ingame.IMyShipConnector))
            {
                additional += 10;
            }
            */

            AddConnectedGridBlock(deconstruct, currentBlock);

            return deconstruct.AddingList.Count + deconstruct.AddingGridList.Count + additional;
        }

        private void CreateGridStack(NaniteDeconstructionGrid deconstruct, MyCubeGrid grid, MyCubeBlock beacon)
        {
            DateTime start = DateTime.Now;
            List<IMyCubeGrid> gridList = GridHelper.GetGridGroup((IMyCubeGrid)grid);
            IMyCubeGrid mainGrid = gridList.OrderByDescending(x => ((MyCubeGrid)x).GetBlocks().Count).FirstOrDefault();
            if (mainGrid == null)
                return;

            List<IMySlimBlock> blocks = new List<IMySlimBlock>();
            mainGrid.GetBlocks(blocks);
            IMySlimBlock block = blocks.OrderBy(x => GetBlockConnections(deconstruct, x)).FirstOrDefault();
            if (block == null) // No blocks ?
                return;

            if(beacon != null && mainGrid == beacon.CubeGrid)
                block = (IMySlimBlock)beacon.SlimBlock;

            //deconstruct.GridTree = new List<Node<IMySlimBlock>>();
            //deconstruct.GridTree.Add(new Node<IMySlimBlock>(block));

            CreateRemovalOrder(deconstruct, block);
            DateTime end = DateTime.Now;
            deconstruct.AddingGridList.Clear();
            deconstruct.AddingList.Clear();
            Logging.Instance.WriteLine($"PROCESS Creating Grid Stack.  Total Process Time: {(end - start).TotalMilliseconds}ms");
        }

        private void CreateRemovalOrder(NaniteDeconstructionGrid deconstruct, IMySlimBlock startBlock)
        {
            IMySlimBlock currentBlock = startBlock;
            deconstruct.AddingList.Clear();
            deconstruct.GridsProcessed.Clear();

            while (true)
            {
                if (!deconstruct.GridsProcessed.Contains(currentBlock.CubeGrid))
                {
                    ((MyCubeGrid)currentBlock.CubeGrid).OnGridSplit += OnGridSplit;
                    deconstruct.GridsProcessed.Add(currentBlock.CubeGrid);
                }

                MyObjectBuilder_CubeBlock block = (MyObjectBuilder_CubeBlock)currentBlock.GetObjectBuilder();
                MyCubeBlockDefinition blockDefinition;
                MyDefinitionManager.Static.TryGetCubeBlockDefinition(block.GetId(), out blockDefinition);

                // Get real block max
                Vector3I Max = Vector3I.Zero;
                Vector3I Min = block.Min;
                ComputeMax(blockDefinition, block.BlockOrientation, ref Min, out Max);

                AddNeighbours(deconstruct, currentBlock, Min, new Vector3I(Min.X, Max.Y, Max.Z), -Vector3I.UnitX);
                AddNeighbours(deconstruct, currentBlock, Min, new Vector3I(Max.X, Min.Y, Max.Z), -Vector3I.UnitY);
                AddNeighbours(deconstruct, currentBlock, Min, new Vector3I(Max.X, Max.Y, Min.Z), -Vector3I.UnitZ);
                AddNeighbours(deconstruct, currentBlock, new Vector3I(Max.X, Min.Y, Min.Z), Max, Vector3I.UnitX);
                AddNeighbours(deconstruct, currentBlock, new Vector3I(Min.X, Max.Y, Min.Z), Max, Vector3I.UnitY);
                AddNeighbours(deconstruct, currentBlock, new Vector3I(Min.X, Min.Y, Max.Z), Max, Vector3I.UnitZ);

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
                    //deconstruct.TreePosition++;
                    //deconstruct.GridTree.Add(new Node<IMySlimBlock>(currentBlock));
                }
                else
                {
                    currentBlock = deconstruct.AddingList[0];
                    deconstruct.AddingList.Remove(currentBlock);
                }
            }

            // Find user defined priority blocks for deconstruction.  
            FindPriorityBlocks(deconstruct, startBlock);

            Logging.Instance.WriteLine($"Block Count: {deconstruct.RemoveList.Count}");
            Logging.Instance.WriteLine($"Grid Count: {deconstruct.GridsProcessed.Count}");
            //Logging.Instance.WriteLine($"Tree Size: {deconstruct.GridTree.Sum(x => x.All.Count())}");
        }

        private void FindPriorityBlocks(NaniteDeconstructionGrid deconstruct, IMySlimBlock startBlock)
        {
            var grids = GridHelper.GetGridGroup(startBlock.CubeGrid);

            foreach(var grid in grids)
            {
                List<IMySlimBlock> blocks = new List<IMySlimBlock>();
                grid.GetBlocks(blocks);
                foreach(var block in blocks)
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
            {
                for (temp.Y = min.Y; temp.Y <= max.Y; temp.Y++)
                {
                    for (temp.Z = min.Z; temp.Z <= max.Z; temp.Z++)
                    {
                        AddNeighbour(deconstruct, block, temp, normalDirection);
                    }
                }
            }
        }

        private void AddNeighbour(NaniteDeconstructionGrid deconstruct, IMySlimBlock block, Vector3I pos, Vector3I dir)
        {
            var otherBlock = (IMySlimBlock)block.CubeGrid.GetCubeBlock(pos + dir);
            if (otherBlock != null && otherBlock != block && !deconstruct.AddingList.Contains(otherBlock) && !deconstruct.RemoveList.Contains(otherBlock))
            {
                deconstruct.AddingList.Add(otherBlock);

                /*
                if(deconstruct.GridTree != null)
                {
                    var parent = deconstruct.GridTree[deconstruct.TreePosition].All.FirstOrDefault(x => x.Value == block);
                    parent.Add(otherBlock);
                }
                */
            }
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
            Logging.Instance.WriteLine(string.Format("WARNING Split detected: {0} - {1} ({2})", original.EntityId, newGrid.EntityId, newGrid.GetBlocks().Count));

            ((IMyCubeGrid)original).Physics.LinearVelocity = Vector3.Zero;
            ((IMyCubeGrid)original).Physics.AngularVelocity = Vector3.Zero;
            //original.Physics.Enabled = false;
            //AddPhysicless((IMyCubeGrid)original);

            ((IMyCubeGrid)newGrid).Physics.LinearVelocity = Vector3.Zero;
            ((IMyCubeGrid)newGrid).Physics.AngularVelocity = Vector3.Zero;
            ((IMyCubeGrid)newGrid).Physics.LinearDamping = 100.0f;
            ((IMyCubeGrid)newGrid).Physics.AngularDamping = 100.0f;
            //newGrid.Physics.Enabled = false;
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
