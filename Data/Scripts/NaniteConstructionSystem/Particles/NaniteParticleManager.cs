using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;
using VRage.ModAPI;
using VRage.Game;

using NaniteConstructionSystem.Entities;
using NaniteConstructionSystem.Entities.Beacons;
using NaniteConstructionSystem.Entities.Detectors;

namespace NaniteConstructionSystem.Particles
{
    public class NaniteParticleManager
    {
        protected List<NaniteParticle> m_particles;
        public List<NaniteParticle> Particles
        {
            get { return m_particles; }
        }

        public static int TotalParticleCount
        {
            get; set;
        }

        private const int m_maxTotalParticles = 160;
        public static int MaxTotalParticles
        {
            get { return m_maxTotalParticles; }
        }

        private int m_updateCount;
        private NaniteConstructionBlock m_constructionBlock;
        public NaniteParticleManager(NaniteConstructionBlock block)
        {
            m_particles = new List<NaniteParticle>();
            m_constructionBlock = block;
            m_updateCount = 0;
        }

        public void AddParticle(Vector4 startColor, Vector4 endColor, float minTime, float distanceDivisor, object target, IMyTerminalBlock miningHammer = null)
        {
            Vector3D targetPosition = Vector3D.Zero;
            if (target is IMyEntity)
            {
                targetPosition = ((IMyEntity)target).GetPosition();
            }
            else if (target is IMySlimBlock)
            {
                IMySlimBlock slimBlock = (IMySlimBlock)target;
                if (slimBlock.FatBlock != null)
                {
                    targetPosition = slimBlock.FatBlock.GetPosition();
                }
                else
                {
                    var size = slimBlock.CubeGrid.GridSizeEnum == MyCubeSize.Small ? 0.5f : 2.5f;
                    var destinationPosition = new Vector3D(slimBlock.Position * size);
                    targetPosition = Vector3D.Transform(destinationPosition, slimBlock.CubeGrid.WorldMatrix);
                }
            }
            else if (target is NaniteMiningItem)
            {
                var miningTarget = target as NaniteMiningItem;
                targetPosition = miningTarget.Position;
            }
            else if (target is IMyPlayer)
            {
                var destinationPosition = new Vector3D(0f, 2f, 0f);
                targetPosition = Vector3D.Transform(destinationPosition, (target as IMyPlayer).Controller.ControlledEntity.Entity.WorldMatrix);
            }

            double distance = Vector3D.Distance(m_constructionBlock.ConstructionBlock.GetPosition(), targetPosition);
            int time = (int)Math.Max(minTime, (distance / distanceDivisor) * 1000f);
            int tailLength = Math.Max(1, 15 - ((int)(m_particles.Count / 40f)));
            NaniteParticle particle = new NaniteParticle(time, (IMyCubeBlock)m_constructionBlock.ConstructionBlock, target, startColor, endColor, tailLength, 0.05f);
            m_particles.Add(particle);

            particle.Start();

            TotalParticleCount++;
        }

        public void Update()
        {
            m_updateCount++;

            if (MyAPIGateway.Session.Player == null)
                return;

            foreach (var item in m_particles)
                item.Update();

            foreach (var particle in m_particles)
                particle.Draw();
        }

        public void CheckParticleLife()
        {
            MyAPIGateway.Parallel.Start(() =>
            {
                try
                {
                    List<NaniteParticle> particlesToRemove = new List<NaniteParticle>();
                    foreach (var particle in m_particles)
                        if (MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - particle.StartTime > particle.LifeTime)
                            particlesToRemove.Add(particle);
                    foreach (var particle in particlesToRemove)
                        MyAPIGateway.Utilities.InvokeOnGameThread(() => 
                            {m_particles.Remove(particle);});
                }
                catch (Exception ex)
                    {VRage.Utils.MyLog.Default.WriteLineAndConsole($"CheckParticleLife() Error: {ex.ToString()}");}
            });
        }

        private void CallTargetRemoved(object target, bool cancelled)
        {
            MyAPIGateway.Parallel.Start(() =>
            {
                List<NaniteConstructionBlock> factoryGroup = new List<NaniteConstructionBlock>();
                lock (m_constructionBlock.FactoryGroup)
                    factoryGroup = new List<NaniteConstructionBlock>(m_constructionBlock.FactoryGroup);

                foreach (NaniteConstructionBlock factory in factoryGroup)
                    factory.ParticleManager.TargetRemoved(target, cancelled);
            });
        }

        public void CancelTarget(object target)
            {CallTargetRemoved(target, true);}

        public void CompleteTarget(object target)
            {CallTargetRemoved(target, false);}

        public void TargetRemoved(object target, bool cancelled)
        {
            foreach (var item in m_particles)
            {
                if (target is long && item.Destination is IMyEntity && ((IMyEntity)item.Destination).EntityId == (long)target)
                    item.Complete(cancelled);
                else if ((target is IMyEntity || target is IMySlimBlock || target is IMyPlayer) && item.Destination == target)
                    item.Complete(cancelled);
                else if (target is NaniteMiningItem && item.Destination == target)
                    item.Complete(cancelled);
            }
        }
    }
}
