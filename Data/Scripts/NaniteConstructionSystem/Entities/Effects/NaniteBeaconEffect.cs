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
        private MyLight m_light;
        public MyLight Light
        {
            get { return m_light; }
        }

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

            m_light = MyLights.AddLight();
            //* m_light.Start(MyLight.LightTypeEnum.PointLight, color, 2f, 4f);
            m_light.GlareType = VRageRender.Lights.MyGlareTypeEnum.Normal;
            m_light.GlareOn = true;
            m_light.GlareQuerySize = 1;
            m_light.GlareIntensity = 0.5f;

            var flareId = new MyDefinitionId(typeof(MyObjectBuilder_FlareDefinition), "BeaconSmall");
            var flare = (MyFlareDefinition)MyDefinitionManager.Static.GetDefinition(flareId);
            m_light.GlareSize = flare.Size;
            m_light.SubGlares = flare.SubGlares;

            //m_light.GlareMaterial = "LightGlare";
            //m_light.GlareType = VRageRender.Lights.MyGlareTypeEnum.Normal;
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

                m_light.Intensity = 0f;
                m_light.GlareIntensity = 0f;
                m_light.UpdateLight();

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

            m_light.Intensity = MathExtensions.TrianglePulse(m_updateCount, 0.8f, 150);
            m_light.GlareIntensity = MathExtensions.TrianglePulse(m_updateCount, 0.8f, 150);
            //m_light.GlareSize = 0.098f;

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

            m_light.Position = position;
            m_light.UpdateLight();

            m_updateCount++;
        }

        public override void InactiveUpdate()
        {
            m_owner.SetEmissiveParts("Emissive-Beacon", Color.Red, 1f);
            m_owner.SetEmissiveParts("Emissive0", Color.Red, 1f);
            MyCubeBlockEmissive.SetEmissiveParts((MyEntity)m_owner, 1f, Color.Red, Color.White);
            m_light.Intensity = 0f;
            m_light.GlareIntensity = 0f;
            m_light.UpdateLight();
        }

        public override void ActivatingUpdate(int position, int maxPosition)
        {

        }

        public override void DeactivatingUpdate(int position, int maxPosition)
        {
            
        }

        public override void Unload()
        {
            if (m_soundEmitter != null && m_soundEmitter.IsPlaying)
                m_soundEmitter.StopSound(true);

            if (m_light != null)
            {
                m_light.GlareOn = false;
                m_light.LightOn = false;
                m_light.ReflectorOn = false;
                m_light.UpdateLight();
                MyLights.RemoveLight(m_light);
            }
        }
    }
}
