using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Generic;
using System.Linq;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;
using NaniteConstructionSystem.Entities.Beacons;

namespace NaniteConstructionSystem.Entities.Targets
{
    public abstract class NaniteTargetBlocksBase
    {
        protected FastResourceLock m_lock; 
        public FastResourceLock Lock {
            get { return m_lock; }
        }

        protected List<object> m_targetList;
        public List<object> TargetList
        {
            get { return m_targetList; }
        }

        protected List<object> m_potentialTargetList;
        public List<object> PotentialTargetList
        {
            get { return m_potentialTargetList; }
        }

        public int PotentialTargetListCount;

        protected Dictionary<string, int> m_componentsRequired;
        public Dictionary<string, int> ComponentsRequired
        {
            get { return m_componentsRequired; }
        }

        protected string m_lastInvalidTargetReason;
        public string LastInvalidTargetReason
        {
            get { return m_lastInvalidTargetReason; }
        }

        public abstract string TargetName { get; }

        protected NaniteConstructionBlock m_constructionBlock;
        protected MyCubeBlock m_factoryCubeBlock;

        public NaniteTargetBlocksBase(NaniteConstructionBlock constructionBlock)
        {
            m_lock = new FastResourceLock();
            m_targetList = new List<object>();
            m_potentialTargetList = new List<object>();
            m_componentsRequired = new Dictionary<string, int>();
            m_constructionBlock = constructionBlock;
            m_factoryCubeBlock = ((MyCubeBlock)m_constructionBlock.ConstructionBlock);
        }

        public abstract int GetMaximumTargets();
        public abstract float GetPowerUsage();
        public abstract float GetMinTravelTime();
        public abstract float GetSpeed();
        public abstract bool IsEnabled(NaniteConstructionBlock factory);
        public abstract void FindTargets(ref Dictionary<string, int> available, List<NaniteConstructionBlock> blockList);
        public abstract void ParallelUpdate(List<IMyCubeGrid> gridList, List<BlockTarget> gridBlocks);
        public abstract void Update();
        public abstract void CancelTarget(object obj);
        public abstract void CompleteTarget(object obj);

        private float m_maxDistance = 300f;

        public virtual void Remove(object target)
        {
            TargetList.Remove(target);
            PotentialTargetList.Remove(target);
        }

        internal bool IsAreaBeaconValid(IMyCubeBlock cubeBlock)
        {
            if (cubeBlock == null || !((IMyFunctionalBlock)cubeBlock).Enabled || !((IMyFunctionalBlock)cubeBlock).IsFunctional
              || !MyRelationsBetweenPlayerAndBlockExtensions.IsFriendly(cubeBlock.GetUserRelationToOwner(m_constructionBlock.ConstructionBlock.OwnerId)))
                return false;

            float range = NaniteConstructionManager.Settings != null ? NaniteConstructionManager.Settings.AreaBeaconMaxDistanceFromNaniteFacility : 300f;

            foreach (var factory in m_constructionBlock.FactoryGroup)
                if (IsEnabled(factory))
                {
                    if (Vector3D.Distance(cubeBlock.GetPosition(), factory.ConstructionBlock.GetPosition()) < range)
                        return true;

                    foreach (var grid in factory.GridGroup.ToList())
                        if (cubeBlock.CubeGrid == grid)
                            return true;
                }

            return false;
        }

        internal void CheckConstructionOrProjectionAreaBeacons(bool isProjection = false)
        {
            foreach (var beaconBlock in NaniteConstructionManager.BeaconList.Where(x => x.Value is NaniteAreaBeacon).ToList())
            {
                IMyCubeBlock cubeBlock = beaconBlock.Value.BeaconBlock;

                if (!IsAreaBeaconValid(cubeBlock))
                    continue;

                var item = beaconBlock.Value as NaniteAreaBeacon;
                if ( (isProjection && !item.Settings.AllowProjection) || !item.Settings.AllowRepair)
                    continue;

                float range = NaniteConstructionManager.Settings != null ? NaniteConstructionManager.Settings.ConstructionMaxBeaconDistance : 300f;

                if (isProjection)
                    range = NaniteConstructionManager.Settings != null ? NaniteConstructionManager.Settings.ProjectionMaxBeaconDistance : 300f;

                HashSet<IMyEntity> entities = new HashSet<IMyEntity>();
                MyAPIGateway.Entities.GetEntities(entities);
                foreach (var entity in entities)
                {
                    var grid = entity as IMyCubeGrid;

                    if (grid == null || (grid.GetPosition() - cubeBlock.GetPosition()).LengthSquared() >= range * range)
                        continue;
                        
                    foreach (IMySlimBlock block in ((MyCubeGrid)grid).GetBlocks())
                    {
                        BoundingBoxD blockbb;
                        block.GetWorldBoundingBox(out blockbb, true);
                        if (item.IsInsideBox(blockbb))
                            m_constructionBlock.ScanBlocksCache.Add(new BlockTarget(block, true, item));
                    }
                }
            }
        }

        internal void InvalidTargetReason(string reason)
        {
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                { m_lastInvalidTargetReason = reason; });
        }

        internal NaniteConstructionBlock GetNearestFactory(string targetName, Vector3D distance)
        {
            foreach ( var factory in m_constructionBlock.FactoryGroup.OrderBy(x => x.ConstructionBlock != null ? Vector3D.Distance(x.ConstructionBlock.GetPosition(), distance) : double.MaxValue ) )
                if (factory.EnabledParticleTargets[targetName])
                    return factory;

            return m_constructionBlock;
        }

        /// <summary>
        /// Checks if an item is in range of a group of master-slave factories, and that the factory in range has the type of target enabled
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        internal bool IsInRange(Vector3D itemPosition, float range = 300f)
        {
            foreach (var factory in m_constructionBlock.FactoryGroup)
                if (factory.ConstructionBlock != null && IsEnabled(factory)
                    && Vector3D.DistanceSquared(factory.ConstructionBlock.GetPosition(), itemPosition) < range * range)
                    return true;

            return false;
        }

        internal bool IsInRange(NaniteConstructionBlock factory, Vector3D itemPosition, float range = 300f)
        {
            if (factory.ConstructionBlock != null && IsEnabled(factory)
                && Vector3D.DistanceSquared(factory.ConstructionBlock.GetPosition(), itemPosition) < range * range)
                return true;

            return false;
        }
        

        internal void AddTarget(object target)
        {
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                if (m_constructionBlock.IsUserDefinedLimitReached())
                    InvalidTargetReason("User defined maximum nanite limit reached");
                else if (target != null)
                    m_targetList.Add(target);
            });
        }

        public virtual void CheckBeacons(){}
        public virtual void CheckAreaBeacons(){}
    }
}
