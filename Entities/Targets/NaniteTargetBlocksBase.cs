using System.Collections.Generic;
using VRage;
using VRage.Game.ModAPI;

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

        public NaniteTargetBlocksBase(NaniteConstructionBlock constructionBlock)
        {
            m_lock = new FastResourceLock();
            m_targetList = new List<object>();
            m_potentialTargetList = new List<object>();
            m_componentsRequired = new Dictionary<string, int>();
            m_constructionBlock = constructionBlock;
        }

        public abstract int GetMaximumTargets();
        public abstract float GetPowerUsage();
        public abstract float GetMinTravelTime();
        public abstract float GetSpeed();
        public abstract bool IsEnabled();
        public abstract void FindTargets(ref Dictionary<string, int> available);
        public abstract void ParallelUpdate(List<IMyCubeGrid> gridList, List<IMySlimBlock> gridBlocks);
        public abstract void Update();
        public abstract void CancelTarget(object obj);
        public abstract void CompleteTarget(object obj);

        public virtual void Remove(object target)
        {
            TargetList.Remove(target);

            using(Lock.AcquireExclusiveUsing())
                PotentialTargetList.Remove(target);
        }
    }
}
