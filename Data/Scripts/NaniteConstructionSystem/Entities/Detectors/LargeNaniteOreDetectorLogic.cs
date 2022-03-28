using NaniteConstructionSystem.Extensions;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;

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
            Logging.Instance.WriteLine($"ADDING Large Ore Detector: {Entity.EntityId}", 1);
            m_detector = new LargeNaniteOreDetector((IMyFunctionalBlock)Entity);

            base.Init(objectBuilder);
            NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            try {
                base.UpdateOnceBeforeFrame();
                m_detector.Init();

                NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.EACH_100TH_FRAME;

                if (Sync.IsClient)
                {
                    NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.EACH_FRAME;
                    (Entity as IMyOreDetector).AppendingCustomInfo += m_detector.AppendingCustomInfo;
                }
            } catch(Exception exc) {
                MyLog.Default.WriteLineAndConsole($"##MOD: nanites UpdateOnceBeforeFrame, ERROR: {exc}");
            }
        }

        public override void UpdateBeforeSimulation()
        { // CLIENT ONLY
            try {
                base.UpdateBeforeSimulation();

                m_detector.DrawStatus();
                m_detector.DrawScanningSphere();
            } catch(Exception exc) {
                MyLog.Default.WriteLineAndConsole($"##MOD: nanites UpdateBeforeSimulation, ERROR: {exc}");
            }
        }

        public override void UpdateBeforeSimulation100()
        {
            try {
                base.UpdateBeforeSimulation100();

                if (Sync.IsServer)
                {
                    MyAPIGateway.Parallel.Start(() =>
                    {
                        try
                            { m_detector.CheckScan(); }
                        catch (Exception e)
                            {VRage.Utils.MyLog.Default.WriteLineAndConsole($"NaniteOreDetector.CheckScan exception: {e}");}
                    });

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

                    m_detector.UpdateStatus();
                }


                if (Sync.IsClient && MyAPIGateway.Gui?.GetCurrentScreen == MyTerminalPageEnum.ControlPanel)
                {
                    string oldCustomInfo = (Entity as IMyOreDetector).CustomInfo;
                    (Entity as IMyOreDetector).RefreshCustomInfo();

                    if ((Entity as IMyOreDetector).CustomInfo != oldCustomInfo)
                    {
                        MyCubeBlock cubeBlock = (MyCubeBlock)(IMyCubeBlock)m_detector.Block;
                        MyOwnershipShareModeEnum shareMode;
                        long ownerId;

                        if (cubeBlock.IDModule != null)
                        {
                            ownerId = cubeBlock.IDModule.Owner;
                            shareMode = cubeBlock.IDModule.ShareMode;
                        }
                        else
                            return;

                        cubeBlock.ChangeOwner(ownerId, shareMode == MyOwnershipShareModeEnum.None ? MyOwnershipShareModeEnum.Faction : MyOwnershipShareModeEnum.None);
                        cubeBlock.ChangeOwner(ownerId, shareMode);
                    }
                }
            } catch(Exception exc) {
                MyLog.Default.WriteLineAndConsole($"##MOD: nanites UpdateBeforeSimulation100, ERROR: {exc}");
            }
        }

        public override bool IsSerialized()
        {
            return false;
        }
    }
}
