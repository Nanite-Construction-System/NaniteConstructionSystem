using VRageMath;

namespace NaniteConstructionSystem.Entities.Effects.LightningBolt
{
    public class LightningBoltPath
    {
        public Vector3D Start { get; set; }
        public Vector3D End { get; set; }
        public Vector4 Color { get; set; }

        public LightningBoltPath(Vector3D start, Vector3D end, Vector4 color)
        {
            Start = start;
            End = end;
            Color = color;
        }
    }
}
