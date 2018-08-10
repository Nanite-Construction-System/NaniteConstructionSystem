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
        private BigNaniteOreDetector m_detector = null;
        public BigNaniteOreDetector Detector
        {
            get { return m_detector; }
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.EACH_FRAME;
            NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.EACH_10TH_FRAME;

            (Entity as IMyOreDetector).AppendingCustomInfo += AppendingCustomInfo;
        }

        private void AppendingCustomInfo(IMyTerminalBlock arg1, StringBuilder arg2)
        {
            arg2.Append($"Range: {(arg1 as IMyOreDetector).Range}");
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
    }
}
