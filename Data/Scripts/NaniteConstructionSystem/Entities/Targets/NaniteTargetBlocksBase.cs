using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Generic;
using System.Linq;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

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
        public abstract void FindTargets(ref Dictionary<string, int> available, List<NaniteConstructionBlock> blockList);
        public abstract void ParallelUpdate(List<IMyCubeGrid> gridList, List<IMySlimBlock> gridBlocks);
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
            if (cubeBlock == null || !((IMyFunctionalBlock)cubeBlock).Enabled || !((IMyFunctionalBlock)cubeBlock).IsFunctional)
                return false;

            if (Vector3D.Distance(cubeBlock.GetPosition(), m_constructionBlock.ConstructionBlock.GetPosition()) > m_maxDistance)
            {
                bool foundInGroup = false;
                foreach (var grid in m_constructionBlock.GridGroup.ToList())
                    if (cubeBlock.CubeGrid == grid)
                    {
                        foundInGroup = true;
                        break;
                    }

                if (!foundInGroup)
                    return false;
            }

            if (!MyRelationsBetweenPlayerAndBlockExtensions.IsFriendly(cubeBlock.GetUserRelationToOwner(m_constructionBlock.ConstructionBlock.OwnerId)))
                return false;

            return true;
        }

        internal void InvalidTargetReason(string reason)
        {
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                m_lastInvalidTargetReason = reason;
            });
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
