using NaniteConstructionSystem.Extensions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace NaniteConstructionSystem.Entities.Effects
{
    class OreDetectorEffect
    {
        const string SUBPART_SHPERE = "Sphere";
        const string EMISSIVE_COOLER = "Cooler_Emissive";
        const string EMISSIVE_SUB_SPHERE = "Sphere_Emissive";
        const string EMISSIVE_PILLAR = "Pillar_Emissive";
        const string EMISSIVE_COIL_PREFIX = "Coil_Emissive";
        const int MAX_COILS = 8;

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
        private long m_coolerStatusCount;
        const float CORE_MAX_COUNT = 20f;
        private short m_coreSpeedIterator;

        public void ActiveUpdate()
        {
            m_block.SetEmissiveParts(EMISSIVE_PILLAR, Color.Green, 1f);

            DrawCoil(false);
            DrawCooler(false);
            SpinCore(false);

            m_updateCount++;
        }

        public void InactiveUpdate()
        {
            m_block.SetEmissiveParts(EMISSIVE_PILLAR, Color.Red, 1f);

            DrawCoil(false);
            DrawCooler(false);
            SpinCore(false);

            m_updateCount++;
        }

        public void ScanningUpdate()
        {
            m_block.SetEmissiveParts(EMISSIVE_PILLAR, Color.Green, 1f);

            DrawCooler(true);
            SpinCore(true);
            DrawCoil(true);

            m_updateCount++;
        }

        public void ScanCompleteUpdate()
        {
            m_block.SetEmissiveParts(EMISSIVE_PILLAR, Color.Green, 1f);

            DrawCooler(false);
            SpinCore(false);
            DrawCoil(false);

            m_updateCount++;
        }

        private void DrawCooler(bool inUse)
        {
            // should take 3 sec to "heat" up
            if (inUse && m_coolerStatusCount < COOLER_MAX_COUNT)
                m_coolerStatusCount++;
            else if (!inUse && m_coolerStatusCount > 0)
                m_coolerStatusCount--;
            else
            {
                m_block.SetEmissiveParts(EMISSIVE_COOLER, Color.Black, 1f);
                return;
            }

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

            m_block.SetEmissiveParts(EMISSIVE_COOLER, heatColor, emissivity);
        }

        private void DrawCoil(bool active)
        {
            if (!active)
            {
                for (int i = 0; i < MAX_COILS; i++)
                    m_block.SetEmissiveParts($"{EMISSIVE_COIL_PREFIX}{i}", Color.DarkRed, 0f);
                return;
            }

            int updateCount = m_updateCount >> 1;

            int iteration = (updateCount + 1) % MAX_COILS;
            int rearIteration = updateCount % MAX_COILS;
            for (int i = 0; i < MAX_COILS; i++)
            {
                if (i == iteration)
                    m_block.SetEmissiveParts($"{EMISSIVE_COIL_PREFIX}{i}", Color.DarkRed, 0.2f);
                else if (i == rearIteration)
                    m_block.SetEmissiveParts($"{EMISSIVE_COIL_PREFIX}{i}", Color.DarkRed, 0.05f);
                else
                    m_block.SetEmissiveParts($"{EMISSIVE_COIL_PREFIX}{i}", Color.DarkRed, 0f);
            }
        }

        private void SpinCore(bool active)
        {
            MyEntitySubpart subpart;
            if (m_block.TryGetSubpart(SUBPART_SHPERE, out subpart))
            {
                // inactive and spinned down
                if (!active && m_coreSpeedIterator == 0)
                {
                    subpart.SetEmissiveParts(EMISSIVE_SUB_SPHERE, Color.Red, 0f);
                    m_updateCount = 0;
                    return;
                }

                // spin up until full speed
                else if (active && m_coreSpeedIterator < CORE_MAX_COUNT && m_updateCount % 30 == 0)
                    m_coreSpeedIterator++;
                // spin down until stopped
                else if (!active && m_coreSpeedIterator > 0 && m_updateCount % 30 == 0)
                    m_coreSpeedIterator--;

                subpart.PositionComp.LocalMatrix = subpart.PositionComp.LocalMatrix * Matrix.CreateRotationY(m_coreSpeedIterator * 0.01f);
                subpart.SetEmissiveParts(EMISSIVE_SUB_SPHERE, Color.Red, m_coreSpeedIterator * 0.01f);
            }
        }
    }
}
