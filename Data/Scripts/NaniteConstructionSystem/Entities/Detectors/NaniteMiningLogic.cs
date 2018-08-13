using Sandbox.Common.ObjectBuilders;
using VRage.Game.Components;
using VRage.ObjectBuilders;
using Sandbox.ModAPI;

namespace NaniteConstructionSystem.Entities.Detectors
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OreDetector), false, "NaniteUltrasonicHammer")]
    public class NaniteMiningHammer : MyGameLogicComponent
    {
        private NaniteMining m_mining = null;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Logging.Instance.WriteLine($"ADDING Mining Hammer: {Entity.EntityId}");
            m_mining = new NaniteMining((IMyTerminalBlock)Entity);
            NaniteConstructionManager.MiningList.Add(m_mining);
            NaniteConstructionManager.NaniteSync.SendNeedHammerTerminalSettings(Entity.EntityId);
        }

        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return null;
        }
    }
}
