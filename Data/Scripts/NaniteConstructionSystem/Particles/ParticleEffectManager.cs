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

            MyAPIGateway.Parallel.Start(() =>
            {
                if (m_updateCount % 120 == 0)
                {
                    MyAPIGateway.Parallel.ForEach(m_particles, particle => 
                    {
                        try
                            {particle.UpdateMatrix();}
                        catch (System.Exception e)
                            {VRage.Utils.MyLog.Default.WriteLineAndConsole($"NaniteConstructionSystem.Particles.ParticleEffectManager.Update:\n{e.ToString()}");}
                    }); 

                    if (Sync.IsClient)
                        Cleanup();
                }
            });
        } 

        private void Cleanup()
        {
            HashSet<TargetEntity> remove = new HashSet<TargetEntity>();

            MyAPIGateway.Parallel.ForEach(m_particles, item => 
            {
                try
                {
                    IMyEntity entity;
                    if (!MyAPIGateway.Entities.TryGetEntityById(item.TargetGridId, out entity))
                    {
                        remove.Add(item);
                        return;
                    }

                    IMyCubeGrid grid = entity as IMyCubeGrid;
                    IMySlimBlock slimBlock = grid.GetCubeBlock(item.TargetPosition);
                    if (slimBlock == null)
                    {
                        remove.Add(item);
                        return;
                    }

                    if (slimBlock.IsDestroyed || slimBlock.IsFullyDismounted || (slimBlock.FatBlock != null && slimBlock.FatBlock.Closed))
                    {
                        remove.Add(item);
                        return;
                    }
                }
                catch (System.Exception e)
                    {VRage.Utils.MyLog.Default.WriteLineAndConsole($"NaniteConstructionSystem.Particles.ParticleEffectManager.Cleanup:\n{e.ToString()}");}
            });

            if (remove.Count < 1)
                return;

            MyAPIGateway.Utilities.InvokeOnGameThread(() => 
            {
                foreach (var item in remove)
                    if(item != null)
                    {
                        item.Unload();
                        m_particles.Remove(item);
                    }
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
