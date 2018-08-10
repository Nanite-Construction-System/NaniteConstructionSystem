using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;

namespace NaniteConstructionSystem.Entities.Detectors
{

    class BigNaniteOreDetector
    {
        public const int MAX_UPGRADES = 8;
        public const float MIN_RANGE = 350f;

        public IMyOreDetector m_block;
        public MyOreDetectorDefinition BlockDefinition => (m_block as MyCubeBlock).BlockDefinition as MyOreDetectorDefinition;

        public BigNaniteOreDetector(IMyFunctionalBlock beaconBlock)
        {
            m_block = beaconBlock as IMyOreDetector;

            m_block.UpgradeValues.Add("Range", 0f);

            m_block.BroadcastUsingAntennas = false;
        }

        public float GetMaxRange()
        {
            float upgradeRangeAddition = (BlockDefinition.MaximumRange - MIN_RANGE) / (float)MAX_UPGRADES;
            return MIN_RANGE + (m_block.UpgradeValues["Range"] * upgradeRangeAddition);
        }

        public float GetMaxRangePercent()
        {
            float upgradeRangeAddition = (BlockDefinition.MaximumRange - MIN_RANGE) / (float)MAX_UPGRADES;
            return MIN_RANGE + (m_block.UpgradeValues["Range"] * upgradeRangeAddition) / BlockDefinition.MaximumRange;
        }
    }
}
