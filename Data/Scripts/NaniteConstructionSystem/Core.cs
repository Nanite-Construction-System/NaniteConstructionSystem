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

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class NaniteConstructionManager : MySessionComponentBase
    {
        public static NaniteConstructionManager Instance;

        // Unique storage identifer
        public readonly Guid OreDetectorSettingsGuid = new Guid("7D46082D-747A-45AF-8CD1-99A03E68CF97");

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
                Logging.Instance.WriteLine($"Logging Started");

                if (!Sync.IsServer)
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
            catch (Exception ex) { Logging.Instance.WriteLine($"Exception in BeforeStart: {ex}"); }
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

                //foreach (var item in BeaconList.ToList())
                //    item.Value.Close();

                BeaconList.Clear();

                Logging.Instance.Close();

                //if(Logging.Instance != null)
                //    Logging.Instance.Close();
            }
            catch (Exception ex) { Logging.Instance.WriteLine($"Exception in BeforeStart: {ex}"); }
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
            {
                VRage.Utils.MyLog.Default.WriteLineAndConsole($"Update Error:\n{e.ToString()}");
            }
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
            //MyAPIGateway.TerminalControls.AddControl<Ingame.IMyAssembler>(allowFactoryCheck);

            //// --- Mining Hammer
            //var separate = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, Ingame.IMyOreDetector>("Separate");
            //m_customHammerControls.Add(separate);

            //var oreList = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, Ingame.IMyOreDetector>("OreList");
            //oreList.Title = MyStringId.GetOrCompute("Valid Ores (deselect to ignore): ");
            //oreList.Multiselect = true;
            //oreList.VisibleRowsCount = 8;
            //oreList.ListContent = OreListContent;
            //oreList.ItemSelected = OreListSelected;
            //m_customHammerControls.Add(oreList);

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
            else if (block.BlockDefinition.SubtypeName == "LargeNaniteOreDetector")
            {
                controls.RemoveRange(controls.Count - 2, 2);
                (m_customOreDetectorControls[0] as IMyTerminalControlSlider).SetLimits(0, (block.GameLogic as LargeNaniteOreDetectorLogic).Detector.MaxRange);
                controls.AddRange(m_customOreDetectorControls);
                return;
            }

            if (!(block.BlockDefinition.SubtypeName == "LargeNaniteControlFacility"))
                return;

            foreach (var item in m_customControls)
                controls.Add(item);
        }

        private void MessageEntered(string messageText, ref bool sendToOthers)
        {
            if(messageText.ToLower() == "/nanite")
            {
                string message = @"10/29/2018

-Version 2 release!
-All target processing moved to parallel for better performance
-Tons of code optimizations
-New models!
-Old models have a rusty look. They can be torn down to retrieve parts
-New mining logic and ore detector block
                
02/16/2018
-Fixed settings load on world load - BAM5
-Fixed some texture issues, and cleaned up textures folder
-Adjusted beacon costs and build times
-Fixed beacons working without being built - BAM5
-Added github for posting issues https://github.com/nukeguard/NaniteConstructionSystem/issues

02/06/2018
-Fix for failed compilation for particle effects

09/10/2017
-fixed workshop path for center model
-identified two errors in log, need to figure out what cause is and how to fix.

05/02/2017
- Fixed issues from not being updated in awhile.
- Fixed Projections not building properly.
- Fixed Mining Nanites not properly removing voxels.
- Added area beacons.  Area beacons allow you to setup construction, deconstruction and projection zones.  Ships inside the area created by these beacons will be repaired, deconstructed or
  projections will be built.  (For now, projector must be inside of area beacon zone).
- Updated and fixed models.

6/21/2016
- Added Medical Nanites.  These nanites will heal injured astronauts that are within 300m of the factory.

6/07/2016
- Added Mining Nanites
- Added Nanite Ultrasonic Hammer Ore Locator (NUHOL).  This is used for nanites to locate ore to mine.  Hammers must be placed inside voxels to function.  Their range by default are 20m radius x 100m length (Cylinder pointing down from the bottom of the NUHOL).
- Updated settings
- Fixed a few settings issue

05/29/2016
- Added projection beacon
- Added controls to the Nanite Factory terminal screen
- Added the ability for the factory to queue required components in attached assemblers
- Added controls to assemblers to allow factories to queue items in them

05/10/2016
- Added upgrades for functionality.  Factory functionality now requires upgrades.  Each upgrade adds 3 nanites of that type.  Each factory can have up to 8 upgrades.  Upgrade slots are next to conveyor ports on sides.
- Added a bunch of new settings
- Added repair beacon which allows ships to be repaired remotely
- Added speed upgrade - each upgrade adds 5m/s to nanites as well as dropping min travel time by 1s
- Added power upgrade - each upgrade reduces power usage by 2MW
";
                MyAPIGateway.Utilities.ShowMissionScreen("Nanite Control Factory", "Update", "", message);
                sendToOthers = false;
            }

            if (messageText.ToLower() == "/nanitebug")
            {
                var message = "";

                message += $"{NaniteBlocks.Count} Factories\n\n";
                foreach (var item in NaniteBlocks.ToList())
                {
                    message += $"Factory: {item.Key}:\n";
                    message += $"- Exists: {item.Value.ConstructionBlock != null}\n";

                    var name = "N/A";
                    if (item.Value.ConstructionBlock != null)
                        name = item.Value.ConstructionBlock.CustomName;

                    message += $"- Name: {name}\n";
                    message += $"- Init: {item.Value.Initialized}\n";
                    message += $"- IsUserDefinedLimitReached: {item.Value.IsUserDefinedLimitReached()}\n";

                    message += $"- Targets:\n";
                    foreach (var target in item.Value.Targets)
                    {
                        message += $"-- {target.TargetName}:\n";
                        message += $"--- Enabled: {target.IsEnabled(item.Value)}\n";
                        message += $"--- ComponentsRequired: {target.ComponentsRequired.Count}\n";
                        message += $"--- MaxTargets: {target.GetMaximumTargets()}\n";
                        message += $"--- MinTravelTime: {target.GetMinTravelTime()}\n";
                        message += $"--- GetPowerUsage: {target.GetPowerUsage()}\n";
                        message += $"--- Speed: {target.GetSpeed()}\n";
                        message += $"--- LastInvalidTargetReason: {target.LastInvalidTargetReason}\n";
                        message += $"--- PotentialTargetList: {target.PotentialTargetList.Count}\n";
                        message += $"--- TargetList: {target.TargetList.Count}\n";
                    }

                    message += $"- Particles {item.Value.ParticleManager.Particles.Count}:\n";
                    foreach (var particle in item.Value.ParticleManager.Particles)
                    {
                        message += $"-- Particle:\n";
                        message += $"--- IsCancelled: {particle.IsCancelled}\n";
                        message += $"--- IsCompleted: {particle.IsCompleted}\n";
                        message += $"--- LifeTime: {particle.LifeTime}\n";
                    }

                    message += "\n";
                }

                message += $"ParticleMananger: {NaniteParticleManager.TotalParticleCount} / {NaniteParticleManager.MaxTotalParticles}\n";

                MyAPIGateway.Utilities.ShowMissionScreen("Nanite Control Factory", "Debug", "", message);
                sendToOthers = false;
            }
        }

        private void LoadSettings()
        {
            try
            {
                m_settings = NaniteSettings.Load();
                UpdateSettingsChanges();
            }
            catch(Exception ex)
            {
                Logging.Instance.WriteLine(string.Format("LoadSettings error: {0}", ex.ToString()));
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
