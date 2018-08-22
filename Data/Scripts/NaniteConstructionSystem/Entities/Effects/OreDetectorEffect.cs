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
        const string EMISSIVE_CORE = "Core_Emissive";
        const string EMISSIVE_COOLER = "Cooler_Emissive";
        const string EMISSIVE_PILLAR = "Pillar_Emissive";

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

        const float COOLER_MAX_COUNT = 4000f;
        private short m_coolerStatusCount;

        public void ActiveUpdate()
        {
            m_block.SetEmissiveParts(EMISSIVE_PILLAR, Color.Green, 1f);
            m_block.SetEmissiveParts(EMISSIVE_CORE, Color.Black, 1f);
            DrawCooler(false);
        }

        public void InactiveUpdate()
        {
            m_block.SetEmissiveParts(EMISSIVE_PILLAR, Color.Red, 1f);
            m_block.SetEmissiveParts(EMISSIVE_CORE, Color.Black, 1f);
            DrawCooler(false);
        }

        public void ScanningUpdate()
        {
            m_block.SetEmissiveParts(EMISSIVE_PILLAR, Color.Green, 1f);
            m_block.SetEmissiveParts(EMISSIVE_CORE, Color.Blue, MathExtensions.TrianglePulse(m_updateCount, 0.8f, 150));
            DrawCooler(true);
            m_updateCount++;
        }

        public void ScanCompleteUpdate()
        {
            m_block.SetEmissiveParts(EMISSIVE_PILLAR, Color.Green, 1f);
            m_block.SetEmissiveParts(EMISSIVE_CORE, Color.Aquamarine, 1f);
            DrawCooler(false);
        }

        private void DrawCooler(bool inUse)
        {
            // should take 3 sec to "heat" up
            if (inUse && m_coolerStatusCount < COOLER_MAX_COUNT)
                m_coolerStatusCount++;
            else if (!inUse && m_coolerStatusCount > 0)
                m_coolerStatusCount--;

            Color heatColor = Color.DarkRed;
            float emissivity = 0.2f;
            if (m_coolerStatusCount > 0 && m_coolerStatusCount != COOLER_MAX_COUNT)
            {
                float rFactor = (float)heatColor.R / COOLER_MAX_COUNT;
                float gFactor = (float)heatColor.G / COOLER_MAX_COUNT;
                float bFactor = (float)heatColor.B / COOLER_MAX_COUNT;
                heatColor.R = (byte)(rFactor * (float)m_coolerStatusCount);
                heatColor.G = (byte)(gFactor * (float)m_coolerStatusCount);
                heatColor.B = (byte)(bFactor * (float)m_coolerStatusCount);
                float emissivityFactor = emissivity / COOLER_MAX_COUNT;
                emissivity = emissivityFactor * m_coolerStatusCount;
            }
            else if (m_coolerStatusCount == 0)
            {
                heatColor.R = 0;
                heatColor.G = 0;
                heatColor.B = 0;
            }

            //Color heatColor = Color.DarkBlue;

            m_block.SetEmissiveParts(EMISSIVE_COOLER, heatColor, emissivity);
        }
    }
}
