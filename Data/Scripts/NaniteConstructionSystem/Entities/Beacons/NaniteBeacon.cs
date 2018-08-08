using System.Collections.Generic;
using Sandbox.ModAPI;

using NaniteConstructionSystem.Entities.Effects;

namespace NaniteConstructionSystem.Entities.Beacons
{
    public class NaniteBeacon
    {
        protected IMyTerminalBlock m_beaconBlock;
        public IMyTerminalBlock BeaconBlock
        {
            get { return m_beaconBlock; }
        }

        protected List<NaniteBlockEffectBase> m_effects;

        public NaniteBeacon(IMyTerminalBlock beaconBlock)
        {
            m_beaconBlock = beaconBlock;
            m_effects = new List<NaniteBlockEffectBase>();
            NaniteConstructionManager.BeaconList.Add(m_beaconBlock.EntityId, this);
        }

        public virtual void Update()
        {
            var functional = m_beaconBlock as IMyFunctionalBlock;
            if (functional != null)
            {
                if (!functional.Enabled)
                {
                    foreach (var item in m_effects)
                        item.InactiveUpdate();

                    return;
                }
            }

            foreach (var item in m_effects)
                item.ActiveUpdate();
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
