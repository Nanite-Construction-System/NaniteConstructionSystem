using System.Collections.Generic;
using System.Linq;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ModAPI;
using VRage.Utils;

namespace NaniteConstructionSystem.Entities.Detectors
{
    class BigNaniteOreDetector : NaniteOreDetector
    {
        public BigNaniteOreDetector(IMyFunctionalBlock block) : base(block)
        {
            supportFilter = true;
            maxScanningLevel = 2;
            minRange = 350f;
            basePower = 0.5f;
        }
    }
}
