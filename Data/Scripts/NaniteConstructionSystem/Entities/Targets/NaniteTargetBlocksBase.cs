using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Generic;
using System.Linq;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;
using NaniteConstructionSystem.Entities.Beacons;
using NaniteConstructionSystem.Extensions;
using VRage.ModAPI;

namespace NaniteConstructionSystem.Entities.Targets
{
    public abstract class NaniteTargetBlocksBase
    {
        protected FastResourceLock m_lock = new FastResourceLock();
        public FastResourceLock Lock {
            get { return m_lock; }
        }

        public List<object> TargetList = new List<object>();
        public List<object> PotentialTargetList = new List<object>();

        public List<object> PotentialIgnoredList = new List<object>();
        public Dictionary<object, int> IgnoredCheckedTimes = new Dictionary<object, int>();

        public int PotentialTargetListCount;

        public Dictionary<string, int> ComponentsRequired = new Dictionary<string, int>();

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
        public abstract void AddToIgnoreList(object obj);
        public abstract void CompleteTarget(object obj);

        private float m_maxDistance = 300f;

        public virtual void Remove(object target)
        {
            TargetList.Remove(target);
            PotentialTargetList.Remove(target);
        }

        internal void InvalidTargetReason(string reason)
        {
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                { m_lastInvalidTargetReason = reason; });
        }

        internal NaniteConstructionBlock GetNearestFactory(string targetName, Vector3D distance)
        {
            foreach ( var factory in m_constructionBlock.FactoryGroup.OrderBy(x => x.ConstructionBlock != null ? Vector3D.DistanceSquared(x.ConstructionBlock.GetPosition(), distance) : double.MaxValue ) )
                if (factory.EnabledParticleTargets[targetName])
                    return factory;

            return m_constructionBlock;
        }

        /// <summary>
        /// Checks if an item is in range of a group of master-slave factories, and that the factory in range has the type of target enabled
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        ///
        internal bool IsInRange(IMySlimBlock block, float range)
        {
            foreach (NaniteConstructionBlock factory in m_constructionBlock.FactoryGroup)
                if (IsInRange(factory, block, range))
                    return true;

            return false;
        }

        internal bool IsInRange(Vector3D position, float range)
        {
            foreach (NaniteConstructionBlock factory in m_constructionBlock.FactoryGroup)
                if (IsInRange(factory, position, range))
                    return true;

            return false;
        }

        internal bool IsInRange(NaniteConstructionBlock factory, Vector3D position, float range)
        {
            range = System.Math.Min(range, MyAPIGateway.Session.SessionSettings.SyncDistance);

            if (factory.ConstructionBlock != null && IsEnabled(factory)
                && Vector3D.DistanceSquared(factory.ConstructionBlock.GetPosition(), position) < range * range)
                return true;

            return false;
        }

        internal bool IsInRange(NaniteConstructionBlock factory, IMySlimBlock block, float range)
        {
            range = System.Math.Min(range, MyAPIGateway.Session.SessionSettings.SyncDistance);

            if (factory.ConstructionBlock != null && IsEnabled(factory)
                && Vector3D.DistanceSquared(factory.ConstructionBlock.GetPosition(), EntityHelper.GetBlockPosition(block)) < range * range)
                return true;

            return false;
        }


        internal void AddTarget(object target)
        {
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                if (m_constructionBlock.IsUserDefinedLimitReached())
                {
                    InvalidTargetReason("User defined maximum nanite limit reached");
                }
                else if (target != null)
                {
                    TargetList.Add(target);
                }
            });
        }

        public virtual void CheckBeacons(){}
    }
}
