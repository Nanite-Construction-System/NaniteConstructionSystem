using Sandbox.ModAPI;
using VRageMath;
using Sandbox.Game.Lights;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage.Utils;
using VRage.Game;
using Sandbox.Definitions;

using NaniteConstructionSystem.Extensions;
using System;

namespace NaniteConstructionSystem.Entities.Effects
{
    public class NaniteBeaconEffect : NaniteBlockEffectBase
    {

        private int m_updateCount;
        public int UpdateCount
        {
            get { return m_updateCount; }
        }

        private MySoundPair m_soundPair;
        private MyEntity3DSoundEmitter m_soundEmitter;
        private IMyFunctionalBlock m_owner;
        private Vector3 m_localOffset;
        private Vector4 m_color;

        public NaniteBeaconEffect(MyCubeBlock owner, Vector3 localOffset, Vector4 color)
        {
            m_owner = owner as IMyFunctionalBlock;
            m_localOffset = localOffset;
            m_color = color;

            m_updateCount = 0;
        }

        public override void ActiveUpdate()
        {
            float emissive = MathExtensions.TrianglePulse(m_updateCount, 1f, 150);

            //MyCubeBlockEmissive.SetEmissiveParts((MyEntity)m_owner, emissive, Color.FromNonPremultiplied(m_color) * 0f, Color.White);
            m_owner.SetEmissiveParts("Emissive-Beacon", Color.FromNonPremultiplied(m_color) * emissive, 1f);
            m_owner.SetEmissiveParts("Emissive0", Color.FromNonPremultiplied(m_color) * emissive, emissive);
            MyCubeBlockEmissive.SetEmissiveParts((MyEntity)m_owner, emissive, Color.FromNonPremultiplied(m_color) * emissive, Color.White);

            if (!m_owner.IsFunctional || !m_owner.Enabled)
            {
                if (m_soundEmitter != null)
                {
                    if (m_soundEmitter.IsPlaying)
                        m_soundEmitter.StopSound(true);
                }

                return;
            }

            if (m_soundEmitter == null)
            {
                m_soundPair = new MySoundPair("NaniteBeacon");
                m_soundEmitter = new MyEntity3DSoundEmitter((MyEntity)m_owner, true);
                m_soundEmitter.PlaySound(m_soundPair, true);
            }
            else if (!m_soundEmitter.IsPlaying)
            {
                m_soundEmitter.PlaySound(m_soundPair, true);
            }

            Vector3D position = m_owner.GetPosition();
            if (m_owner.CubeGrid.GridSizeEnum == VRage.Game.MyCubeSize.Small)
            {
                Vector3D localPosition = new Vector3D(0f, 0.30f, 0f);
                position = Vector3D.Transform(localPosition + m_localOffset, m_owner.WorldMatrix);
            }
            else
            {
                Vector3D localPosition = new Vector3D(0f, -0.05f, 0f);
                position = Vector3D.Transform(localPosition + m_localOffset, m_owner.WorldMatrix);
            }

            m_updateCount++;
        }

        public override void InactiveUpdate()
        {
            m_owner.SetEmissiveParts("Emissive-Beacon", Color.Red, 1f);
            m_owner.SetEmissiveParts("Emissive0", Color.Red, 1f);
            MyCubeBlockEmissive.SetEmissiveParts((MyEntity)m_owner, 1f, Color.Red, Color.White);
        }

        public override void ActivatingUpdate(int position, int maxPosition)
        {

        }

        public override void DeactivatingUpdate(int position, int maxPosition)
        {
            
        }

        public override void Unload()
        {
            try {
                if (m_soundEmitter != null && m_soundEmitter.IsPlaying)
                    m_soundEmitter.StopSound(true);

            } catch(Exception e)
            { Logging.Instance.WriteLine($"CheckGridGroup() Error: {e.ToString()}"); }
            
        }
    }
}
