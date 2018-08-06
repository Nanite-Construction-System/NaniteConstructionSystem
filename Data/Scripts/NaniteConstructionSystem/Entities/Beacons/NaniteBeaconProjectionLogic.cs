using Sandbox.Common.ObjectBuilders;
using VRage.Game.Components;
using VRage.ObjectBuilders;
using Sandbox.ModAPI;

namespace NaniteConstructionSystem.Entities.Beacons
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OxygenFarm), true, "LargeNaniteBeaconProjection", "SmallNaniteBeaconProjection")]
    public class NaniteBeaconProjectionLogic : MyGameLogicComponent
    {
        private NaniteBeacon m_beacon = null;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Logging.Instance.WriteLine(string.Format("ADDING Projection Beacon: {0}", Entity.EntityId));
            m_beacon = new NaniteBeaconProjection((IMyTerminalBlock)Entity);
            NaniteConstructionManager.BeaconList.Add(m_beacon);
        }

        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return null;
        }
    }
}
