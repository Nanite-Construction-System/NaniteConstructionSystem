using VRageMath;
using System;

namespace NaniteConstructionSystem.Extensions
{
    public static class MathExtensions
    {
        public static Quaternion CreateQuaternionFromNormalVector(this Vector3D a, Vector3D b)
        {
            var dot = (float)Vector3D.Dot(a, b);
            if (dot < -0.999999f)
                return new Quaternion(Orthogonal(a), 0f);

            Quaternion q = new Quaternion(Vector3D.Cross(a, b), dot);
            q.W += q.LengthSquared();
            return Quaternion.Normalize(q);
        }

        public static Quaternion CreateQuaternionFromVector(this Vector3D a, Vector3D b)
        {
            var norm_a_norm_b = (float)Math.Sqrt(Vector3.Dot(a, a) * Vector3.Dot(b, b));
            var real_part = (float)norm_a_norm_b + (float)Vector3D.Dot(a, b);
            if (real_part < 1.0e-6f * norm_a_norm_b)
            {
                return new Quaternion(Orthogonal(a), 0f);
            }

            var w = Vector3D.Cross(a, b);
            return Quaternion.Normalize(new Quaternion(w, real_part));
        }

        private static Vector3D Orthogonal(Vector3D v)
        {
            return Math.Abs(v.X) > Math.Abs(v.Z) ? new Vector3D(-v.Y, v.X, 0.0f) : new Vector3D(0.0f, -v.Z, v.Y);
        }

        /// <summary>
        /// Pulses between amplitude and zero taking period * 2 amount of time to go from zero to amplitude back to zero
        /// </summary>
        /// <param name="time">Time part</param>
        /// <param name="amplitude">Amplitude of pulse (max range)</param>
        /// <param name="period">Amount of time required to reach amplitude</param>
        /// <returns></returns>
        public static float TrianglePulse(float time, float amplitude, float period)
        {
            return (amplitude / period) * (period - Math.Abs(time % (2 * period) - period));
        }
    }

    public static class IMyStorageExtensions
    {
        public static void ClampVoxel(this VRage.ModAPI.IMyStorage self, ref Vector3I voxelCoord, int distance = 1)
        {
            if (self == null) return;
            var sizeMinusOne = self.Size - distance;
            Vector3I.Clamp(ref voxelCoord, ref Vector3I.Zero, ref sizeMinusOne, out voxelCoord);
        }
    }
}
