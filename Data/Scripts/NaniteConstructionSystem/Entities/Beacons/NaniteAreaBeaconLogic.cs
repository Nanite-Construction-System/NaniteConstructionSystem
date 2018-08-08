using Sandbox.Common.ObjectBuilders;
using VRage.Game.Components;
using VRage.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Game;
using System.Collections.Generic;
using VRageMath;
using VRage.Utils;

using NaniteConstructionSystem.Extensions;

namespace NaniteConstructionSystem.Entities.Beacons
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Projector), false, "LargeNaniteAreaBeacon")]
    public class NaniteAreaBeaconLogic : MyGameLogicComponent
    {
        private NaniteBeacon m_beacon = null;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Logging.Instance.WriteLine(string.Format("ADDING Area Beacon: {0}", Entity.EntityId));
            m_beacon = new NaniteAreaBeacon((IMyTerminalBlock)Entity);
            NaniteConstructionManager.BeaconList.Add(m_beacon);

            if(Sync.IsClient)
            {
                NaniteConstructionManager.NaniteSync.SendNeedBeaconTerminalSettings(Entity.EntityId);
            }            
        }

        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return null;
        }
    }
}
