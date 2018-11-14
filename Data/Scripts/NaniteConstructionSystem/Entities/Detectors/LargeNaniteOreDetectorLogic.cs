using NaniteConstructionSystem.Extensions;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System.Collections.Generic;
using System.Collections.Concurrent;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;

namespace NaniteConstructionSystem.Entities.Detectors
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OreDetector), false, "LargeNaniteOreDetector")]
    public class LargeNaniteOreDetectorLogic : MyGameLogicComponent
    {
        public MyModStorageComponentBase Storage { get; set; }

        public float OldRange;
        public List<string> OldOreListSelected;

        private LargeNaniteOreDetector m_detector = null;
        public LargeNaniteOreDetector Detector
        {
            get { return m_detector; }
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Logging.Instance.WriteLine($"ADDING Large Ore Detector: {Entity.EntityId}");
            m_detector = new LargeNaniteOreDetector((IMyFunctionalBlock)Entity);

            base.Init(objectBuilder);
            NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.EACH_10TH_FRAME;

            (Entity as IMyOreDetector).AppendingCustomInfo += m_detector.AppendingCustomInfo;
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            m_detector.Init();

            if (Sync.IsServer)
                NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.EACH_100TH_FRAME;

            if (Sync.IsClient)
                NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void UpdateBeforeSimulation()
        {
            base.UpdateBeforeSimulation();

            m_detector.DrawStatus();
            m_detector.DrawScanningSphere();
        }

        public override void UpdateBeforeSimulation10()
        {
            base.UpdateBeforeSimulation10();

            if (Sync.IsServer)
                m_detector.UpdateStatus();

            if (Sync.IsClient && MyAPIGateway.Gui?.GetCurrentScreen == MyTerminalPageEnum.ControlPanel)
            {
                string oldCustomInfo = (Entity as IMyOreDetector).CustomInfo;
                (Entity as IMyOreDetector).RefreshCustomInfo();

                if ((Entity as IMyOreDetector).CustomInfo != oldCustomInfo)
                {
                    // Toggle to trigger UI update
                    (Entity as IMyOreDetector).ShowInToolbarConfig = !(Entity as IMyOreDetector).ShowInToolbarConfig;
                    (Entity as IMyOreDetector).ShowInToolbarConfig = !(Entity as IMyOreDetector).ShowInToolbarConfig;
                }
            }
        }

        public override void UpdateBeforeSimulation100()
        {
            base.UpdateBeforeSimulation100();
            m_detector.CheckScan();

            bool forceRescan = false;
            if (OldRange != m_detector.Range)
            {
                forceRescan = true;
                OldRange = m_detector.Range;
            }
            if (OldOreListSelected != m_detector.OreListSelected)
            {
                forceRescan = true;
                OldOreListSelected = m_detector.OreListSelected;
            }
            if (forceRescan)
                m_detector.DepositGroup.Clear();
        }

        public override bool IsSerialized()
        {
            return false;
        }
    }
}
