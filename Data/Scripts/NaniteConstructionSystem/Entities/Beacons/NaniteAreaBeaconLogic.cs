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
            base.Init(objectBuilder);
            NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();

            Logging.Instance.WriteLine(string.Format("ADDING Area Beacon: {0}", Entity.EntityId));
            m_beacon = new NaniteAreaBeacon((IMyTerminalBlock)Entity);

            if (Sync.IsClient)
            {
                NaniteConstructionManager.NaniteSync.SendNeedBeaconTerminalSettings(Entity.EntityId);
            }
        }

        public override void Close()
        {
            m_beacon.Close();
            base.Close();
        }

        public override void UpdateBeforeSimulation()
        {
            base.UpdateBeforeSimulation();
            m_beacon.Update();
        }
    }
}
