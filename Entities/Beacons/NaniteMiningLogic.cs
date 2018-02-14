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
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OreDetector), true, "NaniteUltrasonicHammer")]
    public class NaniteMiningHammer : MyGameLogicComponent
    {
        private NaniteMining m_mining = null;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Logging.Instance.WriteLine(string.Format("ADDING Mining Hammer: {0}", Entity.EntityId));
            m_mining = new NaniteMining((IMyTerminalBlock)Entity);
            NaniteConstructionManager.MiningList.Add(m_mining);
            NaniteConstructionManager.NaniteSync.SendNeedHammerTerminalSettings(Entity.EntityId);

            if (Sync.IsClient)
            {
                NaniteConstructionManager.NaniteSync.SendNeedHammerTerminalSettings(Entity.EntityId);
            }

        }

        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return null;
        }
    }
}
