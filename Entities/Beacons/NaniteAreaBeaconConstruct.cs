using System;
using System.Collections;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRageMath;
using Sandbox.Game.Entities;
using VRage.Game;

using NaniteConstructionSystem.Entities.Effects;
using NaniteConstructionSystem.Extensions;

namespace NaniteConstructionSystem.Entities.Beacons
{
    public class NaniteAreaBeaconConstruct : NaniteBeacon
    {
        int count = 0;
        public NaniteAreaBeaconConstruct(IMyTerminalBlock beaconBlock) : base(beaconBlock)
        {

        }

        public override void Update()
        {
            if (!NaniteConstructionManager.BeaconTerminalSettings.ContainsKey(BeaconBlock.EntityId))
                NaniteConstructionManager.BeaconTerminalSettings.Add(BeaconBlock.EntityId, new Settings.NaniteBeaconTerminalSettings());

            var setting = NaniteConstructionManager.BeaconTerminalSettings[BeaconBlock.EntityId];
            MatrixD matrix = BeaconBlock.WorldMatrix * MatrixD.CreateRotationX(MathHelper.ToRadians(setting.RotationX));
            matrix *= MatrixD.CreateRotationY(MathHelper.ToRadians(setting.RotationY));
            matrix *= MatrixD.CreateRotationZ(MathHelper.ToRadians(setting.RotationZ));
            matrix.Translation = BeaconBlock.WorldMatrix.Translation + new Vector3D(setting.OffsetX, setting.OffsetY, setting.OffsetZ);

            BoundingBoxD bb = new BoundingBoxD(Vector3D.Zero, new Vector3D(setting.Height, setting.Width, setting.Depth));

            DrawTransparentBox(matrix, bb);
            base.Update();
        }

        private void DrawTransparentBox(MatrixD matrix, BoundingBoxD bb)
        {
            count++;
            Color color = Color.FromNonPremultiplied(new Vector4(0.1f, 0.1f, 0.1f, 0.7f));
            MySimpleObjectDraw.DrawTransparentBox(ref matrix, ref bb, ref color, MySimpleObjectRasterizer.Solid, 1, 0.04f, VRage.Utils.MyStringId.GetOrCompute("HoneyComb"), null, false);

            Vector3D[] vertices = bb.GetCorners();
            List<Line> lines = new List<Line>();

            // Cuboid Outline
            lines.Add(new Line(vertices[0], vertices[1], false));
            lines.Add(new Line(vertices[1], vertices[2], false));
            lines.Add(new Line(vertices[2], vertices[3], false));
            lines.Add(new Line(vertices[3], vertices[0], false));
            lines.Add(new Line(vertices[0], vertices[4], false));
            lines.Add(new Line(vertices[1], vertices[5], false));

            lines.Add(new Line(vertices[4], vertices[5], false));
            lines.Add(new Line(vertices[5], vertices[6], false));
            lines.Add(new Line(vertices[6], vertices[7], false));
            lines.Add(new Line(vertices[7], vertices[4], false));
            lines.Add(new Line(vertices[2], vertices[6], false));
            lines.Add(new Line(vertices[3], vertices[7], false));

            // Crosses
            lines.Add(new Line(vertices[0], vertices[2], false));
            lines.Add(new Line(vertices[1], vertices[3], false));

            lines.Add(new Line(vertices[4], vertices[6], false));
            lines.Add(new Line(vertices[5], vertices[7], false));

            lines.Add(new Line(vertices[0], vertices[7], false));
            lines.Add(new Line(vertices[3], vertices[4], false));

            lines.Add(new Line(vertices[1], vertices[6], false));
            lines.Add(new Line(vertices[2], vertices[5], false));

            lines.Add(new Line(vertices[0], vertices[5], false));
            lines.Add(new Line(vertices[1], vertices[4], false));

            lines.Add(new Line(vertices[3], vertices[6], false));
            lines.Add(new Line(vertices[2], vertices[7], false));

            foreach (var item in lines)
            {
                var to = Vector3D.Transform(item.To, matrix);
                var from = Vector3D.Transform(item.From, matrix);

                var dir = Vector3D.Normalize(to - from);
                var length = (to - from).Length();
                MyTransparentGeometry.AddLineBillboard(VRage.Utils.MyStringId.GetOrCompute("Firefly"), new Vector4(1f, 1f, 1f, 0.7f), from, dir, (float)length, 0.1f);
            }
        }
    }
}
