using NaniteConstructionSystem.Extensions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRageMath;

namespace NaniteConstructionSystem.Entities.Effects
{
    class OreDetectorEffect
    {
        private MyCubeBlock m_block;

        public OreDetectorEffect(MyCubeBlock block)
        {
            m_block = block;
        }

        private int m_updateCount;
        public int UpdateCount
        {
            get { return m_updateCount; }
        }

        public void ActiveUpdate()
        {
            m_block.SetEmissiveParts("Nanite_Emissive", Color.Green, 1f);
        }

        public void InactiveUpdate()
        {
            m_block.SetEmissiveParts("Nanite_Emissive", Color.Red, 1f);
        }

        public void ScanningUpdate()
        {
            m_block.SetEmissiveParts("Nanite_Emissive", Color.Blue, MathExtensions.TrianglePulse(m_updateCount, 0.8f, 150));
            m_updateCount++;
        }

        public void ScanCompleteUpdate()
        {
            m_block.SetEmissiveParts("Nanite_Emissive", Color.Aquamarine, 1f);
        }
    }
}
