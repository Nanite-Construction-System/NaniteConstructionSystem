using System;
using System.Text;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using VRage.Game.Components;
using Sandbox.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;
using VRage;
using VRage.Network;
using VRageMath;
using VRage.Game;
using Sandbox.Definitions;
using Sandbox.Common.ObjectBuilders;
using Ingame = Sandbox.ModAPI.Ingame;
using Sandbox.Game.Lights;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Voxels;
using Sandbox.Engine.Voxels;
using Sandbox.Game.AI;
using Sandbox.Game.Components;

using NaniteConstructionSystem.Entities;
using NaniteConstructionSystem.Extensions;
using NaniteConstructionSystem.Entities.Beacons;
using NaniteConstructionSystem.Particles;
using NaniteConstructionSystem.Settings;

namespace NaniteConstructionSystem
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class NaniteConstructionManager : MySessionComponentBase
    {
        public static NaniteConstructionManager Instance;

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

        private static HashSet<NaniteBeacon> m_beaconList;
        public static HashSet<NaniteBeacon> BeaconList
        {
            get
            {
                if (m_beaconList == null)
                    m_beaconList = new HashSet<NaniteBeacon>();

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

        private static Dictionary<long, NaniteHammerTerminalSettings> m_hammerTerminalSettings;
        public static Dictionary<long, NaniteHammerTerminalSettings> HammerTerminalSettings
        {
            get
            {
                if (m_hammerTerminalSettings == null)
                    m_hammerTerminalSettings = new Dictionary<long, NaniteHammerTerminalSettings>();

                return m_hammerTerminalSettings;
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

        private static HashSet<NaniteMining> m_miningList;
        public static HashSet<NaniteMining> MiningList
        {
            get
            {
                if (m_miningList == null)
                    m_miningList = new HashSet<NaniteMining>();

                return m_miningList;
            }
        }

        private bool m_initialize = false;
        private TerminalSettings m_terminalSettingsManager = new TerminalSettings();
        private List<IMyTerminalControl> m_customControls = new List<IMyTerminalControl>();
        private IMyTerminalControl m_customAssemblerControl;
        private List<IMyTerminalControl> m_customHammerControls = new List<IMyTerminalControl>();
        private List<IMyTerminalControl> m_customBeaconControls = new List<IMyTerminalControl>();
        private List<IMyTerminalAction> m_customBeaconActions = new List<IMyTerminalAction>();
        private DateTime m_lastPlayer;
        private Action<IMyTerminalBlock> m_oldAction;

        public NaniteConstructionManager()
        {
            Instance = this;
            m_sync = new NaniteConstructionManagerSync();
            m_lastPlayer = DateTime.MinValue;
        }

        public override void UpdateBeforeSimulation()
        {
            if (MyAPIGateway.Session == null)
                return;

            if (!m_initialize)
            {
                m_initialize = true;
                Initialize();
                return;
            }

            try
            {
                ProcessNaniteBlocks();
                ProcessBeaconBlocks();
                ProcessParticleEffects();
                ProcessMiningBlocks();
                //Test();
            }
            catch (Exception ex)
            {
                Logging.Instance.WriteLine(string.Format("Update Error: {0}", ex.ToString()));
            }
        }

        public void Test()
        {
            if (DateTime.Now - m_lastPlayer < TimeSpan.FromSeconds(2))
                return;

            m_lastPlayer = DateTime.Now;
            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);

            foreach(var item in players)
            {
                Logging.Instance.WriteLine(string.Format("Here: {0}", item.DisplayName));
                if (item.Controller.ControlledEntity.Entity != null)
                {

                    foreach (var comp in item.Controller.ControlledEntity.Entity.Components)
                    {
                        if (comp.GetType().Name == "MyCharacterStatComponent")
                        {
                            MyCharacterStatComponent stat = comp as MyCharacterStatComponent;
                            Logging.Instance.WriteLine(string.Format("Player {0}: {1} of {2}", item.DisplayName, stat.Health.Value, stat.Health.MaxValue));

                            if (stat.Health.Value < 100)
                            {
                                MyEntityStat entityStat;
                                stat.Stats.TryGetValue(MyStringHash.GetOrCompute("Health"), out entityStat);
                                entityStat.Value = stat.Health.Value + 5;
                            }                                    
                        }
                    }
                }
            }
        }

        private void ProcessNaniteBlocks()
        {
            foreach (var item in NaniteBlocks.ToList())
            {
                if (item.Value.ConstructionBlock == null || item.Value.ConstructionBlock.Parent == null) // || item.Value.ConstructionBlock.Parent.Physics == null)
                {
                    //Logging.Instance.WriteLine(string.Format("REMOVING Nanite Factory due to parent gone or physicless: {0}", item.Value.ConstructionBlock.EntityId));
                    //item.Value.Unload();
                    //NaniteBlocks.Remove(item.Key);
                    continue;
                }

                item.Value.Update();
            }
        }

        private void ProcessBeaconBlocks()
        {
            foreach(var item in BeaconList.ToList())
            {
                if(item.BeaconBlock.Closed || item.BeaconBlock.CubeGrid.Closed) // || item.BeaconBlock.CubeGrid.Physics == null)
                {
                    Logging.Instance.WriteLine(string.Format("REMOVING {1} Beacon: {0}", item.BeaconBlock.EntityId, item.GetType().Name));
                    item.Close();
                    continue;
                }

                item.Update();
            }
        }

        private void ProcessParticleEffects()
        {
            ParticleManager.Update();
        }

        private void ProcessMiningBlocks()
        {
            foreach(var item in MiningList.ToList())
            {
                if (item.MiningBlock.Closed || item.MiningBlock.CubeGrid.Closed)
                {
                    Logging.Instance.WriteLine(string.Format("REMOVING Mining Hammer: {0}", item.MiningBlock.EntityId));
                    item.Close();
                    continue;
                }

                if (item.MiningBlock.CubeGrid.Physics == null)
                    continue;

                item.Update();
            }
        }

        private void Initialize()
        {
            if (!Sync.IsServer)
            {
                MyAPIGateway.Entities.OnEntityAdd += Entities_OnEntityAdd;
            }

            m_sync.Initialize();

            CleanupOldBlocks();
            LoadSettings();

            MyAPIGateway.Utilities.MessageEntered += MessageEntered;

            InitializeControls();

            m_terminalSettingsManager.Load();

            if(Sync.IsClient)
            {
                m_sync.SendLogin();

                foreach (var item in NaniteBlocks)
                {
                    m_sync.SendNeedTerminalSettings(item.Key);
                }

                foreach (var item in AssemblerBlocks)
                {
                    m_sync.SendNeedAssemblerSettings(item.Value.EntityId);
                }

                foreach (var item in HammerTerminalSettings)
                {
                    m_sync.SendNeedHammerTerminalSettings(item.Key);
                }

                foreach (var item in BeaconTerminalSettings)
                {
                    m_sync.SendNeedBeaconTerminalSettings(item.Key);
                }
            }
        }

        private void InitializeControls()
        {
            MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlGetter;
            MyAPIGateway.TerminalControls.CustomActionGetter += CustomActionGetter;

            // --- Repair Checkbox
            var repairCheck = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, SpaceEngineers.Game.ModAPI.Ingame.IMyOxygenFarm>("AllowRepair");
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

            // --- Projection Checkbox
            var projectionCheck = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, SpaceEngineers.Game.ModAPI.Ingame.IMyOxygenFarm>("AllowProjection");
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

            // --- Cleanup Checkbox
            var cleanupCheck = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, SpaceEngineers.Game.ModAPI.Ingame.IMyOxygenFarm>("AllowCleanup");
            cleanupCheck.Title = MyStringId.GetOrCompute("Cleanup");
            cleanupCheck.Tooltip = MyStringId.GetOrCompute("When checked, the factory will cleanup floating objects, ore, components, or corpses.  It will return the objects back to the factory.");
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

            // --- Deconstruction Checkbox
            var deconstructCheck = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, SpaceEngineers.Game.ModAPI.Ingame.IMyOxygenFarm>("AllowDeconstruct");
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

            // --- Mining Checkbox
            var miningCheck = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, SpaceEngineers.Game.ModAPI.Ingame.IMyOxygenFarm>("AllowMining");
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

            // --- Medical Checkbox
            var medicalCheck = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, SpaceEngineers.Game.ModAPI.Ingame.IMyOxygenFarm>("AllowMedical");
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

            // --- Max Nanites
            var maxNaniteTextBox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, SpaceEngineers.Game.ModAPI.Ingame.IMyOxygenFarm>("MaxNaniteText");
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
            var useAssemblerCheck = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, SpaceEngineers.Game.ModAPI.Ingame.IMyOxygenFarm>("UseAssemblers");
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

            // --- Mining Hammer
            var separate = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, Ingame.IMyOreDetector>("Separate");
            m_customHammerControls.Add(separate);

            var oreList = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, Ingame.IMyOreDetector>("OreList");
            oreList.Title = MyStringId.GetOrCompute("Valid Ores (deselect to ignore): ");
            oreList.Multiselect = true;
            oreList.VisibleRowsCount = 8;
            oreList.ListContent = OreListContent;
            oreList.ItemSelected = OreListSelected;
            m_customHammerControls.Add(oreList);

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

            // -- Allow Repair
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

            // -- Allow Projection
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

            /*
            var blueprintButton = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyProjector>("SelectBlueprint");
            blueprintButton.Title = MyStringId.GetOrCompute("Select Blueprint ...");
            blueprintButton.Tooltip = MyStringId.GetOrCompute("Select a blueprint to project");
            blueprintButton.Action = SelectBlueprint;
            m_customBeaconControls.Add(blueprintButton);
            */

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

        private void SelectBlueprint(IMyTerminalBlock block)
        {
            m_oldAction(block);
        }

        private void OreListSelected(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> list)
        {
            if (!HammerTerminalSettings.ContainsKey(block.EntityId))
                HammerTerminalSettings.Add(block.EntityId, new NaniteHammerTerminalSettings(true));

            HammerTerminalSettings[block.EntityId].SelectedOres.Clear();
            foreach (var item in list)
            {
                HammerTerminalSettings[block.EntityId].SelectedOres.Add(item.Text.ToString());
            }

            m_sync.SendHammerTerminalSettings(block.EntityId);
            block.RefreshCustomInfo();

            // Trigger a refresh
            var detector = block as Ingame.IMyOreDetector;
            var action = detector.GetActionWithName("BroadcastUsingAntennas");
            action.Apply(block);
            action.Apply(block);
        }

        private void OreListContent(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> list, List<MyTerminalControlListBoxItem> selected)
        {
            var miningItem = MiningList.FirstOrDefault(x => x.MiningBlock == block);
            if (miningItem == null)
                return;

            foreach(var item in MyDefinitionManager.Static.GetVoxelMaterialDefinitions().Select(x => x.MinedOre).Distinct())
            {
                MyTerminalControlListBoxItem listItem = new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(item), MyStringId.GetOrCompute(item), null);
                list.Add(listItem);
            }

            if (!HammerTerminalSettings.ContainsKey(block.EntityId))
                HammerTerminalSettings.Add(block.EntityId, new NaniteHammerTerminalSettings(true));

            var oreList = HammerTerminalSettings[block.EntityId];
            foreach(var item in oreList.SelectedOres)
            {
                var listItem = list.FirstOrDefault(x => x.Text.ToString() == item);
                if(listItem != null)
                {
                    selected.Add(listItem);
                }
            }
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
            if(block.BlockDefinition.SubtypeName == "LargeNaniteAreaBeacon")
            {
                if (m_oldAction == null)
                {
                    IMyTerminalControlButton button = null;
                    foreach (var item in controls)
                    {
                        if (item.Id == "Blueprint")
                        {
                            button = item as IMyTerminalControlButton;
                            m_oldAction = button.Action;
                        }
                    }
                }

                controls.RemoveRange(controls.Count - 17, 16);
                controls.AddRange(m_customBeaconControls);
                return;
            }

            if(block.BlockDefinition.TypeId == typeof(MyObjectBuilder_Assembler))
            {
                controls.Add(m_customAssemblerControl);
                return;
            }

            if(block.BlockDefinition.SubtypeName == "NaniteUltrasonicHammer")
            {
                controls.RemoveAt(controls.Count - 1);
                controls.RemoveAt(controls.Count - 1);
                foreach (var item in m_customHammerControls)
                    controls.Add(item);

                return;
            }

            if (!(block.BlockDefinition.SubtypeName == "LargeNaniteFactory"))
                return;

            foreach (var item in m_customControls)
                controls.Add(item);
        }

        private void MessageEntered(string messageText, ref bool sendToOthers)
        {
            if(messageText.ToLower() == "/nanite")
            {
                string message = @"05/02/2017
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
        }

        private void LoadSettings()
        {
            try
            {
                m_settings = NaniteSettings.Load();

                var def = MyDefinitionManager.Static.GetCubeBlockDefinition(new MyDefinitionId(typeof(MyObjectBuilder_OxygenFarm), "LargeNaniteFactory"));
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
            catch(Exception ex)
            {
                Logging.Instance.WriteLine(string.Format("LoadSettings error: {0}", ex.ToString()));
            }
        }

        private void CleanupOldBlocks()
        {
            HashSet<IMyEntity> grids = new HashSet<IMyEntity>();
            HashSet<IMyCubeGrid> gridsToRemove = new HashSet<IMyCubeGrid>();
            MyAPIGateway.Entities.GetEntities(grids, x => x is IMyCubeGrid);
            foreach(var item in grids)
            {
                if(((IMyCubeGrid)item).DisplayName == "SmallNaniteWelderCube")
                {
                    gridsToRemove.Add((IMyCubeGrid)item);
                }
            }

            foreach (var item in gridsToRemove)
                item.Close();
        }

        /// <summary>
        /// This is required to remove blocks that are supposed to not exist on the clients.  Clients don't need actual welders as welding
        /// happen on the server.  (This is obsolete, but leaving for now)
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
                /*
                foreach (var item in grid.GetBlocks())
                {
                    IMySlimBlock slimBlock = (IMySlimBlock)item;
                    if (slimBlock.FatBlock == null)
                        continue;

                    IMyCubeBlock block = (IMyCubeBlock)slimBlock.FatBlock;
                    if (block.BlockDefinition.SubtypeName.Contains("NaniteShipWelder"))
                    {
                        if(!obj.Closed)
                            obj.Close();

                        return;
                    }
                }
                */
            }
        }

        public static List<NaniteConstructionBlock> GetConstructionBlocks(IMyCubeGrid grid)
        {
            List<IMyCubeGrid> gridList = GridHelper.GetGridGroup(grid);
            List<NaniteConstructionBlock> blockList = new List<NaniteConstructionBlock>();

            foreach(var item in NaniteBlocks)
            {
                if(gridList.Contains(item.Value.ConstructionBlock.CubeGrid))
                {
                    if(!blockList.Contains(item.Value))
                        blockList.Add(item.Value);
                }
            }

            return blockList;
        }

        protected override void UnloadData()
        {
            m_sync.Unload();

            MyAPIGateway.Utilities.MessageEntered -= MessageEntered;
            MyAPIGateway.Entities.OnEntityAdd -= Entities_OnEntityAdd;

            NaniteBlocks.Clear();
            ProjectorBlocks.Clear();
            AssemblerBlocks.Clear();

            foreach (var item in BeaconList.ToList())
                item.Close();

            BeaconList.Clear();

            foreach (var item in MiningList.ToList())
                item.Close();

            MiningList.Clear();

            Logging.Instance.Close();

            //if(Logging.Instance != null)
            //    Logging.Instance.Close();
        }

        public override void SaveData()
        {
            m_terminalSettingsManager.Save();
        }
    }
}
