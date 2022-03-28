using System;
using Sandbox.Common.ObjectBuilders;
using VRage.Game.Components;
using VRage.ObjectBuilders;
using VRage.Utils;
using Sandbox.ModAPI;
using NaniteConstructionSystem.Extensions;

namespace NaniteConstructionSystem.Entities.Beacons
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, "LargeNaniteBeaconConstruct", "SmallNaniteBeaconConstruct")]
    public class NaniteBeaconConstructLogic : MyGameLogicComponent
    {
        private NaniteBeacon m_beacon = null;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            if (Sync.IsClient)
                NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.EACH_10TH_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            try {
                base.UpdateOnceBeforeFrame();

                Logging.Instance.WriteLine($"ADDING Repair Beacon: {Entity.EntityId}", 1);
                m_beacon = new NaniteBeaconConstruct((IMyFunctionalBlock)Entity);
            } catch(Exception exc) {
                MyLog.Default.WriteLineAndConsole($"##MOD: nanites UpdateOnceBeforeFrame, ERROR: {exc}");
            }
        }

        public override void Close()
        {
            if (m_beacon == null)
                return;

            m_beacon.Close();
            base.Close();
        }

        public override void UpdateBeforeSimulation10()
        {
            try {
                base.UpdateBeforeSimulation10();
                m_beacon.Update();
            } catch(Exception exc) {
                MyLog.Default.WriteLineAndConsole($"##MOD: nanites UpdateBeforeSimulation10, ERROR: {exc}");
            }
        }
    }
}
