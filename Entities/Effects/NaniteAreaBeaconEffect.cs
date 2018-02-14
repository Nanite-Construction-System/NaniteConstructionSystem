using Sandbox.ModAPI;
using VRageMath;
using Sandbox.Game.Lights;
using Sandbox.Game.Entities;
using VRage.Game.Entity;

using NaniteConstructionSystem.Extensions;

namespace NaniteConstructionSystem.Entities.Effects
{
    public class NaniteAreaBeaconEffect : NaniteBlockEffectBase
    {
        private MySoundPair m_soundPair;
        private MyEntity3DSoundEmitter m_soundEmitter;
        private IMyFunctionalBlock m_owner;

        public NaniteAreaBeaconEffect(MyCubeBlock owner)
        {
            m_owner = owner as IMyFunctionalBlock;
        }

        public override void ActiveUpdate()
        {
            m_owner.SetEmissiveParts("Emissive-Beacon", Color.Lime, 1f);
            m_owner.SetEmissiveParts("Emissive0", Color.Lime, 1f);
            MyCubeBlockEmissive.SetEmissiveParts((MyEntity)m_owner, 1f, Color.Lime, Color.White);

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
            if (m_soundEmitter != null && m_soundEmitter.IsPlaying)
                m_soundEmitter.StopSound(true);
        }
    }
}
