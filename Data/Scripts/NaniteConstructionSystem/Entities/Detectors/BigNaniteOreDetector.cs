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

    class BigNaniteOreDetector
    {
        public const int MAX_UPGRADES = 8;
        public const float MIN_RANGE = 350f;
        public const float RANGE_PER_UPGRADE = 50f;
        public const float DEFAULT_POWER = 0.5f;
        public const float POWER_PER_UPGRADE = 0.125f;

        static readonly MyDefinitionId gId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");

        public float Range { get; set; }
        public IMyOreDetector m_block;
        public MyOreDetectorDefinition BlockDefinition => (m_block as MyCubeBlock).BlockDefinition as MyOreDetectorDefinition;

        private float _maxRange = MIN_RANGE;
        public float MaxRange
        {
            get { return _maxRange; }
        }

        private float _power = DEFAULT_POWER;
        public float Power
        {
            get { return Sink.CurrentInputByType(gId);  }
        }

        public bool HasFilterUpgrade
        {
            get { return m_block.UpgradeValues["Filter"] > 0f;  }
        }

        internal MyResourceSinkInfo ResourceInfo;
        internal MyResourceSinkComponent Sink;

        public BigNaniteOreDetector(IMyFunctionalBlock beaconBlock)
        {
            m_block = beaconBlock as IMyOreDetector;

            m_block.Components.TryGet(out Sink);
            ResourceInfo = new MyResourceSinkInfo()
            {
                ResourceTypeId = gId,
                MaxRequiredInput = 0f,
                RequiredInputFunc = () => _power
            };
            Sink.RemoveType(ref ResourceInfo.ResourceTypeId);
            Sink.Init(MyStringHash.GetOrCompute("Utility"), ResourceInfo);
            Sink.AddType(ref ResourceInfo);

            m_block.UpgradeValues.Add("Range", 0f);
            m_block.UpgradeValues.Add("Scanning", 0f);
            m_block.UpgradeValues.Add("Filter", 0f);
            m_block.UpgradeValues.Add("PowerEfficiency", 0f);

            m_block.BroadcastUsingAntennas = false;
            Range = MIN_RANGE; // TODO replace with setting system

            m_block.OnUpgradeValuesChanged += UpdatePower;
            UpdatePower();
        }

        private void UpdatePower()
        {
            float upgradeRangeAddition = 0f;
            float upgradeRangeMultiplicator = 1;
            for (int i = 1; i <= (int)m_block.UpgradeValues["Range"]; i++)
            {
                upgradeRangeAddition += RANGE_PER_UPGRADE * upgradeRangeMultiplicator;

                if (upgradeRangeMultiplicator == 1f)
                    upgradeRangeMultiplicator = 0.7f;
                else if (upgradeRangeMultiplicator > 0f)
                    upgradeRangeMultiplicator -= 0.1f;
            }
            _maxRange = MIN_RANGE + upgradeRangeAddition;
            if (Range > _maxRange)
                Range = _maxRange;

            _power = DEFAULT_POWER;
            _power += m_block.UpgradeValues["Range"] * POWER_PER_UPGRADE;
            _power += m_block.UpgradeValues["Filter"] * 0.1f;
            _power *= 1 + m_block.UpgradeValues["Scanning"];
            //_power -= m_block.UpgradeValues["PowerEfficiency"] * 1f;
            Sink.Update();

            Logging.Instance.WriteLine($"Updated power {_power}");
        }

        public List<string> GetScanningFrequencies()
        {
            List<string> frequencies = new List<string>();
            if (m_block.UpgradeValues["Scanning"] >= 2f)
                frequencies.Add("8kHz-2MHz");
            if (m_block.UpgradeValues["Scanning"] >= 1f)
                frequencies.Add("15MHz-40MHz");

            frequencies.Add("75MHz-310MHz");

            return frequencies;
        }

        public List<MyTerminalControlListBoxItem> GetOreList()
        {
            List<MyTerminalControlListBoxItem> list = new List<MyTerminalControlListBoxItem>();
            foreach (var item in MyDefinitionManager.Static.GetVoxelMaterialDefinitions().Select(x => x.MinedOre).Distinct())
            {
                MyStringId stringId = MyStringId.GetOrCompute(item);

                // Filter upgrade
                if (m_block.UpgradeValues["Scanning"] < 1f && (stringId.String == "Uranium" || stringId.String == "Platinum" || stringId.String == "Silver" || stringId.String == "Gold"))
                    continue;
                if (m_block.UpgradeValues["Scanning"] < 2f && (stringId.String == "Uranium" || stringId.String == "Platinum"))
                    continue;

                MyTerminalControlListBoxItem listItem = new MyTerminalControlListBoxItem(stringId, stringId, null);
                list.Add(listItem);
            }
            return list;
        }
    }
}
