using Sandbox.Common.ObjectBuilders;
using VRage.Game.Components;
using VRage.ObjectBuilders;
using Sandbox.ModAPI;
using NaniteConstructionSystem.Extensions;

namespace NaniteConstructionSystem.Entities.Beacons
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, "LargeNaniteBeaconProjection", "SmallNaniteBeaconProjection")]
    public class NaniteBeaconProjectionLogic : MyGameLogicComponent
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
            base.UpdateOnceBeforeFrame();

            Logging.Instance.WriteLine($"ADDING Projection Beacon: {Entity.EntityId}");
            m_beacon = new NaniteBeaconProjection((IMyFunctionalBlock)Entity);
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
            base.UpdateBeforeSimulation10();
            m_beacon.Update();
        }
    }
}
