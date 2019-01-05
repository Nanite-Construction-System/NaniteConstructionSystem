using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using VRage.Game.Components;
using Sandbox.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;
using VRage.Game;
using Sandbox.Definitions;
using Sandbox.Common.ObjectBuilders;
using Ingame = Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces.Terminal;

using NaniteConstructionSystem.Entities;
using NaniteConstructionSystem.Extensions;
using NaniteConstructionSystem.Entities.Beacons;
using NaniteConstructionSystem.Particles;
using NaniteConstructionSystem.Settings;
using NaniteConstructionSystem.Entities.Detectors;

namespace NaniteConstructionSystem
{
    public class GridTargetInfo
    {
        public List<long> Factories = new List<long>();
        public MyCubeBlock BeaconBlock;
        public bool IsAreaBeacon;

        public GridTargetInfo(long factoryId, MyCubeBlock beaconBlock = null, bool isAreaBeacon = false)
        {
            Factories.Add(factoryId);
            BeaconBlock = beaconBlock;
            IsAreaBeacon = isAreaBeacon;
        }
    }

    public class BlockTarget
    {
        public IMySlimBlock Block;
        public bool IsRemote;
        public NaniteAreaBeacon AreaBeacon;

        public BlockTarget(IMySlimBlock block, bool isRemote = false, NaniteAreaBeacon areaBeacon = null)
        {
            Block = block;
            IsRemote = isRemote;
            AreaBeacon = areaBeacon;
        }
    }

    public class NaniteVersionClass
    {
        public int Major = 2;
        public int Revision = 0;
        public int Build = 1;

        public NaniteVersionClass(){}
    }

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class NaniteConstructionManager : MySessionComponentBase
    {
        public static NaniteConstructionManager Instance;

        // Unique storage identifer
        public readonly Guid OreDetectorSettingsGuid = new Guid("7D46082D-747A-45AF-8CD1-99A03E68CF97");

        public NaniteVersionClass NaniteVersion = new NaniteVersionClass();

        private static Dictionary<long, NaniteConstructionBlock> m_naniteBlocks;
        public static Dictionary<long, NaniteConstructionBlock> NaniteBlocks
        {
            get
            {
                if (m_naniteBlocks == null)
                    m_naniteBlocks = new Dictionary<long, NaniteConstructionBlock>();

                return m_naniteBlocks;
            }
        }

        private static Dictionary<long, IMyCubeBlock> m_projectorBlocks;
        public static Dictionary<long, IMyCubeBlock> ProjectorBlocks
        {
            get
            {
                if (m_projectorBlocks == null)
                    m_projectorBlocks = new Dictionary<long, IMyCubeBlock>();

                return m_projectorBlocks;
            }
        }

        private static Dictionary<long, NaniteBeacon> m_beaconList;
        public static Dictionary<long, NaniteBeacon> BeaconList
        {
            get
            {
                if (m_beaconList == null)
                    m_beaconList = new Dictionary<long, NaniteBeacon>();

                return m_beaconList;
            }
        }

        public static NaniteSettings m_settings;
        public static NaniteSettings Settings
        {
            get { return m_settings; }
            set { m_settings = value; }
        }

        public static ParticleEffectManager m_particleManager;
        public static ParticleEffectManager ParticleManager
        {
            get
            {
                if (m_particleManager == null)
                    m_particleManager = new ParticleEffectManager();

                return m_particleManager;
            }
        }

        private static Dictionary<long, NaniteTerminalSettings> m_terminalSettings;
        public static Dictionary<long, NaniteTerminalSettings> TerminalSettings
        {
            get
            {
                if (m_terminalSettings == null)
                    m_terminalSettings = new Dictionary<long, NaniteTerminalSettings>();

                return m_terminalSettings;
            }
        }

        private static Dictionary<long, NaniteAssemblerSettings> m_assemblerSettings;
        public static Dictionary<long, NaniteAssemblerSettings> AssemblerSettings
        {
            get
            {
                if (m_assemblerSettings == null)
                    m_assemblerSettings = new Dictionary<long, NaniteAssemblerSettings>();

                return m_assemblerSettings;
            }
        }

        private static Dictionary<long, NaniteBeaconTerminalSettings> m_beaconTerminalSettings;
        public static Dictionary<long, NaniteBeaconTerminalSettings> BeaconTerminalSettings
        {
            get
            {
                if (m_beaconTerminalSettings == null)
                    m_beaconTerminalSettings = new Dictionary<long, NaniteBeaconTerminalSettings>();

                return m_beaconTerminalSettings;
            }
        }


        private static Dictionary<long, IMyCubeBlock> m_assemblerBlocks;
        public static Dictionary<long, IMyCubeBlock> AssemblerBlocks
        {
            get
            {
                if (m_assemblerBlocks == null)
                    m_assemblerBlocks = new Dictionary<long, IMyCubeBlock>();

                return m_assemblerBlocks;
            }
        }

        private static NaniteConstructionManagerSync m_sync;
        public static NaniteConstructionManagerSync NaniteSync
        {
            get { return m_sync; }
        }

        private static Dictionary<long, NaniteOreDetector> m_oreDetectors;
        public static Dictionary<long, NaniteOreDetector> OreDetectors
        {
            get
            {
                if (m_oreDetectors == null)
                    m_oreDetectors = new Dictionary<long, NaniteOreDetector>();

                return m_oreDetectors;
            }
        }

        private int m_updateTimer;

        private TerminalSettings m_terminalSettingsManager = new TerminalSettings();
        private List<IMyTerminalControl> m_customControls = new List<IMyTerminalControl>();
        private IMyTerminalControl m_customAssemblerControl;
        private List<IMyTerminalControl> m_customHammerControls = new List<IMyTerminalControl>();
        private List<IMyTerminalControl> m_customBeaconControls = new List<IMyTerminalControl>();
        private List<IMyTerminalAction> m_customBeaconActions = new List<IMyTerminalAction>();
        private List<IMyTerminalControl> m_customOreDetectorControls = new List<IMyTerminalControl>();

        public NaniteConstructionManager()
        {
            Instance = this;
            m_sync = new NaniteConstructionManagerSync();
        }

        #region Simulation / Init
        public override void BeforeStart()
        {
            base.BeforeStart();

            try
            {
                Logging.Instance.WriteLine($"Logging Started: Nanite Control Facility | Version {NaniteVersion.Major}.{NaniteVersion.Revision} | Build {NaniteVersion.Build}");
                
                if (Sync.IsClient)
                {
                    MyAPIGateway.Entities.OnEntityAdd += Entities_OnEntityAdd;
                    MyAPIGateway.Utilities.MessageEntered += MessageEntered;
                }

                m_sync.Initialize();
                MyAPIGateway.Multiplayer.RegisterMessageHandler(MessageHub.MessageId, MessageHub.HandleMessage);

                if (Sync.IsServer)
                {
                    LoadSettings();
                    InitializeControls();
                }

                MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlGetter;
                MyAPIGateway.TerminalControls.CustomActionGetter += CustomActionGetter;

                m_terminalSettingsManager.Load();

                MyAPIGateway.Session.OnSessionReady += Session_OnSessionReady;
            }
            catch (Exception ex)
                { MyLog.Default.WriteLineAndConsole($"Exception in Nanite.Core.BeforeStart: {ex.ToString()}"); }
        }

        protected override void UnloadData()
        {
            try
            {
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(MessageHub.MessageId, MessageHub.HandleMessage);
                m_sync.Unload();

                if (!Sync.IsServer)
                {
                    MyAPIGateway.Utilities.MessageEntered -= MessageEntered;
                    MyAPIGateway.Entities.OnEntityAdd -= Entities_OnEntityAdd;
                }
                MyAPIGateway.Session.OnSessionReady -= Session_OnSessionReady;

                NaniteBlocks.Clear();
                ProjectorBlocks.Clear();
                AssemblerBlocks.Clear();

                BeaconList.Clear();

                Logging.Instance.Close();
            }
            catch (Exception ex)
                { MyLog.Default.WriteLineAndConsole($"Exception in Nanite.Core.UnloadData: {ex}"); }
        }

        private void Session_OnSessionReady()
        {
            CleanupOldBlocks();

            if (Sync.IsClient)
            {
                MessageHub.SendMessageToServer(new MessageClientConnected());

                foreach (var item in NaniteBlocks)
                    m_sync.SendNeedTerminalSettings(item.Key);

                foreach (var item in AssemblerBlocks)
                    m_sync.SendNeedAssemblerSettings(item.Value.EntityId);

                foreach (var item in BeaconTerminalSettings)
                    m_sync.SendNeedBeaconTerminalSettings(item.Key);
            }
        }
        #endregion

        public override void UpdateBeforeSimulation()
        {
            if (MyAPIGateway.Session == null)
                return;

            try
            {
                if (m_updateTimer++ % 60 == 0)
                    Logging.Instance.WriteToFile();
                
                ParticleManager.Update();
            }
            catch (Exception e)
                { MyLog.Default.WriteLineAndConsole($"Nanite.Core.UpdateBeforeSimulation Error:\n{e.ToString()}"); }
        }

        private void ScanGrid()
        {

        }

        public void InitializeControls()
        {
            // --- Repair Checkbox
            if (Settings.ConstructionEnabled)
            {
                var repairCheck = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, Ingame.IMyShipWelder>("AllowRepair");
                repairCheck.Title = MyStringId.GetOrCompute("Repair / Construction");
                repairCheck.Tooltip = MyStringId.GetOrCompute("When checked, the factory will repair or construct unbuilt blocks.");
                repairCheck.Getter = (x) =>
                {
                    if (!TerminalSettings.ContainsKey(x.EntityId))
                        TerminalSettings.Add(x.EntityId, new NaniteTerminalSettings());

                    return TerminalSettings[x.EntityId].AllowRepair;
                };

                repairCheck.Setter = (x, y) =>
                {
                    if (!TerminalSettings.ContainsKey(x.EntityId))
                        TerminalSettings.Add(x.EntityId, new NaniteTerminalSettings());

                    TerminalSettings[x.EntityId].AllowRepair = y;
                    m_sync.SendTerminalSettings(x);
                };
                m_customControls.Add(repairCheck);
            }


            // --- Projection Checkbox
            if (Settings.ProjectionEnabled)
            {
                var projectionCheck = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, Ingame.IMyShipWelder>("AllowProjection");
                projectionCheck.Title = MyStringId.GetOrCompute("Projection Construction");
                projectionCheck.Tooltip = MyStringId.GetOrCompute("When checked, the factory will repair or construct unbuilt blocks.");
                projectionCheck.Getter = (x) =>
                {
                    if (!TerminalSettings.ContainsKey(x.EntityId))
                        TerminalSettings.Add(x.EntityId, new NaniteTerminalSettings());

                    return TerminalSettings[x.EntityId].AllowProjection;
                };

                projectionCheck.Setter = (x, y) =>
                {
                    if (!TerminalSettings.ContainsKey(x.EntityId))
                        TerminalSettings.Add(x.EntityId, new NaniteTerminalSettings());

                    TerminalSettings[x.EntityId].AllowProjection = y;
                    m_sync.SendTerminalSettings(x);
                };
                m_customControls.Add(projectionCheck);
            }

            // --- Cleanup Checkbox
            if (Settings.CleanupEnabled)
            {
                var cleanupCheck = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, Ingame.IMyShipWelder>("AllowCleanup");
                cleanupCheck.Title = MyStringId.GetOrCompute("Cleanup");
                cleanupCheck.Tooltip = MyStringId.GetOrCompute("When checked, the factory will cleanup floating objects, ore, components, or corpses. It will return the objects back to the factory.");
                cleanupCheck.Getter = (x) =>
                {
                    if (!TerminalSettings.ContainsKey(x.EntityId))
                        TerminalSettings.Add(x.EntityId, new NaniteTerminalSettings());

                    return TerminalSettings[x.EntityId].AllowCleanup;
                };

                cleanupCheck.Setter = (x, y) =>
                {
                    if (!TerminalSettings.ContainsKey(x.EntityId))
                        TerminalSettings.Add(x.EntityId, new NaniteTerminalSettings());

                    TerminalSettings[x.EntityId].AllowCleanup = y;
                    m_sync.SendTerminalSettings(x);
                };
                m_customControls.Add(cleanupCheck);
            }

            // --- Deconstruction Checkbox
            if (Settings.DeconstructionEnabled)
            {
                var deconstructCheck = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, Ingame.IMyShipWelder>("AllowDeconstruct");
                deconstructCheck.Title = MyStringId.GetOrCompute("Deconstruction");
                deconstructCheck.Tooltip = MyStringId.GetOrCompute("When checked, the factory will attempt to deconstruct ships that have a deconstruction beacon on them.");
                deconstructCheck.Getter = (x) =>
                {
                    if (!TerminalSettings.ContainsKey(x.EntityId))
                        TerminalSettings.Add(x.EntityId, new NaniteTerminalSettings());

                    return TerminalSettings[x.EntityId].AllowDeconstruct;
                };

                deconstructCheck.Setter = (x, y) =>
                {
                    if (!TerminalSettings.ContainsKey(x.EntityId))
                        TerminalSettings.Add(x.EntityId, new NaniteTerminalSettings());

                    TerminalSettings[x.EntityId].AllowDeconstruct = y;
                    m_sync.SendTerminalSettings(x);
                };
                m_customControls.Add(deconstructCheck);
            }

            // --- Mining Checkbox
            if (Settings.MiningEnabled)
            {
                var miningCheck = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, Ingame.IMyShipWelder>("AllowMining");
                miningCheck.Title = MyStringId.GetOrCompute("Mining");
                miningCheck.Tooltip = MyStringId.GetOrCompute("When checked, the factory will attempt to mine resources if it detects a NUHOL.");
                miningCheck.Getter = (x) =>
                {
                    if (!TerminalSettings.ContainsKey(x.EntityId))
                        TerminalSettings.Add(x.EntityId, new NaniteTerminalSettings());

                    return TerminalSettings[x.EntityId].AllowMining;
                };

                miningCheck.Setter = (x, y) =>
                {
                    if (!TerminalSettings.ContainsKey(x.EntityId))
                        TerminalSettings.Add(x.EntityId, new NaniteTerminalSettings());

                    TerminalSettings[x.EntityId].AllowMining = y;
                    m_sync.SendTerminalSettings(x);
                };
                m_customControls.Add(miningCheck);
            }

            // --- Medical Checkbox
            if (Settings.MedicalEnabled)
            {
                var medicalCheck = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, Ingame.IMyShipWelder>("AllowMedical");
                medicalCheck.Title = MyStringId.GetOrCompute("Medical");
                medicalCheck.Tooltip = MyStringId.GetOrCompute("When checked, the factory will attempt to heal players.");
                medicalCheck.Getter = (x) =>
                {
                    if (!TerminalSettings.ContainsKey(x.EntityId))
                        TerminalSettings.Add(x.EntityId, new NaniteTerminalSettings());

                    return TerminalSettings[x.EntityId].AllowMedical;
                };

                medicalCheck.Setter = (x, y) =>
                {
                    if (!TerminalSettings.ContainsKey(x.EntityId))
                        TerminalSettings.Add(x.EntityId, new NaniteTerminalSettings());

                    TerminalSettings[x.EntityId].AllowMedical = y;
                    m_sync.SendTerminalSettings(x);
                };
                m_customControls.Add(medicalCheck);
            }

            // --- Max Nanites
            var maxNaniteTextBox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, Ingame.IMyShipWelder>("MaxNaniteText");
            maxNaniteTextBox.Title = MyStringId.GetOrCompute("Max. Nanites (0 = unlimited)");
            maxNaniteTextBox.Tooltip = MyStringId.GetOrCompute("This is the maximum nanites that the factory will release.");
            maxNaniteTextBox.Getter = (x) =>
            {
                if (!TerminalSettings.ContainsKey(x.EntityId))
                    TerminalSettings.Add(x.EntityId, new NaniteTerminalSettings());

                StringBuilder sb = new StringBuilder(TerminalSettings[x.EntityId].MaxNanites.ToString());
                return sb;
            };

            maxNaniteTextBox.Setter = (x, y) =>
            {
                if (!TerminalSettings.ContainsKey(x.EntityId))
                    TerminalSettings.Add(x.EntityId, new NaniteTerminalSettings());

                int max = 0;
                int.TryParse(y.ToString(), out max);
                TerminalSettings[x.EntityId].MaxNanites = max;
                m_sync.SendTerminalSettings(x);
            };
            m_customControls.Add(maxNaniteTextBox);

            // --- Use Assembler Checkbox
            var useAssemblerCheck = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, Ingame.IMyShipWelder>("UseAssemblers");
            useAssemblerCheck.Title = MyStringId.GetOrCompute("Use Assemblers");
            useAssemblerCheck.Tooltip = MyStringId.GetOrCompute("When checked, the factory will queue component parts in marked assemblers that are attached to the factory");
            useAssemblerCheck.Getter = (x) =>
            {
                if (!TerminalSettings.ContainsKey(x.EntityId))
                    TerminalSettings.Add(x.EntityId, new NaniteTerminalSettings());

                return TerminalSettings[x.EntityId].UseAssemblers;
            };

            useAssemblerCheck.Setter = (x, y) =>
            {
                if (!TerminalSettings.ContainsKey(x.EntityId))
                    TerminalSettings.Add(x.EntityId, new NaniteTerminalSettings());

                TerminalSettings[x.EntityId].UseAssemblers = y;
                m_sync.SendTerminalSettings(x);
            };
            m_customControls.Add(useAssemblerCheck);

            // --- Factory checkbox in assembler
            var allowFactoryCheck = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, Ingame.IMyAssembler>("AllowFactory");
            allowFactoryCheck.Title = MyStringId.GetOrCompute("Nanite Factory Queuing");
            allowFactoryCheck.Tooltip = MyStringId.GetOrCompute("When checked, this will allow the nanite factory to queue components for construction.");
            allowFactoryCheck.Getter = (x) =>
            {
                if (!AssemblerSettings.ContainsKey(x.EntityId))
                    AssemblerSettings.Add(x.EntityId, new NaniteAssemblerSettings());

                return AssemblerSettings[x.EntityId].AllowFactoryUsage;
            };

            allowFactoryCheck.Setter = (x, y) =>
            {
                if (!AssemblerSettings.ContainsKey(x.EntityId))
                    AssemblerSettings.Add(x.EntityId, new NaniteAssemblerSettings());

                AssemblerSettings[x.EntityId].AllowFactoryUsage = y;
                m_sync.SendAssemblerSettings(x);
            };
            m_customAssemblerControl = allowFactoryCheck;

            // --- Area Beacons
            // -- Separator
            var separateArea = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyProjector>("SeparateArea");
            m_customBeaconControls.Add(separateArea);

            // -- Highlight Area
            var highlightCheck = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyProjector>("HighlightArea");
            highlightCheck.Title = MyStringId.GetOrCompute("Hightlight Area");
            highlightCheck.Tooltip = MyStringId.GetOrCompute("When checked, it will show you the area this beacon covers");
            highlightCheck.Getter = (x) =>
            {
                if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                    BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                return BeaconTerminalSettings[x.EntityId].HighlightArea;
            };

            highlightCheck.Setter = (x, y) =>
            {
                if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                    BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                BeaconTerminalSettings[x.EntityId].HighlightArea = y;
                m_sync.SendBeaconTerminalSettings(x.EntityId);
            };

            m_customBeaconControls.Add(highlightCheck);

            // -- Allow Repair
            if (Settings.ConstructionEnabled)
            {
                var areaAllowRepairCheck = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyProjector>("AreaAllowRepair");
                areaAllowRepairCheck.Title = MyStringId.GetOrCompute("Allow Repair");
                areaAllowRepairCheck.Tooltip = MyStringId.GetOrCompute("When checked, factories will repair blocks inside the beacon area");
                areaAllowRepairCheck.Getter = (x) =>
                {
                    if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                        BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                    return BeaconTerminalSettings[x.EntityId].AllowRepair;
                };

                areaAllowRepairCheck.Setter = (x, y) =>
                {
                    if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                        BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                    BeaconTerminalSettings[x.EntityId].AllowRepair = y;
                    m_sync.SendBeaconTerminalSettings(x.EntityId);
                };

                m_customBeaconControls.Add(areaAllowRepairCheck);
            }

            // -- Allow Deconstruct
            if (Settings.DeconstructionEnabled)
            {
                var areaAllowDeconstructCheck = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyProjector>("AreaAllowDeconstruct");
                areaAllowDeconstructCheck.Title = MyStringId.GetOrCompute("Allow Deconstruct");
                areaAllowDeconstructCheck.Tooltip = MyStringId.GetOrCompute("When checked, factories will deconstruct blocks inside the beacon area");
                areaAllowDeconstructCheck.Getter = (x) =>
                {
                    if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                        BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                    return BeaconTerminalSettings[x.EntityId].AllowDeconstruction;
                };

                areaAllowDeconstructCheck.Setter = (x, y) =>
                {
                    if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                        BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                    BeaconTerminalSettings[x.EntityId].AllowDeconstruction = y;
                    m_sync.SendBeaconTerminalSettings(x.EntityId);
                };

                m_customBeaconControls.Add(areaAllowDeconstructCheck);
            }

            // -- Allow Projection
            if (Settings.ProjectionEnabled)
            {
                var areaAllowProjectionCheck = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyProjector>("AreaAllowProjection");
                areaAllowProjectionCheck.Title = MyStringId.GetOrCompute("Allow Projection");
                areaAllowProjectionCheck.Tooltip = MyStringId.GetOrCompute("When checked, factories will build projected blocks inside the beacon area");
                areaAllowProjectionCheck.Getter = (x) =>
                {
                    if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                        BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                    return BeaconTerminalSettings[x.EntityId].AllowProjection;
                };

                areaAllowProjectionCheck.Setter = (x, y) =>
                {
                    if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                        BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                    BeaconTerminalSettings[x.EntityId].AllowProjection = y;
                    m_sync.SendBeaconTerminalSettings(x.EntityId);
                };

                m_customBeaconControls.Add(areaAllowProjectionCheck);
            }

            // -- Separator
            var separateSliderArea = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyProjector>("SeparateSliderArea");
            m_customBeaconControls.Add(separateSliderArea);

            // -- Height
            var heightSlider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyProjector>("HeightSlider");
            heightSlider.Title = MyStringId.GetOrCompute("Area Height");
            heightSlider.Tooltip = MyStringId.GetOrCompute("Height of area this beacon scans");
            heightSlider.SetLimits(Settings.AreaBeaconMinSize, Settings.AreaBeaconMaxSize);
            heightSlider.Getter = (x) =>
            {
                if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                    BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                return BeaconTerminalSettings[x.EntityId].Height;
            };

            heightSlider.Setter = (x, y) =>
            {
                if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                    BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                BeaconTerminalSettings[x.EntityId].Height = (int)y;
                m_sync.SendBeaconTerminalSettings(x.EntityId);
            };

            heightSlider.Writer = (x, y) =>
            {
                if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                    BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                y.Append(BeaconTerminalSettings[x.EntityId].Height.ToString() + "m");
            };

            m_customBeaconControls.Add(heightSlider);

            CreateSliderActions("Height", heightSlider, (int)Settings.AreaBeaconMinSize, (int)Settings.AreaBeaconMaxSize);

            // -- Width
            var widthSlider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyProjector>("WidthSlider");
            widthSlider.Title = MyStringId.GetOrCompute("Area Width");
            widthSlider.Tooltip = MyStringId.GetOrCompute("Width of area this beacon scans");
            widthSlider.SetLimits(Settings.AreaBeaconMinSize, Settings.AreaBeaconMaxSize);
            widthSlider.Getter = (x) =>
            {
                if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                    BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                return BeaconTerminalSettings[x.EntityId].Width;
            };

            widthSlider.Setter = (x, y) =>
            {
                if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                    BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                BeaconTerminalSettings[x.EntityId].Width = (int)y;
                m_sync.SendBeaconTerminalSettings(x.EntityId);
            };

            widthSlider.Writer = (x, y) =>
            {
                if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                    BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                y.Append(BeaconTerminalSettings[x.EntityId].Width.ToString() + "m");
            };

            m_customBeaconControls.Add(widthSlider);

            CreateSliderActions("Width", widthSlider, (int)Settings.AreaBeaconMinSize, (int)Settings.AreaBeaconMaxSize);

            // -- Depth
            var depthSlider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyProjector>("DepthSlider");
            depthSlider.Title = MyStringId.GetOrCompute("Area Depth");
            depthSlider.Tooltip = MyStringId.GetOrCompute("Depth of area this beacon scans");
            depthSlider.SetLimits(Settings.AreaBeaconMinSize, Settings.AreaBeaconMaxSize);
            depthSlider.Getter = (x) =>
            {
                if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                    BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                return BeaconTerminalSettings[x.EntityId].Depth;
            };

            depthSlider.Setter = (x, y) =>
            {
                if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                    BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                BeaconTerminalSettings[x.EntityId].Depth = (int)y;
                m_sync.SendBeaconTerminalSettings(x.EntityId);
            };

            depthSlider.Writer = (x, y) =>
            {
                if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                    BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                y.Append(BeaconTerminalSettings[x.EntityId].Depth.ToString() + "m");
            };

            m_customBeaconControls.Add(depthSlider);

            CreateSliderActions("Depth", depthSlider, (int)Settings.AreaBeaconMinSize, (int)Settings.AreaBeaconMaxSize);

            // -- OffsetX
            var offsetXSlider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyProjector>("OffsetXSlider");
            offsetXSlider.Title = MyStringId.GetOrCompute("Area Offset X");
            offsetXSlider.Tooltip = MyStringId.GetOrCompute("X Offset of area this beacon scans");
            offsetXSlider.SetLimits(-((int)(Settings.AreaBeaconMaxSize)), ((int)(Settings.AreaBeaconMaxSize)));
            offsetXSlider.Getter = (x) =>
            {
                if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                    BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                return BeaconTerminalSettings[x.EntityId].OffsetX;
            };

            offsetXSlider.Setter = (x, y) =>
            {
                if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                    BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                BeaconTerminalSettings[x.EntityId].OffsetX = (int)y;
                m_sync.SendBeaconTerminalSettings(x.EntityId);
            };

            offsetXSlider.Writer = (x, y) =>
            {
                if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                    BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                y.Append(BeaconTerminalSettings[x.EntityId].OffsetX.ToString() + "m");
            };

            m_customBeaconControls.Add(offsetXSlider);

            CreateSliderActions("OffsetX", offsetXSlider, -((int)(Settings.AreaBeaconMaxSize)), ((int)(Settings.AreaBeaconMaxSize)));

            // -- OffsetY
            var offsetYSlider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyProjector>("OffsetYSlider");
            offsetYSlider.Title = MyStringId.GetOrCompute("Area Offset Y");
            offsetYSlider.Tooltip = MyStringId.GetOrCompute("Y Offset of area this beacon scans");
            offsetYSlider.SetLimits(-((int)(Settings.AreaBeaconMaxSize)), ((int)(Settings.AreaBeaconMaxSize)));
            offsetYSlider.Getter = (x) =>
            {
                if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                    BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                return BeaconTerminalSettings[x.EntityId].OffsetY;
            };

            offsetYSlider.Setter = (x, y) =>
            {
                if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                    BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                BeaconTerminalSettings[x.EntityId].OffsetY = (int)y;
                m_sync.SendBeaconTerminalSettings(x.EntityId);
            };

            offsetYSlider.Writer = (x, y) =>
            {
                if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                    BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                y.Append(BeaconTerminalSettings[x.EntityId].OffsetY.ToString() + "m");
            };

            m_customBeaconControls.Add(offsetYSlider);

            CreateSliderActions("OffsetY", offsetYSlider, -((int)(Settings.AreaBeaconMaxSize)), ((int)(Settings.AreaBeaconMaxSize)));

            // -- OffsetZ
            var offsetZSlider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyProjector>("OffsetZSlider");
            offsetZSlider.Title = MyStringId.GetOrCompute("Area Offset Z");
            offsetZSlider.Tooltip = MyStringId.GetOrCompute("Z Offset of area this beacon scans");
            offsetZSlider.SetLimits(-((int)(Settings.AreaBeaconMaxSize)), ((int)(Settings.AreaBeaconMaxSize)));
            offsetZSlider.Getter = (x) =>
            {
                if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                    BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                return BeaconTerminalSettings[x.EntityId].OffsetZ;
            };

            offsetZSlider.Setter = (x, y) =>
            {
                if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                    BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                BeaconTerminalSettings[x.EntityId].OffsetZ = (int)y;
                m_sync.SendBeaconTerminalSettings(x.EntityId);
            };

            offsetZSlider.Writer = (x, y) =>
            {
                if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                    BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                y.Append(BeaconTerminalSettings[x.EntityId].OffsetZ.ToString() + "m");
            };

            m_customBeaconControls.Add(offsetZSlider);

            CreateSliderActions("OffsetZ", offsetZSlider, -((int)(Settings.AreaBeaconMaxSize)), ((int)(Settings.AreaBeaconMaxSize)));

            // -- RotationX
            var rotationXSlider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyProjector>("RotationXSlider");
            rotationXSlider.Title = MyStringId.GetOrCompute("Area Rotation X");
            rotationXSlider.Tooltip = MyStringId.GetOrCompute("X Rotation of area this beacon scans");
            rotationXSlider.SetLimits(0, 359);
            rotationXSlider.Getter = (x) =>
            {
                if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                    BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                return BeaconTerminalSettings[x.EntityId].RotationX;
            };

            rotationXSlider.Setter = (x, y) =>
            {
                if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                    BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                BeaconTerminalSettings[x.EntityId].RotationX = (int)y;
                m_sync.SendBeaconTerminalSettings(x.EntityId);
            };

            rotationXSlider.Writer = (x, y) =>
            {
                if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                    BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                y.Append(BeaconTerminalSettings[x.EntityId].RotationX.ToString() + "°");
            };

            m_customBeaconControls.Add(rotationXSlider);

            CreateSliderActions("RotationX", rotationXSlider, 0, 359, true);

            // -- RotationY
            var rotationYSlider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyProjector>("RotationYSlider");
            rotationYSlider.Title = MyStringId.GetOrCompute("Area Rotation Y");
            rotationYSlider.Tooltip = MyStringId.GetOrCompute("Y Rotation of area this beacon scans");
            rotationYSlider.SetLimits(0, 359);
            rotationYSlider.Getter = (x) =>
            {
                if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                    BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                return BeaconTerminalSettings[x.EntityId].RotationY;
            };

            rotationYSlider.Setter = (x, y) =>
            {
                if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                    BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                BeaconTerminalSettings[x.EntityId].RotationY = (int)y;
                m_sync.SendBeaconTerminalSettings(x.EntityId);
            };

            rotationYSlider.Writer = (x, y) =>
            {
                if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                    BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                y.Append(BeaconTerminalSettings[x.EntityId].RotationY.ToString() + "°");
            };

            m_customBeaconControls.Add(rotationYSlider);

            CreateSliderActions("RotationY", rotationYSlider, 0, 359, true);

            // -- RotationZ
            var rotationZSlider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyProjector>("RotationZSlider");
            rotationZSlider.Title = MyStringId.GetOrCompute("Area Rotation Z");
            rotationZSlider.Tooltip = MyStringId.GetOrCompute("Z Rotation of area this beacon scans");
            rotationZSlider.SetLimits(0, 359);
            rotationZSlider.Getter = (x) =>
            {
                if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                    BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                return BeaconTerminalSettings[x.EntityId].RotationZ;
            };

            rotationZSlider.Setter = (x, y) =>
            {
                if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                    BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                BeaconTerminalSettings[x.EntityId].RotationZ = (int)y;
                m_sync.SendBeaconTerminalSettings(x.EntityId);
            };

            rotationZSlider.Writer = (x, y) =>
            {
                if (!BeaconTerminalSettings.ContainsKey(x.EntityId))
                    BeaconTerminalSettings.Add(x.EntityId, new NaniteBeaconTerminalSettings());

                y.Append(BeaconTerminalSettings[x.EntityId].RotationZ.ToString() + "°");
            };

            m_customBeaconControls.Add(rotationZSlider);
            CreateSliderActions("RotationZ", rotationZSlider, 0, 359, true);

            // Range slider
            var detectRangeSlider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyOreDetector>("Range");
            detectRangeSlider.Title = MyStringId.GetOrCompute("Range");
            detectRangeSlider.Tooltip = MyStringId.GetOrCompute("Maximum detection range");
            detectRangeSlider.SetLimits(0, 350);
            detectRangeSlider.Getter = (x) =>
            {
                return (x.GameLogic as LargeNaniteOreDetectorLogic).Detector.Range;
            };

            detectRangeSlider.Setter = (x, y) =>
            {
                (x.GameLogic as LargeNaniteOreDetectorLogic).Detector.Range = y;
            };

            detectRangeSlider.Writer = (x, y) =>
            {
                y.Append($"{Math.Round((x.GameLogic as LargeNaniteOreDetectorLogic).Detector.Range)} m");
            };
            m_customOreDetectorControls.Add(detectRangeSlider);


            var showScanRadius = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, Ingame.IMyOreDetector>("ShowScanRange");
            showScanRadius.Title = MyStringId.GetOrCompute("Display Scan range");
            showScanRadius.Tooltip = MyStringId.GetOrCompute("When checked, it will show you the scan range this detector covers");
            showScanRadius.Getter = (x) =>
            {
                return (x.GameLogic as LargeNaniteOreDetectorLogic).Detector.ShowScanRadius;
            };
            showScanRadius.Setter = (x, y) =>
            {
                (x.GameLogic as LargeNaniteOreDetectorLogic).Detector.ShowScanRadius = y;
            };
            m_customOreDetectorControls.Add(showScanRadius);

            var separate = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, Ingame.IMyOreDetector>("Separate");
            m_customOreDetectorControls.Add(separate);

            var oreList = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, Ingame.IMyOreDetector>("OreList");
            oreList.Title = MyStringId.GetOrCompute("Select Desired Ores: ");
            oreList.Multiselect = true;
            oreList.VisibleRowsCount = 8;
            oreList.ListContent = (block, list, selected) =>
            {
                var possibleOreList = (block.GameLogic as LargeNaniteOreDetectorLogic).Detector.GetTerminalOreList();
                list.AddList(possibleOreList);
                foreach (var item in (block.GameLogic as LargeNaniteOreDetectorLogic).Detector.OreListSelected)
                {
                    var listItem = possibleOreList.FirstOrDefault(ore => ore.Text.ToString() == item);
                    if (listItem != null)
                        selected.Add(listItem);
                }
            };
            oreList.ItemSelected = (block, selected) =>
            {
                List<string> config = new List<string>();
                foreach (var item in selected)
                    config.Add(item.Text.ToString());
                (block.GameLogic as LargeNaniteOreDetectorLogic).Detector.OreListSelected = config;
            };
            oreList.Visible = (x) =>
            {
                return (x.GameLogic as LargeNaniteOreDetectorLogic).Detector.HasFilterUpgrade;
            };
            m_customOreDetectorControls.Add(oreList);
        }

        private void CreateSliderActions(string sliderName, IMyTerminalControlSlider slider, int minValue, int maxValue, bool wrap = false)
        {
            var heightSliderActionInc = MyAPIGateway.TerminalControls.CreateAction<IMyProjector>(string.Format("{0}SliderAction_Increase", sliderName));
            heightSliderActionInc.Name = new StringBuilder(string.Format("{0} Increase", sliderName));
            heightSliderActionInc.Icon = @"Textures\GUI\Icons\Actions\Increase.dds";
            heightSliderActionInc.Enabled = (x) => true;
            heightSliderActionInc.Action = (x) =>
            {
                if (slider.Getter(x) < maxValue)
                    slider.Setter(x, slider.Getter(x) + 1);
                else if (wrap)
                    slider.Setter(x, minValue);
            };
            m_customBeaconActions.Add(heightSliderActionInc);

            var heightSliderActionDec = MyAPIGateway.TerminalControls.CreateAction<IMyProjector>(string.Format("{0}SliderAction_Decrease", sliderName));
            heightSliderActionDec.Name = new StringBuilder(string.Format("{0} Decrease", sliderName));
            heightSliderActionDec.Icon = @"Textures\GUI\Icons\Actions\Decrease.dds";
            heightSliderActionDec.Enabled = (x) => true;
            heightSliderActionDec.Action = (x) =>
            {
                if (slider.Getter(x) > minValue)
                    slider.Setter(x, slider.Getter(x) - 1);
                else if (wrap)
                    slider.Setter(x, maxValue);
            };
            m_customBeaconActions.Add(heightSliderActionDec);
        }

        private void CustomActionGetter(IMyTerminalBlock block, List<IMyTerminalAction> actions)
        {
            if (block.BlockDefinition.SubtypeName == "LargeNaniteAreaBeacon")
            {
                actions.Clear();
                actions.AddRange(m_customBeaconActions);
                return;
            }
        }

        private void CustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            Logging.Instance.WriteLine($"CustomControlGetter : {block.BlockDefinition.SubtypeName}");
            if (block.BlockDefinition.SubtypeName == "LargeNaniteAreaBeacon")
            {
                controls.RemoveRange(controls.Count - 17, 16);
                controls.AddRange(m_customBeaconControls);
                return;
            }
            else if (block.BlockDefinition.TypeId == typeof(MyObjectBuilder_Assembler))
            {
                controls.Add(m_customAssemblerControl);
                return;
            }
            else if (block.BlockDefinition.SubtypeName == "NaniteUltrasonicHammer")
            {
                controls.RemoveAt(controls.Count - 1);
                controls.RemoveAt(controls.Count - 1);
                foreach (var item in m_customHammerControls)
                    controls.Add(item);

                return;
            }
            else if (block.BlockDefinition.SubtypeName == "LargeNaniteControlFacility")
            { 
                controls.RemoveAt(controls.Count - 1);
                    // Remove "Help Others" checkbox, since the block is a ShipWelder

                foreach (var item in m_customControls)
                    controls.Add(item);
            }
            else if (block.BlockDefinition.SubtypeName == "LargeNaniteOreDetector")
            {
                controls.RemoveRange(controls.Count - 2, 2);
                (m_customOreDetectorControls[0] as IMyTerminalControlSlider).SetLimits(0, (block.GameLogic as LargeNaniteOreDetectorLogic).Detector.MaxRange);
                controls.AddRange(m_customOreDetectorControls);
                return;
            }           
        }

        private void MessageEntered(string messageText, ref bool sendToOthers)
        {
            bool donothing = false;
            string message = "";
            string title = "";

            try
            {

            switch (messageText.ToLower())
            {
case "/nanite changelog":
title = "Changelog";
message = @"
VERSION 2.0! Jan. 6, 2019 --->

  - All target processing moved to parallel for better performance
  - Code optimized and made more stable for dedicated servers
  - New models
  - Old models have a rusty look. They can be torn down to retrieve parts
  - New mining logic and ore detector block
  - To use, install a Nanite Ore Detector near a Nanite Facility
  - Move the whole setup near some ore, and let it do its magic
  - For better control and more ore detection, install the following
  - Ore detector scanning upgrades (up to 2)
  - Ore detector filter upgrade (only 1)
  - Ore detector range upgrades
  - These upgrades are installed on the ore detector itself
  - Nearby, friendly facilities now automatically share
    upgrades and grid inventories
  - New help documentation. Type: /nanite help
  - New logging system for admins. For info, type: /nanite help config
  - Projector upgrade removed. Construction upgrade now also
    affects projection nanites
";
break;

case "/nanite help":
title = "Help";
message = $@"
<--- Commands --->

/nanite help
/nanite help basics
/nanite help assemblers
/nanite help beacons
/nanite help colors
{(NaniteConstructionManager.Settings.CleanupEnabled ? "/nanite help cleanup" : "")}
{(NaniteConstructionManager.Settings.ConstructionEnabled ? "/nanite help construction OR /nanite help repair" : "")}
{(NaniteConstructionManager.Settings.ProjectionEnabled ? "/nanite help projections" : "")}
{(NaniteConstructionManager.Settings.DeconstructionEnabled ? "/nanite help deconstruction" : "")}
{(NaniteConstructionManager.Settings.MedicalEnabled ? "/nanite help medical" : "")}
{(NaniteConstructionManager.Settings.MiningEnabled ? "/nanite help mining" : "")}
/nanite help upgrades
/nanite help cooperation
/nanite changelog
/nanite credits
";
break;

case "/nanite help upgrades":

title = "Upgrades";
message = $@"
<--- Improving Performance --->

Upgrades allow the player to fine tune the capabilities of both
the Nanite Control Facility and the Nanite Ore Detector.
Here's what they do.

<--- Nanite Control Facility Upgrades --->

Construction: Increases construction/repair nanites by {NaniteConstructionManager.Settings.ConstructionNanitesPerUpgrade}
and projection nanites by {NaniteConstructionManager.Settings.ProjectionNanitesPerUpgrade}.

Deconstruction: Increases deconstruction nanites by {NaniteConstructionManager.Settings.DeconstructionNanitesPerUpgrade}.

Cleanup: Increases cleanup nanites by {NaniteConstructionManager.Settings.CleanupNanitesPerUpgrade}.

Medical: Increases medical nanites by {NaniteConstructionManager.Settings.MedicalNanitesPerUpgrade}.

Mining: Increases mining nanites by {NaniteConstructionManager.Settings.MiningNanitesPerUpgrade}.

Speed: Reduces nanite travel time by {NaniteConstructionManager.Settings.SpeedIncreasePerUpgrade} seconds.

Power: Reduces nanite power consumption by {NaniteConstructionManager.Settings.PowerDecreasePerUpgrade}MW.


<--- Nanite Ore Detector Upgrades --->

Range: Increases range by {NaniteConstructionManager.Settings.OreDetectorRangePerUpgrade}m.

Power: Decreases power usage by {NaniteConstructionManager.Settings.OreDetectorPowerPercentReducedPerEfficiencyUpgrade * 100} percent.

Scanning: Allows rare ores to be detected (max 2 upgrades).

Filter: Allows selection of ore data to be stored (max 1 upgrade).

";
break;

case "/nanite credits":

title = "Credits";
message = $@"
<--- Meet the Developers --->

- Nukeguard -
Modeling, textures, block definitions, distribution

- Tysis -
Programming of original mod

- Fank -
Programming, concept/implementation focus

- Splen -
Programming, optimization/performance focus
Documentation/help/tutorials

<--- GitHub --->

https://github.com/nukeguard/NaniteConstructionSystem
Please post any bug reports, feature suggestions and other
issues here. Include your log files and config for bug
reports. No exceptions.

<--- Testing Server --->

Splen's Server, STC Trading Co., will always be using the
latest development version of Nanite Control Facility.
If you want to test new features before they appear in the
live version, join us! Get the server address, rules and
more information at https://discord.gg/neAUzaq

<--- A Sincere Thanks --->

Thank you for downloading and supporting this mod. We've
all worked very hard on it. Thanks for making the Space
Engineers community so great and giving us the opportunity
to work on one of the oldest, most well-known and downloaded mods.

And of course, thank you to Keen Software House for not giving
up after so many years of development, even after the peak
popularity has died down. Your commitment to your community has
given us the motivation to keep this mod alive.

";
break;

case "/nanite help cooperation":

title = "Facility Cooperation";
message = $@"
<--- Working Together --->

Friendly Nanite Control Facilities within {NaniteConstructionManager.Settings.MasterSlaveDistance}m of each
other will automatically join together and combine upgrades and
target scanning to both increase performance and productivity.

All settings within individual facilities are respected and no
additional configuration is needed. It should mostly feel like
nothing has changed, but you may notice a few things:

<--- Shared Resources --->

Inventories are shared between Facilities as well as their grids
and subgrids. That means that if two separate grids with
individual conveyor systems and cargo containers and their own
Nanite Control Facilities are floating nearby each other, they
may join up and be able to pull construction/repair and
projection components for jobs from each other's inventories.
This may or may not be desired, but for now, it's not
configurable. Just be aware of it.

Conversely, items retrieved from deconstruction jobs, cleanup
jobs and mining will sometimes appear in friendly facilities'
inventories. This is normal.

In the future, it may be possible for the developers to add
better support for inventory operations.
";
break;

case "/nanite help mining":

if (!NaniteConstructionManager.Settings.MiningEnabled)
    donothing = true;

title = "Mining";
message = $@"
<--- Mining Nanites --->

When this box is checked in a Nanite Control Facility, it will scan
for nearby Nanite Ore Detectors within {NaniteConstructionManager.Settings.OreDetectorToNaniteFactoryCommunicationDistance}m.
If an ore detector is found, the facility will attempt to download
any ore target information from the ore detector. This usually takes
a few seconds.

A Nanite Ore Detector is very different from the built-in ore
detectors that you are probably familiar with. Unlike the vanilla
ore detectors, a Nanite Ore Detector does NOT provide the player
with on-screen locations of ore. Instead, the Nanite Ore Detector
carefully scans nearby voxels (asteroids or planets) for their
material content, then saves the information in its onboard
data storage.

These blocks are fairly large (3x5x3) and also have eight upgrade
slots which can take up an additional two spaces on each side.
Plan accordingly.

There's no need to connect the ore detector to any conveyor system.
The only thing it needs is a fair amount of power. All data is
transferred wirelessly: It doesn't even have to be on the same
grid as any Nanite Control Facility.

Once your Nanite Ore Detector is built, consider installing some
upgrades. Nanite Ore Detector Scanning Upgrades allow the ore
detector to find more valuable materials, such as gold and
platinum. Only two are needed to maximize the scanning frequency.

Also consider installing a single Nanite Ore Detector Filter
Upgrade, which allows the user to select which ore location data
will be stored. This is good for filtering out more common materials
like stone or ice. Simple highlight the ores in the list that should
be stored (CTRL + click by default).

Install as many Nanite Ore Detector Range Upgrades as
desired. Each one will increase the detector's maximum range by {NaniteConstructionManager.Settings.OreDetectorRangePerUpgrade}m.

Actual range scanned is controlled by a slider in the control panel.
Larger ranges will take longer to scan the area, so be sure to only
scan what is needed to save time. To visualize the scanning area,
a convenient checkbox in the ore detector will project a spherical
measurement of the scanning range.

Finally, consider installing Nanite Ore Detector Power Efficiency
Upgrades if power consumption is a concern. Each upgrade will reduce
the total power consumed by {NaniteConstructionManager.Settings.OreDetectorPowerPercentReducedPerEfficiencyUpgrade * 100} percent.

ALL of the above mentioned upgrades are installed on the Nanite
Ore Detector itself, NOT on the Nanite Control Facility.

WARNING: Multiple Nanite Ore Detectors within {NaniteConstructionManager.Settings.OreDetectorToNaniteFactoryCommunicationDistance}m
of each other will shut down automatically, as their radio waves
will block out each others' signals. Only one Ore Detector is
needed, even for many facilities within range. Once any problems
are rectified, restart the Ore Detector by switching it back on
in the control panel.

Once the Ore Detector has the desired upgrades installed and has
been configured properly in the control panel, turn it on. The
scanning radar in the middle of the detector will begin to quickly
spin. Monitor the progress of the scanning by viewing the info
terminal in the right side of the control panel.

If properly configured, nearby, online Nanite Control Facilities
will produce RED nanites, which will then travel to the desired
ores and extract them with surgical precision. No undesired
materials will be disturbed, even if the ore vein is completely
surrounded by stone, dirt or ice.

To make the nanites travel faster, install Nanite Speed Upgrades
on the facility. To produce more mining nanites at once,
install Nanite Mining Upgrades on the facility.

For more information about Nanite Upgrades, type in chat:
/nanite help upgrades

<--- Troubleshooting --->

If things aren't working as expected, check the factory itself
in the control panel. Scroll down on the right side info
box to see the status of the factory and the mining targets.

Ensure the proper checkbox is enabled on the facility itself
and that the facility is within {NaniteConstructionManager.Settings.MiningMaxDistance}m of the
desired ore.

The facility will attempt to automatically clear its inventory
space if needed to make room for mined ore. Please ensure that
the conveyor system is properly connected to cargo containers
or refineries that can receive ore if needed. Please note
that newly added cargo containers may not be immediately detected
by the Nanite Control Facility. Please give it a few minutes
to rescan the grid when new blocks are installed.
";
break;

case "/nanite help medical":

if (!NaniteConstructionManager.Settings.MedicalEnabled)
    donothing = true;

title = "Medical";
message = $@"
<--- Medical Nanites --->

When this box is checked in a Nanite Control Facility, it will scan
for nearby players within {NaniteConstructionManager.Settings.MedicalMaxDistance}m of the facility.
If an injured player is found, the facility will produce
WHITE nanites that will slowly increase the player's health.

To make the nanites travel faster, install Nanite Speed Upgrades
on the facility. To produce more medical nanites at once,
install Nanite Medical Upgrades on the facility.

For more information about Nanite Upgrades, type in chat:
/nanite help upgrades

<--- Troubleshooting --->

If things aren't working as expected, check the factory itself
in the control panel. Scroll down on the right side info
box to see the status of the factory and the medical targets.

Ensure the proper checkbox is enabled on the facility itself
and that the injured player is within the range described above.
";
break;

case "/nanite help deconstruction":

if (!NaniteConstructionManager.Settings.DeconstructionEnabled)
    donothing = true;

title = "Deconstruction";
message = $@"
<--- Deconstruction Nanites --->

When this box is checked in a Nanite Control Facility, it will scan
for nearby Nanite Beacons or Nanite Area Beacons for grids that
are marked for deconstruction.

For more info on properly setting up beacons, type in chat:
/nanite help beacons

When a potential grid is found, the Facility scans the blocks on
the grid to determine the best possible order for deconstruction,
saving the beacon itself for last, if one exists. Then, CYAN
nanites will be produced by the facility. They will move to the
target blocks, spend some time grinding, and then turn GREEN
if the grind was successful or RED on failure.

For more information on the colors of the factory and nanite:
/nanite help colors

The components recieved from the grind will then be moved to
the facility's inventory. If there's no room, the components
will appear floating where the block once was.

To make the nanites travel faster, install Nanite Speed Upgrades
on the facility. To produce more deconstruction nanites at once,
install Nanite Deconstruction Upgrades on the facility.

For more information about Nanite Upgrades, type in chat:
/nanite help upgrades

<--- Troubleshooting --->

If things aren't working as expected, check the factory itself
in the control panel. Scroll down on the right side info
box to see the status of the factory and the deconstruction targets.

To ensure components can be moved to the Facility, make sure
there's enough inventory space in the facility itself and that
the conveyor system is properly connected. Please note that
new inventory blocks, such as cargo containers, may not
immediately be detected by the facility. Please give the
facility a few minutes to detect new blocks added to the grid.
";
break;

case "/nanite help projections":

if (!NaniteConstructionManager.Settings.ProjectionEnabled)
    donothing = true;

title = "Projections";
message = $@"
<--- Projection Nanites --->

When this box is checked in a Nanite Control Facility, it will scan
the Facility's grid group (which includes connected subgrids)
for projectors that are currently projecting blueprints.

When a potential target is found, the Facility scans the connected
inventory of the grid and subgrids for the parts needed to add the
first component to each block on the blueprint, starting with a
block that has a physical connection to an existing block.
If the parts cannot be found and the proper settings are
enabled, the Facility will also attempt to queue up these parts
for production in an assembler.

For more information on configuring assemblers, type in chat:
/nanite help assemblers

To build on nearby grids that are not connected to the facility's
grid group, consider using a Nanite Beacon. Fore info, type:
/nanite help beacons

If the needed components are available, PINK nanites will be
created by the facility and move to the target block. After
spending some time welding, GREEN nanites indicate success,
and RED nanites indicate failure.

For more information on the colors of the factory and nanite:
/nanite help colors

To make the nanites travel faster, install Nanite Speed Upgrades
on the facility. To produce more projection nanites at once,
install Nanite Construction Upgrades on the facility.

For more information about Nanite Upgrades, type in chat:
/nanite help upgrades

<--- Troubleshooting --->

If things aren't working as expected, check the factory itself
in the control panel. Scroll down on the right side info
box to see the status of the factory and the projection targets.

To ensure components can be moved to the Facility, make sure
there's enough inventory space in the facility itself and that
the conveyor system is properly connected. Please note that
new inventory blocks, such as cargo containers, may not
immediately be detected by the facility. Please give the
facility a few minutes to detect new blocks added to the grid.
";
break;

case "/nanite help repair":
case "/nanite help construction":

if (!NaniteConstructionManager.Settings.ConstructionEnabled)
    donothing = true;

title = "Construction/Repair";
message = $@"
<--- Construction/Repair Nanites --->

When this box is checked in a Nanite Control Facility, it will scan
the Facility's grid group (which includes connected subgrids)
for deformed, incomplete or damaged blocks.

When a potential target is found, the Facility scans the connected
inventory of the grid and subgrids for the parts needed to do the
job. If the parts cannot be found and the proper settings are
enabled, the Facility will also attempt to queue up these parts
for production in an assembler.

For more information on configuring assemblers, type in chat:
/nanite help assemblers

To repair nearby grids that are not connected to the facility's
grid group, consider using a Nanite Beacon. Fore info, type:
/nanite help beacons

If the needed components are available, BLUE nanites will be
created by the facility and move to the target block. After
spending some time welding, GREEN nanites indicate success,
and RED nanites indicate failure.

For more information on the colors of the factory and nanite:
/nanite help colors

To make the nanites travel faster, install Nanite Speed Upgrades
on the facility. To produce more projection nanites at once,
install Nanite Construction Upgrades on the facility.

For more information about Nanite Upgrades, type in chat:
/nanite help upgrades

<--- Troubleshooting --->

If things aren't working as expected, check the factory itself
in the control panel. Scroll down on the right side info
box to see the status of the factory and the repair targets.

To ensure components can be moved to the Facility, make sure
there's enough inventory space in the facility itself and that
the conveyor system is properly connected. Please note that
new inventory blocks, such as cargo containers, may not
immediately be detected by the facility. Please give the
facility a few minutes to detect new blocks added to the grid.
";
break;

case "/nanite help cleanup":

if (!NaniteConstructionManager.Settings.CleanupEnabled)
    donothing = true;

title = "Cleanup";
message = $@"
<--- Cleanup Nanites --->

When this box is checked in a Nanite Control Facility, it will scan
the area within {NaniteConstructionManager.Settings.CleanupMaxDistance}m from the Facility
for loose objects that can normally be picked up by the player.

If a target is found, YELLOW nanites will be produced by the
facility and move toward the target. The item will then be
added to the Facility's inventory.

If the Nanite Facility's inventory is over 75% full, the facility
will flush its inventory into any free cargo containers on the grid.
This is to make room for the objects that the cleanup nanites are
trying to pick up.

In the future, this functionality will be more configurable and
obey sorters and other inventory rules.

To reduce the time it takes for nanites to clean up objects,
install Nanite Speed Upgrades on any of the facility's eight
upgrade slots.

To increase the amount of cleanup nanites the factory can
produce simultaneously, install Nanite Cleanup Upgrades.

For more detailed information on upgrades, type in chat:
/nanite help upgrades

<--- Troubleshooting --->

If things aren't working as expected, check the factory itself
in the control panel. Scroll down on the right side info
box to see the status of the factory and the cleanup targets.

To ensure items can be moved to the Facility, make sure
there's enough inventory space in the facility itself and that
the conveyor system is properly connected. Please note that
new inventory blocks, such as cargo containers, may not
immediately be detected by the facility. Please give the
facility a few minutes to detect new blocks added to the grid.
";
break;

case "/nanite help beacons":
title = "Beacons";
message = $@"
<--- Nanite Beacons --->

Nanite Control Facilities will automatically queue jobs for targets
on the same grid or attached as subgrids, but what about for other
friendly grids nearby? This is where Beacons come in handy.

There are three beacons: Repair, projection and deconstruction.

REPAIR: When built on a grid, Nanite Facilities within {NaniteConstructionManager.Settings.ConstructionMaxBeaconDistance}m
will scan that grid for blocks that need repaired.

PROJECTION: These should be built on a grid that has a projector that
is projecting a blueprint to be built. All Nanite Control Facilities
within {NaniteConstructionManager.Settings.ProjectionMaxBeaconDistance}m will build that projection.

DECONSTRUCTION: When this beacon is built on a grid, Facilities within
{NaniteConstructionManager.Settings.DeconstructionMaxDistance}m will grind down every block on that grid.

Please note that control panel checkboxes on the Nanite Control
Facility must still be properly configured for the beacons to be
detected.

<--- Area Beacons --->

An Area Beacon is a special beacon that defines an entire area,
rather than a specific grid, for operations. Build this block on
any grid within {NaniteConstructionManager.Settings.AreaBeaconMaxDistanceFromNaniteFacility}m from a Nanite Facility.
Then, configure the size and shape of the area in the control panel.
Finally, use the checkboxes to determine what operations should take
place within that area (if you're making a chop shop, ONLY check
Deconstruction or you may get undesired results).
";
break;

case "/nanite help basics":
title = "Basics";
message = $@"
<--- What are Nanites? --->

Nanites are tiny, flying machines that can easily do many things.
Here's what they are currently configured to do:

- Welding: {(NaniteConstructionManager.Settings.ConstructionEnabled ? "Enabled" : "Disabled")}
- Grinding: {(NaniteConstructionManager.Settings.DeconstructionEnabled ? "Enabled" : "Disabled")}
- Mining: {(NaniteConstructionManager.Settings.MiningEnabled ? "Enabled" : "Disabled")}
- Cleanup loose objects: {(NaniteConstructionManager.Settings.CleanupEnabled ? "Enabled" : "Disabled")}
- Build projections: {(NaniteConstructionManager.Settings.ProjectionEnabled ? "Enabled" : "Disabled")}
- Heal players: {(NaniteConstructionManager.Settings.MedicalEnabled ? "Enabled" : "Disabled")}

Nanites are created and given orders in a Nanite Control Facility.
These 'factory' blocks can be built on any grid just like any
other block in Space Engineers.

Many players use nanites to greatly reduce the time spent doing
the more tedious tasks in the game, such as welding hundreds
of armor blocks by hand. More advanced players can use them to
extract ore from asteroids and planets without touching a drill,
set up automatic ship factories using projectors, or create
chop shops that deconstruct grids within range.

<--- Getting Started --->

First, you'll need to build a Nanite Control Facility.

It's usually pretty expensive, so make sure you have a good amount of
resources and a good grasp of production mechanics before you begin.
The large grid version is 3x3x3, with upgrade slots that stick out
an additional block on each side (5x5x3), so plan accordingly.

Build the facility so it has access to your grid's conveyor system.
The large grid facility has five conveyor connections. One is on
the bottom in the very middle. If viewed from the bottom, that side
looks like this:

                    XXX
                    XOX
                    XXX

where O is the conveyor connection.

The others are on each side on the bottom middle like so:

                    XXX
                    XXX
                    UOU

where O is the conveyor connection and U is where upgrades
can be installed. Upgrade slots CANNOT be used for conveyor
connections, and conversely, conveyor connections will not
support upgrades.

For more information about upgrades, type in chat:
/nanite help upgrades

When repairing blocks and building projections, the facility will
use your grid's connected inventory blocks, such as cargo
containers, to find the parts it needs to do its job.

Power availability can also limit the factory's performance.
Make sure you have ample power and fuel available, especially before
adding upgrades or building additional facilities.

<--- Configuration --->

Once your factory is up and running, open the terminal and scroll down
to see the various checkboxes. Here, you can control what the factory
will or wont do. 'Use Assemblers' is off by default. Check this box
to allow the factory to use assemblers on the same conveyor system
to create parts that are needed for jobs. For more information, type:
/nanite help assemblers
";
break;

case "/nanite help assemblers":
title = "Assemblers";
message = @"
<--- Nanite Facility Configuration --->

Open the terminal of your Nanite Control Facility. 'Use Assemblers' is
off by default. Check this box to allow the factory to use assemblers
on the same conveyor system to create parts that are needed for building
jobs (construction/repair and projection building).

<--- Assembler Configuration --->

Next, open the terminals of all assemblers that will be used by the
Nanite Control Facility. Check the 'Nanite Factory Queuing' box.
Then, open the production tab and make sure the assembler is set to
build, not deconstruction.

Please note that all standard rules for assemblers still apply.
They will not manufacture the parts if they are missing the
required raw materials, such as ingots.

If properly configured, your Nanite Control Facility will now
automatically attempt to queue up parts to be manufactured when in
the 'Missing Parts' state (blinking yellow). To see more information
about the colors used in this mod, type in chat:
/nanite help colors
";
break;

case "/nanite help colors":
title = "Colors";
message = @"
<--- Nanite Facility Colors --->

The Nanite Control Facilities color emitters (located on the 'arms' of
the block) will indicate the current status of the facility.

RED means the facility is disabled. You can turn the facility on and
off by accessing the grid control panel from any terminal, including
the 'monitor' on the Nanite Control Facility itself.

DARK PURPLE (with lightning effects) means the facility is active.
The center spinning orb will move up or down depending on the
status of active jobs. When all the way up, the facility will
make crackling sounds and the lightning will appear. This is the
state that actively creates the nanites themselves.

DEEP PINK (BLINKING) means the facility is missing parts for a job.
Either supply the missing components manually, or check out this
command for more information about automatic assembly:
/nanites help assemblers

DARK YELLOW (BLINKING) means the facility does not have enough
power to enter a nominal state. Increase the grid's power by
creating new reactors/batteries/solar panels or adding more fuel.

LIME (BLINKING) means the facility has invalid targets. This state
is a bit of a wildcard as many things can go wrong, such as
unfriendly facilities already working on targets. Fore more
information on this status, open the terminal of the facility
and scroll down in the information area on the right side.

DARK GREEN means the facility is enabled but currently has no
jobs to do. As long as the proper checkboxes are enabled in the
control panel, the facility will always be actively scanning for
jobs to do while in this state. Certain conditions must be met
for certain situations, however. See the help commands for
mining, beacons and area beacons for more information.

<--- Nanite Colors --->

The nanites themselves will have different colors depending on
what job they are doing and what status they have.

GREEN is a nanite returning from a completed task.

BRIGHT RED is a nanite moving to a mining target (ore or stone).
DARK RED is a nanite coming back from a mining target.

YELLOW is a nanite moving to clean up loose items and bring
them back to the facility's inventory.

PINK is a nanite moving to build a projection target.

CYAN is a nanite moving to deconstruct a block.

WHITE is a nanite moving to heal a player.

BLUE is a nanite moving to construct/repair a block.

For non-mining targets, DARK RED means a nanite is returning
from a task that has failed for some reason.

";
break;

default:
donothing = true;
break;
            }

                if (!donothing)
                {
                    MyAPIGateway.Utilities.ShowMissionScreen("Nanite Control Factory", title, "", message);
                    Logging.Instance.WriteLine("Received user command '/nanite changelog'", 1);
                    sendToOthers = false;
                }
            }
            catch (Exception e)
            { Logging.Instance.WriteLine($"Exception while processing chat command:\n{e.ToString()}"); }
        }

        private void LoadSettings()
        {
            try
            {
                m_settings = NaniteSettings.Load();
                UpdateSettingsChanges();
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine(string.Format("LoadSettings error: {0}", ex.ToString()));
            }
        }

        public void UpdateSettingsChanges()
        {
            var def = MyDefinitionManager.Static.GetCubeBlockDefinition(new MyDefinitionId(typeof(MyObjectBuilder_ShipWelder), "LargeNaniteControlFacility"));
            foreach (var item in def.Components)
            {
                item.Count = (int)((float)item.Count * m_settings.FactoryComponentMultiplier);
                if (item.Count < 1)
                    item.Count = 1;
            }

            foreach (var item in MyDefinitionManager.Static.GetAllDefinitions())
            {
                if (item.Id.TypeId == typeof(MyObjectBuilder_UpgradeModule) && item.Id.SubtypeName.Contains("Nanite"))
                {
                    MyCubeBlockDefinition cubeDef = (MyCubeBlockDefinition)item;
                    foreach (var component in cubeDef.Components)
                    {
                        component.Count = (int)((float)component.Count * m_settings.UpgradeComponentMultiplier);
                        if (component.Count < 1)
                            component.Count = 1;
                    }
                }
            }
        }

        private void CleanupOldBlocks()
        {
            HashSet<IMyEntity> grids = new HashSet<IMyEntity>();
            HashSet<IMyCubeGrid> gridsToRemove = new HashSet<IMyCubeGrid>();
            MyAPIGateway.Entities.GetEntities(grids, x => x is IMyCubeGrid);
            foreach(var item in grids)
                if(((IMyCubeGrid)item).DisplayName == "SmallNaniteWelderCube")
                    gridsToRemove.Add((IMyCubeGrid)item);

            foreach (var item in gridsToRemove)
                item.Close();
        }

        /// <summary>
        /// This is required to remove blocks that are supposed to not exist on the clients.  Clients don't need actual welders as welding
        /// happen on the server. (This is obsolete, but leaving for now)
        /// </summary>
        /// <param name="obj"></param>
        private void Entities_OnEntityAdd(IMyEntity obj)
        {
            if(obj is IMyCubeGrid)
            {
                MyCubeGrid grid = (MyCubeGrid)obj;
                if(grid.DisplayName.Contains("SmallNaniteToolCube"))
                {
                    Logging.Instance.WriteLine("Deleting welder");
                    grid.PositionComp.Scale = 0.0001f;

                    if (grid.Physics != null)
                        grid.Physics.Enabled = false;

                    if (!obj.Closed)
                        obj.Close();
                }
            }
        }

        public static List<NaniteConstructionBlock> GetConstructionBlocks(IMyCubeGrid grid)
        {
            List<NaniteConstructionBlock> blockList = new List<NaniteConstructionBlock>();

            foreach (var item in NaniteBlocks)
                if (MyAPIGateway.GridGroups.GetGroup(grid, GridLinkTypeEnum.Physical).Contains(item.Value.ConstructionBlock.CubeGrid) 
                  && !blockList.Contains(item.Value))
                    blockList.Add(item.Value);

            return blockList;
        }

        public override void SaveData()
        {
            m_terminalSettingsManager.Save();
        }
    }
}
