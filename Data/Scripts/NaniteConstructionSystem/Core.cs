using System;
using System.Text;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using VRageMath;
using VRage.Game.Components;
using VRage.Collections;
using Sandbox.ModAPI;
using Sandbox.Game.Localization;
using VRage.ModAPI;
using VRage.Utils;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;
using VRage.Game;
using Sandbox.Definitions;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using Sandbox.Game.EntityComponents;
using Sandbox.Common.ObjectBuilders;
using Ingame = Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using IMyProjector = Sandbox.ModAPI.IMyProjector;
using IMyTerminalBlock = Sandbox.ModAPI.IMyTerminalBlock;

using NaniteConstructionSystem.Entities;
using NaniteConstructionSystem.Extensions;
using NaniteConstructionSystem.Entities.Beacons;
using NaniteConstructionSystem.Particles;
using NaniteConstructionSystem.Settings;
using NaniteConstructionSystem;
using NaniteConstructionSystem.Integration;

namespace NaniteConstructionSystem
{
    public class GridTargetInfo
    {
        public List<long> Factories = new List<long>();
        public MyCubeBlock BeaconBlock;

        public GridTargetInfo(long factoryId, MyCubeBlock beaconBlock = null)
        {
            Factories.Add(factoryId);
            BeaconBlock = beaconBlock;
        }
    }

    public class BlockTarget
    {
        public IMySlimBlock Block;
        public bool IsRemote;

        public BlockTarget(IMySlimBlock block, bool isRemote = false)
        {
            Block = block;
            IsRemote = isRemote;
        }
    }

    public class NaniteVersionClass
    {
        public int Major = 2;
        public int Revision = 0;
        public int Build = 6;

        public NaniteVersionClass(){}
    }

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class NaniteConstructionManager : MySessionComponentBase
    {
        public static NaniteConstructionManager Instance;

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

        private int m_updateTimer;

        private TerminalSettings m_terminalSettingsManager = new TerminalSettings();
        private List<IMyTerminalControl> m_customControls = new List<IMyTerminalControl>();
        private IMyTerminalControl m_customAssemblerControl;
        private IMyTerminalControl m_customOreSelect;
        public static List<string> OreList = new List<string>(); //NaniteConstructionManager.OreList

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
                Logging.Instance.WriteLine($"IsClient: {Sync.IsClient} | IsServer {Sync.IsServer} | IsDedicated {Sync.IsDedicated}");

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
            ProjectorIntegration.LogVersion();

            CleanupOldBlocks();

            if (Sync.IsClient)
            {
                MessageHub.SendMessageToServer(new MessageClientConnected());

                foreach (var item in NaniteBlocks)
                    m_sync.SendNeedTerminalSettings(item.Key);

                foreach (var item in AssemblerBlocks)
                    if (item.Value != null)
                        m_sync.SendNeedAssemblerSettings(item.Value.EntityId);

            }
        }
        #endregion

        public override void UpdateBeforeSimulation()
        {
            if (MyAPIGateway.Session == null)
                return;

            try
            {
                //if (m_updateTimer++ % 60 == 0)
                //    Logging.Instance.WriteToFile();

                ParticleManager.Update();
            }
            catch (Exception e)
                { MyLog.Default.WriteLineAndConsole($"Nanite.Core.UpdateBeforeSimulation Error:\n{e.ToString()}"); }
        }

        private void ScanGrid()
        {

        }

        private void CreateOreList()
        {
            var allVoxelMaterials = MyDefinitionManager.Static.GetVoxelMaterialDefinitions();

            foreach (var voxelMaterial in allVoxelMaterials)
            {
                if (!string.IsNullOrEmpty(voxelMaterial.MinedOre)) {
                    if (OreList.Contains(voxelMaterial.MinedOre) == false) {
                        OreList.Add(voxelMaterial.MinedOre);
                    }
                }
            }
        }

        private void AddComboBoxItem(List<MyTerminalControlComboBoxItem> subSystemList)
        {

            var generatedItem = new MyTerminalControlComboBoxItem {
                Key = 0,
                Value = MyStringId.GetOrCompute("")
            };
            subSystemList.Add(generatedItem);

            var keyIndex = 0;
            foreach (string ore in OreList) {
                keyIndex++;
                var generatedItemForeach = new MyTerminalControlComboBoxItem {
                    Key = keyIndex,
                    Value = MyStringId.GetOrCompute(ore)
                };
                subSystemList.Add(generatedItemForeach);
            }
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
            if (Settings.LifeSupportEnabled)
            {
                var lifeSupportCheck = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, Ingame.IMyShipWelder>("AllowLifeSupport");
                lifeSupportCheck.Title = MyStringId.GetOrCompute("Life Support");
                lifeSupportCheck.Tooltip = MyStringId.GetOrCompute("When checked, the factory will attempt to heal players.");
                lifeSupportCheck.Getter = (x) =>
                {
                    if (!TerminalSettings.ContainsKey(x.EntityId))
                        TerminalSettings.Add(x.EntityId, new NaniteTerminalSettings());

                    return TerminalSettings[x.EntityId].AllowLifeSupport;
                };

                lifeSupportCheck.Setter = (x, y) =>
                {
                    if (!TerminalSettings.ContainsKey(x.EntityId))
                        TerminalSettings.Add(x.EntityId, new NaniteTerminalSettings());

                    TerminalSettings[x.EntityId].AllowLifeSupport = y;
                    m_sync.SendTerminalSettings(x);
                };
                m_customControls.Add(lifeSupportCheck);
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

            CreateOreList();

            // mining beacon ore select
            IMyTerminalControlCombobox Control = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCombobox, Ingame.IMyBatteryBlock>($"MiningBeacon_OrePicker");
            Control.Title = MyStringId.GetOrCompute("Ore Selector");
            Control.Tooltip = MyStringId.GetOrCompute("Select ore to mine");
            Control.Getter = (block) =>
            {
                if (block == null || block.CustomData == null || block.CustomData == "") {
                    return 0;
                } else {
                    var stringCustom = block.CustomData;
                    return long.Parse(stringCustom);
                }
            };
            Control.ComboBoxContent = AddComboBoxItem;
            Control.Setter = (block, v) => {
                block.CustomData = v.ToString();
                Control.UpdateVisual();
            };

            Control.Enabled = (block) => {
                return true;
            };
            Control.Visible = (block) =>
            {
                var targetBlock = block.SlimBlock;

                if (targetBlock == null)
                    return false;

                var subtypeId = block.BlockDefinition.SubtypeName;
                if (subtypeId == null)
                    return false;

                return (subtypeId == "NaniteBeaconMine");
            };

            m_customOreSelect = Control;
        }

        private void CustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            try
            {
                if (block == null || block.BlockDefinition.IsNull() || block.BlockDefinition.SubtypeName == null)
                    return;

                Logging.Instance.WriteLine($"CustomControlGetter : {block.BlockDefinition.SubtypeName}");
                if (block.BlockDefinition.TypeId == typeof(MyObjectBuilder_Assembler))
                {
                    controls.Add(m_customAssemblerControl);
                    return;
                }
                if (block.BlockDefinition.TypeId == typeof(MyObjectBuilder_BatteryBlock))
                {
                    controls.Add(m_customOreSelect);
                    return;
                }
                else if (block.BlockDefinition.SubtypeName == "LargeNaniteControlFacility")
                {
                    controls.RemoveAt(controls.Count - 1);
                        // Remove "Help Others" checkbox, since the block is a ShipWelder

                    foreach (var item in m_customControls)
                        controls.Add(item);
                }
                else if (block.BlockDefinition.SubtypeName == "SmallNaniteControlFacility")
                {
                    controls.RemoveAt(controls.Count - 1);
                        // Remove "Help Others" checkbox, since the block is a ShipWelder

                    foreach (var item in m_customControls)
                        controls.Add(item);
                }
            }
            catch (Exception e)
                { Logging.Instance.WriteLine($"Exception in NaniteConstructionManager.CustomControlGetter:\n{e.ToString()}"); }
        }

        private void MessageEntered(string messageText, ref bool sendToOthers)
        {
            bool donothing = false;
            string message = "";
            string title = "";

            try
            {

                Localization.Help(messageText, out donothing, out message, out title);

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

            var def2 = MyDefinitionManager.Static.GetCubeBlockDefinition(new MyDefinitionId(typeof(MyObjectBuilder_ShipWelder), "SmallNaniteControlFacility"));
            foreach (var item in def2.Components)
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
