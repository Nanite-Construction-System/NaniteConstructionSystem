using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.Components;
using VRageMath;
using Sandbox.ModAPI;
using VRage.ModAPI;
using VRage.Game;
using VRage.ObjectBuilders;
using VRage.Game.Entity;
using VRage.Utils;
using Sandbox.Game.Entities;
using VRage.Library.Utils;
//using Ingame = VRage.ModAPI.Ingame;
using Ingame = VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Definitions;
using VRage;
using Sandbox.Game.EntityComponents;
using System.IO;
using NaniteConstructionSystem.Extensions;

namespace NaniteConstructionSystem.Entities.Effects.LightningBolt
{
    public class LightningBoltEffect : NaniteBlockEffectBase
    {
        private static int m_globalBolts;
        private static List<LightningBoltItem> m_boltPool;
        public static List<LightningBoltItem> BoltPool
        {
            get { return m_boltPool; }
        }

        private static List<LightningBoltPath> m_boltPathPool;
        private List<LightningBoltInstance> m_activeBolts;
        private MyCubeBlock m_source;
        private const int m_maxBolts = 12;
        private const int m_globalMaxBolts = 50;

        public LightningBoltEffect(MyCubeBlock block)
        {
            m_source = block;
            m_activeBolts = new List<LightningBoltInstance>();

            if (m_boltPool == null)
                InitializeBoltPools();
        }

        private void InitializeBoltPools()
        {
            m_boltPool = new List<LightningBoltItem>();
            m_boltPathPool = new List<LightningBoltPath>();

            for (int r = 0; r < 50; r++)
            {
                for (int s = 0; s < 1; s++)
                    m_boltPool.Add(new LightningBoltItem(Vector3D.Zero, new Vector3D(0f, 4f, 0f)));
            }

            m_boltPathPool.Add(new LightningBoltPath(new Vector3D(1.96f, 2.98f, 1.96f), new Vector3D(2.4f, 1.35, 2.4f), new Vector4(0.45f, 0.45f, 0.75f, 0.75f)));
            m_boltPathPool.Add(new LightningBoltPath(new Vector3D(-1.96f, 2.98f, 1.96f), new Vector3D(-2.4f, 1.35, 2.4f), new Vector4(0.45f, 0.45f, 0.75f, 0.75f)));
            m_boltPathPool.Add(new LightningBoltPath(new Vector3D(1.96f, 2.98f, -1.96f), new Vector3D(2.4f, 1.35, -2.4f), new Vector4(0.45f, 0.45f, 0.75f, 0.75f)));
            m_boltPathPool.Add(new LightningBoltPath(new Vector3D(-1.96f, 2.98f, -1.96f), new Vector3D(-2.4f, 1.35, -2.4f), new Vector4(0.45f, 0.45f, 0.75f, 0.75f)));

            m_boltPathPool.Add(new LightningBoltPath(new Vector3D(2.4f, 1.35, 2.4f), new Vector3D(1.96f, 2.98f, 1.96f), new Vector4(0.75f, 0.45f, 0.75f, 0.75f)));
            m_boltPathPool.Add(new LightningBoltPath(new Vector3D(-2.4f, 1.35, 2.4f), new Vector3D(-1.96f, 2.98f, 1.96f), new Vector4(0.75f, 0.45f, 0.75f, 0.75f)));
            m_boltPathPool.Add(new LightningBoltPath(new Vector3D(2.4f, 1.35, -2.4f), new Vector3D(1.96f, 2.98f, -1.96f), new Vector4(0.75f, 0.45f, 0.75f, 0.75f)));
            m_boltPathPool.Add(new LightningBoltPath(new Vector3D(-2.4f, 1.35, -2.4f), new Vector3D(-1.96f, 2.98f, -1.96f), new Vector4(0.75f, 0.45f, 0.75f, 0.75f)));

            m_boltPathPool.Add(new LightningBoltPath(new Vector3D(2.4f, 1.35, 2.4f), new Vector3D(1.96f, -0.25f, 1.96f), new Vector4(0.75f, 0.45f, 0.75f, 0.75f)));
            m_boltPathPool.Add(new LightningBoltPath(new Vector3D(-2.4f, 1.35, 2.4f), new Vector3D(-1.96f, -0.25f, 1.96f), new Vector4(0.75f, 0.45f, 0.75f, 0.75f)));
            m_boltPathPool.Add(new LightningBoltPath(new Vector3D(2.4f, 1.35, -2.4f), new Vector3D(1.96f, -0.25f, -1.96f), new Vector4(0.75f, 0.45f, 0.75f, 0.75f)));
            m_boltPathPool.Add(new LightningBoltPath(new Vector3D(-2.4f, 1.35, -2.4f), new Vector3D(-1.96f, -0.25f, -1.96f), new Vector4(0.75f, 0.45f, 0.75f, 0.75f)));

            m_boltPathPool.Add(new LightningBoltPath(new Vector3D(1.96f, -0.25f, 1.96f), new Vector3D(2.4f, 1.35, 2.4f), new Vector4(0.45f, 0.45f, 0.75f, 0.75f)));
            m_boltPathPool.Add(new LightningBoltPath(new Vector3D(-1.96f, -0.25f, 1.96f), new Vector3D(-2.4f, 1.35, 2.4f), new Vector4(0.45f, 0.45f, 0.75f, 0.75f)));
            m_boltPathPool.Add(new LightningBoltPath(new Vector3D(1.96f, -0.25f, -1.96f), new Vector3D(2.4f, 1.35, -2.4f), new Vector4(0.45f, 0.45f, 0.75f, 0.75f)));
            m_boltPathPool.Add(new LightningBoltPath(new Vector3D(-1.96f, -0.25f, -1.96f), new Vector3D(-2.4f, 1.35, -2.4f), new Vector4(0.45f, 0.45f, 0.75f, 0.75f)));
        }

        public override void ActiveUpdate()
        {
            if (MyAPIGateway.Session.Player == null)
                return;

            //if (Vector3D.Distance(MyAPIGateway.Session.Camera.Position, m_source.PositionComp.GetPosition()) > 50f)
            if (Vector3D.Distance(MyAPIGateway.Session.Player.GetPosition(), m_source.PositionComp.GetPosition()) > 50f)
                return;

            for (int s = m_activeBolts.Count - 1; s >= 0; s--)
            {
                var activeItem = m_activeBolts[s];

                activeItem.Position++;
                if (activeItem.Position >= activeItem.MaxPosition)
                {
                    m_activeBolts.Remove(activeItem);
                    m_globalBolts--;
                    continue;
                }
            }

            if (m_activeBolts.Count < m_maxBolts && m_globalBolts < m_globalMaxBolts)
            {
                int numAdd = Math.Min(2, m_maxBolts - m_activeBolts.Count);                
                for (int r = 0; r < numAdd; r++)
                {
                    var bolt = m_boltPool[MyUtils.GetRandomInt(0, m_boltPool.Count)];
                    var boltPath = m_boltPathPool[MyUtils.GetRandomInt(0, m_boltPathPool.Count)];
                    var boltItem = new LightningBoltInstance(bolt, boltPath, 60 + MyUtils.GetRandomInt(-10, 15));
                    m_activeBolts.Add(boltItem);
                    m_globalBolts++;
                }
            }

            for (int s = 0; s < m_activeBolts.Count; s++)
            {
                var activeItem = m_activeBolts[s];

                Vector3D sourceBolt = new Vector3D(0f, 4f, 0f);
                Vector3D startBoltPoint = activeItem.Path.Start - new Vector3D(0f, 1.5f, 0f);
                Vector3D endBoltPoint = activeItem.Path.End - new Vector3D(0f, 1.5f, 0f);
                Vector3D endDiff = endBoltPoint - startBoltPoint;
                Vector3D endBoltPosition = startBoltPoint + Vector3D.Normalize(endDiff) * (endDiff.Length() * ((float)activeItem.Position / (float)activeItem.MaxPosition));

                Quaternion rot = sourceBolt.CreateQuaternionFromVector(endBoltPosition);

                int startPos = 0;
                int maxPos = activeItem.Bolt.Points.Count;
                var previousPoint = activeItem.Bolt.Points[startPos];
                var color = activeItem.Path.Color;
                for (int r = startPos + 1; r < maxPos; r++)
                {
                    var currentPoint = activeItem.Bolt.Points[startPos + r];

                    if (previousPoint.Length() > endBoltPosition.Length())
                        break;

                    // Spin the bolt on the Y axis
                    var startPoint = Vector3D.Transform(currentPoint, MatrixD.CreateRotationY(MathHelper.ToRadians((float)activeItem.Position * 5)));
                    // Rotate the bolt towards the endpoint
                    startPoint = Vector3D.Transform(startPoint, rot);
                    // Move the bolt up to the center of the sphere
                    startPoint += new Vector3D(0f, 1.5f, 0f);
                    // Place it in the world properly
                    startPoint = Vector3D.Transform(startPoint, m_source.WorldMatrix);

                    var endPoint = Vector3D.Transform(previousPoint, MatrixD.CreateRotationY(MathHelper.ToRadians((float)activeItem.Position * 5)));
                    endPoint = Vector3D.Transform(endPoint, rot);
                    endPoint += new Vector3(0f, 1.5f, 0f);
                    endPoint = Vector3D.Transform(endPoint, m_source.WorldMatrix);

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

        public override void InactiveUpdate()
        {

        }

        public override void ActivatingUpdate(int position, int maxPosition)
        {

        }

        public override void DeactivatingUpdate(int position, int maxPosition)
        {

        }

        public override void Unload()
        {

        }
    }
}
