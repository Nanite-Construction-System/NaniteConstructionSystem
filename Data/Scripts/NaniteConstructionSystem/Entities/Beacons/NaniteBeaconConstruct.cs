using Sandbox.ModAPI;
using VRageMath;
using Sandbox.Game.Entities;

using NaniteConstructionSystem.Entities.Effects;

namespace NaniteConstructionSystem.Entities.Beacons
{
    public class NaniteBeaconConstruct : NaniteBeacon
    {
        public NaniteBeaconConstruct(IMyTerminalBlock beaconBlock) : base(beaconBlock)
        {
            m_effects.Add(new NaniteBeaconEffect((MyCubeBlock)m_beaconBlock, Vector3.Zero, new Vector4(0.55f, 0.55f, 0.95f, 0.75f)));
        }
    }
}
