using System;
using System.Collections.Generic;
using System.Linq;
using VRageMath;
using Sandbox.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRage.Game;
using VRage;
using VRage.Game.ModAPI;

using NaniteConstructionSystem.Entities.Beacons;

namespace NaniteConstructionSystem.Particles
{
    public class NaniteParticle
    {
        private bool m_cancel;
        public bool IsCancelled
        {
            get
            {
                return m_cancel;
            }
        }

        private bool m_complete;
        public bool IsCompleted
        {
            get
            {
                return m_complete;
            }
        }

        private IMyCubeBlock m_source;
        public IMyCubeBlock SourceBlock
        {
            get { return m_source; }
        }

        private object m_destination;
        public object Destination
        {
            get { return m_destination; }
        }

        private List<Vector3D> m_previousPoints;
        private Vector4 m_startColor;
        private Vector4 m_endColor;
        private int m_tailLength;
        private float m_scale;

        private Vector3D m_position;

        private int m_startTime;
        private List<ParticleRelativePath> m_paths;
        private int m_lifeTime;  

        public int StartTime
        {
            get { return m_startTime; }
        }

        public int LifeTime
        {
            get { return m_lifeTime; }
        }

        public NaniteParticle(int lifeTime, IMyCubeBlock source, object destination, Vector4 startColor, Vector4 endColor, int tailLength, float scale)
        {
            m_previousPoints = new List<Vector3D>();
            m_source = source;
            m_destination = destination;
            m_startColor = startColor;
            m_endColor = endColor;
            m_tailLength = tailLength;
            m_scale = scale;
            m_cancel = false;

            m_lifeTime = lifeTime;
            m_paths = new List<ParticleRelativePath>();
        }

        public void Start()
        {
            float size = 1.75f;
            /*
            if(m_destination is IMySlimBlock)
            {
                var slim = (IMySlimBlock)m_destination;
                if (slim.FatBlock != null)
                {
                    var destBlock = (IMyCubeBlock)slim.FatBlock;
                    size = destBlock.LocalVolume.Radius;
                }
            }
            */

            m_paths.Add(new ParticleRelativePath(m_source, m_destination, 10, 0.3f));
            m_paths.Add(new ParticleRelativePath(m_destination, m_destination, 6, size));
            m_paths.Add(new ParticleRelativePath(m_destination, m_source, 10, 1f, true));

            m_position = GetSourcePosition();
            m_startTime = (int)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
        }

        public void StartMining(IMyTerminalBlock miningHammer)
        {
            float size = 1.75f;

            m_paths.Add(new ParticleRelativePath(m_source, miningHammer, 16, 1f, true));
            m_paths.Add(new ParticleRelativePath(miningHammer, miningHammer, 6, size));
            m_paths.Add(new ParticleRelativePath(miningHammer, m_destination, 10, 0.3f));
            m_paths.Add(new ParticleRelativePath(m_destination, m_destination, 6, size));
            m_paths.Add(new ParticleRelativePath(m_destination, miningHammer, 10, 0.3f));
            m_paths.Add(new ParticleRelativePath(miningHammer, m_source, 10, 1f, true));

            m_position = GetSourcePosition();
            m_startTime = (int)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
        }

        public void Update()
        {
            try
            {
                if (MyAPIGateway.Session.Player == null)
                    return;

                if (m_paths.Count == 0)
                    return;

                foreach (var item in m_paths)
                    item.Update();

                Vector3D newPosition = m_position;
                double globalRatio = GetGlobalRatio();
                var pointCount = GetPathPointCount() - 1;
                int pathIndex = 1 + (int)(globalRatio * pointCount);
                float localRatio = (float)(globalRatio * pointCount - Math.Truncate(globalRatio * pointCount));

                Vector3D catmullPosition = Vector3D.CatmullRom(GetPathPoint(pathIndex - 1), GetPathPoint(pathIndex), GetPathPoint(pathIndex + 1), GetPathPoint(pathIndex + 2), localRatio);

                if (catmullPosition.IsValid())
                    newPosition = catmullPosition;

                m_previousPoints.Add(m_position);
                m_position = newPosition;
            }
            catch (Exception ex)
            {
                Logging.Instance.WriteLine(string.Format("Update Exception: {0} - {1} {2}", ex.ToString(), m_paths.Count, GetPathPointCount()));
            }
        }

        public void Draw()
        {
            if (MyAPIGateway.Session.Player == null)
                return;

            try
            {
                var scale = m_scale;
                float width = scale / 1.66f;
                float height = scale;

                Vector4 drawColor = GetColor();

                if (m_previousPoints.Count > 0)
                {
                    Vector3D previousPoint = m_position;
                    int count = 0;
                    for (int r = m_previousPoints.Count - 1; r >= 1; r--)
                    {
                        Vector3D processPoint = m_previousPoints[r];
                        if (previousPoint != processPoint)
                        {
                            Vector4 color = drawColor * (1f - (count / (float)m_tailLength));
                            Vector3D direction = Vector3D.Normalize(previousPoint - processPoint);
                            var length = (float)(previousPoint - processPoint).Length();
                            var modifiedLength = length * 3f;
                            var modifiedWidth = width * (1f - (count / (float)m_tailLength));

                            if (modifiedLength > 0f && Vector3D.DistanceSquared(processPoint, MyAPIGateway.Session.Player.GetPosition()) < 50f * 50f)
                            {
                                if (modifiedLength <= 2f)
                                    MyTransparentGeometry.AddLineBillboard(MyStringId.GetOrCompute("Firefly"), color, processPoint, direction, modifiedLength, modifiedWidth);
                                else
                                {
                                    MyTransparentGeometry.AddLineBillboard(MyStringId.GetOrCompute("Firefly"), color, processPoint, direction, modifiedLength / 2f, modifiedWidth);
                                    if (count >= m_tailLength / 3)
                                        break;
                                }
                            }
                        }

                        previousPoint = processPoint;

                        count++;
                        if (count >= m_tailLength)
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.Instance.WriteLine(string.Format("Draw Exception: {0}", ex.ToString()));
            }
        }

        private Vector4 GetColor()
        {
            Vector4 drawColor = m_startColor;
            int localIndex = 0;
            int listIndex = 0;
            GetCurrentIndices(out localIndex, out listIndex);

            if (listIndex == 1) // orbiting
            {
                Vector4 drawColorDiff = (m_endColor - m_startColor) / m_paths[listIndex].GetPointCount();
                drawColor = drawColor + drawColorDiff * localIndex;
            }
            else if (listIndex >= m_paths.Count - 1 && m_paths.Count > 1) // || listIndex  > 3) // returning
            {
                drawColor = m_endColor;
            }
            else if (m_cancel)
            {
                drawColor = new Vector4(0.95f, 0.45f, 0.45f, 0.75f);
            }
            else if (m_complete)
            {
                drawColor = new Vector4(0.45f, 0.95f, 0.45f, 0.75f);
                //drawColor = m_endColor;
            }

            return drawColor;
        }

        public void Complete(bool cancel = false)
        {
            if (m_complete)
                return;

            m_complete = true;
            m_cancel = cancel;
            int currentIndex = GetCurrentListIndex();
            if ((m_paths.Count == 3 && currentIndex < 2 && m_paths.Count > 2) ||
                (m_paths.Count == 6 && currentIndex < 4 && m_paths.Count > 2))
            {
                if (m_paths.Count == 3)
                {
                    for (int r = 0; r < 3; r++)
                        m_paths.RemoveAt(0);
                }
                else
                {
                    for (int r = 0; r < 6; r++)
                        m_paths.RemoveAt(0);
                }

                CreateCompletePath(currentIndex);
                int timeRemaining = m_lifeTime - ((int)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - m_startTime);
                m_lifeTime = Math.Min(20000, Math.Max(8000, timeRemaining));
                m_startTime = (int)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
            }
        }

        private void CreateCompletePath(int currentIndex)
        {
            List<Vector3D> pushList = null;
            if (m_previousPoints.Count < 3)
                return;

            var position = m_position;
            pushList = new List<Vector3D>();
            pushList.Add(position);
            if (currentIndex == 1 && m_previousPoints.Count > 0)
            {
                Vector3D normal = VRage.Utils.MyUtils.GetRandomVector3Normalized();
                BoundingSphereD sphere = new BoundingSphereD(position, 3);                
                pushList.Add(MyUtils.GetRandomBorderPosition(ref sphere));
                pushList.Add(MyUtils.GetRandomBorderPosition(ref sphere));
            }

            m_paths.Add(new ParticleRelativePath(position, m_source, 10, 0.6f, true, pushList));
        }

        private Vector3D GetSourcePosition()
        {
            return Vector3D.Transform(new Vector3D(0f, 1.5f, 0f), m_source.WorldMatrix);
        }

        private Vector3D GetDestinationPosition()
        {
            if (m_destination is IMySlimBlock)
            {
                var destination = (IMySlimBlock)m_destination;
                if (destination.FatBlock != null)
                    return destination.FatBlock.PositionComp.GetPosition();

                var size = destination.CubeGrid.GridSizeEnum == MyCubeSize.Small ? 0.5f : 2.5f;
                var destinationPosition = new Vector3D(destination.Position * size);

                return Vector3D.Transform(destinationPosition, destination.CubeGrid.WorldMatrix);
            }
            else if(m_destination is IMyEntity)
            {
                return ((IMyEntity)m_destination).GetPosition();
            }
            else if(m_destination is NaniteMiningItem)
            {
                return (m_destination as NaniteMiningItem).Position;
            }
            else if(m_destination is IMyPlayer)
            {
                var destinationPosition = new Vector3D(0f, 2f, 0f);
                var targetPosition = Vector3D.Transform(destinationPosition, (m_destination as IMyPlayer).Controller.ControlledEntity.Entity.WorldMatrix);
                return targetPosition;
                //return (m_destination as IMyPlayer).GetPosition();
            }

            return Vector3D.Zero;
        }

        private double GetGlobalRatio()
        {
            return MathHelper.Clamp((double)((int)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - m_startTime) / (double)m_lifeTime, 0.0, 1.0);
        }

        private int GetPathPointCount()
        {
            int count = 0;
            for (int r = 0; r < m_paths.Count; r++)
            {
                var path = m_paths[r];
                count += path.GetPointCount();
            }

            return count;
        }

        private void GetCurrentIndices(out int localIndex, out int listIndex)
        {
            double globalRatio = GetGlobalRatio();
            var pointCount = GetPathPointCount() - 1;
            int pathIndex = 1 + (int)(globalRatio * pointCount);
            ParticleRelativePath path = null;
            localIndex = 0;
            listIndex = 0;
            GetRelativePath(pathIndex, out path, out localIndex, out listIndex);
        }

        private int GetCurrentListIndex()
        {
            double globalRatio = GetGlobalRatio();
            var pointCount = GetPathPointCount() - 1;
            int pathIndex = 1 + (int)(globalRatio * pointCount);
            ParticleRelativePath path = null;
            int localIndex = 0;
            int listIndex = 0;
            GetRelativePath(pathIndex, out path, out localIndex, out listIndex);
            if (path == null)
                return 0;

            return listIndex;
        }

        private Vector3D GetPathPoint(int index)
        {
            ParticleRelativePath path = null;
            int localIndex = 0;
            int listIndex = 0;
            GetRelativePath(index, out path, out localIndex, out listIndex);
            if (path == null)
                return m_paths[m_paths.Count() - 1].GetPoint(m_paths[m_paths.Count() - 1].GetPointCount());

            Vector3D point = path.GetPoint(localIndex);
            return point;
        }

        private void GetRelativePath(int index, out ParticleRelativePath particlePath, out int localIndex, out int listIndex)
        {
            particlePath = null;
            localIndex = 0;
            listIndex = 0;

            if (m_paths.Count < 1)
                return;

            if (index < 0 || index >= GetPathPointCount())
                return;

            int pointSum = 0;
            for (int r = 0; r < m_paths.Count; r++)
            {
                var pathList = m_paths[r];
                listIndex = r;
                float ratio = (float)(index + 1) / (pointSum + pathList.GetPointCount());
                if (ratio <= 1.0f)
                    break;

                pointSum += pathList.GetPointCount();
            }

            particlePath = m_paths[listIndex];
            localIndex = index - pointSum;
        }
    }
}
