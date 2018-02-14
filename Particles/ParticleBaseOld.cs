/*using System.Collections.Generic;
using System.Linq;
using VRageMath;
using Sandbox.ModAPI;

namespace NaniteConstructionSystem.Particles
{
    public class ParticleBaseOld
    {
        internal int m_startTime;
        internal int m_lifeTime;
        internal Vector3D m_position;
        internal int m_pointCount;

        public List<ParticlePath> ParticlePaths { get; private set; }
        public bool Completed { get; set; }
        public bool Started { get; set; }

        public ParticleBaseOld(int lifeTime)
        {
            m_lifeTime = lifeTime;
            m_pointCount = 0;
            ParticlePaths = new List<ParticlePath>();
            Completed = false;
            Started = false;
        }

        public virtual void Start()
        {
            if (ParticlePaths.Count < 1)
                return;

            Started = true;
            m_startTime = (int)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
        }

        public virtual void Update()
        {
            if (Started && !Completed)
            {
                if ((int)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - m_startTime > m_lifeTime)
                {
                    Completed = true;
                    ParticlePaths.Clear();
                }
            }
        }

        public virtual void Draw()
        {

        }

        internal Vector3D GetCurrentPoint()
        {
            return Vector3D.Zero;
        }

        internal double GetGlobalRatio()
        {
            return MathHelper.Clamp((double)((int)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - m_startTime) / (double)m_lifeTime, 0.0, 1.0);
        }

        internal int GetPathPointCount()
        {
            //return ParticlePaths.Sum(x => x.PointCount);
            return m_pointCount;

        }

        internal Vector3D GetPathPoint(int index)
        {
            ParticlePath path = null;
            int localIndex = 0;
            int listIndex = 0;
            GetRelativePath(index, out path, out localIndex, out listIndex);
            if (path == null)
                return Vector3D.Zero;

            Vector3D point = path.GetPoint(localIndex);
            return point;
        }

        internal void GetRelativePath(int index, out ParticlePath particlePath, out int localIndex, out int listIndex)
        {
            particlePath = null;
            localIndex = 0;
            listIndex = 0;

            if (ParticlePaths.Count < 1)
                return;

            if (index < 0 || index >= GetPathPointCount())
                return;

            int pointSum = 0;
            for (int r = 0; r < ParticlePaths.Count; r++)
            {
                var pathList = ParticlePaths[r];
                listIndex = r;
                float ratio = (float)(index + 1) / (pointSum + pathList.PointCount);
                if (ratio <= 1.0f)
                    break;

                pointSum += pathList.PointCount;
            }

            particlePath = ParticlePaths[listIndex];
            localIndex = index - pointSum;
        }
    }
}
*/