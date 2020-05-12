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
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Entities.Character.Components;
using Sandbox.Game;
using VRage.Game.Components;
using VRage.Utils;

namespace NaniteConstructionSystem.Entities.Targets
{
    public class NaniteLifeSupportTarget
    {
        public int ParticleCount { get; set; }
        public double StartTime { get; set; }
        public double LifeSupportTime { get; set; }
        public double LastUpdate { get; set; }
    }

    public class NaniteLifeSupportTargets : NaniteTargetBlocksBase
    {
        public override string TargetName
        {
            get
            {
                return "Life Support";
            }
        }

        private float m_maxDistance;

        private float m_o2RefillLevel;
        private float m_o2RefillPerTick;

        private float m_h2RefillLevel;
        private float m_h2RefillPerTick;

        private float m_energyRefillLevel;
        private float m_energyRefillPerTick;

        private float m_healthRefillPerTick;

        private Dictionary<IMyPlayer, NaniteLifeSupportTarget> m_targetTracker;
        private MySoundPair m_progressSound;
        private MyEntity3DSoundEmitter m_progressSoundEmitter;

        public List<IMyGasTank> connectedGasTanks;

        public NaniteLifeSupportTargets(NaniteConstructionBlock constructionBlock) : base(constructionBlock)
        {
            m_maxDistance = NaniteConstructionManager.Settings.LifeSupportMaxDistance;
            m_targetTracker = new Dictionary<IMyPlayer, NaniteLifeSupportTarget>();

            m_o2RefillLevel = NaniteConstructionManager.Settings.LifeSupportOxygenRefillLevel;
            m_o2RefillPerTick = NaniteConstructionManager.Settings.LifeSupportOxygenPerTick;

            m_h2RefillLevel = NaniteConstructionManager.Settings.LifeSupportHydrogenRefillLevel;
            m_h2RefillPerTick = NaniteConstructionManager.Settings.LifeSupportHydrogenPerTick;

            m_energyRefillLevel = NaniteConstructionManager.Settings.LifeSupportEnergyRefillLevel;
            m_energyRefillPerTick = NaniteConstructionManager.Settings.LifeSupportEnergyPerTick;

            m_healthRefillPerTick = NaniteConstructionManager.Settings.LifeSupportHealthPerTick;

            m_progressSoundEmitter = new MyEntity3DSoundEmitter((MyEntity)constructionBlock.ConstructionBlock);
            m_progressSound = new MySoundPair("BlockMedicalProgress");

            connectedGasTanks = new List<IMyGasTank>();
        }

        public override int GetMaximumTargets()
        {
            return (int)Math.Min((NaniteConstructionManager.Settings.LifeSupportNanitesNoUpgrade * m_constructionBlock.FactoryGroup.Count)
              + m_constructionBlock.UpgradeValue("LifeSupportNanites"), NaniteConstructionManager.Settings.LifeSupportMaxStreams);
        }

        public override float GetMinTravelTime()
        {
            return Math.Max(1f, NaniteConstructionManager.Settings.LifeSupportMinTravelTime
              - m_constructionBlock.UpgradeValue("MinTravelTime"));
        }

        public override float GetPowerUsage()
        {
            return Math.Max(1, NaniteConstructionManager.Settings.LifeSupportPowerPerStream
              - (int)m_constructionBlock.UpgradeValue("PowerNanites"));
        }

        public override float GetSpeed()
        {
            return NaniteConstructionManager.Settings.LifeSupportDistanceDivisor
              + m_constructionBlock.UpgradeValue("SpeedNanites");
        }

        public override bool IsEnabled(NaniteConstructionBlock factory)
        {
            if (!((IMyFunctionalBlock)factory.ConstructionBlock).Enabled
              || !((IMyFunctionalBlock)factory.ConstructionBlock).IsFunctional 
              || (NaniteConstructionManager.TerminalSettings.ContainsKey(factory.ConstructionBlock.EntityId) 
              && !NaniteConstructionManager.TerminalSettings[factory.ConstructionBlock.EntityId].AllowLifeSupport))
            {
                factory.EnabledParticleTargets[TargetName] = false;
                return false;
            }
                
            factory.EnabledParticleTargets[TargetName] = true;
            return true;
        }

        public override void ParallelUpdate(List<IMyCubeGrid> gridList, List<BlockTarget> gridBlocks)
        {
            List<IMyPlayer> players = new List<IMyPlayer>();            
            try
            {
                MyAPIGateway.Players.GetPlayers(players);
            }
            catch
            {
                Logging.Instance.WriteLine("NaniteLifeSupportTargets.ParallelUpdate: Error getting players, skipping");
                return;
            }

            foreach (var item in players)
            {
                var functional = m_constructionBlock.ConstructionBlock as IMyFunctionalBlock;
                MyRelationsBetweenPlayerAndBlock relations = functional.GetUserRelationToOwner(item.IdentityId);
                if (relations != MyRelationsBetweenPlayerAndBlock.Owner && relations != MyRelationsBetweenPlayerAndBlock.FactionShare)
                    continue;

                if (!DoesTargetNeedLifeSupport(item))
                    continue;

                if (PotentialTargetList.Contains(item) || TargetList.Contains(item))
                    continue;

                if (IsInRange(item.GetPosition(), m_maxDistance))
                    PotentialTargetList.Add(item);
            }

            List<IMyGasTank> removalList = new List<IMyGasTank>();

            foreach (IMyGasTank tank in connectedGasTanks)
                if (!GridHelper.IsValidGasConnection(m_constructionBlock.ConstructionCubeBlock, tank))
                    removalList.Add(tank);

            foreach (IMyGasTank tank in removalList)
                connectedGasTanks.Remove(tank);
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

            lock (m_potentialTargetList)
            {
                foreach(IMyPlayer item in m_potentialTargetList.ToList())
                {                 
                    if (item == null || TargetList.Contains(item) || item.Controller == null || item.Controller.ControlledEntity == null || item.Controller.ControlledEntity.Entity == null)
                        continue;

                    bool found = false;
                    foreach (var block in blockList.ToList())
                    {
                        if (block != null && block.Targets.First(x => x is NaniteLifeSupportTargets).TargetList.Contains(item))
                        {
                            found = true;
                            InvalidTargetReason("Another factory has this block as a target");
                            break;
                        }
                    }

                    if (found)
                        continue;

                    if (m_constructionBlock.HasRequiredPowerForNewTarget(this))
                    {
                        AddTarget(item);

                        Logging.Instance.WriteLine(string.Format("[Life Support] Adding LifeSupport Target: conid={0} type={1} playerName={2} position={3}", 
                          m_constructionBlock.ConstructionBlock.EntityId, item.GetType().Name, item.DisplayName, item.GetPosition()), 1);

                        if (++TargetListCount >= maxTargets) 
                            break;
                    }
                    else
                    {
                        InvalidTargetReason("Not enough power for new target!");
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
                /*
                if (!IsEnabled())
                {
                    Logging.Instance.WriteLine("CANCELLING Medical Target due to being disabled");
                    CancelTarget(target);
                    return;
                }
                */

                if (!((m_constructionBlock.FactoryState == NaniteConstructionBlock.FactoryStates.Active || m_constructionBlock.FactoryState == NaniteConstructionBlock.FactoryStates.MissingParts) && (TargetList.Count > 0 || PotentialTargetList.Count > 0)))
                    return;

                if (player.Controller == null || player.Controller.ControlledEntity == null || player.Controller.ControlledEntity.Entity == null)
                {
                    Logging.Instance.WriteLine("[Life Support] Cancelling Life Support target due to entity not existing", 1);
                    CancelTarget(target);
                    return;
                }

                if (!IsInRange(player.GetPosition(), m_maxDistance))
                {
                    Logging.Instance.WriteLine("[Life Support] Cancelling Life Support target due to being out of range", 1);
                    CancelTarget(target);
                    return;
                }

                if (!DoesTargetNeedLifeSupport((IMyPlayer)target) && !m_targetTracker.ContainsKey(player))
                {
                    CompleteTarget(target);
                    return;
                }

                if (m_targetTracker.ContainsKey(player))
                {
                    var trackedItem = m_targetTracker[player];
                    if (MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - trackedItem.StartTime >= trackedItem.LifeSupportTime &&
                        MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - trackedItem.LastUpdate > 1000)
                    {
                        trackedItem.LastUpdate = MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
                        trackedItem.LifeSupportTime += NaniteConstructionManager.Settings.LifeSupportSecondsPerTick * 1000;

                        bool healingDone = HealTarget(player);
                        bool refillDone = RefillTarget(player);
                        
                        if (healingDone && refillDone)
                        {
                            CompleteTarget(player);
                            return;
                        }
                        else
                        {
                            m_progressSoundEmitter.Entity = (MyEntity)player.Controller.ControlledEntity.Entity;
                            m_progressSoundEmitter.PlaySound(m_progressSound, true, true);
                        }
                    }
                }
            }

            CreateLifeSupportParticles(player);
        }

        private bool DoesTargetNeedLifeSupport(IMyPlayer player)
        {
            float health = MyVisualScriptLogicProvider.GetPlayersHealth(player.IdentityId);
            float oxygen = MyVisualScriptLogicProvider.GetPlayersOxygenLevel(player.IdentityId);
            float hydrogen = MyVisualScriptLogicProvider.GetPlayersHydrogenLevel(player.IdentityId);
            float energy = MyVisualScriptLogicProvider.GetPlayersEnergyLevel(player.IdentityId);

            if (health < 100f
                || oxygen < m_o2RefillLevel
                || hydrogen < m_h2RefillLevel
                || energy < m_energyRefillLevel)
                return true;

            return false;
        }

        private bool HealTarget(IMyPlayer player)
        {
            float health = MyVisualScriptLogicProvider.GetPlayersHealth(player.IdentityId);

            if (health <= 0)
                return true;

            if (health + m_healthRefillPerTick <= 100f)
                MyVisualScriptLogicProvider.SetPlayersHealth(player.IdentityId, health + m_healthRefillPerTick);
            else
            {
                MyVisualScriptLogicProvider.SetPlayersHealth(player.IdentityId, 100f);
                return true;
            }

            return false;
        }

        private bool RefillTarget(IMyPlayer player)
        {
            float oxygen = MyVisualScriptLogicProvider.GetPlayersOxygenLevel(player.IdentityId);
            float hydrogen = MyVisualScriptLogicProvider.GetPlayersHydrogenLevel(player.IdentityId);
            float energy = MyVisualScriptLogicProvider.GetPlayersEnergyLevel(player.IdentityId);

            bool oxygenRefilled = false;
            bool hydrogenRefilled = false;
            bool energyRefilled = false;

            bool hasOxygen;
            bool hasHydrogen;

            CheckTanks(out hasOxygen, out hasHydrogen);

            Logging.Instance.WriteLine($"[Life Support] Tank status: Oxygen - {hasOxygen}, Hydrogen - {hasHydrogen}", 2);

            if (hasOxygen && m_o2RefillLevel > 0f)
            {
                if (oxygen + m_o2RefillPerTick <= 1f)
                    MyVisualScriptLogicProvider.SetPlayersOxygenLevel(player.IdentityId, oxygen + m_o2RefillPerTick);
                else
                {
                    MyVisualScriptLogicProvider.SetPlayersOxygenLevel(player.IdentityId, 1f);
                    oxygenRefilled = true;
                }
            }

            if (hasHydrogen && m_h2RefillLevel > 0f)
            {
                if (hydrogen + m_h2RefillPerTick <= 1f)
                    MyVisualScriptLogicProvider.SetPlayersHydrogenLevel(player.IdentityId, hydrogen + m_h2RefillPerTick);
                else
                {
                    MyVisualScriptLogicProvider.SetPlayersHydrogenLevel(player.IdentityId, 1f);
                    hydrogenRefilled = true;
                }
            }

            if (m_energyRefillLevel > 0f)
            {
                if (energy + m_energyRefillPerTick <= 1f)
                    MyVisualScriptLogicProvider.SetPlayersEnergyLevel(player.IdentityId, energy + m_energyRefillPerTick);
                else
                {
                    MyVisualScriptLogicProvider.SetPlayersEnergyLevel(player.IdentityId, 1f);
                    energyRefilled = true;
                }
            }

            if (oxygenRefilled && hydrogenRefilled && energyRefilled)
                return true;

            return false;
        }

        private void CheckTanks(out bool hasOxygen, out bool hasHydrogen)
        {
            hasOxygen = false;
            hasHydrogen = false;

            foreach (IMyGasTank tank in connectedGasTanks)
            {
                Logging.Instance.WriteLine($"[Life Support] Checking gas tank: {tank.DisplayNameText}", 2);

                if (!hasOxygen && tank.DisplayNameText.ToLower().Contains("oxygen") && tank.FilledRatio > 0f)
                    hasOxygen = true;
                else if (!hasHydrogen && tank.DisplayNameText.ToLower().Contains("hydrogen") && tank.FilledRatio > 0f)
                    hasHydrogen = true;
                else
                    return;
            }
        }

        private void CreateLifeSupportParticles(IMyPlayer target)
        {
            if (!m_targetTracker.ContainsKey(target))
                CreateTrackerItem(target);

            MyAPIGateway.Parallel.Start(() =>
            {
                try
                {
                    Vector4 startColor = new Vector4(1f, 1f, 1f, 1f);
                    Vector4 endColor = new Vector4(0.4f, 0.4f, 0.4f, 0.35f);
                    var nearestFactory = GetNearestFactory(TargetName, target.GetPosition());

                    if (nearestFactory.ParticleManager.Particles.Count < NaniteParticleManager.MaxTotalParticles)
                        MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                        {
                            if (nearestFactory != null && target != null)
                                nearestFactory.ParticleManager.AddParticle(startColor, endColor, GetMinTravelTime() * 1000f, GetSpeed(), target);
                        });
                }
                catch (Exception e)
                    {Logging.Instance.WriteLine($"{e}");}
            }); 
        }

        private void CreateTrackerItem(IMyPlayer target)
        {
            double distance = Vector3D.Distance(m_constructionBlock.ConstructionBlock.GetPosition(), target.GetPosition());
            int time = (int)Math.Max(GetMinTravelTime() * 1000f, (distance / GetSpeed()) * 1000f);

            NaniteLifeSupportTarget lifeSupportTarget = new NaniteLifeSupportTarget();
            lifeSupportTarget.ParticleCount = 0;
            lifeSupportTarget.StartTime = MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
            lifeSupportTarget.LastUpdate = MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
            lifeSupportTarget.LifeSupportTime = time * 0.66f; // - 1000;
            m_targetTracker.Add(target, lifeSupportTarget);

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
                Logging.Instance.WriteLine(string.Format("[Life Support] Cancelling Life Support target: {0} - {1} (Player={2},Position={3})",
                  m_constructionBlock.ConstructionBlock.EntityId, item.GetType().Name, item.DisplayName, item.GetPosition()), 1);

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
                Logging.Instance.WriteLine(string.Format("[Life Support] Completing Life Support target: {0} - {1} (Player={2},Position={3})",
                  m_constructionBlock.ConstructionBlock.EntityId, item.GetType().Name, item.DisplayName, item.GetPosition()), 1);

            TargetList.RemoveAll(x => ((IMyPlayer)x).IdentityId == player.IdentityId);
            PotentialTargetList.RemoveAll(x => ((IMyPlayer)x).IdentityId == player.IdentityId);

            m_targetTracker.Remove(player);
        }
    }
}
