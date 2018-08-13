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
            else if(target is NaniteMiningItem)
            {
                var miningTarget = target as NaniteMiningItem;
                targetPosition = miningTarget.Position;
            }
            else if(target is IMyPlayer)
            {
                var destinationPosition = new Vector3D(0f, 2f, 0f);
                targetPosition = Vector3D.Transform(destinationPosition, (target as IMyPlayer).Controller.ControlledEntity.Entity.WorldMatrix);
                //targetPosition = (target as IMyPlayer).GetPosition();
            }

            double distance = Vector3D.Distance(m_constructionBlock.ConstructionBlock.GetPosition(), targetPosition);
            int time = (int)Math.Max(minTime, (distance / distanceDivisor) * 1000f);
            int tailLength = Math.Max(1, 15 - ((int)(m_particles.Count / 40f)));
            NaniteParticle particle = new NaniteParticle(time, (IMyCubeBlock)m_constructionBlock.ConstructionBlock, target, startColor, endColor, tailLength, 0.05f);
            m_particles.Add(particle);

            if (miningHammer == null)
                particle.Start();
            else
                particle.StartMining(miningHammer);

            TotalParticleCount++;
        }

        public void Update()
        {
            foreach (var item in m_particles)
            {
                item.Update();
            }

            for (int r = m_particles.Count - 1; r >= 0; r--)
            {
                var particle = m_particles[r];
                if (MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - particle.StartTime > particle.LifeTime)
                {
                    m_particles.RemoveAt(r);
                    TotalParticleCount--;
                }
            }

            foreach (var particle in m_particles)
            {
                particle.Draw();
            }

            m_updateCount++;
        }

        public void CancelTarget(object target)
        {
            TargetRemoved(target, true);
        }

        public void CompleteTarget(object target)
        {
            TargetRemoved(target, false);
        }

        private void TargetRemoved(object target, bool cancelled)
        {
            foreach (var item in m_particles)
            {
                if (target is long)
                {
                    if (item.Destination is IMyEntity)
                    {
                        if (((IMyEntity)item.Destination).EntityId == (long)target)
                        {
                            item.Complete(cancelled);
                        }
                    }
                }
                else if (target is IMyEntity || target is IMySlimBlock || target is IMyPlayer)
                {
                    if (item.Destination == target)
                    {
                        item.Complete(cancelled);
                    }
                }
                else if(target is NaniteMiningItem)
                {
                    if(item.Destination == target)
                    {
                        item.Complete(cancelled);
                    }
                }
            }
        }
    }
}
