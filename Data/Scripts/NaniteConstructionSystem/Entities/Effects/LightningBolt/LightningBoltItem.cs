using System;
using System.Collections.Generic;
using System.Linq;
using VRageMath;
using VRage.Utils;
//using Ingame = VRage.Game.ModAPI.Ingame;
//using VRage.Game.ModAPI;

namespace NaniteConstructionSystem.Entities.Effects.LightningBolt
{
    public class LightningBoltItem
    {
        private List<Vector3D> m_points;
        public List<Vector3D> Points
        {
            get { return m_points; }
        }

        private Vector3D m_start;
        public Vector3D StartPosition
        {
            get { return m_start; }
        }

        private Vector3D m_end;
        public Vector3D EndPosition
        {
            get { return m_end; }
        }

        public LightningBoltItem(Vector3D start, Vector3D end)
        {
            m_points = new List<Vector3D>();
            m_start = start;
            m_end = end;

            Vector3D startPos = start;
            Vector3D endPos = end;

            var dir = Vector3D.Normalize(endPos - startPos);
            var length = (float)(endPos - startPos).Length() / 15f;

            BoundingSphereD sphere = new BoundingSphereD(Vector3D.Zero, 0.5f);
            List<Vector3D> linePoints = new List<Vector3D>();
            linePoints.Add(startPos - dir * 0.01);
            linePoints.Add(startPos);
            for (int r = 0; r < 16; r++)
            {
                startPos = startPos + dir * length;
                if (r < 8)
                    sphere = new BoundingSphereD(startPos, (r + 1) * 0.01f);
                else
                    sphere = new BoundingSphereD(startPos, (16 - (r + 1)) * 0.01f);

                linePoints.Add(MyUtils.GetRandomBorderPosition(ref sphere));
            }
            linePoints.Add(endPos);
            int totalPoints = 60;
            for (int r = 0; r < totalPoints; r++)
            {
                Vector3D position = linePoints.First();
                double globalRatio = MathHelper.Clamp((float)r / totalPoints, 0.0f, 1.0f);
                var pointCount = linePoints.Count() - 4;
                int pathIndex = 1 + (int)(globalRatio * pointCount);
                float localRatio = (float)(globalRatio * pointCount - Math.Truncate(globalRatio * pointCount));
                Vector3D catmullPosition = Vector3D.CatmullRom(linePoints[pathIndex - 1], linePoints[pathIndex], linePoints[pathIndex + 1], linePoints[pathIndex + 2], localRatio);
                m_points.Add(catmullPosition);
            }
        }
    }
}
