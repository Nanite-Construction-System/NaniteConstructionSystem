using System.Collections.Generic;
using VRage.Game;
using VRageMath;
using VRage.ModAPI;
using VRage.Game.ModAPI;
using Sandbox.ModAPI;
using NaniteConstructionSystem.Extensions;

namespace NaniteConstructionSystem.Particles
{
    public class ParticleEffectManager
    {
        private HashSet<TargetEntity> m_particles;
        private int m_updateCount;
        public ParticleEffectManager()
        {
            m_particles = new HashSet<TargetEntity>();
            m_updateCount = 0;
        }

        public void AddParticle(long targetGridId, Vector3I position, string effectId)
        {
            Logging.Instance.WriteLine(string.Format("ADDING particle effect: grid={0} pos={1} effid={2}", targetGridId, position, effectId));
            var target = new TargetEntity(targetGridId, position, effectId);
            m_particles.Add(target);
        }

        public void RemoveParticle(long targetGridId, Vector3I position)
        {
            Logging.Instance.WriteLine(string.Format("REMOVING particle effect: {0} {1}", targetGridId, position));

            foreach(var item in m_particles)
            {
                if (item.TargetGridId == targetGridId && item.TargetPosition == position)
                {
                    item.Unload();
                    m_particles.Remove(item);
                    return;
                }
            }

            Logging.Instance.WriteLine("REMOVE Failed - Running cleanup");
            Cleanup();
        }

        public void Update()
        {
            m_updateCount++;

            MyAPIGateway.Parallel.Start(() => {
                if (m_updateCount % 120 == 0)
                {
                    MyAPIGateway.Parallel.ForEach(m_particles, particle => {
                        try {
                            particle.UpdateMatrix();
                        } catch (System.Exception e) {
                            VRage.Utils.MyLog.Default.WriteLineAndConsole($"NaniteConstructionSystem.Particles.ParticleEffectManager.Update: {e}");
                        }
                    }); 

                    if (Sync.IsClient) {
                        Cleanup();
                    }
                }
            });
        } 

        private void Cleanup()
        {
            // Old method caused a race condition when the background process was still removing entities but the in-game class was already unloaded.
            // Also a lot of other nasty bugs (leaks, crashes, hangs and SE not closing) appear to be caused by this.
            MyAPIGateway.Parallel.ForEach(new HashSet<TargetEntity>(m_particles), item => { // Invocation 0
                try {
                    if(item == null) {
                        RemoveEntity(item);
                        return;
                    }

                    IMyEntity entity;
                    if (!MyAPIGateway.Entities.TryGetEntityById(item.TargetGridId, out entity)) {
                        RemoveEntity(item);
                        return;
                    }

                    IMyCubeGrid grid = entity as IMyCubeGrid;
                    IMySlimBlock slimBlock = grid.GetCubeBlock(item.TargetPosition);
                    if (slimBlock == null) {
                        RemoveEntity(item);
                        return;
                    }

                    if (slimBlock.IsDestroyed || slimBlock.IsFullyDismounted || (slimBlock.FatBlock != null && slimBlock.FatBlock.Closed)) {
                        RemoveEntity(item);
                        return;
                    }
                } catch (System.Exception e) when (e.ToString().Contains("IndexOutOfRangeException")) {
                    Logging.Instance.WriteLine("IndexOutOfRangeException occurred in ParticleEffectManager.Cleanup. This is likely harmless and can be ignored.");
                } catch (System.Exception e) {
                    VRage.Utils.MyLog.Default.WriteLineAndConsole($"NaniteConstructionSystem.Particles.ParticleEffectManager.Cleanup (Invocation 0): \n{e}");
                }
            });
        }

        public void RemoveEntity(TargetEntity item)
        {
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            { // Invocation 1
                try
                {
                    // double check since the m_particles could have changed
                    if (item != null) item.Unload();
                    if (m_particles != null && m_particles.Contains(item)) m_particles.Remove(item);
                }
                catch (System.Exception e)
                { VRage.Utils.MyLog.Default.WriteLineAndConsole($"NaniteConstructionSystem.Particles.ParticleEffectManager.Cleanup (Invocation 1): \n{e}"); }
            });
        }
    }

    public class TargetEntity
    {
        private long m_targetGridId;
        public long TargetGridId { get { return m_targetGridId; } }

        private Vector3I m_targetPosition;
        public Vector3I TargetPosition { get { return m_targetPosition; } }

        private MyParticleEffect m_particle;

        public TargetEntity(long targetGridId, Vector3I targetPosition, string effectId)
        {
            m_targetGridId = targetGridId;
            m_targetPosition = targetPosition;
        }

        public void Unload()
        {
            if (m_particle == null)
                return;

            m_particle.Stop();
            m_particle = null;
        }

        public void UpdateMatrix()
        {
            IMyEntity entity;
            if (m_particle == null || m_targetGridId == null || m_targetPosition == null || !MyAPIGateway.Entities.TryGetEntityById(m_targetGridId, out entity))
                return;

            var grid = entity as IMyCubeGrid;
            if (grid == null)
                return;

            var slimBlock = grid.GetCubeBlock(m_targetPosition);
            if (slimBlock == null)
                return;

            var matrix = EntityHelper.GetBlockWorldMatrix(slimBlock);

            MyAPIGateway.Utilities.InvokeOnGameThread(() => 
                {m_particle.WorldMatrix = matrix;});
        }
    }
}
