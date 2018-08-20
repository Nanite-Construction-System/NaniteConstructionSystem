using Sandbox.ModAPI;

namespace NaniteConstructionSystem.Entities.Detectors
{
    public class LargeNaniteOreDetector : NaniteOreDetector
    {
        public LargeNaniteOreDetector(IMyFunctionalBlock block) : base(block)
        {
            supportFilter = true;
            maxScanningLevel = 2;
            minRange = 50f;
            basePower = 0.5f;
        }
    }
}
