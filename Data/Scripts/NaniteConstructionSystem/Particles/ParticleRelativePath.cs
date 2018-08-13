using System;
using System.Collections.Generic;
using System.Linq;
using VRageMath;
using Sandbox.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRage.Game;
using VRage.Game.ModAPI;
using Sandbox.Game.Entities;
//using VRage.Game.ModAPI;

using NaniteConstructionSystem.Entities.Beacons;
using NaniteConstructionSystem.Entities.Detectors;

namespace NaniteConstructionSystem.Particles
{
    /// <summary>
    /// A particle's path has a start and end, but the start and end may change during it's journey, so path positions need to change as those
    /// positions relative to one another change.  The deviation must always remain the same, but the actual path position may change slightly as things
    /// move around.
    /// </summary>
    public class ParticleRelativePath
    {
        private int m_count;
        private float m_deviationSize;
        private Vector3D m_currentStart;
        private Vector3D m_currentEnd;

        private List<Vector3D> m_pathPoints;
        private List<Vector3D> m_deviations;
        private List<Vector3D> m_staticPathList;
        private bool m_shrinkDeviation;

        private object m_start;
        private object m_end;

        public ParticleRelativePath(object start, object end, int count, float deviationSize, bool shrinkDeviation = false, List<Vector3D> staticPathList = null)
        {
            m_start = start;
            m_end = end;

            m_count = count;
            m_deviationSize = deviationSize;
            m_currentStart = GetPosition(start);
            m_currentEnd = GetPosition(end);

            m_pathPoints = new List<Vector3D>();
            m_deviations = new List<Vector3D>();
            m_staticPathList = staticPathList;
            m_shrinkDeviation = shrinkDeviation;

            GenerateInitialPoints();
        }

        private void GenerateInitialPoints()
        {
            Vector3D start = GetPosition(m_start);
            Vector3D end = GetPosition(m_end);
            Vector3D normal = Vector3D.Zero;

            m_currentStart = start;
            m_currentEnd = end;

            if (end != start)
                normal = Vector3.Normalize(end - start);

            m_pathPoints.Add(start);
            m_deviations.Add(Vector3D.Zero);

            BoundingSphereD sphere = new BoundingSphereD(Vector3D.Zero, m_deviationSize * 2);
            if (m_staticPathList != null)
            {
                foreach (var item in m_staticPathList)
                {
                    m_pathPoints.Add(item);
                    m_deviations.Add(Vector3D.Zero);
                    //m_deviations.Add(MyUtils.GetRandomBorderPosition(ref sphere));
                    if (m_start is Vector3D)
                        m_start = item;
                }

                m_deviations[m_deviations.Count - 1] = Vector3D.Zero;
            }

            double length = (end - start).Length() / (m_count - 1);
            for (int r = 0; r < m_count - 1; r++)
            {
                Vector3D point = new Vector3D(start + (normal * length) * r);
                m_pathPoints.Add(point);

                if (!m_shrinkDeviation)
                    sphere = new BoundingSphereD(Vector3D.Zero, m_deviationSize);
                else
                    sphere = new BoundingSphereD(Vector3D.Zero, (m_deviationSize / m_count) * ((float)(m_count + 1) - r));

                m_deviations.Add(MyUtils.GetRandomBorderPosition(ref sphere));
            }
            //m_pathPoints[m_pathPoints.Count - 1] = end;
            //m_deviations[m_deviations.Count - 1] = Vector3D.Zero;
            m_pathPoints.Add(end);
            m_deviations.Add(Vector3D.Zero);
        }

        public void Update()
        {
            int pos = 0;
            try
            {
                if (GetPosition(m_start) == m_currentStart && GetPosition(m_end) == m_currentEnd)
                    return;

                if (GetPosition(m_start) == Vector3D.Zero || GetPosition(m_end) == Vector3D.Zero)
                    return;

                var start = GetPosition(m_start);
                var end = GetPosition(m_end);

                if (m_staticPathList != null)
                {
                    pos += m_staticPathList.Count;
                }
                else
                {
                    m_pathPoints[pos] = start;
                    pos += 1;
                }

                Vector3D normal = Vector3D.Zero;
                if (end != start)
                    normal = Vector3.Normalize(end - start);

                double length = (end - start).Length() / (m_count - 1);
                for (int r = 0; r < m_pathPoints.Count - pos; r++)
                {
                    if (m_shrinkDeviation && m_deviations[r + pos] == Vector3D.Zero)
                        continue;
                    
                    m_pathPoints[r + pos] = new Vector3D(start + (normal * length) * r);
                }
            }
            catch(Exception ex)
            {
                Logging.Instance.WriteLine(string.Format("Update Error - {0} {1}: {2}", m_pathPoints.Count, pos, ex.ToString()));
            }
        }

        public int GetPointCount()
        {
            return m_pathPoints.Count();
        }

        public Vector3D GetPoint(int index)
        {
            var pos = index;
            if (index >= GetPointCount())
                pos = GetPointCount() - 1;
            if (index < 0)
                pos = 0;

            return m_pathPoints[pos] + m_deviations[pos];
        }

        private Vector3D GetPosition(object item)
        {
            if (item is IMyEntity)
            {
                IMyEntity entity = (IMyEntity)item;
                if(item is MyCubeBlock)
                {
                    MyCubeBlock block = (MyCubeBlock)item;
                    if(block.BlockDefinition.Id.SubtypeName == "LargeNaniteFactory")
                    {
                        return Vector3D.Transform(new Vector3D(0f, 1.5f, 0f), entity.WorldMatrix);
                    }
                    else if(block.BlockDefinition.Id.SubtypeName == "NaniteUltrasonicHammer")
                    {
                        return Vector3D.Transform(new Vector3D(0f, 3.5f, -0.5f), entity.WorldMatrix);
                    }
                }

                return entity.GetPosition();
            }
            else if (item is IMySlimBlock)
            {
                IMySlimBlock slimBlock = (IMySlimBlock)item;
                if (slimBlock.FatBlock != null)
                    return slimBlock.FatBlock.GetPosition();

                var size = slimBlock.CubeGrid.GridSizeEnum == MyCubeSize.Small ? 0.5f : 2.5f;
                var destinationPosition = new Vector3D(slimBlock.Position * size);
                return Vector3D.Transform(destinationPosition, slimBlock.CubeGrid.WorldMatrix);
            }
            else if(item is NaniteMiningItem)
            {
                return (item as NaniteMiningItem).Position;
            }
            else if (item is Vector3D)
            {
                return (Vector3D)item;
            }
            else if (item is IMyPlayer)
            {
                var destinationPosition = new Vector3D(0f, 2f, 0f);
                var targetPosition = Vector3D.Transform(destinationPosition, (item as IMyPlayer).Controller.ControlledEntity.Entity.WorldMatrix);
                return targetPosition;

                //return (item as IMyPlayer).GetPosition();
            }

            return Vector3D.Zero;
        }
    }
}
