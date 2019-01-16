using System.Collections.Generic;
using VRageMath;
using Sandbox.ModAPI;
using VRage.ModAPI;
using VRage.Game;
using VRage.ObjectBuilders;
using VRage.Game.Entity;
using VRage.Utils;
using Sandbox.Game.Entities;
using System.IO;

using NaniteConstructionSystem.Extensions;
using NaniteConstructionSystem.Entities.Effects.LightningBolt;

namespace NaniteConstructionSystem.Entities.Effects
{
    public class CenterOrbEffect : NaniteBlockEffectBase
    {
        private List<LightningBoltInstance> m_activeBolts;
        private MyEntity m_centerSphere;
        private MyCubeBlock m_block;
        private int m_activePosition = 0;

        private const int m_maxBolts = 2;

        public CenterOrbEffect(MyCubeBlock block)
        {
            m_block = block;
            CreateCenterSphere(m_block);

            m_activeBolts = new List<LightningBoltInstance>();
        }

        public override void ActiveUpdate()
        {
            m_centerSphere.PositionComp.LocalMatrix = Matrix.CreateFromTransformScale(Quaternion.Identity, new Vector3(0f, 1.5f, 0f), Vector3.One) * Matrix.CreateRotationY(MathHelper.ToRadians(m_activePosition));
            MyCubeBlockEmissive.SetEmissiveParts(m_centerSphere, 1f, Color.FromNonPremultiplied(new Vector4(0.05f, 0.05f, 0.35f, 0.75f)), Color.White);
            UpdateBolts(true);
            m_activePosition++;
        }

        public override void ActivatingUpdate(int position, int maxPosition)
        {
            UpdatePosition(position, maxPosition);
            UpdateBolts();
        }

        public override void DeactivatingUpdate(int position, int maxPosition)
        {
            UpdatePosition(position, maxPosition);
            UpdateBolts();
        }

        public override void InactiveUpdate()
        {
            if (m_centerSphere == null)
            {
                if (m_block != null)
                    CreateCenterSphere(m_block);

                return;
            }
            
            m_centerSphere.PositionComp.LocalMatrix = Matrix.CreateFromTransformScale(Quaternion.Identity, new Vector3(0f, -1.0f, 0f), Vector3.One);
            MyCubeBlockEmissive.SetEmissiveParts(m_centerSphere, 0.0f, Color.Black, Color.White);
            m_activePosition = 0;
        }

        public override void Unload()
        {
            if (m_centerSphere != null)
                m_centerSphere.Close();
        }

        private void UpdatePosition(int position, int maxPosition)
        {
            Vector3D sphereDiff = new Vector3D(0f, 1.5f, 0f) - new Vector3D(0f, -1f, 0f);
            m_centerSphere.PositionComp.LocalMatrix = Matrix.CreateFromTransformScale(Quaternion.Identity, new Vector3(0f, -1f, 0f) + sphereDiff * ((float)position / maxPosition), Vector3.One) * Matrix.CreateRotationY(MathHelper.ToRadians(position / 5f));
            MyCubeBlockEmissive.SetEmissiveParts(m_centerSphere, 1f * ((float)position / maxPosition), Color.FromNonPremultiplied(new Vector4(0.05f, 0.05f, 0.35f, 0.75f)), Color.White);
        }

        private void CreateCenterSphere(MyEntity block)
        {
            if (block == null || m_centerSphere != null)
                return;

            MyEntitySubpart centerSphereSubpart;
            
            if (block.TryGetSubpart("sphere", out centerSphereSubpart))
            {
                m_centerSphere = centerSphereSubpart as MyEntity;
                if (m_centerSphere == null)
                    return;                    

                m_centerSphere.Render.EnableColorMaskHsv = true;
                m_centerSphere.Render.ColorMaskHsv = block.Render.ColorMaskHsv;
                m_centerSphere.Render.PersistentFlags = MyPersistentEntityFlags2.CastShadows;
                m_centerSphere.PositionComp.LocalMatrix = Matrix.CreateFromTransformScale(Quaternion.Identity, new Vector3(0f, -1.0f, 0f), Vector3.One);
                m_centerSphere.Flags = EntityFlags.Visible | EntityFlags.NeedsDraw | EntityFlags.NeedsDrawFromParent | EntityFlags.InvalidateOnMove;
                m_centerSphere.OnAddedToScene(block);

                MyCubeBlockEmissive.SetEmissiveParts(m_centerSphere, 0.0f, Color.FromNonPremultiplied(new Vector4(0.35f, 0.05f, 0.35f, 0.75f)), Color.White);
            }
        }

        private void UpdateBolts(bool active = false)
        {
            if (MyAPIGateway.Session.Player == null)
                return;

            if (Vector3D.DistanceSquared(MyAPIGateway.Session.Player.GetPosition(), m_block.PositionComp.GetPosition()) > 50f * 50f)                
                return;

            for (int s = m_activeBolts.Count - 1; s >= 0; s--)
            {
                var activeItem = m_activeBolts[s];

                activeItem.Position++;
                if (activeItem.Position >= activeItem.MaxPosition)
                {
                    m_activeBolts.Remove(activeItem);
                    continue;
                }
            }

            if (m_activeBolts.Count < m_maxBolts)
            {
                int numAdd = m_maxBolts - m_activeBolts.Count;
                for (int r = 0; r < numAdd; r++)
                {
                    var bolt = LightningBoltEffect.BoltPool[MyUtils.GetRandomInt(0, LightningBoltEffect.BoltPool.Count)];
                    var boltItem = new LightningBoltInstance(bolt, null, 60 + MyUtils.GetRandomInt(-10, 15));
                    m_activeBolts.Add(boltItem);
                }
            }

            for (int s = 0; s < m_activeBolts.Count; s++)
            {
                var activeItem = m_activeBolts[s];

                Vector3D endBoltPosition = m_centerSphere.PositionComp.LocalMatrix.Translation + new Vector3D(0f, 1.5f, 0f);

                int startPos = 0;
                int maxPos = activeItem.Bolt.Points.Count;
                var previousPoint = activeItem.Bolt.Points[startPos];                
                var color = new Vector4(0.35f, 0.05f, 0.35f, 0.75f);
                if (active)
                    color = new Vector4(0.05f, 0.05f, 0.35f, 0.75f);

                for (int r = startPos + 1; r < maxPos; r++)
                {
                    var currentPoint = activeItem.Bolt.Points[startPos + r];

                    if (previousPoint.Length() > endBoltPosition.Length())
                        break;

                    var startPoint = Vector3D.Transform(currentPoint, MatrixD.CreateRotationY(MathHelper.ToRadians((float)activeItem.Position * 5)));
                    startPoint -= new Vector3D(0f, 1.5f, 0f);
                    startPoint = Vector3D.Transform(startPoint, m_block.WorldMatrix);

                    var endPoint = Vector3D.Transform(previousPoint, MatrixD.CreateRotationY(MathHelper.ToRadians((float)activeItem.Position * 5)));
                    endPoint -= new Vector3D(0f, 1.5f, 0f);
                    endPoint = Vector3D.Transform(endPoint, m_block.WorldMatrix);

                    var dir = Vector3D.Normalize(endPoint - startPoint);
                    var length = (endPoint - startPoint).Length() * 2f;

                    float pulse = MathExtensions.TrianglePulse((float)activeItem.Position, 1f, activeItem.MaxPosition / 2f) + 0.2f;
                    if (activeItem.Position < 10)
                        pulse = MathExtensions.TrianglePulse((float)activeItem.Position, 1f, 2.5f);

                    Vector4 diff = color * pulse;
                    float thickness = (0.0125f);

                    MyTransparentGeometry.AddLineBillboard(MyStringId.GetOrCompute("Testfly"), diff, startPoint, dir, (float)length, (float)thickness);
                    previousPoint = currentPoint;
                }
            }
        }
    }
}
