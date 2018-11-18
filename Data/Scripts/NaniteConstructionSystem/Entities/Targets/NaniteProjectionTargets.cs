using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.ModAPI;
using VRageMath;
using VRage.Game.ModAPI;
using Sandbox.Definitions;
using Sandbox.Common.ObjectBuilders;

using NaniteConstructionSystem.Particles;
using NaniteConstructionSystem.Extensions;
using NaniteConstructionSystem.Entities.Beacons;

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

        public NaniteProjectionTargets(NaniteConstructionBlock constructionBlock) : base(constructionBlock)
        {
            m_count = 0;
            m_targetBlocks = new Dictionary<IMySlimBlock, NaniteProjectionTarget>();
            m_maxDistance = NaniteConstructionManager.Settings.ProjectionMaxBeaconDistance;
        }

        public override int GetMaximumTargets()
        {
            return (int)Math.Min(NaniteConstructionManager.Settings.ProjectionNanitesNoUpgrade 
              + (((MyCubeBlock)m_constructionBlock.ConstructionBlock).UpgradeValues["ConstructionNanites"] 
              * NaniteConstructionManager.Settings.ProjectionNanitesPerUpgrade), NaniteConstructionManager.Settings.ProjectionMaxStreams);
        }

        public override float GetPowerUsage()
        {
            return Math.Max(1, NaniteConstructionManager.Settings.ProjectionPowerPerStream 
              - (int)(((MyCubeBlock)m_constructionBlock.ConstructionBlock).UpgradeValues["PowerNanites"] 
              * NaniteConstructionManager.Settings.PowerDecreasePerUpgrade));
        }

        public override float GetMinTravelTime()
        {
            return Math.Max(1f, NaniteConstructionManager.Settings.ProjectionMinTravelTime 
              - (((MyCubeBlock)m_constructionBlock.ConstructionBlock).UpgradeValues["SpeedNanites"] 
              * NaniteConstructionManager.Settings.MinTravelTimeReductionPerUpgrade));
        }

        public override float GetSpeed()
        {
            return NaniteConstructionManager.Settings.ProjectionDistanceDivisor 
              + (((MyCubeBlock)m_constructionBlock.ConstructionBlock).UpgradeValues["SpeedNanites"] 
              * (float)NaniteConstructionManager.Settings.SpeedIncreasePerUpgrade);
        }

        public override bool IsEnabled()
        {
            if (!((IMyFunctionalBlock)m_constructionBlock.ConstructionBlock).Enabled 
              || !((IMyFunctionalBlock)m_constructionBlock.ConstructionBlock).IsFunctional 
              || m_constructionBlock.ConstructionBlock.CustomName.ToLower().Contains("NoProjection".ToLower()) 
              || (NaniteConstructionManager.TerminalSettings.ContainsKey(m_constructionBlock.ConstructionBlock.EntityId) 
              && !NaniteConstructionManager.TerminalSettings[m_constructionBlock.ConstructionBlock.EntityId].AllowProjection))
                return false;

            return true;
        }

        public override void FindTargets(ref Dictionary<string, int> available, List<NaniteConstructionBlock> blockList)
        {
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                m_lastInvalidTargetReason = "";
                ComponentsRequired.Clear();
            });

            if (!IsEnabled()) 
                return;

            if (TargetList.Count >= GetMaximumTargets())
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
            
            foreach (var item in PotentialTargetList.OrderBy(x => Vector3D.Distance(sourcePosition, EntityHelper.GetBlockPosition((IMySlimBlock)x))).ToList())
            {
                if (item == null || TargetList.Contains(item)) 
                    continue;

                missing = inventoryManager.GetProjectionComponents((IMySlimBlock)item);
                bool haveComponents = inventoryManager.CheckComponentsAvailable(ref missing, ref available);
                if (haveComponents && m_constructionBlock.HasRequiredPowerForNewTarget(this) 
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

                    if (found)
                        continue;

                    AddTarget(item);

                    IMySlimBlock slimBlock = (IMySlimBlock)item;
                    var def = slimBlock.BlockDefinition as MyCubeBlockDefinition;
                    Logging.Instance.WriteLine(string.Format("ADDING Projection Target: conid={0} subtypeid={1} entityID={2} position={3}", 
                        m_constructionBlock.ConstructionBlock.EntityId, def.Id.SubtypeId, slimBlock.FatBlock != null ? slimBlock.FatBlock.EntityId : 0, slimBlock.Position));

                    if (++TargetListCount >= GetMaximumTargets()) 
                        break;
                }
                else if (!haveComponents)
                    LastInvalidTargetReason = "Missing components to start projected block";

                else if (!m_constructionBlock.HasRequiredPowerForNewTarget(this))
                    LastInvalidTargetReason = "Insufficient power for another target.";
            }
            if (LastInvalidTargetReason != "")
                InvalidTargetReason(LastInvalidTargetReason);

            PotentialTargetList.Clear();
        }

        public override void Update()
        {
            foreach (var item in m_targetList.ToList())
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
                    Logging.Instance.WriteLine("CANCELLING Projection Target due to invalid position");
                    CancelTarget(target);
                    return;
                }

                if (!IsEnabled())
                {
                    Logging.Instance.WriteLine("CANCELLING Projection Target due to being disabled");
                    CancelTarget(target);
                    return;
                }

                if (!m_constructionBlock.IsPowered())
                {
                    Logging.Instance.WriteLine("CANCELLING Projection Target due to power shortage");
                    CancelTarget(target);
                    return;
                }

                if (m_constructionBlock.FactoryState != NaniteConstructionBlock.FactoryStates.Active)
                    return;

                double distance = EntityHelper.GetDistanceBetweenBlockAndSlimblock((IMyCubeBlock)m_constructionBlock.ConstructionBlock, target);
                int time = (int)Math.Max(GetMinTravelTime() * 1000f, (distance / GetSpeed()) * 1000f);
                if (!m_targetBlocks.ContainsKey(target))
                {
                    NaniteProjectionTarget projectionTarget = new NaniteProjectionTarget();
                    projectionTarget.ParticleCount = 0;
                    projectionTarget.StartTime = (int)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
                    m_targetBlocks.Add(target, projectionTarget);
                    m_constructionBlock.SendAddTarget(target, TargetTypes.Projection, GetProjectorByBlock(target));
                }

                if (MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - m_targetBlocks[target].StartTime >= time / 2.5 && !m_targetBlocks[target].CheckInventory)
                {
                    m_targetBlocks[target].CheckInventory = true;
                    if (!m_constructionBlock.InventoryManager.ProcessMissingComponents(target))
                    {
                        Logging.Instance.WriteLine("CANCELLING Projection Target due to missing components");
                        CancelTarget(target);
                        return;
                    }
                }

                if (MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - m_targetBlocks[target].StartTime >= time / 2)
                {
                    ProcessBuildBlock(target);
                    CompleteTarget(target);
                    return;
                }

                if (!m_potentialTargetList.Contains(target))
                {
                    Logging.Instance.WriteLine("COMPLETING Projection Target since potential target is missing");
                    CompleteTarget(target);
                    return;
                }
            }
            CreateProjectionParticle(target);
        }

        public void CancelTarget(IMySlimBlock target)
        {
            Logging.Instance.WriteLine(string.Format("CANCELLING Projection Target: {0} - {1} (EntityID={2},Position={3})", 
              m_constructionBlock.ConstructionBlock.EntityId, target.GetType().Name, target.FatBlock != null ? target.FatBlock.EntityId : 0, target.Position));

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
            Logging.Instance.WriteLine(string.Format("COMPLETING Projection Target: {0} - {1} (EntityID={2},Position={3})", 
              m_constructionBlock.ConstructionBlock.EntityId, target.GetType().Name, target.FatBlock != null ? target.FatBlock.EntityId : 0, target.Position));

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
                Logging.Instance.WriteLine($"ADD ProjectionParticle Target: {target.Position}");
                NaniteProjectionTarget projectionTarget = new NaniteProjectionTarget();
                projectionTarget.ParticleCount = 0;
                projectionTarget.StartTime = (int)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
                m_targetBlocks.Add(target, projectionTarget);
            }

            if (NaniteParticleManager.TotalParticleCount > NaniteParticleManager.MaxTotalParticles)
                return;

            Vector4 startColor = new Vector4(0.95f, 0.0f, 0.95f, 0.75f);
            Vector4 endColor = new Vector4(0.035f, 0.0f, 0.35f, 0.75f);
            m_constructionBlock.ParticleManager.AddParticle(startColor, endColor, GetMinTravelTime() * 1000f, GetSpeed(), target);
        }

        public override void ParallelUpdate(List<IMyCubeGrid> gridList, List<IMySlimBlock> blocks)
        {
            if (!IsEnabled())
                return;

            using (m_lock.AcquireExclusiveUsing())
                TargetList.Clear();

            foreach (var item in blocks)
                CheckBlockProjection(item);
        }

        public override void CheckBeacons()
        {
            foreach (var beaconBlock in NaniteConstructionManager.BeaconList.Where(x => x.Value is NaniteBeaconProjection 
              && Vector3D.DistanceSquared(m_constructionBlock.ConstructionBlock.GetPosition(), x.Value.BeaconBlock.GetPosition()) < m_maxDistance * m_maxDistance).ToList())
            {
                IMyCubeBlock item = (IMyCubeBlock)beaconBlock.Value.BeaconBlock;

				if (item == null || !((IMyFunctionalBlock)item).Enabled || !((IMyFunctionalBlock)item).IsFunctional 
                  || !MyRelationsBetweenPlayerAndBlockExtensions.IsFriendly(item.GetUserRelationToOwner(m_constructionBlock.ConstructionBlock.OwnerId)))
					continue;

                List<IMySlimBlock> beaconBlocks = new List<IMySlimBlock>();

                foreach (var grid in MyAPIGateway.GridGroups.GetGroup((IMyCubeGrid)item.CubeGrid, GridLinkTypeEnum.Physical))
                    grid.GetBlocks(beaconBlocks);

                foreach (var block in beaconBlocks)
                    CheckBlockProjection(block);
            }
        }

        public static long GetProjectorByBlock(IMySlimBlock block)
        {
            foreach(var item in NaniteConstructionManager.ProjectorBlocks)
            {
                var projector = item.Value as IMyProjector;
                if(projector.ProjectedGrid != null && projector.ProjectedGrid.EntityId == block.CubeGrid.EntityId)
                    return projector.EntityId;
            }

            return 0;
        }

        public override void CheckAreaBeacons()
        {
            foreach (var beaconBlock in NaniteConstructionManager.BeaconList.Where(x => x.Value is NaniteAreaBeacon).ToList())
            {
                IMyCubeBlock cubeBlock = beaconBlock.Value.BeaconBlock;

				if (cubeBlock == null || !((IMyFunctionalBlock)cubeBlock).Enabled || !((IMyFunctionalBlock)cubeBlock).IsFunctional)
					continue;

				var item = beaconBlock.Value as NaniteAreaBeacon;
                if (!item.Settings.AllowProjection)
                    continue;

                HashSet<IMyEntity> entities = new HashSet<IMyEntity>();
                MyAPIGateway.Entities.GetEntities(entities);
                foreach (var entity in entities)
                {
                    var grid = entity as IMyCubeGrid;

                    if (grid == null || (grid.GetPosition() - cubeBlock.GetPosition()).LengthSquared() >= m_maxDistance * m_maxDistance)
                        continue;
                        
                    foreach (IMySlimBlock block in ((MyCubeGrid)grid).GetBlocks())
                    {
                        BoundingBoxD blockbb;
                        block.GetWorldBoundingBox(out blockbb, true);
                        if (item.IsInsideBox(blockbb))
                            CheckBlockProjection(block);
                    }
                }
            }
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
            MyCubeGrid grid = (MyCubeGrid)projector.ProjectedGrid;

            foreach(IMySlimBlock block in grid.GetBlocks())
            {
                if(projector.CanBuild(block, false) == BuildCheckResult.OK)
                {
                    using (Lock.AcquireExclusiveUsing())
                    {
                        if(!PotentialTargetList.Contains(block))
                            PotentialTargetList.Add(block);
                    }
                }
            }
        }

        private void ProcessBuildBlock(IMySlimBlock block)
        {
            foreach(var item in NaniteConstructionManager.ProjectorBlocks)
            {
                var projector = item.Value as IMyProjector;
                if(projector != null && projector.ProjectedGrid == block.CubeGrid)
                {
                    projector.Build(block, m_constructionBlock.ConstructionBlock.OwnerId, m_constructionBlock.ConstructionBlock.EntityId, false);
                    break;
                }
            }
        }

        private bool UpdateProjection(MyCubeBlock projector, MyCubeGrid projectedGrid, MyObjectBuilder_ProjectorBase projectorBuilder)
        {
            MyCubeGrid cubeGrid = projector.CubeGrid;
            MyObjectBuilder_CubeGrid gridBuilder = (MyObjectBuilder_CubeGrid)projectedGrid.GetObjectBuilder();
            bool found = false;
            List<IMySlimBlock> blocks = new List<IMySlimBlock>();
            ((IMyCubeGrid)projectedGrid).GetBlocks(blocks, e => {return true;});

            foreach (IMySlimBlock block in blocks)
            {
                Vector3 worldPosition = projectedGrid.GridIntegerToWorld(block.Min);
                Vector3I realPosition = cubeGrid.WorldToGridInteger(worldPosition);
                var realBlock = (IMySlimBlock)cubeGrid.GetCubeBlock(realPosition);
                if (realBlock != null)
                    continue;

                MyCubeBlockDefinition blockDefinition = (MyCubeBlockDefinition)block.BlockDefinition;
                
                if (CanBuildBlock(block, projectedGrid, projector, cubeGrid, projectorBuilder, blockDefinition))
                {
                    var slimBlock = (IMySlimBlock)projectedGrid.GetCubeBlock(block.Min);
                    if (slimBlock != null && slimBlock.CubeGrid.GetPosition() != Vector3D.Zero)
                    {
                        PotentialTargetList.Add(slimBlock);
                        found = true;
                    }
                }
                else
                {
                    using (m_lock.AcquireExclusiveUsing())
                    {
                        foreach (var item in blockDefinition.Components)
                        {
                            if (!ComponentsRequired.ContainsKey(item.Definition.Id.SubtypeName))
                                ComponentsRequired.Add(item.Definition.Id.SubtypeName, item.Count);
                            else
                                ComponentsRequired[item.Definition.Id.SubtypeName] += item.Count;
                        }
                    }
                }
            }

            return found;
        }

        private bool CanBuildBlock(IMySlimBlock block, MyCubeGrid blockGrid, MyCubeBlock projector, MyCubeGrid projectorGrid, MyObjectBuilder_ProjectorBase projectorBuilder, MyCubeBlockDefinition blockDefinition)
        {
            MyBlockOrientation blockOrientation = block.Orientation;

            Matrix local;
            blockOrientation.GetMatrix(out local);
            var gridOrientation = GetGridOrientation(projectorBuilder);
            if (gridOrientation != Matrix.Identity)
            {
                var afterRotation = Matrix.Multiply(local, gridOrientation);
                blockOrientation = new MyBlockOrientation(ref afterRotation);
            }

            Quaternion blockOrientationQuat;
            blockOrientation.GetQuaternion(out blockOrientationQuat);

            Quaternion projQuat = Quaternion.Identity;
            projector.Orientation.GetQuaternion(out projQuat);
            blockOrientationQuat = Quaternion.Multiply(projQuat, blockOrientationQuat);

            // Get real block max            
            Vector3I blockMax = Vector3I.Zero;
            Vector3I blockMin = block.Min;
            ComputeMax(blockDefinition, blockOrientation, ref blockMin, out blockMax);
            var position = ComputePositionInGrid(new MatrixI(blockOrientation), blockDefinition, blockMin);

            Vector3I projectedMin = projectorGrid.WorldToGridInteger(blockGrid.GridIntegerToWorld(block.Min));
            Vector3I projectedMax = projectorGrid.WorldToGridInteger(blockGrid.GridIntegerToWorld(blockMax));
            Vector3I blockPos = projectorGrid.WorldToGridInteger(blockGrid.GridIntegerToWorld(position));

            Vector3I min = new Vector3I(Math.Min(projectedMin.X, projectedMax.X), Math.Min(projectedMin.Y, projectedMax.Y), Math.Min(projectedMin.Z, projectedMax.Z));
            Vector3I max = new Vector3I(Math.Max(projectedMin.X, projectedMax.X), Math.Max(projectedMin.Y, projectedMax.Y), Math.Max(projectedMin.Z, projectedMax.Z));

            projectedMin = min;
            projectedMax = max;

            if (!projectorGrid.CanAddCubes(projectedMin, projectedMax))
            {
                IMySlimBlock slimBlock = (IMySlimBlock)blockGrid.GetCubeBlock(block.Min);
                if (slimBlock == null || slimBlock.FatBlock == null)
                    return false;

                Logging.Instance.WriteLine(string.Format("Can not add block: {0}: {1} - {2} {3} {4} {5}", slimBlock.FatBlock.EntityId, blockDefinition.Id, projectedMin, projectedMax, blockMin, blockMax)); //, slimBlock.FatBlock.EntityId));
                return false;
            }

            var mountPoints = blockDefinition.GetBuildProgressModelMountPoints(1.0f);
            bool isConnected = MyCubeGrid.CheckConnectivity(projectorGrid, blockDefinition, mountPoints, ref blockOrientationQuat, ref blockPos);
            if (isConnected && projectorGrid.GetCubeBlock(blockPos) == null)
                return true;
            else
                return false;
        }

        private void BuildBlock(IMySlimBlock block, MyCubeBlock constructionBlock)
        {
            MyObjectBuilder_CubeBlock cubeBlock = block.GetObjectBuilder();
            MyObjectBuilder_ProjectorBase projectorBuilder = null;
            MyCubeGrid projectorGrid = null;
            MyCubeGrid blockGrid = (MyCubeGrid)block.CubeGrid;
            MyCubeBlock projector = null;

            foreach(var item in NaniteConstructionManager.ProjectorBlocks)
            {
                var projectorTest = item.Value as IMyProjector;
                if (projectorTest == null)
                    continue;

                if (projectorTest.ProjectedGrid == null)
                    continue;

                if(projectorTest.ProjectedGrid == block.CubeGrid)
                {
                    projector = (MyCubeBlock)projectorTest;
                    projectorGrid = projector.CubeGrid;
                    projectorBuilder = (MyObjectBuilder_ProjectorBase)projector.GetObjectBuilderCubeBlock();
                    break;
                }
            }

            if(projectorBuilder == null)
            {
                Logging.Instance.WriteLine("PROBLEM Can not locate projector that is projecting target!");
                return;
            }

            Quaternion quat = Quaternion.Identity;
            var orientation = block.Orientation;

            Matrix local;
            orientation.GetMatrix(out local);
            var gridOrientation = GetGridOrientation(projectorBuilder);
            if (gridOrientation != Matrix.Identity)
            {
                var afterRotation = Matrix.Multiply(local, gridOrientation);
                orientation = new MyBlockOrientation(ref afterRotation);
            }

            Quaternion projQuat = Quaternion.Identity;
            projector.Orientation.GetQuaternion(out projQuat);
            orientation.GetQuaternion(out quat);
            quat = Quaternion.Multiply(projQuat, quat);

            // Get real block max
            MyCubeBlockDefinition blockDefinition = (MyCubeBlockDefinition)block.BlockDefinition;
            Vector3I blockMax = block.Max;
            Vector3I blockMin = block.Min;
            Vector3I position = block.Position;

            Vector3I min = projectorGrid.WorldToGridInteger(blockGrid.GridIntegerToWorld(blockMin));
            Vector3I max = projectorGrid.WorldToGridInteger(blockGrid.GridIntegerToWorld(blockMax));
            Vector3I pos = projectorGrid.WorldToGridInteger(blockGrid.GridIntegerToWorld(block.Position));

            Vector3I projectedMin = new Vector3I(Math.Min(min.X, max.X), Math.Min(min.Y, max.Y), Math.Min(min.Z, max.Z));
            Vector3I projectedMax = new Vector3I(Math.Max(min.X, max.X), Math.Max(min.Y, max.Y), Math.Max(min.Z, max.Z));

            MyCubeGrid.MyBlockLocation location = new MyCubeGrid.MyBlockLocation(blockDefinition.Id, projectedMin, projectedMax, pos,
              quat, 0, constructionBlock.OwnerId);

            MyObjectBuilder_CubeBlock objectBuilder = cubeBlock;
            objectBuilder.SetupForProjector();
            var functionalBuilder = objectBuilder as MyObjectBuilder_FunctionalBlock;
            if (functionalBuilder != null && !functionalBuilder.Enabled)
                functionalBuilder.Enabled = true;

            var terminalBuilder = objectBuilder as MyObjectBuilder_TerminalBlock;
            if (terminalBuilder != null)
                terminalBuilder.Owner = constructionBlock.OwnerId;

            var shipConnector = objectBuilder as MyObjectBuilder_ShipConnector;
            if(shipConnector != null)
            {
                shipConnector.Connected = false;
                shipConnector.ConnectedEntityId = 0;
                shipConnector.MasterToSlaveGrid = null;
                shipConnector.MasterToSlaveTransform = null;
            }

            location.EntityId = 0; // MyEntityIdentifier.AllocateId();
            objectBuilder.EntityId = 0;
            objectBuilder.BuiltBy = constructionBlock.OwnerId;

            objectBuilder.ConstructionInventory = null;
            projector.CubeGrid.BuildBlockRequest(block.GetColorMask().PackHSVToUint(), location, objectBuilder, constructionBlock.EntityId, false, constructionBlock.OwnerId);
        }

        private void ComputeMax(MyCubeBlockDefinition definition, MyBlockOrientation orientation, ref Vector3I min, out Vector3I max)
        {
            Vector3I size = definition.Size - 1;
            MatrixI localMatrix = new MatrixI(orientation);
            Vector3I.TransformNormal(ref size, ref localMatrix, out size);
            Vector3I.Abs(ref size, out size);
            max = min + size;
        }

        private Vector3I ComputePositionInGrid(MatrixI localMatrix, MyCubeBlockDefinition blockDefinition, Vector3I min)
        {
            var center = blockDefinition.Center;
            var sizeMinusOne = blockDefinition.Size - 1;
            Vector3I rotatedBlockSize;
            Vector3I rotatedCenter;
            Vector3I.TransformNormal(ref sizeMinusOne, ref localMatrix, out rotatedBlockSize);
            Vector3I.TransformNormal(ref center, ref localMatrix, out rotatedCenter);
            var trueSize = Vector3I.Abs(rotatedBlockSize);
            var offsetCenter = rotatedCenter + min;

            if (rotatedBlockSize.X != trueSize.X)
                offsetCenter.X += trueSize.X;

            if (rotatedBlockSize.Y != trueSize.Y)
                offsetCenter.Y += trueSize.Y;

            if (rotatedBlockSize.Z != trueSize.Z)
                offsetCenter.Z += trueSize.Z;

            return offsetCenter;
        }

        private Matrix GetGridOrientation(MyObjectBuilder_ProjectorBase projectorBuilder)
        {
            m_dirForward = Vector3.Forward;
            m_dirUp = Vector3.Up;
            m_orientationAngle = 0f;

            RotateAroundAxis(0, Math.Sign(projectorBuilder.ProjectionRotation.X), Math.Abs(projectorBuilder.ProjectionRotation.X * MathHelper.PiOver2));
            RotateAroundAxis(1, Math.Sign(projectorBuilder.ProjectionRotation.Y), Math.Abs(projectorBuilder.ProjectionRotation.Y * MathHelper.PiOver2));
            RotateAroundAxis(2, Math.Sign(projectorBuilder.ProjectionRotation.Z), Math.Abs(projectorBuilder.ProjectionRotation.Z * MathHelper.PiOver2));

            return Matrix.CreateWorld(Vector3.Zero, m_dirForward, m_dirUp) * Matrix.CreateFromAxisAngle(m_dirUp, m_orientationAngle);
        }

        private void RotateAroundAxis(int axisIndex, int sign, float angleDelta)
        {
            switch (axisIndex)
            {
                case 0:
                    if (sign < 0)
                        UpMinus(angleDelta);
                    else
                        UpPlus(angleDelta);
                    break;

                case 1:
                    if (sign < 0)
                        AngleMinus(angleDelta);
                    else
                        AnglePlus(angleDelta);
                    break;

                case 2:
                    if (sign < 0)
                        RightPlus(angleDelta);
                    else
                        RightMinus(angleDelta);
                    break;
            }
        }

        private void AnglePlus(float angle)
        {
            m_orientationAngle += angle;
            if (m_orientationAngle >= (float)Math.PI * 2.0f)
                m_orientationAngle -= (float)Math.PI * 2.0f;
        }

        private void AngleMinus(float angle)
        {
            m_orientationAngle -= angle;
            if (m_orientationAngle < 0.0f)
                m_orientationAngle += (float)Math.PI * 2.0f;
        }

        private void UpPlus(float angle)
        {
            ApplyOrientationAngle();
            Vector3 right = Vector3.Cross(m_dirForward, m_dirUp);
            float cos = (float)Math.Cos(angle);
            float sin = (float)Math.Sin(angle);
            Vector3 up = m_dirUp * cos - m_dirForward * sin;
            m_dirForward = m_dirUp * sin + m_dirForward * cos;
            m_dirUp = up;
        }

        private void UpMinus(float angle)
        {
            UpPlus(-angle);
        }

        private void RightPlus(float angle)
        {
            ApplyOrientationAngle();
            Vector3 right = Vector3.Cross(m_dirForward, m_dirUp);
            float cos = (float)Math.Cos(angle);
            float sin = (float)Math.Sin(angle);
            m_dirUp = m_dirUp * cos + right * sin;
        }

        private void RightMinus(float angle)
        {
            RightPlus(-angle);
        }
        private void ApplyOrientationAngle()
        {
            m_dirForward = Vector3.Normalize(m_dirForward);
            m_dirUp = Vector3.Normalize(m_dirUp);

            Vector3 right = Vector3.Cross(m_dirForward, m_dirUp);
            float cos = (float)Math.Cos(m_orientationAngle);
            float sin = (float)Math.Sin(m_orientationAngle);
            m_dirForward = m_dirForward * cos - right * sin;
            m_orientationAngle = 0.0f;
        }
    }
}
