using System.Collections.Concurrent;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI;
using VRageMath;
using NaniteConstructionSystem.Extensions;

namespace NaniteConstructionSystem.Entities.Targets
{
    public abstract class NaniteTargetBlocksBase
    {

        public HashSet<object> TargetList = new HashSet<object>();
        public HashSet<object> PotentialTargetList = new HashSet<object>();
        public HashSet<object> PotentialIgnoredList = new HashSet<object>();
        public Dictionary<object, int> IgnoredCheckedTimes = new Dictionary<object, int>();

        public int PotentialTargetListCount;

        public ConcurrentDictionary<string, int> ComponentsRequired = new ConcurrentDictionary<string, int>();

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

        public abstract void ClearInternalTargetList();
        public abstract int GetMaximumTargets();
        public abstract float GetPowerUsage();
        public abstract float GetMinTravelTime();
        public abstract float GetSpeed();
        public abstract bool IsEnabled(NaniteConstructionBlock factory);
        public abstract void FindTargets(ref Dictionary<string, int> available, List<NaniteConstructionBlock> blockList);
        public abstract void ParallelUpdate(List<IMyCubeGrid> gridList, ConcurrentBag<BlockTarget> gridBlocks);
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
            { 
                m_lastInvalidTargetReason = reason; 
            });
        }

        internal NaniteConstructionBlock GetNearestFactory(string targetName, Vector3D distance)
        {
            return m_constructionBlock.FactoryGroup
                .Where(factory => factory.EnabledParticleTargets[targetName])
                .OrderBy(factory => factory.ConstructionBlock != null
                    ? Vector3D.DistanceSquared(factory.ConstructionBlock.GetPosition(), distance)
                    : double.MaxValue)
                .FirstOrDefault() ?? m_constructionBlock;
        }

        /// <summary>
        /// Checks if an item is in range of a group of master-slave factories, and that the factory in range has the type of target enabled
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        ///
        internal bool IsInRange(IMySlimBlock block, float range)
        {
            return m_constructionBlock.FactoryGroup.Any(factory => IsInRange(factory, block, range));
        }

        internal bool IsInRange(Vector3D position, float range)
        {
            return m_constructionBlock.FactoryGroup.Any(factory => IsInRange(factory, position, range));
        }

        internal bool IsInRange(NaniteConstructionBlock factory, Vector3D position, float range)
        {
            range = System.Math.Min(range, MyAPIGateway.Session.SessionSettings.SyncDistance);
            return factory.ConstructionBlock != null && IsEnabled(factory) &&
                   Vector3D.DistanceSquared(factory.ConstructionBlock.GetPosition(), position) < range * range;
        }

        internal bool IsInRange(NaniteConstructionBlock factory, IMySlimBlock block, float range)
        {
            range = System.Math.Min(range, MyAPIGateway.Session.SessionSettings.SyncDistance);
            return factory.ConstructionBlock != null && IsEnabled(factory) &&
                   Vector3D.DistanceSquared(factory.ConstructionBlock.GetPosition(), EntityHelper.GetBlockPosition(block)) < range * range;
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
                    if (TargetList.Any(t => t == target))
                        return;
                    
                    TargetList.Add(target);
                }
            });
        }

        public virtual void CheckBeacons(){}
    }
}
