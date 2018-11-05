using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.ModAPI;
using VRageMath;
using VRage.Game.Entity;
using Sandbox.Game.Entities;
using VRage.Game;
using Sandbox.Game.Components;

using NaniteConstructionSystem.Particles;
using NaniteConstructionSystem.Extensions;

namespace NaniteConstructionSystem.Entities.Targets
{
    public class NaniteMedicalTarget
    {
        public int ParticleCount { get; set; }
        public double StartTime { get; set; }
        public double HealTime { get; set; }
        public double LastUpdate { get; set; }
    }

    public class NaniteMedicalTargets : NaniteTargetBlocksBase
    {
        public override string TargetName
        {
            get
            {
                return "Medical";
            }
        }

        private float m_maxDistance;
        private Dictionary<IMyPlayer, NaniteMedicalTarget> m_targetTracker;
        private MySoundPair m_progressSound;
        private MyEntity3DSoundEmitter m_progressSoundEmitter;

        public NaniteMedicalTargets(NaniteConstructionBlock constructionBlock) : base(constructionBlock)
        {
            m_maxDistance = NaniteConstructionManager.Settings.MedicalMaxDistance;
            m_targetTracker = new Dictionary<IMyPlayer, NaniteMedicalTarget>();

            m_progressSoundEmitter = new MyEntity3DSoundEmitter((MyEntity)constructionBlock.ConstructionBlock);
            m_progressSound = new MySoundPair("BlockMedicalProgress");
        }

        public override int GetMaximumTargets()
        {
            MyCubeBlock block = (MyCubeBlock)m_constructionBlock.ConstructionBlock;
            return (int)Math.Min(NaniteConstructionManager.Settings.MedicalNanitesNoUpgrade + (block.UpgradeValues["MedicalNanites"] * NaniteConstructionManager.Settings.MedicalNanitesPerUpgrade), NaniteConstructionManager.Settings.MedicalMaxStreams);
        }

        public override float GetMinTravelTime()
        {
            MyCubeBlock block = (MyCubeBlock)m_constructionBlock.ConstructionBlock;
            return Math.Max(1f, NaniteConstructionManager.Settings.MedicalMinTravelTime - (block.UpgradeValues["SpeedNanites"] * NaniteConstructionManager.Settings.MinTravelTimeReductionPerUpgrade));
        }

        public override float GetPowerUsage()
        {
            MyCubeBlock block = (MyCubeBlock)m_constructionBlock.ConstructionBlock;
            return Math.Max(1, NaniteConstructionManager.Settings.MedicalPowerPerStream - (int)(block.UpgradeValues["PowerNanites"] * NaniteConstructionManager.Settings.PowerDecreasePerUpgrade));
        }

        public override float GetSpeed()
        {
            MyCubeBlock block = (MyCubeBlock)m_constructionBlock.ConstructionBlock;
            return NaniteConstructionManager.Settings.MedicalDistanceDivisor + (block.UpgradeValues["SpeedNanites"] * (float)NaniteConstructionManager.Settings.SpeedIncreasePerUpgrade);
        }

        public override bool IsEnabled()
        {
            bool result = true;
            if (!((IMyFunctionalBlock)m_constructionBlock.ConstructionBlock).Enabled ||
                !((IMyFunctionalBlock)m_constructionBlock.ConstructionBlock).IsFunctional ||
                m_constructionBlock.ConstructionBlock.CustomName.ToLower().Contains("NoMedical".ToLower()))
                result = false;

            if (NaniteConstructionManager.TerminalSettings.ContainsKey(m_constructionBlock.ConstructionBlock.EntityId))
            {
                if (!NaniteConstructionManager.TerminalSettings[m_constructionBlock.ConstructionBlock.EntityId].AllowMedical)
                    return false;
            }

            return result;
        }

        public override void ParallelUpdate(List<IMyCubeGrid> gridList, List<IMySlimBlock> gridBlocks)
        {
            using (Lock.AcquireExclusiveUsing())
                PotentialTargetList.Clear();

            List<IMyPlayer> players = new List<IMyPlayer>();            
            try
            {
                MyAPIGateway.Players.GetPlayers(players);
            }
            catch
            {
                Logging.Instance.WriteLine(string.Format("Error getting players, skipping"));
                return;
            }

            if (!IsEnabled())
                return;

            foreach (var item in players)
            {
                var functional = m_constructionBlock.ConstructionBlock as IMyFunctionalBlock;
                MyRelationsBetweenPlayerAndBlock relations = functional.GetUserRelationToOwner(item.IdentityId);
                if (relations != MyRelationsBetweenPlayerAndBlock.Owner && relations != MyRelationsBetweenPlayerAndBlock.FactionShare)
                    continue;

                if (item.Controller == null || item.Controller.ControlledEntity == null || item.Controller.ControlledEntity.Entity == null)
                    continue;

                bool damaged = false;
                foreach(var component in item.Controller.ControlledEntity.Entity.Components)
                {
                    var stat = component as MyCharacterStatComponent;
                    if(stat != null)
                    {
                        if (stat.Health.Value < stat.Health.MaxValue)
                            damaged = true;

                        break;
                    }
                }

                if (!damaged)
                    continue;

                if (Vector3D.DistanceSquared(m_constructionBlock.ConstructionBlock.GetPosition(), item.GetPosition()) < m_maxDistance * m_maxDistance)
                {
                    using (m_lock.AcquireExclusiveUsing())
                        PotentialTargetList.Add(item);
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

            lock (m_potentialTargetList)
            {
                foreach(IMyPlayer item in m_potentialTargetList.ToList())
                {                 
                    if (item == null || TargetList.Contains(item) || item.Controller == null || item.Controller.ControlledEntity == null || item.Controller.ControlledEntity.Entity == null)
                        continue;

                    bool found = false;
                    foreach (var block in blockList.ToList())
                    {
                        if (block != null && block.Targets.First(x => x is NaniteMedicalTargets).TargetList.Contains(item))
                        {
                            found = true;
                            InvalidTargetReason("Another factory has this block as a target");
                            break;
                        }
                    }

                    if (found)
                        continue;

                    if (Vector3D.DistanceSquared(m_constructionBlock.ConstructionBlock.GetPosition(), item.GetPosition()) < m_maxDistance * m_maxDistance 
                      && m_constructionBlock.HasRequiredPowerForNewTarget(this))
                    {
                        AddTarget(item);

                        Logging.Instance.WriteLine(string.Format("ADDING Medical Target: conid={0} type={1} playerName={2} position={3}", 
                          m_constructionBlock.ConstructionBlock.EntityId, item.GetType().Name, item.DisplayName, item.GetPosition()));

                        if (++TargetListCount >= GetMaximumTargets()) 
                            break;
                    }
                }
            }
        }

        public override void Update()
        {
            foreach(var item in TargetList.ToList())
            {
                ProcessItem(item);
            }            
        }

        private void ProcessItem(object target)
        {
            var player = target as IMyPlayer;
            if (player == null)
                return;

            if (Sync.IsServer)
            {
                if (!IsEnabled())
                {
                    Logging.Instance.WriteLine("CANCELLING Medical Target due to being disabled");
                    CancelTarget(target);
                    return;
                }

                if (m_constructionBlock.FactoryState != NaniteConstructionBlock.FactoryStates.Active)
                    return;

                if(player.Controller == null || player.Controller.ControlledEntity == null || player.Controller.ControlledEntity.Entity == null)
                {
                    Logging.Instance.WriteLine("CANCELLING Medical Target due to entity not existing");
                    CancelTarget(target);
                    return;
                }

                if (Vector3D.DistanceSquared(m_constructionBlock.ConstructionBlock.GetPosition(), player.GetPosition()) > m_maxDistance * m_maxDistance)
                {
                    Logging.Instance.WriteLine("CANCELLING Medical Target due to distance");
                    CancelTarget(target);
                    return;
                }

                if(!IsTargetDamaged((IMyPlayer)target))
                {
                    CompleteTarget(target);
                    return;
                }

                if (m_targetTracker.ContainsKey(player))
                {
                    var trackedItem = m_targetTracker[player];
                    if (MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - trackedItem.StartTime >= trackedItem.HealTime &&
                        MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - trackedItem.LastUpdate > 1000)
                    {
                        trackedItem.LastUpdate = MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
                        trackedItem.HealTime += NaniteConstructionManager.Settings.MedicalSecondsPerHealTick * 1000;
                        
                        if(HealTarget(player))
                        {
                            CompleteTarget(player);
                            return;
                        }
                    }
                }
            }

            CreateMiningParticles(player);
        }

        private bool IsTargetDamaged(IMyPlayer player)
        {
            foreach (var item in player.Controller.ControlledEntity.Entity.Components)
            {
                var stat = item as MyCharacterStatComponent;
                if (stat != null)
                {
                    if(stat.Health.Value < stat.Health.MaxValue)
                        return true;

                    break;
                }
            }

            return false;
        }

        private bool HealTarget(IMyPlayer player)
        {
            foreach(var item in player.Controller.ControlledEntity.Entity.Components)
            {
                var stat = item as MyCharacterStatComponent;
                if(stat != null)
                {
                    if (stat.Health.Value <= stat.Health.MinValue)
                        return true;

                    if(stat.Health.Value < stat.Health.MaxValue)
                    {
                        stat.Health.Value += NaniteConstructionManager.Settings.MedicalHealthPerHealTick;
                        m_progressSoundEmitter.Entity = (MyEntity)player.Controller.ControlledEntity.Entity;
                        m_progressSoundEmitter.PlaySound(m_progressSound, true, true);
                    }
                      
                    if(stat.Health.Value >= stat.Health.MaxValue)
                        return true;

                    break;
                }
            }

            return false;
        }

        private void CreateMiningParticles(IMyPlayer target)
        {
            if (!m_targetTracker.ContainsKey(target))
                CreateTrackerItem(target);

            if (NaniteParticleManager.TotalParticleCount > NaniteParticleManager.MaxTotalParticles)
                return;

            m_targetTracker[target].ParticleCount++;
            int size = (int)Math.Max(60f, NaniteParticleManager.TotalParticleCount);
            if ((float)m_targetTracker[target].ParticleCount / size < 1f)
                return;

            m_targetTracker[target].ParticleCount = 0;

            // Create Particle
            Vector4 startColor = new Vector4(1f, 1f, 1f, 1f);
            Vector4 endColor = new Vector4(0.4f, 0.4f, 0.4f, 0.35f);
            m_constructionBlock.ParticleManager.AddParticle(startColor, endColor, GetMinTravelTime() * 1000f, GetSpeed(), target);
        }

        private void CreateTrackerItem(IMyPlayer target)
        {
            double distance = Vector3D.Distance(m_constructionBlock.ConstructionBlock.GetPosition(), target.GetPosition());
            int time = (int)Math.Max(GetMinTravelTime() * 1000f, (distance / GetSpeed()) * 1000f);

            NaniteMedicalTarget medicalTarget = new NaniteMedicalTarget();
            medicalTarget.ParticleCount = 0;
            medicalTarget.StartTime = MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
            medicalTarget.LastUpdate = MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
            medicalTarget.HealTime = time * 0.66f; // - 1000;
            m_targetTracker.Add(target, medicalTarget);

            m_constructionBlock.SendAddTarget(target);
        }

        public override void CancelTarget(object obj)
        {
            if (m_progressSoundEmitter.IsPlaying)
                m_progressSoundEmitter.StopSound(true);

            var player = obj as IMyPlayer;
            if (player == null)
                return;

            if (Sync.IsServer)
                m_constructionBlock.SendCancelTarget(player);

            m_constructionBlock.ParticleManager.CancelTarget(obj);

            foreach (IMyPlayer item in TargetList.Where(x => (IMyPlayer)x == player))
                Logging.Instance.WriteLine(string.Format("CANCELLING Medical Target: {0} - {1} (Player={2},Position={3})", m_constructionBlock.ConstructionBlock.EntityId, item.GetType().Name, item.DisplayName, item.GetPosition()));

            TargetList.RemoveAll(x => ((IMyPlayer)x).IdentityId == player.IdentityId);
            PotentialTargetList.RemoveAll(x => ((IMyPlayer)x).IdentityId == player.IdentityId);

            m_targetTracker.Remove(player);
        }

        public override void CompleteTarget(object obj)
        {
            if (m_progressSoundEmitter.IsPlaying)
                m_progressSoundEmitter.StopSound(true);

            var player = obj as IMyPlayer;
            if (player == null)
                return;

            if (Sync.IsServer)
                m_constructionBlock.SendCompleteTarget(player);

            m_constructionBlock.ParticleManager.CompleteTarget(obj);

            foreach (IMyPlayer item in TargetList.Where(x => (IMyPlayer)x == player))
                Logging.Instance.WriteLine(string.Format("COMPLETING Medical Target: {0} - {1} (Player={2},Position={3})", m_constructionBlock.ConstructionBlock.EntityId, item.GetType().Name, item.DisplayName, item.GetPosition()));

            TargetList.RemoveAll(x => ((IMyPlayer)x).IdentityId == player.IdentityId);
            PotentialTargetList.RemoveAll(x => ((IMyPlayer)x).IdentityId == player.IdentityId);

            m_targetTracker.Remove(player);
        }
    }
}
