using System;
using System.Collections;
using System.Collections.Generic;
using Sandbox.ModAPI;
using Sandbox.Game.Entities;

using VRageMath;
using VRage.Game;
using VRage.Utils;

using NaniteConstructionSystem.Settings;
using NaniteConstructionSystem.Entities.Effects;
using NaniteConstructionSystem.Extensions;

namespace NaniteConstructionSystem.Entities.Beacons
{
    public class NaniteAreaBeacon : NaniteBeacon
    {
        private MatrixD m_areaMatrix;
        public MatrixD AreaMatrix
        {
            get { return m_areaMatrix; }
        }

        private BoundingBoxD m_areaBoundingBox;
        public BoundingBoxD AreaBoundingBox
        {
            get { return m_areaBoundingBox; }
        }

        public NaniteBeaconTerminalSettings Settings
        {
            get
            {
                if (!NaniteConstructionManager.BeaconTerminalSettings.ContainsKey(BeaconBlock.EntityId))
                    NaniteConstructionManager.BeaconTerminalSettings.Add(BeaconBlock.EntityId, new NaniteBeaconTerminalSettings());

                return NaniteConstructionManager.BeaconTerminalSettings[BeaconBlock.EntityId];
            }
        }

        public NaniteAreaBeacon(IMyFunctionalBlock beaconBlock) : base(beaconBlock)
        {
            m_effects.Add(new NaniteAreaBeaconEffect((MyCubeBlock)m_beaconBlock));
        }

        public override void Update()
        {
            var setting = Settings;

            m_areaMatrix = BeaconBlock.WorldMatrix * MatrixD.CreateRotationX(MathHelper.ToRadians(setting.RotationX));
            m_areaMatrix *= MatrixD.CreateRotationY(MathHelper.ToRadians(setting.RotationY));
            m_areaMatrix *= MatrixD.CreateRotationZ(MathHelper.ToRadians(setting.RotationZ));
            m_areaMatrix.Translation = BeaconBlock.WorldMatrix.Translation + Vector3D.Transform(new Vector3D(setting.OffsetX, setting.OffsetY, setting.OffsetZ), BeaconBlock.WorldMatrix.GetOrientation());
            m_areaBoundingBox = new BoundingBoxD(Vector3D.Zero, new Vector3D(setting.Height, setting.Width, setting.Depth));

            if (setting.HighlightArea && BeaconBlock.Enabled && !Sync.IsDedicated)
                DrawTransparentBox(m_areaMatrix, m_areaBoundingBox);

            base.Update();
        }

        public bool IsInsideBox(BoundingBoxD worldAABB, bool intersectionAllowed = true)
        {
            var localBB = worldAABB.TransformSlow(MatrixD.Invert(m_areaMatrix));
            if (m_areaBoundingBox.Contains(localBB) != ContainmentType.Disjoint)
            {
                if (!intersectionAllowed && m_areaBoundingBox.Contains(localBB) == ContainmentType.Intersects)
                    return false;

                return true;
            }

            return false;
        }

        private void DrawTransparentBox(MatrixD matrix, BoundingBoxD bb, Color? lineColor = null, bool drawBorder = true)
        {
            // Client is not synced yet
            if (Sync.IsClient && NaniteConstructionManager.Settings == null)
                return;

            Color color = Color.FromNonPremultiplied(new Vector4(0.1f, 0.1f, 0.1f, 0.7f));
            MySimpleObjectDraw.DrawTransparentBox(ref matrix, ref bb, ref color, MySimpleObjectRasterizer.Solid, 1, 0.04f, MyStringId.GetOrCompute("HoneyComb"), null, false);

            if (!drawBorder)
                return;

            var diff = (float)bb.Max.Max() / NaniteConstructionManager.Settings.AreaBeaconMaxSize;
            float lineSize = (0.1f * diff) + 0.01f;

            Color checkLineColor = Color.FromNonPremultiplied(new Vector4(1f, 1f, 1f, 0.7f));
            if (lineColor.HasValue)
                checkLineColor = lineColor.Value;

            foreach (var item in bb.GetLines())
            {
                var to = Vector3D.Transform(item.To, matrix);
                var from = Vector3D.Transform(item.From, matrix);
                var dir = Vector3D.Normalize(to - from);
                var length = (to - from).Length();
                MyTransparentGeometry.AddLineBillboard(MyStringId.GetOrCompute("Firefly"), checkLineColor, from, dir, (float)length, lineSize);
            }
        }
    }

    public static class BoundingBoxExtensions
    {
        public static IEnumerable<Line> GetLines(this BoundingBoxD box)
        {
            Vector3D[] vertices = box.GetCorners();

            // Edges
            yield return new Line(vertices[0], vertices[1], false);
            yield return new Line(vertices[0], vertices[1], false);
            yield return new Line(vertices[1], vertices[2], false);
            yield return new Line(vertices[2], vertices[3], false);
            yield return new Line(vertices[3], vertices[0], false);
            yield return new Line(vertices[0], vertices[4], false);
            yield return new Line(vertices[1], vertices[5], false);

            yield return new Line(vertices[4], vertices[5], false);
            yield return new Line(vertices[5], vertices[6], false);
            yield return new Line(vertices[6], vertices[7], false);
            yield return new Line(vertices[7], vertices[4], false);
            yield return new Line(vertices[2], vertices[6], false);
            yield return new Line(vertices[3], vertices[7], false);

            // Crosses
            yield return new Line(vertices[0], vertices[2], false);
            yield return new Line(vertices[1], vertices[3], false);

            yield return new Line(vertices[4], vertices[6], false);
            yield return new Line(vertices[5], vertices[7], false);

            yield return new Line(vertices[0], vertices[7], false);
            yield return new Line(vertices[3], vertices[4], false);

            yield return new Line(vertices[1], vertices[6], false);
            yield return new Line(vertices[2], vertices[5], false);

            yield return new Line(vertices[0], vertices[5], false);
            yield return new Line(vertices[1], vertices[4], false);

            yield return new Line(vertices[3], vertices[6], false);
            yield return new Line(vertices[2], vertices[7], false);
        }
    }
}
