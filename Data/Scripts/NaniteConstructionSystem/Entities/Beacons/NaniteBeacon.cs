using System.Collections.Generic;
using Sandbox.ModAPI;

using NaniteConstructionSystem.Entities.Effects;
using NaniteConstructionSystem.Extensions;

namespace NaniteConstructionSystem.Entities.Beacons
{
    public class NaniteBeacon
    {
        protected IMyFunctionalBlock m_beaconBlock;
        public IMyFunctionalBlock BeaconBlock
        {
            get { return m_beaconBlock; }
        }

        protected List<NaniteBlockEffectBase> m_effects;

        public NaniteBeacon(IMyFunctionalBlock beaconBlock)
        {
            m_beaconBlock = beaconBlock;
            m_effects = new List<NaniteBlockEffectBase>();

            if (!NaniteConstructionManager.BeaconList.ContainsKey(m_beaconBlock.EntityId))
                NaniteConstructionManager.BeaconList.Add(m_beaconBlock.EntityId, this);
        }

        public virtual void Update()
        {
            if (m_beaconBlock.Enabled)
            {
                foreach (var item in m_effects)
                    item.ActiveUpdate();
            }
            else
            {
                foreach (var item in m_effects)
                    item.InactiveUpdate();
            }
        }

        public virtual void Close()
        {
            foreach (var item in m_effects)
                item.Unload();

            m_effects.Clear();

            NaniteConstructionManager.BeaconList.Remove(m_beaconBlock.EntityId);
        }
    }
}
