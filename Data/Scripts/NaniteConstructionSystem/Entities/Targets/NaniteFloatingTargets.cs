using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.ModAPI;
using VRageMath;
using VRage.ModAPI;
using VRage.Game.Entity;
using Sandbox.Game.Entities;
using Sandbox.Game;
using Sandbox.Definitions;
using Ingame = Sandbox.ModAPI.Ingame;
using VRage.Game;

using NaniteConstructionSystem.Particles;
using NaniteConstructionSystem.Extensions;

namespace NaniteConstructionSystem.Entities.Targets
{
    public class NaniteFloatingTarget
    {
        public int ParticleCount { get; set; }
        public double StartTime { get; set; }
        public double CarryTime { get; set; }
        public double LastUpdate { get; set; }
    }

    public class NaniteFloatingTargets : NaniteTargetBlocksBase
    {
        public override string TargetName
        {
            get { return "Cleanup"; }
        }

        private HashSet<IMyEntity> m_entities = new HashSet<IMyEntity>();
        private Dictionary<IMyEntity, NaniteFloatingTarget> m_targetTracker;

        private float m_carryVolume = 10f;
        private float m_maxDistance = 500f;

        public NaniteFloatingTargets(NaniteConstructionBlock constructionBlock) : base(constructionBlock)
        {
            m_targetTracker = new Dictionary<IMyEntity, NaniteFloatingTarget>();
            m_carryVolume = NaniteConstructionManager.Settings.CleanupCarryVolume;
            m_maxDistance = NaniteConstructionManager.Settings.CleanupMaxDistance;
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

            foreach (IMyEntity item in PotentialTargetList.ToList())
            {
                if (item == null || TargetList.Contains(item) || item.Closed) 
                    continue;

                bool found = false;
                foreach (var block in blockList.ToList())
                {
                    if (block != null && block.Targets.First(x => x is NaniteFloatingTargets).TargetList.Contains(item))
                    {
                        InvalidTargetReason("Another factory has this block as a target");
                        found = true;
                        break;
                    }
                }

                if (found) 
                    continue;

                if (m_constructionBlock.HasRequiredPowerForNewTarget(this))
                {
                    AddTarget(item);
                    
                    Logging.Instance.WriteLine(string.Format("[Floating] Adding Floating Object Target: conid={0} type={1} entityID={2} position={3}", 
                        m_constructionBlock.ConstructionBlock.EntityId, item.GetType().Name, item.EntityId, item.GetPosition()), 1);

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

        public override int GetMaximumTargets()
        {
            return (int)Math.Min((NaniteConstructionManager.Settings.CleanupNanitesNoUpgrade * m_constructionBlock.FactoryGroup.Count)
              + m_constructionBlock.UpgradeValue("CleanupNanites"), NaniteConstructionManager.Settings.CleanupMaxStreams);
        }

        public override float GetPowerUsage()
        {
            return Math.Max(1, NaniteConstructionManager.Settings.CleanupPowerPerStream
              - (int)m_constructionBlock.UpgradeValue("PowerNanites"));
        }

        public override float GetMinTravelTime()
        {
            return Math.Max(1f, NaniteConstructionManager.Settings.CleanupMinTravelTime 
              - m_constructionBlock.UpgradeValue("MinTravelTime"));
        }

        public override float GetSpeed()
        {
            return NaniteConstructionManager.Settings.CleanupDistanceDivisor
              + m_constructionBlock.UpgradeValue("SpeedNanites");
        }

        public override bool IsEnabled(NaniteConstructionBlock factory)
        {
            if (!((IMyFunctionalBlock)factory.ConstructionBlock).Enabled
              || !((IMyFunctionalBlock)factory.ConstructionBlock).IsFunctional 
              || (NaniteConstructionManager.TerminalSettings.ContainsKey(factory.ConstructionBlock.EntityId) 
              && !NaniteConstructionManager.TerminalSettings[factory.ConstructionBlock.EntityId].AllowCleanup))
            {
                factory.EnabledParticleTargets[TargetName] = false;
                return false;
            }
                
            factory.EnabledParticleTargets[TargetName] = true;
            return true;
        }

        public override void Update()
        {
            foreach (var item in TargetList.ToList())
                ProcessItem(item);
        }

        private void ProcessItem(object target)
        {
            IMyEntity floating = target as IMyEntity;

            if (floating == null)
                return;

            if (Sync.IsServer)
            {
                if (!((m_constructionBlock.FactoryState == NaniteConstructionBlock.FactoryStates.Active || m_constructionBlock.FactoryState == NaniteConstructionBlock.FactoryStates.MissingParts) && (TargetList.Count > 0 || PotentialTargetList.Count > 0)))
                    return;

                if (floating.Closed)
                {
                    CompleteTarget(floating);
                    return;
                }

                if (!m_targetTracker.ContainsKey(floating))
                    m_constructionBlock.SendAddTarget(floating);

                if (m_targetTracker.ContainsKey(floating))
                {
                    if (!IsInRange(floating.GetPosition(), m_maxDistance))
                    {
                        Logging.Instance.WriteLine("[Floating] Cancelling Cleanup Target due to being out of range", 1);
                        CancelTarget(floating);
                        return;
                    }

                    var trackedItem = m_targetTracker[floating];
                    if (MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - trackedItem.StartTime >= trackedItem.CarryTime 
                        && MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - trackedItem.LastUpdate > 2000)
                    {
                        trackedItem.LastUpdate = MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
                        if (!TransferFromTarget((IMyEntity)target))
                        {
                            Logging.Instance.WriteLine("[Floating] Cancelling Cleanup Target due to insufficient storage", 1);
                            CancelTarget(floating);
                            return;
                        }
                    }
                }
            }
            
            CreateFloatingParticle(floating);
        }

        private void OpenBag(IMyEntity bagEntity)
        {
            try
            {
                MyInventoryBagEntity bag = bagEntity as MyInventoryBagEntity;
                if (bag == null)
                    return;

                foreach (var item in bag.GetInventory().GetItems().ToList())
                    MyFloatingObjects.Spawn(new MyPhysicalInventoryItem(item.Amount, item.Content), bagEntity.WorldMatrix.Translation, bagEntity.WorldMatrix.Forward, bagEntity.WorldMatrix.Up);

                bagEntity.Close();
            }
            catch (Exception ex)
                {Logging.Instance.WriteLine(string.Format("OpenBag Error(): {0}", ex.ToString()));}
        }

        private void OpenCharacter(IMyEntity charEntity)
        {
            try
            {
                MyEntity entity = (MyEntity)charEntity;
                charEntity.Close();
            }
            catch(Exception ex)
                {Logging.Instance.WriteLine(string.Format("OpenCharacter() Error: {0}", ex.ToString()));}
        }

        private bool TransferFromTarget(IMyEntity target, bool transfer=true)
        {
            if (target is IMyCharacter)
            {
                if (transfer)
                    MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                        {OpenCharacter(target);});

                return true;
            }

            if (target is MyInventoryBagEntity)
            {
                if (transfer)
                    MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                        {OpenBag(target);});

                return true;
            }

            MyInventory targetInventory = ((MyCubeBlock)m_constructionBlock.ConstructionBlock).GetInventory();
            var def = MyDefinitionManager.Static.GetPhysicalItemDefinition
              (new VRage.Game.MyDefinitionId(((MyFloatingObject)target).Item.Content.TypeId, ((MyFloatingObject)target).Item.Content.SubtypeId));
            float amount = GetNaniteInventoryAmountThatFits(target, (MyCubeBlock)m_constructionBlock.ConstructionBlock);
            if ((int)amount > 0 || MyAPIGateway.Session.CreativeMode)
            {
                if (MyAPIGateway.Session.CreativeMode)
                    m_carryVolume = 10000f;

                if ((int)amount > 1 && (amount / def.Volume) > m_carryVolume)
                    amount = m_carryVolume / def.Volume;

                if ((int)amount < 1)
                    amount = 1f;

                if (transfer)
                    targetInventory.PickupItem(((MyFloatingObject)target), (int)amount);

                return true;
            }
            
            return false;
        }

        private float GetNaniteInventoryAmountThatFits(IMyEntity target, MyCubeBlock block)
        {
            if (!block.HasInventory)
                return 0f;

            var def = MyDefinitionManager.Static.GetPhysicalItemDefinition
              (new VRage.Game.MyDefinitionId(((MyFloatingObject)target).Item.Content.TypeId, ((MyFloatingObject)target).Item.Content.SubtypeId));
            MyInventory inventory = block.GetInventory();
            MyFixedPoint amountFits = inventory.ComputeAmountThatFits
              (new VRage.Game.MyDefinitionId(((MyFloatingObject)target).Item.Content.TypeId, ((MyFloatingObject)target).Item.Content.SubtypeId));

            return (float)amountFits;
        }

        public override void AddToIgnoreList(object obj){
            if (PotentialIgnoredList.Contains(obj) == false) {
                PotentialIgnoredList.Add(obj);
                if (PotentialTargetList.Contains(obj)) {
                    PotentialTargetList.Remove(obj);
                }
            }
        }

        public void CancelTarget(IMyEntity obj)
        {
            Logging.Instance.WriteLine(string.Format("[Floating] Cancelling Floating Object Target: {0} - {1} (EntityID={2},Position={3})", 
              m_constructionBlock.ConstructionBlock.EntityId, obj.GetType().Name, obj.EntityId, obj.GetPosition()), 1);

            if (Sync.IsServer)
                m_constructionBlock.SendCancelTarget(obj);

            m_constructionBlock.ParticleManager.CancelTarget(obj);

            if (m_targetTracker.ContainsKey(obj))
                m_targetTracker.Remove(obj);

            Remove(obj);
        }

        public void CancelTarget(long entityId)
        {
            m_constructionBlock.ParticleManager.CancelTarget(entityId);
            foreach (var item in m_targetTracker.ToList())
                if (item.Key.EntityId == entityId)
                    m_targetTracker.Remove(item.Key);

            foreach (IMyEntity item in TargetList.Where(x => ((IMyEntity)x).EntityId == entityId))
                Logging.Instance.WriteLine(string.Format("[Floating] Cancelling Floating Object Target: {0} - {1} (EntityID={2},Position={3})", 
                  m_constructionBlock.ConstructionBlock.EntityId, item.EntityId, item.GetPosition()), 1);

            TargetList.RemoveAll(x => ((IMyEntity)x).EntityId == entityId);
            PotentialTargetList.RemoveAll(x => ((IMyEntity)x).EntityId == entityId);            
        }

        public override void CancelTarget(object obj)
        {
            var target = obj as IMyEntity;
            if (target != null)
                CancelTarget(target);
        }

        public override void CompleteTarget(object obj)
        {
            var target = obj as IMyEntity;
            if (target != null)
                CompleteTarget(target);
        }

        public void CompleteTarget(IMyEntity obj)
        {
            Logging.Instance.WriteLine(string.Format("[Floating] Completing Floating Object Target: {0} - {1} (EntityID={2},Position={3})", 
              m_constructionBlock.ConstructionBlock.EntityId, obj.GetType().Name, obj.EntityId, obj.GetPosition()), 1);

            if (Sync.IsServer)
                m_constructionBlock.SendCompleteTarget(obj);

            m_constructionBlock.ParticleManager.CompleteTarget(obj);

            if (m_targetTracker.ContainsKey(obj))
                m_targetTracker.Remove(obj);

            Remove(obj);
        }

        public void CompleteTarget(long entityId)
        {
            m_constructionBlock.ParticleManager.CompleteTarget(entityId);
            foreach (var item in m_targetTracker.ToList())
                if (item.Key.EntityId == entityId)
                    m_targetTracker.Remove(item.Key);

            foreach(IMyEntity item in TargetList.Where(x => ((IMyEntity)x).EntityId == entityId))
                Logging.Instance.WriteLine(string.Format("[Floating] Completing Floating Object Target: {0} - {1} (EntityID={2},Position={3})", 
                  m_constructionBlock.ConstructionBlock.EntityId, item.GetType().Name, item.EntityId, item.GetPosition()), 1);

            TargetList.RemoveAll(x => ((IMyEntity)x).EntityId == entityId);
            PotentialTargetList.RemoveAll(x => ((IMyEntity)x).EntityId == entityId);
        }

        private void CreateFloatingParticle(IMyEntity target)
        {
            Logging.Instance.WriteLine("[Floating] Completing Floating Object Target: CreateFloatingParticle", 1);
            double distance = Vector3D.Distance(m_constructionBlock.ConstructionBlock.GetPosition(), target.GetPosition());
            int time = (int)Math.Max(GetMinTravelTime() * 1000f, (distance / GetSpeed()) * 1000f);

            // This should be moved somewhere else.  It initializes a tracker
            if (!m_targetTracker.ContainsKey(target))
            {
                NaniteFloatingTarget floatingTarget = new NaniteFloatingTarget();
                floatingTarget.ParticleCount = 0;
                floatingTarget.StartTime = MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
                floatingTarget.LastUpdate = MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
                floatingTarget.CarryTime = time - 1000;
                m_targetTracker.Add(target, floatingTarget);
            }

            try {
                Vector4 startColor = new Vector4(0.75f, 0.75f, 0.0f, 0.75f);
                Vector4 endColor = new Vector4(0.20f, 0.20f, 0.0f, 0.75f);

                var nearestFactory = m_constructionBlock;
                if (nearestFactory.ParticleManager.Particles.Count < NaniteParticleManager.MaxTotalParticles) {
                    MyAPIGateway.Utilities.InvokeOnGameThread(() => {
                        nearestFactory.ParticleManager.AddParticle(startColor, endColor, GetMinTravelTime() * 1000f, GetSpeed(), target);
                    });
                }

            } catch (Exception e) {
                Logging.Instance.WriteLine($"{e}");
            }
        }

        public override void ParallelUpdate(List<IMyCubeGrid> gridList, List<BlockTarget> blocks)
        {
            if (!IsEnabled(m_constructionBlock))
            {
                PotentialTargetList.Clear();
                return;
            }

            m_entities.Clear();
            try
            {
                MyAPIGateway.Entities.GetEntities(m_entities, x => x is IMyFloatingObject || x is MyInventoryBagEntity || x is IMyCharacter);
            }
            catch
            {
                Logging.Instance.WriteLine("NaniteFloatingTargets.ParallelUpdate: Error getting entities, skipping.");
                return; 
            }

            foreach (var item in m_entities.ToList())
            {
                if (item == null)
                    continue;

                if (item is IMyCharacter)
                {
                    var charBuilder = (MyObjectBuilder_Character)item.GetObjectBuilder();
                    if (charBuilder.LootingCounter <= 0f)
                        continue;
                }

                if (IsInRange(item.GetPosition(), m_maxDistance) && TransferFromTarget(item, false))
                    PotentialTargetList.Add(item);
            }
        }
    }
}
