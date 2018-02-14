/*using System;
using System.Collections.Generic;
using System.Linq;
using VRageMath;
using Sandbox.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRage.Game;

namespace NaniteConstructionSystem.Particles
{
    public class NaniteParticleWelderOld : ParticleBase
    {
        private Vector3D m_sourcePosition;
        public Vector3D SourcePosition
        {
            get
            {
                return m_sourcePosition;
            }
        }

        private Vector3D m_destinationPosition;
        public Vector3D DestinationPosition
        {
            get
            {
                return m_destinationPosition;
            }
        }

        private IMyCubeGrid m_sourceParent;
        private IMyCubeGrid m_destinationParent;

        private bool m_cancel;
        public bool IsCancelled
        {
            get
            {
                return m_cancel;
            }
        }

        private List<Vector3D> m_previousPoints;
        private Vector4 m_startColor;
        private Vector4 m_endColor;
        private int m_tailLength;
        private float m_scale;
        private bool m_complete;

        public NaniteParticleWelderOld(int lifeTime, IMyCubeBlock source, IMySlimBlock destination, Vector4 startColor, Vector4 endColor, int tailLength, float scale) : base(lifeTime)
        {
            m_previousPoints = new List<Vector3D>();
            m_sourceParent = source.CubeGrid;
            m_sourcePosition = source.Position;
            m_destinationParent = destination.CubeGrid;
            m_destinationPosition = destination.Position;
            m_startColor = startColor;
            m_endColor = endColor;
            m_tailLength = tailLength;
            m_scale = scale;
            m_cancel = false;
        }

        public override void Start()
        {
            CreateDestinationPath();
            CreateOrbitingPath();
            CreateReturningPath();

            base.Start();

            if (GetPathPointCount() > 0)
                m_position = GetPathPoint(0);
        }

        public override void Update()
        {
            base.Update();
            if (!Started || Completed)
                return;

            try
            {
                Vector3D newPosition = m_position;
                double globalRatio = GetGlobalRatio();
                var pointCount = GetPathPointCount() - 4;
                int pathIndex = 1 + (int)(globalRatio * pointCount);
                float localRatio = (float)(globalRatio * pointCount - Math.Truncate(globalRatio * pointCount));
                Vector3D catmullPosition = Vector3D.CatmullRom(GetPathPoint(pathIndex - 1), GetPathPoint(pathIndex), GetPathPoint(pathIndex + 1), GetPathPoint(pathIndex + 2), localRatio);

                if (catmullPosition.IsValid())
                    newPosition = catmullPosition;

                m_previousPoints.Add(m_position);
                
                //if (m_previousPoints.Count > m_tailLength + 2)
                //{
                //    m_previousPoints.Dequeue();
                //}
                

                m_position = newPosition;
            }
            catch(Exception ex)
            {
                Logging.Instance.WriteLine(string.Format("Update Exception: {0}", ex.ToString()));
            }
        }

        public override void Draw()
        {
            base.Draw();

            if (!Started || Completed)
                return;

            try
            {
                var scale = m_scale;
                float width = scale / 1.66f;
                float height = scale;

                ParticlePath path = null;
                int localIndex = 0;
                int listIndex = 0;
                GetCurrentRelativePath(out path, out localIndex, out listIndex);
                if (path == null)
                {
                    Logging.Instance.WriteLine("Path null");
                    return;
                }

                Vector4 drawColor = path.StartColor;
                if (path.EndColor != Vector4.Zero)
                {
                    Vector4 drawColorDiff = (m_endColor - m_startColor) / path.PointCount;
                    drawColor = drawColor + drawColorDiff * localIndex;
                }

                Vector3D sourcePosition = Vector3D.Zero;
                Vector3D destinationPosition = Vector3D.Zero;
                GetBlockPositions(out sourcePosition, out destinationPosition);

                MatrixD parentMatrix = m_sourceParent.WorldMatrix;

                if (m_previousPoints.Count > 0)
                {
                    Vector3D previousPoint = m_position;
                    int count = 0;
                    for (int r = m_previousPoints.Count - 1; r >= 1; r--)
                    {
                        //Vector3D processPoint = m_previousPoints.ElementAt(r);
                        Vector3D processPoint = m_previousPoints[r];

                        Vector3D startPoint = Vector3D.Transform(previousPoint, parentMatrix);
                        Vector3D endPoint = Vector3D.Transform(processPoint, parentMatrix);

                        Vector4 color = drawColor * (1f - (count / (float)m_tailLength));
                        //Vector4 color = drawColor + (((Vector4.One - drawColor) / (float)m_tailLength) * count);
                        Vector3D direction = Vector3D.Normalize(endPoint - startPoint);
                        var length = (float)(endPoint - startPoint).Length();
                        var modifiedLength = length * 3f; // * 3f;
                        var modifiedWidth = width * (1f - (count / (float)m_tailLength));

                        if (modifiedLength > 0f)
                        {
                            MyTransparentGeometry.AddLineBillboard("Firefly", color, startPoint, direction, modifiedLength, modifiedWidth);
                        }

                        previousPoint = processPoint;

                        count++;
                        if (count >= m_tailLength)
                            break;
                    }
                }
            }
            catch(Exception ex)
            {
                Logging.Instance.WriteLine(string.Format("Draw Exception: {0}", ex.ToString()));
            }
        }

        public void Cancel()
        {
            m_cancel = true;
            if (GetCurrentListIndex() < 2 && ParticlePaths.Count > 2)
            {
                CreateCancelPath();

                for (int r = 0; r < 3; r++)
                    ParticlePaths.RemoveAt(0);

                int timeRemaining = m_lifeTime - ((int)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - m_startTime);
                m_lifeTime = Math.Max(2000, Math.Min(timeRemaining, 5000));
                m_startTime = (int)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
            }
        }

        public void Complete()
        {
            if (m_complete)
                return;

            m_complete = true;
            if (GetCurrentListIndex() < 2 && ParticlePaths.Count > 2)
            {
                CreateCompletePath();

                for (int r = 0; r < 3; r++)
                    ParticlePaths.RemoveAt(0);

                int timeRemaining = m_lifeTime - ((int)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - m_startTime);
                m_lifeTime = Math.Max(2000, Math.Min(timeRemaining, 5000));
                m_startTime = (int)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
            }
        }

        private void CreateDestinationPath()
        {
            Vector3D sourcePosition = Vector3D.Zero;
            Vector3D destinationPosition = Vector3D.Zero;
            GetBlockPositions(out sourcePosition, out destinationPosition);

            Vector3D direction = Vector3D.Normalize(destinationPosition - sourcePosition);
            Vector3D position = sourcePosition;
            float length = (float)(destinationPosition - sourcePosition).Length() / 11;

            ParticlePath path = new ParticlePath(m_startColor, Vector4.Zero);
            path.Add(sourcePosition - direction * length);
            path.Add(sourcePosition);
            for (int r = 0; r < 10; r++)
            {
                position = position + direction * length;
                BoundingSphereD sphere = new BoundingSphereD(position, 0.3f);
                path.Add(MyUtils.GetRandomBorderPosition(ref sphere));
            }

            // Add path
            ParticlePaths.Add(path);
            m_pointCount += path.PointCount;
        }

        private void CreateOrbitingPath()
        {
            Vector3D sourcePosition = Vector3D.Zero;
            Vector3D destinationPosition = Vector3D.Zero;
            GetBlockPositions(out sourcePosition, out destinationPosition);

            ParticlePath path = new ParticlePath(m_startColor, m_endColor);
            BoundingSphereD sphere = new BoundingSphereD(destinationPosition, 1.75f);
            for (int r = 0; r < 10; r++)
            {
                path.Add(MyUtils.GetRandomBorderPosition(ref sphere));
            }
            ParticlePaths.Add(path);
            m_pointCount += path.PointCount;
        }

        private void CreateReturningPath()
        {
            if (ParticlePaths.LastOrDefault() == null)
                return;

            Vector3D sourcePosition = Vector3D.Zero;
            Vector3D destinationPosition = Vector3D.Zero;
            GetBlockPositions(out sourcePosition, out destinationPosition);

            Vector3 position = ParticlePaths.LastOrDefault().GetLastPoint();
            Vector3 direction = Vector3.Normalize(sourcePosition - position);
            float length = (float)(sourcePosition - position).Length() / 9;

            ParticlePath path = new ParticlePath(m_endColor, Vector4D.Zero);
            for (int r = 0; r < 10; r++)
            {
                position = position + direction * length;
                BoundingSphereD sphere = new BoundingSphere(position, 0.06f * (10f - r));
                path.Add(MyUtils.GetRandomBorderPosition(ref sphere));
            }
            path.Add(sourcePosition);
            ParticlePaths.Add(path);
            m_pointCount += path.PointCount;
        }

        private void CreateCancelPath()
        {
            Vector3D sourcePosition = Vector3D.Zero;
            Vector3D destinationPosition = Vector3D.Zero;
            GetBlockPositions(out sourcePosition, out destinationPosition);

            int listIndex = GetCurrentListIndex();
            bool createPush = false;
            if (listIndex == 1)
                createPush = true;

            Vector3D position = m_position;
            if (m_previousPoints.Count > 0)
                position = m_previousPoints.ElementAt(m_previousPoints.Count - 1);

            BoundingSphereD sphere = new BoundingSphere(position, 5f);
            ParticlePath path = new ParticlePath(new Vector4(0.95f, 0.45f, 0.45f, 0.75f), Vector4.Zero);
            if (createPush)
            {
                Vector3D pushPosition = MyUtils.GetRandomBorderPosition(ref sphere);

                // Particle is "pushed" when something it's repairing/building is destroyed
                Vector3D pushDirection = Vector3D.Normalize(pushPosition - position);
                float pushLength = Math.Max(0.0025f, (float)(pushPosition - position).Length() / 2);

                path.Add(position);
                path.Add(position);
                for (int r = 0; r < 3; r++)
                {
                    position = position + pushDirection * pushLength;
                    path.Add(position);
                }
            }
            else
            {
                path.Add(position);
            }

            Vector3D end = sourcePosition;
            Vector3 direction = Vector3.Normalize(end - position);

            float length = (float)(end - position).Length() / 9;
            path.Add(position);
            for (int r = 0; r < 10; r++)
            {
                position = position + direction * length;
                sphere = new BoundingSphereD(position, 0.06f * (10f - r));
                path.Add(MyUtils.GetRandomBorderPosition(ref sphere));
            }
            path.Add(end);

            ParticlePaths.Add(path);
            m_pointCount = path.PointCount;
        }

        private void CreateCompletePath()
        {
            Vector3D sourcePosition = Vector3D.Zero;
            Vector3D destinationPosition = Vector3D.Zero;
            GetBlockPositions(out sourcePosition, out destinationPosition);

            int listIndex = GetCurrentListIndex();
            bool createPush = false;
            if (listIndex == 1)
                createPush = true;

            Vector3D position = m_position;
            if (m_previousPoints.Count > 0)
                position = m_previousPoints.ElementAt(m_previousPoints.Count - 1);

            BoundingSphereD sphere = new BoundingSphere(position, 5f);
            ParticlePath path = new ParticlePath(new Vector4(0.45f, 0.95f, 0.45f, 0.75f), Vector4.Zero);
            if (createPush)
            {
                Vector3D pushPosition = MyUtils.GetRandomBorderPosition(ref sphere);

                // Particle is "pushed" when something it's repairing/building is destroyed
                Vector3D pushDirection = Vector3D.Normalize(pushPosition - position);
                float pushLength = Math.Max(0.0025f, (float)(pushPosition - position).Length() / 3);

                path.Add(position);
                path.Add(position);
                for (int r = 0; r < 2; r++)
                {
                    position = position + pushDirection * pushLength;
                    path.Add(position);
                }
            }
            else
            {
                path.Add(position);
            }

            Vector3D end = sourcePosition;
            Vector3 direction = Vector3.Normalize(end - position);

            float length = (float)(end - position).Length() / 9;
            path.Add(position);
            for (int r = 0; r < 10; r++)
            {
                position = position + direction * length;
                sphere = new BoundingSphereD(position, 0.06f * (10f - r));
                path.Add(MyUtils.GetRandomBorderPosition(ref sphere));
            }
            path.Add(end);

            ParticlePaths.Add(path);
            m_pointCount = path.PointCount;
        }

        private void GetBlockPositions(out Vector3D sourcePosition, out Vector3D destinationPosition)
        {
            float size = m_sourceParent.GridSizeEnum == MyCubeSize.Small ? 0.5f : 2.5f;
            sourcePosition = new Vector3D(m_sourcePosition * size);
            //sourcePosition = Vector3D.Transform(new Vector3D(m_sourcePosition * size), m_sourceParent.WorldMatrix);

            size = m_destinationParent.GridSizeEnum == MyCubeSize.Small ? 0.5f : 2.5f;
            destinationPosition = new Vector3D(m_destinationPosition * size);
            //destinationPosition = Vector3D.Transform(new Vector3D(m_destinationPosition * size), m_destinationParent.WorldMatrix);
        }

        private int GetCurrentListIndex()
        {
            double globalRatio = GetGlobalRatio();
            var pointCount = GetPathPointCount() - 4;
            int pathIndex = 1 + (int)(globalRatio * pointCount);

            ParticlePath path = null;
            int localIndex = 0;
            int listIndex = 0;
            GetRelativePath(pathIndex, out path, out localIndex, out listIndex);
            if (path == null)
                return 0;

            return listIndex;
        }

        private int GetCurrentLocalIndex()
        {
            double globalRatio = GetGlobalRatio();
            var pointCount = GetPathPointCount() - 4;
            int pathIndex = 1 + (int)(globalRatio * pointCount);

            ParticlePath path = null;
            int localIndex = 0;
            int listIndex = 0;
            GetRelativePath(pathIndex, out path, out localIndex, out listIndex);
            if (path == null)
                return 0;

            return localIndex;
        }

        private void GetCurrentRelativePath(out ParticlePath path, out int localIndex, out int listIndex)
        {
            double globalRatio = GetGlobalRatio();
            var pointCount = GetPathPointCount() - 4;
            int pathIndex = 1 + (int)(globalRatio * pointCount);

            path = null;
            localIndex = 0;
            listIndex = 0;
            GetRelativePath(pathIndex, out path, out localIndex, out listIndex);
        }
    }
}
*/