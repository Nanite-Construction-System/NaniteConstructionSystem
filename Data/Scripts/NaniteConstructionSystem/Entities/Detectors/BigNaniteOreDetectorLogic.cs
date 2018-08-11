using System;
using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;

namespace NaniteConstructionSystem.Entities.Detectors
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OreDetector), false, "BigNaniteOreDetector")]
    class BigNaniteOreDetectorLogic : MyGameLogicComponent
    {
        public MyModStorageComponentBase Storage { get; set; }

        private BigNaniteOreDetector m_detector = null;
        public BigNaniteOreDetector Detector
        {
            get { return m_detector; }
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.EACH_10TH_FRAME;
            NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.EACH_100TH_FRAME;

            (Entity as IMyOreDetector).AppendingCustomInfo += AppendingCustomInfo;
        }

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder sb)
        {
            sb.Append("Type: Nanite Ore Detector\n");
            sb.Append($"Current Input: {Detector.Power} MW\n");
            sb.Append($"Frequency:\n");
            foreach (var freq in Detector.GetScanningFrequencies())
                sb.Append($" - [{freq}]\n");
            sb.Append($"Range: {Detector.Range}");
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();

            Logging.Instance.WriteLine($"ADDING Big Ore Detector: {Entity.EntityId}");
            m_detector = new BigNaniteOreDetector((IMyFunctionalBlock)Entity);
        }

        public override void UpdateBeforeSimulation10()
        {
            base.UpdateBeforeSimulation10();

            if (MyAPIGateway.Gui?.GetCurrentScreen == MyTerminalPageEnum.ControlPanel)
            {
                (Entity as IMyOreDetector).RefreshCustomInfo();

                // Toggle to trigger UI update
                (Entity as IMyOreDetector).ShowInToolbarConfig = !(Entity as IMyOreDetector).ShowInToolbarConfig;
                (Entity as IMyOreDetector).ShowInToolbarConfig = !(Entity as IMyOreDetector).ShowInToolbarConfig;
            }
        }

        public override void UpdateBeforeSimulation100()
        {
            base.UpdateBeforeSimulation100();
            Detector.Sink.Update();
        }
    }
}
