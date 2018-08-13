using Sandbox.ModAPI;

namespace NaniteConstructionSystem.Entities.Detectors
{
    class BigNaniteOreDetector : NaniteOreDetector
    {
        public BigNaniteOreDetector(IMyFunctionalBlock block) : base(block)
        {
            supportFilter = true;
            maxScanningLevel = 2;
            minRange = 50f;
            basePower = 0.5f;
        }
    }
}
