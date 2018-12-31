#region Assembly references
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Linq;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game;
using VRage.ModAPI;
using VRageMath;
using VRage.Utils;
using Ingame = Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI;
using NaniteConstructionSystem.Particles;
using NaniteConstructionSystem.Entities.Targets;
using NaniteConstructionSystem.Entities.Effects;
using NaniteConstructionSystem.Entities.Effects.LightningBolt;
using NaniteConstructionSystem.Entities.Tools;
using NaniteConstructionSystem.Entities.Beacons;
using NaniteConstructionSystem.Entities.Detectors;
using NaniteConstructionSystem.Extensions;
using NaniteConstructionSystem.Settings;
#endregion

namespace NaniteConstructionSystem.Entities
{
    public class NaniteConstructionBlock
    {
        #region Variables
        public enum FactoryStates
        {
            Disabled, Enabled, SpoolingUp, SpoolingDown,
            MissingParts, MissingPower, InvalidTargets, Active
        }

        private IMyShipWelder m_constructionBlock;
        public IMyShipWelder ConstructionBlock {get {return m_constructionBlock;} }

        private MyCubeBlock m_constructionCubeBlock;
        public MyCubeBlock ConstructionCubeBlock {get {return m_constructionCubeBlock;} }


        private List<NaniteTargetBlocksBase> m_targets;
        public List<NaniteTargetBlocksBase> Targets {get {return m_targets;} }

        private bool m_initialize;
        public bool Initialized {get {return m_initialize;} }

        private NaniteParticleManager m_particleManager;
        public NaniteParticleManager ParticleManager {get {return m_particleManager;} }

        private NaniteToolManager m_toolManager;
        public NaniteToolManager ToolManager {get {return m_toolManager;} }

        private NaniteConstructionInventory m_inventoryManager;
        public NaniteConstructionInventory InventoryManager {get {return m_inventoryManager;} }

        private FactoryStates m_factoryState;
        public FactoryStates FactoryState {get {return m_factoryState;} }
        private FactoryStates m_lastState;

        private int m_userDefinedNaniteLimit;

        private bool m_updateConnectedInventory;
        public bool UpdateConnectedInventory
        {
            get {return m_updateConnectedInventory;}
            set {m_updateConnectedInventory = value;}
        }

        private int m_spoolPosition;
        public int SpoolPosition {get { return m_spoolPosition;} }

        internal MyResourceSinkInfo ResourceInfo;
        internal MyResourceSinkComponent Sink;

        private float _power = 0.0001f;
        public float Power
            {get { return Sink.CurrentInputByType(MyResourceDistributorComponent.ElectricityId);} }

        private List<NaniteBlockEffectBase> m_effects;
        private MySoundPair m_soundPair;
        private MyEntity3DSoundEmitter m_soundEmitter;

        private int m_updateCount;
        public int UpdateCount
        {
            get {return m_updateCount;}
            set {m_updateCount = value;}
        }

        private Dictionary<MyDefinitionId, MyBlueprintDefinitionBase> m_defCache;
        public Dictionary<string, bool> EnabledParticleTargets = new Dictionary<string, bool>
        {
            ["Construction"] = true,
            ["Deconstruction"] = true,
            ["Cleanup"] = true,
            ["Medical"] = true,
            ["Mining"] = true,
            ["Projection"] = true
        };

        private int m_assemblerUpdateTimer;
        private int m_takeComponentsTimer;
        
        private StringBuilder m_syncDetails;
        private StringBuilder m_targetDetails;
        private StringBuilder m_invalidTargetDetails;
        private StringBuilder m_missingComponentsDetails;

        private int m_potentialTargetsCount;
        public int PotentialTargetsCount
            {get { return m_potentialTargetsCount;} set {m_potentialTargetsCount = value;} }

        private int m_targetsCount;

        private bool m_clientEmissivesUpdate;
        private bool m_forceProcessState;
        private int m_forceProcessStateCooldown;
        
        private MyInventory m_constructionBlockInventory;
        public MyInventory ConstructionBlockInventory {get { return m_constructionBlockInventory;} }

        private ConcurrentBag<IMySlimBlock> m_potentialInventoryBlocks = new ConcurrentBag<IMySlimBlock>();

        private long m_entityId;
        public long EntityId {get { return m_entityId;} }

        public List<NaniteConstructionBlock> FactoryGroup;
        public NaniteConstructionBlock Master;
        public List<NaniteConstructionBlock> Slaves = new List<NaniteConstructionBlock>();
        public List<IMyCubeGrid> GridGroup = new List<IMyCubeGrid>();

        private List<BlockTarget> m_scanBlocksCache = new List<BlockTarget>();
        public List<BlockTarget> ScanBlocksCache {get { return m_scanBlocksCache;}}

        private int m_totalScanBlocksCount;
        public int TotalScanBlocksCount {get { return m_totalScanBlocksCount;} set { m_totalScanBlocksCount = value;}}

        private bool m_scanningActive;
        private bool m_initInventory = true;
        private bool m_isFunctional;
        public bool IsFunctional {get {return m_isFunctional;} }
        
        private const int m_spoolingTime = 3000;
        private const float m_maxDistance = 300f;
        #endregion

        #region Core
        public NaniteConstructionBlock(IMyEntity entity)
        { // Constructor
            m_constructionBlock = (IMyShipWelder)entity;
            m_constructionBlockInventory = ((MyCubeBlock)entity).GetInventory();
            m_constructionBlockInventory.SetFlags(MyInventoryFlags.CanReceive |MyInventoryFlags.CanSend);
            m_defCache = new Dictionary<MyDefinitionId, MyBlueprintDefinitionBase>();

            m_constructionCubeBlock = (MyCubeBlock)entity;
            m_constructionCubeBlock.UpgradeValues.Add("ConstructionNanites", 0f);
            m_constructionCubeBlock.UpgradeValues.Add("DeconstructionNanites", 0f);
            m_constructionCubeBlock.UpgradeValues.Add("CleanupNanites", 0f);
            m_constructionCubeBlock.UpgradeValues.Add("MiningNanites", 0f);
            m_constructionCubeBlock.UpgradeValues.Add("MedicalNanites", 0f);
            m_constructionCubeBlock.UpgradeValues.Add("SpeedNanites", 0f);
            m_constructionCubeBlock.UpgradeValues.Add("PowerNanites", 0f);
        }

        private void Initialize()
        { // Actual init. This occurs once modapi is ready and updating.
            m_initialize = true;

            m_toolManager = new NaniteToolManager();
            m_particleManager = new NaniteParticleManager(this);

            m_targets = new List<NaniteTargetBlocksBase>();
            if (NaniteConstructionManager.Settings.ConstructionEnabled)
                m_targets.Add(new NaniteConstructionTargets(this));
            if (NaniteConstructionManager.Settings.ProjectionEnabled)
                m_targets.Add(new NaniteProjectionTargets(this));
            if (NaniteConstructionManager.Settings.CleanupEnabled)
                m_targets.Add(new NaniteFloatingTargets(this));
            if (NaniteConstructionManager.Settings.DeconstructionEnabled)
                m_targets.Add(new NaniteDeconstructionTargets(this));
            if (NaniteConstructionManager.Settings.MiningEnabled)
                m_targets.Add(new NaniteMiningTargets(this));
            if (NaniteConstructionManager.Settings.MedicalEnabled)
                m_targets.Add(new NaniteMedicalTargets(this));

            m_effects = new List<NaniteBlockEffectBase>();
            m_effects.Add(new LightningBoltEffect((MyCubeBlock)m_constructionBlock));
            m_effects.Add(new CenterOrbEffect((MyCubeBlock)m_constructionBlock));

            m_factoryState = FactoryStates.Disabled;
            m_lastState = FactoryStates.Disabled;
            m_syncDetails = new StringBuilder();
            m_targetDetails = new StringBuilder();
            m_invalidTargetDetails = new StringBuilder();
            m_missingComponentsDetails = new StringBuilder();

            m_soundPair = new MySoundPair("ArcParticleElectrical");
            m_soundEmitter = new MyEntity3DSoundEmitter((MyEntity)m_constructionBlock);
            m_soundEmitter.CustomMaxDistance = 30f;
            m_soundEmitter.CustomVolume = 2f;

            m_inventoryManager = new NaniteConstructionInventory((MyEntity)m_constructionBlock);

            ((IMyTerminalBlock)m_constructionBlock).AppendingCustomInfo += AppendingCustomInfo;

            Sink = ((MyEntity)m_constructionBlock).Components.Get<MyResourceSinkComponent>();

            CheckGridGroup();
            m_entityId = ConstructionBlock.EntityId;

            FactoryGroup = new List<NaniteConstructionBlock>();
            FactoryGroup.Add(this);

            m_isFunctional = ConstructionBlock.IsFunctional;
        }

        private bool FactoryIsRunning()
        {
            return m_isFunctional && m_factoryState != FactoryStates.Disabled;
        }

        public void Update()
        { // Main update loop. Called each frame during game block logic
            if (ConstructionBlock.Closed)
                return;
            
            m_updateCount++;

            if (!m_initialize)
                Initialize();

            if (m_updateCount % 1800 == 0 && m_isFunctional)
            { // Log some status info. Should include some process time profiling in the future
                MyAPIGateway.Parallel.Start(() =>
                {
                    try
                    {
                        string upgrades = "";
                        foreach (var item in ((MyCubeBlock)m_constructionBlock).UpgradeValues)
                            upgrades += string.Format("({0} - {1}) ", item.Key, item.Value);

                        Logging.Instance.WriteLine(string.Format("STATUS Nanite Factory: {0} - (t: {1}  pt: {2}  pw: {3} st: {4}) - {5}", 
                          m_entityId, m_targetsCount, m_potentialTargetsCount, _power, m_factoryState, upgrades));
                    }
                    catch (Exception e)
                        { VRage.Utils.MyLog.Default.WriteLineAndConsole($"Exception while logging Nanite Factory status: {e.ToString()}"); }
                });

                m_scanningActive = false;
            }

            if (Sync.IsServer)
            {
                if (m_updateCount % 300 == 0)
                {
                    if (FactoryIsRunning())
                        MyAPIGateway.Parallel.Start(() =>
                        {
                            try
                                { CheckSlaveMaster(); }
                            catch (Exception e)
                                { VRage.Utils.MyLog.Default.WriteLineAndConsole($"Exception: CheckSlaveMaster: {e.ToString()}"); }
                        });
                    else
                    {
                        Master = null;
                        Slaves.Clear();
                        m_scanBlocksCache.Clear();
                    }
                }

                if (Master == null)
                {
                    if (m_updateCount % 300 == 0 && FactoryIsRunning() && (m_updateConnectedInventory || Master != null || Slaves.Count > 0) )
                    {
                        m_updateConnectedInventory = false;
                        CheckGridGroup();
                    }

                    if (m_updateCount % 60 == 0)
                    {
                        if (FactoryIsRunning())
                            CheckIfAGridBlockIsInventory();

                        ToolManager.Update();
                    }
                        
                    if (m_updateCount == m_takeComponentsTimer && FactoryIsRunning())
                    {
                        MyAPIGateway.Parallel.StartBackground(() =>
                            { InventoryManager.TakeRequiredComponents(); });
                    }

                    ScanForTargets(out m_scanningActive);

                    if (m_updateCount == m_assemblerUpdateTimer)
                    {
                        if (m_factoryState == FactoryStates.MissingParts)
                            ProcessAssemblerQueue();

                        m_scanningActive = false;
                    }
                }

                if (m_forceProcessState || !m_scanningActive || m_updateCount > m_assemblerUpdateTimer + 600)
                    ProcessState();                          // ^Prevent factorystate deadlocks
            }            
            
            UpdateSpoolPosition();
            ParticleManager.Update();
            
            if (Sync.IsClient || !MyAPIGateway.Multiplayer.MultiplayerActive)
            {
                UpdateClientEmissives();
                DrawEffects();
            }

            if (m_updateCount % 60 == 0)
            {
                m_isFunctional = ConstructionBlock.IsFunctional;
                ProcessTargetItems();
            }
                
            if (m_updateCount % 120 == 0)
            {
                ParticleManager.CheckParticleLife(); // removes stubborn Nanite particles

                MyAPIGateway.Parallel.Start(() =>
                { // This is the only place that ever modifies m_userDefinedNaniteLimit, so this should be thread-safe without locks
                    if (NaniteConstructionManager.TerminalSettings.ContainsKey(m_constructionBlock.EntityId)
                      && NaniteConstructionManager.TerminalSettings[m_constructionBlock.EntityId].MaxNanites != m_userDefinedNaniteLimit)
                        m_userDefinedNaniteLimit = NaniteConstructionManager.TerminalSettings[m_constructionBlock.EntityId].MaxNanites;
                });
            }
                
            if (m_updateCount % 180 == 0)
                UpdateTerminal();
        }

        public void Unload()
        {
            m_factoryState = FactoryStates.Disabled; // In case keen doesn't catch that the block is dead, this will invalidate slave/master groups

            if (m_effects != null)
                foreach (var item in m_effects)
                    item.Unload();

            if (m_soundEmitter != null)
                m_soundEmitter.StopSound(true);
        }
        #endregion

		#region Master-slave connection methods
        /// <summary> Checks existing slave-master connections and tries to make new ones. Runs on parallel thread </summary>
        private void CheckSlaveMaster()
        { 
            if (Master != null && !MasterSlaveIsValid(Master, true))
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                {
                    try
                    {
                        Logging.Instance.WriteLine($"Factory {m_entityId} is no longer slaved to {Master.EntityId}.");
                        Master.Slaves.Remove(this);
                        Master = null; 
                    }
                    catch (Exception e)
                    {
                        VRage.Utils.MyLog.Default.WriteLineAndConsole($"Exception: CheckSlaveMaster, second InvokeOnGameThread: {e.ToString()}");
                    }
                });

            else if (Master == null)
            {
                if (Slaves.Count > 1)
                {
                    List<NaniteConstructionBlock> removeList = new List<NaniteConstructionBlock>();

                    foreach (var slave in Slaves)
                        if (!MasterSlaveIsValid(slave))
                            removeList.Add(slave);

                    if (removeList.Count > 0)
                        MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                        {
                            try
                            {
                                foreach (var slave in removeList)
                                {
                                    Logging.Instance.WriteLine($"Factory {slave.EntityId} is no longer slaved to {m_entityId}.");
                                    Slaves.Remove(slave);
                                    slave.Master = null;
                                } 
                            }
                            catch (Exception e)
                            {
                                VRage.Utils.MyLog.Default.WriteLineAndConsole($"Exception: CheckSlaveMaster, second InvokeOnGameThread: {e.ToString()}");
                            }
                        });
                }

                foreach (var factory in NaniteConstructionManager.NaniteBlocks)
                { // Check for a valid master and then become slave/move all slaves over
                    if (factory.Value != this && factory.Value.Master == null && MasterSlaveIsValid(factory.Value))
                    {
                        MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                        {
                            try
                            {
                                if (factory.Value.Master == null)
                                {
                                    Master = factory.Value;

                                    if (!Master.Slaves.Contains(this))
                                    {
                                        Master.Slaves.Add(this);
                                        Logging.Instance.WriteLine($"Factory {m_entityId} is now slaved to {Master.EntityId}.");
                                    }

                                    foreach (var slave in Slaves)
                                    {
                                        slave.Master = Master;

                                        if (!Master.Slaves.Contains(slave))
                                        {
                                            Master.Slaves.Add(slave);
                                            Logging.Instance.WriteLine($"Factory {slave.EntityId} is now slaved to {Master.EntityId}.");
                                        }
                                    }
                                    Slaves.Clear();
                                }
                            }
                            catch (Exception e)
                            {
                                VRage.Utils.MyLog.Default.WriteLineAndConsole($"Exception: CheckSlaveMaster, third InvokeOnGameThread: {e.ToString()}");
                            }
                        });

                        break;
                    }
                }
            }
        }

        private bool MasterSlaveIsValid(NaniteConstructionBlock factory, bool useOtherGridGroup = false)
        {
            if (factory.FactoryState == FactoryStates.Disabled || factory.ConstructionBlock == null || !factory.IsFunctional
              || !MyRelationsBetweenPlayerAndBlockExtensions.IsFriendly(m_constructionBlock.GetUserRelationToOwner(factory.ConstructionBlock.OwnerId)))
                return false;
            
            if (Vector3D.Distance(ConstructionBlock.GetPosition(), factory.ConstructionBlock.GetPosition()) > m_maxDistance)
            {
                bool isInGroup = false;
                List<IMyCubeGrid> grids = useOtherGridGroup ? factory.GridGroup : GridGroup;
                foreach (var grid in grids.ToList())
                    if (ConstructionBlock.CubeGrid == grid)
                    {
                        isInGroup = true;
                        break;
                    }

                if (!isInGroup)
                    return false;
            }

            return true;
        } 
	    #endregion

        #region Terminal information display methods
        private void UpdateTerminal()
        {
            ((IMyTerminalBlock)m_constructionBlock).RefreshCustomInfo();

            if (Sync.IsClient)
            {
                CleanupTargets();

                if (MyAPIGateway.Gui?.GetCurrentScreen == MyTerminalPageEnum.ControlPanel)          
                    UpdateTerminalClient();
            }
        }

        /// <summary> Silly hack needed to get around a longstanding bug. Forces the terminal to update if the player is viewing it. Credit to Equinox </summary>
        void UpdateTerminalClient()
        {
            MyOwnershipShareModeEnum shareMode;
            long ownerId;

            if (m_constructionCubeBlock.IDModule != null)
            {
                ownerId = m_constructionCubeBlock.IDModule.Owner;
                shareMode = m_constructionCubeBlock.IDModule.ShareMode;
            }
            else
                return;

            m_constructionCubeBlock.ChangeOwner(ownerId, shareMode == MyOwnershipShareModeEnum.None ? MyOwnershipShareModeEnum.Faction : MyOwnershipShareModeEnum.None);
            m_constructionCubeBlock.ChangeOwner(ownerId, shareMode);
        }

        /// <summary> Updates .CustomInfo of the factory. Viewed in the control panel </summary>
        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder details)
        { 
            if (m_factoryState == FactoryStates.Disabled || m_updateCount % 180 != 0)
                return;
            
            details.Clear();

            if (Sync.IsServer)
            {    
                MyAPIGateway.Parallel.Start(() =>
                {
                    try
                    {
                        StringBuilder targetDetailsParallel = new StringBuilder();
                        StringBuilder invalidTargetDetailsParallel = new StringBuilder();
                        StringBuilder missingComponentsDetailsParallel = new StringBuilder();
                        bool invalidTitleAppended = false;
                        bool missingCompTitleAppended = false;

                        NaniteConstructionBlock factory = Master != null ? Master : this;
                        
                        foreach (var item in factory.Targets.ToList())
                        {
                            targetDetailsParallel.Append("-----\r\n"
                              + $"{item.TargetName} Nanites\r\n"
                              + "-----\r\n"
                              + $"Possible Targets: {item.PotentialTargetListCount}\r\n"
                              + $"Current Targets: {item.TargetList.Count}\r\n"
                              + $"Max Streams: {item.GetMaximumTargets()}\r\n" // TO DO: Retrieve a cached value for these instead of invoking a method
                              + $"MW/Stream: {item.GetPowerUsage()} MW\r\n"
                              + $"Min. Travel Time: {item.GetMinTravelTime()} s\r\n"
                              + $"Travel Speed: {item.GetSpeed()} m/s\r\n");

                            if (item.LastInvalidTargetReason != null && item.LastInvalidTargetReason != "")
                            {
                                if (!invalidTitleAppended)
                                {
                                    invalidTargetDetailsParallel.Append("\nTarget info:\r\n");
                                    invalidTitleAppended = true;
                                }
                                invalidTargetDetailsParallel.Append($"\n- ({item.TargetName}) " + item.LastInvalidTargetReason);
                            }
                        }

                        if (factory.InventoryManager.ComponentsRequired.Count > 0) 
                            foreach (var component in factory.InventoryManager.ComponentsRequired.ToList())
                                if (component.Value > 0)
                                {
                                    if (!missingCompTitleAppended)
                                    {
                                        missingComponentsDetailsParallel.Append("\r\nNeeded parts:\r\n");
                                        missingCompTitleAppended = true;
                                    }
                                    missingComponentsDetailsParallel.Append($"{component.Key}: {component.Value}\r\n");
                                }

                        MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                        {
                            m_targetDetails = targetDetailsParallel;
                            m_invalidTargetDetails = invalidTargetDetailsParallel;
                            m_missingComponentsDetails = missingComponentsDetailsParallel;
                        });
                    }
                    catch (Exception e)
                        {VRage.Utils.MyLog.Default.WriteLineAndConsole($"NaniteConstructionBlock.AppendingCustomInfo() exception: {e.ToString()}");}
                    
                });

                details.Append($"-- Nanite Factory v2.0 --\n");

                if (m_initInventory && Master == null)
                    details.Append($"\n-INITIALIZING-\nTasks left: {m_potentialInventoryBlocks.Count}\n");

                if (m_totalScanBlocksCount > 0)
                {
                    string percent = (((m_totalScanBlocksCount - m_scanBlocksCache.Count)/m_totalScanBlocksCount) * 100).ToString("0.00");
                    details.Append($"\nScanning ... {percent}%\n"
                      + $"{m_totalScanBlocksCount - m_scanBlocksCache.Count}/{m_totalScanBlocksCount} blocks\n\n");
                }
                else
                    details.Append($"\nWaiting ...\n\n");

                details.Append(m_targetDetails
                  + "-----\r\n"
                  + $"Power Required: {_power} MW\r\n"
                  + $"Status: {m_factoryState.ToString()}\r\n"
                  + $"Active Nanites: {m_particleManager.Particles.Count}\r\n");

                if (m_userDefinedNaniteLimit > 0)
                    details.Append($"Max Nanites: {m_userDefinedNaniteLimit}\r\n");

                details.Append(m_invalidTargetDetails);
                details.Append(m_missingComponentsDetails);

                if (m_syncDetails.Length != details.Length)
                {
                    m_syncDetails.Clear();
                    m_syncDetails.Append(details);
                    SendDetails();
                }      
            }
            else
                details.Append(m_syncDetails);
        }
        #endregion

        #region Power management methods
        private void UpdatePower()
        {
            if (!m_constructionBlock.Enabled || !m_isFunctional)
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(() => 
                    { Sink.SetRequiredInputByType(MyResourceDistributorComponent.ElectricityId, 0.0001f); });

                return;
            }

            float calculatePower = m_targets.Sum(x => (x.TargetList.Count) * x.GetPowerUsage()); 

            float totalPowerRequired = calculatePower == 0f ? 0.0001f : calculatePower;

            if (_power == totalPowerRequired)
                return;

            MyAPIGateway.Utilities.InvokeOnGameThread(() => 
            {
                _power = (totalPowerRequired > 0f) ? totalPowerRequired : 0.0001f;
                Sink.SetRequiredInputByType(MyResourceDistributorComponent.ElectricityId, _power);
            });

            Logging.Instance.WriteLine($"Factory {ConstructionBlock.EntityId} updated power usage to {_power} MegaWatts");
        }

        internal bool HasRequiredPowerForNewTarget(NaniteTargetBlocksBase target)
        {
            return Sink.IsPowerAvailable(MyResourceDistributorComponent.ElectricityId, _power + target.GetPowerUsage());
        }

        internal bool IsPowered()
        {
            return Sink.IsPoweredByType(MyResourceDistributorComponent.ElectricityId);
        }
        #endregion

        #region GridGroup/Inventory management methods
        private void ProcessInventory()
        {
            var inventory = ((MyCubeBlock)m_constructionBlock).GetInventory();
            if(inventory.VolumeFillFactor > 0.75f && (Master != null || GetTarget<NaniteDeconstructionTargets>().TargetList.Count > 0 
              || GetTarget<NaniteFloatingTargets>().TargetList.Count > 0 || GetTarget<NaniteMiningTargets>().TargetList.Count > 0))
            {
                var connectedInventory = InventoryManager.connectedInventory;
                if (Master != null)
                    connectedInventory = Master.InventoryManager.connectedInventory;
                
                GridHelper.TryMoveToFreeCargo((MyCubeBlock)m_constructionBlock, connectedInventory, true);
                Logging.Instance.WriteLine($"PUSHING Factory inventory over 75% full: {m_constructionBlock.EntityId}");
            }
        }

        private void CheckIfAGridBlockIsInventory()
        {
            MyAPIGateway.Parallel.Start(() =>
            {
                IMySlimBlock block = null;
                m_potentialInventoryBlocks.TryTake(out block);
                if (block != null)
                    TryAddToInventoryGroup(block);
            });
        }

        private void OnBlockAdded(IMySlimBlock block)
        {
            MyAPIGateway.Parallel.Start(() =>
            {
                if (Master != null || m_factoryState == FactoryStates.Disabled)
                    return;
                
                Logging.Instance.WriteLine("Block added to grid.");

                TryAddPotentialInventoryBlock(block);
            });
        }

        private void TryAddPotentialInventoryBlock(IMySlimBlock block)
        {
            if (block.FatBlock == null || !(block.FatBlock is IMyTerminalBlock)) //|| block.FatBlock is MyDeviceBase)
                return;

            //Logging.Instance.WriteLine($"Block {block.FatBlock.DisplayNameText} added to inventory check queue.");
            m_potentialInventoryBlocks.Add(block);
        }

        private void TryAddToInventoryGroup(object block)
        {
            IMyInventory inv = null;

            if (block is IMySlimBlock)
            {
                var slimBlock = block as IMySlimBlock;
                if (slimBlock.FatBlock == null || !(slimBlock.FatBlock is IMyTerminalBlock))
                    return;
            }
           
            foreach (var factory in FactoryGroup.ToList())
                if (GridHelper.IsValidInventoryConnection(factory.ConstructionBlockInventory, block, out inv))
                    if (!InventoryManager.connectedInventory.Contains(inv))
                    {
                        MyAPIGateway.Utilities.InvokeOnGameThread(() => 
                        {
                            if (!InventoryManager.connectedInventory.Contains(inv))
                                InventoryManager.connectedInventory.Add(inv);
                        });
                        break;
                    }  
        }

        /// <summary> Checks if the grid group has changed and quickly scans/adds any inventory blocks. Manages grid event handlers </summary>
        private void CheckGridGroup()
        {
            MyAPIGateway.Parallel.Start(() =>
            {
                try
                {
                    List<IMyCubeGrid> removalList = new List<IMyCubeGrid>();
                    List<IMyCubeGrid> newGroup = new List<IMyCubeGrid>(MyAPIGateway.GridGroups.GetGroup((IMyCubeGrid)m_constructionCubeBlock.CubeGrid, GridLinkTypeEnum.Physical));

                    foreach (var slave in Slaves)
                    {
                        List<IMyCubeGrid> slaveGroup = new List<IMyCubeGrid>(MyAPIGateway.GridGroups.GetGroup((IMyCubeGrid)slave.ConstructionCubeBlock.CubeGrid, GridLinkTypeEnum.Physical));
                        foreach (var grid in slaveGroup)
                            if (!newGroup.Contains(grid))
                                newGroup.Add(grid);
                    }

                    foreach (IMyCubeGrid grid in GridGroup)
                    {
                        if (!newGroup.Contains(grid))
                        {
                            Logging.Instance.WriteLine("Removing disconnected grid from grid group.");
                            removalList.Add(grid);
                            grid.OnBlockAdded -= OnBlockAdded;
                        }
                    }
                    
                    foreach (IMyCubeGrid removalgrid in removalList)
                        MyAPIGateway.Utilities.InvokeOnGameThread(() => 
                            { GridGroup.Remove(removalgrid); });

                    foreach (IMyCubeGrid grid in newGroup)
                    {
                        if (!GridGroup.Contains(grid))
                        {
                            Logging.Instance.WriteLine("Adding new grid to grid group.");

                            MyAPIGateway.Utilities.InvokeOnGameThread(() => 
                                { GridGroup.Add(grid); });

                            BuildConnectedInventory(grid);
                            grid.OnBlockAdded += OnBlockAdded;
                        }
                    }
                    
                }
                catch (Exception ex)
                    {VRage.Utils.MyLog.Default.WriteLineAndConsole($"CheckGridGroup() Error: {ex.ToString()}");}
            });
        }

        /// <summary> Scans and adds inventory blocks to a grid in the gridgroup. Adds event handlers </summary>
        public void BuildConnectedInventory(IMyCubeGrid grid)
        {
            MyAPIGateway.Parallel.Start(() =>
            {
                try
                {                   
                    ConcurrentBag<IMySlimBlock> slimBlocks = new ConcurrentBag<IMySlimBlock>(((MyCubeGrid)grid).GetBlocks());

                    foreach (var block in slimBlocks)
                        TryAddPotentialInventoryBlock(block);
                }
                catch (Exception ex)
                    {VRage.Utils.MyLog.Default.WriteLineAndConsole($"BuildConnectedInventory() Error: {ex.ToString()}");}
            });
        }
        #endregion

        #region Target processing methods

        /// <summary> Used during target processing to help determine if more targets should be processed </summary>
        public bool IsUserDefinedLimitReached()
        {
            if (m_userDefinedNaniteLimit != 0 && Targets.Sum(x => x.TargetList.Count) >= m_userDefinedNaniteLimit)
                return true;

            return false;
        }

        private void ProcessAssemblerQueue()
        {
            MyAPIGateway.Parallel.Start(() =>
            {
                Stopwatch stopwatch = Stopwatch.StartNew();

                if (!NaniteConstructionManager.TerminalSettings.ContainsKey(m_constructionBlock.EntityId) 
                  || !NaniteConstructionManager.TerminalSettings[m_constructionBlock.EntityId].UseAssemblers 
                  || InventoryManager.ComponentsRequired.Count < 1) 
                    return;

                List<IMyProductionBlock> assemblerList = new List<IMyProductionBlock>();
                List<IMyProductionBlock> queueableAssemblers = new List<IMyProductionBlock>();

                foreach (var inv in InventoryManager.connectedInventory)
                {
                    IMyEntity entity = inv.Owner as IMyEntity;
                    if (entity == null) 
                        continue;

                    IMyAssembler assembler = entity as IMyAssembler;
                    if (assembler == null || assembler.Mode == Sandbox.ModAPI.Ingame.MyAssemblerMode.Disassembly) 
                        continue;

                    assemblerList.Add((IMyProductionBlock)assembler);

                    if (NaniteConstructionManager.AssemblerSettings.ContainsKey(entity.EntityId) 
                      && NaniteConstructionManager.AssemblerSettings[entity.EntityId].AllowFactoryUsage)
                        queueableAssemblers.Add((IMyProductionBlock)assembler);
                }

                if (queueableAssemblers.Count < 1) 
                    return;

                MyAPIGateway.Parallel.ForEach(InventoryManager.ComponentsRequired, item =>
                {
                    var def = MyDefinitionManager.Static.TryGetBlueprintDefinitionByResultId(new MyDefinitionId(typeof(MyObjectBuilder_Component), item.Key));
                    if (def == null) 
                        return;

                    if (def.Results != null && def.Results[0].Amount > 1)
                    { // If this is some sort of weird modded definition, find the vanilla definition
                        if (m_defCache.ContainsKey(new MyDefinitionId(typeof(MyObjectBuilder_Component), item.Key)))
                            def = m_defCache[new MyDefinitionId(typeof(MyObjectBuilder_Component), item.Key)]; 

                        else
                            foreach (var defTest in MyDefinitionManager.Static.GetBlueprintDefinitions())
                                if (defTest.Results != null && defTest.Results[0].Amount == 1 
                                  && defTest.Results[0].Id == new MyDefinitionId(typeof(MyObjectBuilder_Component), item.Key))
                                    if (!m_defCache.ContainsKey(new MyDefinitionId(typeof(MyObjectBuilder_Component), item.Key)))
                                    {
                                        MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                                            { m_defCache.Add(new MyDefinitionId(typeof(MyObjectBuilder_Component), item.Key), defTest); });

                                        break;
                                    }
                    }

                    int blueprintCount = assemblerList.Sum(x => x.GetQueue().Sum(y => y.Blueprint == def ? (int)y.Amount : 0));

                    if (blueprintCount > 0)
                        return;

                    foreach (var target in queueableAssemblers)
                    {
                        int amount = (int)Math.Ceiling((float)(item.Value) / (float)queueableAssemblers.Count());
                        if (amount < 1)
                            return;

                        Logging.Instance.WriteLine(string.Format("ASSEMBLER Queuing {0} {1} for factory {2} ({4} - {3})", 
                          amount, def.Id, m_constructionBlock.CustomName, blueprintCount, item.Value));

                        MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                            { target.InsertQueueItem(0, def, amount); });
                    }

                    stopwatch.Stop();
                    //Logging.Instance.WriteLine($"ProcessAssemblerQueue {ConstructionBlock.EntityId}: {(stopwatch.ElapsedTicks * 1000000)/Stopwatch.Frequency} microseconds");
                });
            },
            () => { // callback runs after parallel task finishes
                foreach (var item in m_targets.ToList())
                    item.PotentialTargetList.Clear();
            });
        }

        private void GetMissingComponentsPotentialTargets<T>(Dictionary<string, int> addToDictionary, Dictionary<string, int> available) where T : NaniteTargetBlocksBase
        {
            int count = 0;
            foreach (var item in GetTarget<T>().PotentialTargetList)
            {
                var target = item as IMySlimBlock;
                if (target == null)
                    continue;

                if (typeof(T) == typeof(NaniteProjectionTargets))
                {
                    var def = target.BlockDefinition as MyCubeBlockDefinition;
                    var compDefName = def.Components[0].Definition.Id.SubtypeName;
                    if (available.ContainsKey(compDefName))
                        continue;

                    if (addToDictionary.ContainsKey(compDefName))
                        addToDictionary[compDefName] += 1;
                    else
                        addToDictionary.Add(compDefName, 1);
                }
                else
                    target.GetMissingComponents(addToDictionary);

                if (count++ > GetTarget<T>().GetMaximumTargets())
                    break;
            }
        }

        /// <summary>
        /// Scans for block targets including projections. This can be intensive, so we're only doing it once every 5 seconds.
        /// </summary>
        private void ScanForTargets(out bool scanningActive)
        {
            scanningActive = m_scanningActive;
            if (m_factoryState != FactoryStates.Disabled && m_factoryState != FactoryStates.MissingPower && m_updateCount % 300 == 0)
            {
                scanningActive = true; 
                FactoryGroup.Clear();
                FactoryGroup = Slaves.ToList();
                FactoryGroup.Add(this);

                MyAPIGateway.Parallel.StartBackground(() =>
                {
                    try
                    {
                        Stopwatch stopwatch = Stopwatch.StartNew();
                        SendFactoryGroup();
                        ProcessTargetsParallel();
                        ProcessTargets();
                        stopwatch.Stop();
                        Logging.Instance.WriteLine($"ScanForTargets {ConstructionBlock.EntityId}: {(stopwatch.ElapsedTicks * 1000000)/Stopwatch.Frequency} microseconds");
                    }
                    catch (InvalidOperationException ex)
                    {
                        Logging.Instance.WriteLine("ScanForTargets InvalidOperationException: "
                          + "This is likely due to a list being modified during enumeration in a parallel thread, "
                          + $"which is probably harmless.\n{ex.ToString()}");
                    }
                    catch (Exception ex) when (ex.ToString().Contains("IndexOutOfRangeException")) 
                    { // because Keen thinks we shouldn't have access to IndexOutOfRangeException ...
                        Logging.Instance.WriteLine("ScanForTargets IndexOutOfRangeException: "
                          + "This is likely due to a list being modified during enumeration in a parallel thread, "
                          + $"which is probably harmless.\n{ex.ToString()}");
                    }
                    catch (Exception ex)
                        {VRage.Utils.MyLog.Default.WriteLineAndConsole($"NaniteConstructionBlock.ScanForTargets Error: {ex.ToString()}");}
                },
                () => { //callback
                    m_takeComponentsTimer = m_updateCount + 100; //  These timers give the parallel threads time to finish
                    m_assemblerUpdateTimer = m_updateCount + 200; // computing and then finding components/lists.
                    m_scanningActive = false; // This bool forces the m_factorystate to wait for at least one attempt at 
                });                           // finding components before moving to the MissingParts state
            }
        }

        private void ProcessTargetItems()
        {
            try
            {
                foreach (var item in m_targets)
                    item.Update();
            }
            catch (Exception ex)
                {VRage.Utils.MyLog.Default.WriteLineAndConsole($"NaniteConstructionBlock.ProcessTargetItems() Exception: {ex.ToString()}");}
        }

        /// <summary> Used many times during target processing to determine various factory upgrade attributes </summary>
        public float UpgradeValue(string upgrade)
        {
            string type = "";

            if (upgrade == "MinTravelTime")
                type = "SpeedNanites";
            else if (upgrade == "ProjectionNanites")
                type = "ConstructionNanites";
            else
                type = upgrade;

            float v = (float)m_constructionCubeBlock.UpgradeValues[type];
            foreach (var slave in Slaves)
                v += (float)slave.ConstructionCubeBlock.UpgradeValues[type];
            var s = NaniteConstructionManager.Settings;

            switch (upgrade)
            {
                case "SpeedNanites":
                    return (v * (float)s.SpeedIncreasePerUpgrade);
                case "PowerNanites":
                    return (v * (float)s.PowerDecreasePerUpgrade);
                case "MedicalNanites":
                    return (v * (float)s.MedicalNanitesPerUpgrade);
                case "MiningNanites":
                    return (v * (float)s.MiningNanitesPerUpgrade);
                case "CleanupNanites":
                    return (v * (float)s.CleanupNanitesPerUpgrade);
                case "DeconstructionNanites":
                    return (v * (float)s.DeconstructionNanitesPerUpgrade);
                case "ProjectionNanites":
                    return (v * (float)s.ProjectionNanitesPerUpgrade);
                case "ConstructionNanites":
                    return (v * (float)s.ConstructionNanitesPerUpgrade);
                case "MinTravelTime":
                    return (v * (float)s.MinTravelTimeReductionPerUpgrade);
                default:
                    return 1f;
            }       
        } 

        /// <summary> Checks in-range beacons and connected grids for all factories in master-slave group. Runs in parallel </summary>
        private void ProcessTargetsParallel()
        {
            try
            {
                if (m_scanBlocksCache.Count < 1)
                {
                    m_totalScanBlocksCount = 0;

                    foreach (var factory in FactoryGroup)
                        PotentialTargetsCount = 0;

                    List<IMySlimBlock> newGridBlocks = new List<IMySlimBlock>();

                    MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                        { InventoryManager.ComponentsRequired.Clear(); });


                    foreach (var target in m_targets)
                    {
                        target.PotentialTargetListCount = 0;
                        if (target is NaniteConstructionTargets || target is NaniteProjectionTargets)
                        {
                            target.CheckBeacons();
                            target.CheckAreaBeacons();
                        }
                        else if (target is NaniteDeconstructionTargets)
                            target.ParallelUpdate(GridGroup, m_scanBlocksCache);
                    }

                    foreach (IMyCubeGrid grid in GridGroup)
                        grid.GetBlocks(newGridBlocks);

                    if (m_potentialInventoryBlocks.Count < 1)
                    {
                        if (m_initInventory)
                            m_initInventory = false;

                        foreach (IMySlimBlock block in newGridBlocks)
                            TryAddPotentialInventoryBlock(block);
                    }
                        

                    foreach (IMySlimBlock block in newGridBlocks)
                        m_scanBlocksCache.Add(new BlockTarget(block)); 

                    foreach (var factory in FactoryGroup)
                        TotalScanBlocksCount = m_scanBlocksCache.Count;
                }

                int counter = 0;
                List<BlockTarget> blocksToGo = new List<BlockTarget>();

                foreach (var block in m_scanBlocksCache)
                {
                    if (counter++ > 500) //lets make this a configurable amount in the future
                        break;

                    blocksToGo.Add(block);
                }

                foreach (var block in blocksToGo)
                    m_scanBlocksCache.Remove(block);

                foreach (var item in m_targets)
                    if (!(item is NaniteDeconstructionTargets))
                        item.ParallelUpdate(GridGroup, blocksToGo);
            }
            catch (InvalidOperationException ex)
            {
                Logging.Instance.WriteLine("NaniteConstructionBlock.ProcessTargetsParallel InvalidOperationException: "
                  + "This is likely due to a list being modified during enumeration in a parallel thread, "
                  + $"which is probably harmless.\n{ex.ToString()}");
            }
            catch (Exception ex) 
            {
                VRage.Utils.MyLog.Default.WriteLineAndConsole($"ProcessTargetsParallel() Error. Clearing blockcache. {ex.ToString()}");
                m_scanBlocksCache.Clear();
            }
        }

        private void ProcessTargets()
        { // Processes found targets by the factory and also moves inventory. Processed mostly in parallel
            try
            {
                Dictionary<string, int> availableComponents = new Dictionary<string, int>();
                InventoryManager.GetAvailableComponents(ref availableComponents);
                
                foreach (var item in m_targets.ToList())
                    if (item is NaniteConstructionTargets || item is NaniteProjectionTargets) 
                        InventoryManager.SubtractAvailableComponents(item.TargetList.Cast<IMySlimBlock>().ToList(), ref availableComponents, item is NaniteProjectionTargets);            

                var factoryBlockList = NaniteConstructionManager.GetConstructionBlocks((IMyCubeGrid)ConstructionBlock.CubeGrid);

                foreach (var item in m_targets.ToList())
                {
                    m_potentialTargetsCount += item.PotentialTargetList.Count;
                    item.PotentialTargetListCount += item.PotentialTargetList.Count;

                    foreach (var slave in Slaves)
                        PotentialTargetsCount = m_potentialTargetsCount;
                        
                    item.FindTargets(ref availableComponents, factoryBlockList);
                }

                availableComponents = new Dictionary<string, int>();
                InventoryManager.GetAvailableComponents(ref availableComponents);

                foreach (var item in m_targets.ToList())
                    if ((item is NaniteConstructionTargets) || (item is NaniteProjectionTargets))
                        InventoryManager.SubtractAvailableComponents(item.TargetList.Cast<IMySlimBlock>().ToList(), ref availableComponents, item is NaniteProjectionTargets);
                
                foreach (var item in m_targets.ToList())
                    if ((item is NaniteConstructionTargets) || (item is NaniteProjectionTargets)) 
                        InventoryManager.SetupRequiredComponents(item.TargetList.Cast<IMySlimBlock>().ToList(), 
                          item.PotentialTargetList.Cast<IMySlimBlock>().ToList(), item.GetMaximumTargets(), 
                          ref availableComponents, item is NaniteProjectionTargets);
            }
            catch (Exception ex) 
                {VRage.Utils.MyLog.Default.WriteLineAndConsole($"Nanite Construction Factory: Exception at NaniteConstructionBlock.ProcessTargets:\n{ex.ToString()}");}
        }
        #endregion

        #region Emissives, effects and Factory State methods
        private void UpdateSpoolPosition()
        {
            if (m_factoryState == FactoryStates.SpoolingUp && (m_spoolPosition += (int)(1000f / 60f)) >= m_spoolingTime)
            {
                m_spoolPosition = m_spoolingTime;
                m_forceProcessState = true; // ensures no "lag" between spooling up and active states
            }

            else if (m_factoryState == FactoryStates.SpoolingDown && (m_spoolPosition -= (int)(1000f / 60f)) <= 0)
            {
                m_spoolPosition = 0;
                m_forceProcessState = true; // ensures no "lag" between spooling down and enabled states
            }
        }

        private void DrawEffects()
        { // Draws effects (lightning and center spinning orb)
            if (!Sync.IsClient)
                return;

            foreach (var item in m_effects)
                switch (m_factoryState)
                {
                    case FactoryStates.Active:
                        item.ActiveUpdate();
                        break;
                    case FactoryStates.SpoolingUp:
                        item.ActivatingUpdate(m_spoolPosition, m_spoolingTime);
                        break;
                    case FactoryStates.SpoolingDown:
                        item.DeactivatingUpdate(m_spoolPosition, m_spoolingTime);
                        break;
                    default:
                        item.InactiveUpdate();
                        break;
                }
        }

        private void UpdateClientEmissives()
        { // Change color of emissives on the block model to appropriate color. Client only.
            float emissivity = 1.0f;
            IMyFunctionalBlock blockEntity = (IMyFunctionalBlock)m_constructionBlock;
            if (!blockEntity.Enabled || !m_isFunctional)
            {
                m_soundEmitter.StopSound(true);
                MyCubeBlockEmissive.SetEmissiveParts((MyEntity)m_constructionBlock, emissivity, Color.Red, Color.White);
            }
            else
                switch (m_factoryState)
                {
                    case FactoryStates.Active:
                        MyCubeBlockEmissive.SetEmissiveParts((MyEntity)m_constructionBlock, emissivity, 
                          Color.FromNonPremultiplied(new Vector4(0.05f, 0.05f, 0.35f, 0.75f)) 
                          * (((float)m_spoolPosition / m_spoolingTime) + 0.1f), Color.White);
                        break;

                    case FactoryStates.SpoolingUp:
                        if (m_spoolPosition >= m_spoolingTime)
                            m_soundEmitter.PlaySound(m_soundPair, true);

                        MyCubeBlockEmissive.SetEmissiveParts((MyEntity)m_constructionBlock, emissivity, 
                          Color.FromNonPremultiplied(new Vector4(0.05f, 0.05f, 0.35f, 0.75f)) 
                          * (((float)m_spoolPosition / m_spoolingTime) + 0.1f), Color.White);
                        break;

                    case FactoryStates.SpoolingDown:
                        m_soundEmitter.StopSound(true);
                        MyCubeBlockEmissive.SetEmissiveParts((MyEntity)m_constructionBlock, emissivity, 
                          Color.FromNonPremultiplied(new Vector4(0.05f, 0.05f, 0.35f, 0.75f)) 
                          * (((float)m_spoolPosition / m_spoolingTime) + 0.1f), Color.White);
                        break;

                    case FactoryStates.MissingPower:
                        emissivity = (float)MathHelper.Clamp(0.5 * (1 + Math.Sin(2 * 3.14 * m_updateCount * 8)), 0.0, 1.0);
                        MyCubeBlockEmissive.SetEmissiveParts((MyEntity)m_constructionBlock, emissivity, Color.DarkGoldenrod * emissivity, Color.White);
                        break;

                    case FactoryStates.MissingParts:
                        emissivity = (float)MathHelper.Clamp(0.5 * (1 + Math.Sin(2 * 3.14 * m_updateCount * 8)), 0.0, 1.0);
                        MyCubeBlockEmissive.SetEmissiveParts((MyEntity)m_constructionBlock, emissivity, Color.DeepPink * emissivity, Color.White);
                        break;

                    case FactoryStates.InvalidTargets:
                        emissivity = (float)MathHelper.Clamp(0.5 * (1 + Math.Sin(2 * 3.14 * m_updateCount * 8)), 0.0, 1.0);
                        MyCubeBlockEmissive.SetEmissiveParts((MyEntity)m_constructionBlock, emissivity, Color.Lime * emissivity, Color.White);
                        break;

                    case FactoryStates.Enabled:
                        MyCubeBlockEmissive.SetEmissiveParts((MyEntity)m_constructionBlock, emissivity, Color.Green, Color.White);
                        break;

                    default:
                        m_soundEmitter.StopSound(true);
                        MyCubeBlockEmissive.SetEmissiveParts((MyEntity)m_constructionBlock, emissivity, Color.Red, Color.White);
                        break;
                }

            m_clientEmissivesUpdate = false;
        }

        private void ProcessState()
        { // Check state of factory, controls emissives and other things

            if (m_updateCount < m_forceProcessStateCooldown)
                return;
            
            if (m_forceProcessState)
            {
                m_forceProcessStateCooldown = m_updateCount + 120;
                m_forceProcessState = false;
            }              
            else if (m_updateCount % 120 != 0)
                return;

            MyAPIGateway.Parallel.Start(() =>
            {
                try
                {
                    ProcessInventory();
                    UpdatePower();
                    IMyFunctionalBlock blockEntity = (IMyFunctionalBlock)ConstructionBlock;
                    m_targetsCount = m_targets.Sum(x => x.TargetList.Count);
                    FactoryStates newState = m_factoryState;

                    if (!blockEntity.Enabled || !m_isFunctional)
                        newState = FactoryStates.Disabled;

                    if ( (Master != null && (Master.FactoryState == FactoryStates.Active || Master.FactoryState == FactoryStates.SpoolingUp))
                      ||  ( (m_targetsCount > 0 && IsPowered()) || m_particleManager.Particles.Count > 0 ) )
                    {
                        if (m_spoolPosition == m_spoolingTime)
                        {
                            newState = FactoryStates.Active;
                            MyAPIGateway.Utilities.InvokeOnGameThread(() => 
                            {
                                if (m_lastState != newState)
                                    m_updateConnectedInventory = true;
                            });
                        }
                        else
                            newState = FactoryStates.SpoolingUp;
                    }
                    else if (Master != null && m_spoolPosition > 0)
                        newState = FactoryStates.SpoolingDown;
                    else if (Master != null && Master.FactoryState != FactoryStates.Disabled && Master.FactoryState != FactoryStates.SpoolingDown)
                        newState = Master.FactoryState;

                    else if (Master == null)
                    {
                        if (m_targetsCount == 0 && m_potentialTargetsCount > 0 && !IsPowered())
                            newState = FactoryStates.MissingPower;

                        else if (m_targetsCount == 0 && m_potentialTargetsCount > 0)
                        {
                            newState = FactoryStates.InvalidTargets;

                            foreach (var item in InventoryManager.ComponentsRequired.ToList())
                                if (item.Value <= 0)
                                    MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                                        { InventoryManager.ComponentsRequired.Remove(item.Key); });

                            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                            {
                                if (InventoryManager.ComponentsRequired.Count > 0)
                                {
                                    m_factoryState = FactoryStates.MissingParts;
                                    if (m_lastState != m_factoryState)
                                        m_updateConnectedInventory = true;
                                }
                            });
                        }
                        else if (blockEntity.Enabled)
                        { 
                            if (m_spoolPosition <= 0)
                                MyAPIGateway.Utilities.InvokeOnGameThread(() => 
                                {
                                    m_spoolPosition = 0;
                                    m_factoryState = FactoryStates.Enabled;
                                });

                            else
                                newState = FactoryStates.SpoolingDown;
                        }
                        else
                            newState = FactoryStates.Disabled;
                    }

                    MyAPIGateway.Utilities.InvokeOnGameThread(() => 
                    {
                        if (m_factoryState != FactoryStates.Active && m_factoryState != FactoryStates.SpoolingUp 
                          && m_factoryState != FactoryStates.SpoolingDown && m_spoolPosition > 0f)
                        {
                            m_factoryState = FactoryStates.SpoolingDown;
                            newState = FactoryStates.SpoolingDown;
                        }
                            

                        if (m_lastState != m_factoryState)
                            m_lastState = m_factoryState;
                        else if (m_lastState != newState)
                        {
                            m_factoryState = newState;
                            m_lastState = m_factoryState;
                        }

                        SendStateUpdate(m_factoryState);
                    });
                }
                catch (Exception e)
                    { VRage.Utils.MyLog.Default.WriteLineAndConsole($"NaniteConstructionBlock.ProcessState exception: {e.ToString()}"); }
            });
        }
        #endregion

        #region Multiplayer Sync methods

        private void SendStateUpdate(FactoryStates state)
        {
            StateData data = new StateData();
            data.EntityId = ConstructionBlock.EntityId;
            data.State = state;
            SendToPlayerInSyncRange(8950, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
        }

        public void SyncUpdateState(StateData data)
        {
            m_factoryState = data.State;
            if (m_lastState != m_factoryState)
                m_lastState = m_factoryState;
        }

        public void SendAddTarget(IMySlimBlock target, TargetTypes targetType, long projectorId = 0)
        {
            TargetData data = new TargetData();
            data.EntityId = ConstructionBlock.EntityId;
            data.TargetId = target.CubeGrid.EntityId;

            if (projectorId > 0)
                data.TargetId = projectorId;

            data.PositionI = target.Position;
            data.TargetType = targetType;
            SendToPlayerInSyncRange(8951, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
        }

        public void SendAddTarget(IMyEntity target)
        {
            TargetData data = new TargetData();
            data.EntityId = ConstructionBlock.EntityId;
            data.TargetId = target.EntityId;
            data.TargetType = TargetTypes.Floating;
            SendToPlayerInSyncRange(8951, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
        }

        public void SendAddTarget(NaniteMiningItem target)
        {
            TargetData data = new TargetData();
            data.EntityId = ConstructionBlock.EntityId;
            data.TargetId = target.VoxelId;
            data.TargetType = TargetTypes.Voxel;
            data.PositionD = target.Position;
            SendToPlayerInSyncRange(8951, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
        }

        public void SendAddTarget(IMyPlayer target)
        {
            TargetData data = new TargetData();
            data.EntityId = ConstructionBlock.EntityId;
            data.TargetId = target.IdentityId;
            data.TargetType = TargetTypes.Medical;
            SendToPlayerInSyncRange(8951, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
        }

        public void SyncAddTarget(TargetData data)
        {
            Logging.Instance.WriteLine(string.Format("SYNCADD Target: {0} - {1} | {2} - {3}", data.EntityId, data.PositionI, data.PositionD, data.TargetType.ToString()));

            try
            {
                if (data.TargetType == TargetTypes.Medical)
                {
                    var target = GetTarget<NaniteMedicalTargets>().TargetList.FirstOrDefault(x => ((IMyPlayer)x).IdentityId == data.TargetId);
                    if (target == null)
                    {
                        List<IMyPlayer> players = new List<IMyPlayer>();
                        MyAPIGateway.Players.GetPlayers(players);
                        IMyPlayer playerTarget = null;
                        foreach (var item in players)
                            if (item.IdentityId == data.TargetId)
                            {
                                playerTarget = item;
                                break;
                            }

                        if (playerTarget != null)
                            GetTarget<NaniteMedicalTargets>().TargetList.Add(playerTarget);
                    }
                    return;
                }

                if (data.TargetType == TargetTypes.Voxel)
                {
                    var target = GetTarget<NaniteMiningTargets>().TargetList.FirstOrDefault(x => ((NaniteMiningItem)x).Position == new Vector3D(data.PositionD.X, data.PositionD.Y, data.PositionD.Z));
                    if (target == null)
                    {
                        NaniteMiningItem item = new NaniteMiningItem();
                        item.VoxelId = data.TargetId;
                        item.Position = new Vector3D(data.PositionD.X, data.PositionD.Y, data.PositionD.Z);
                        GetTarget<NaniteMiningTargets>().TargetList.Add(item);
                    }

                    return;
                }

                IMyEntity entity;
                if (!MyAPIGateway.Entities.TryGetEntityById(data.TargetId, out entity))
                {
                    Logging.Instance.WriteLine(string.Format("SyncAddTarget Error: Can't locate target entity: {0}", data.TargetId.ToString()));
                    return;
                }

                if (data.TargetType == TargetTypes.Projection || data.TargetType == TargetTypes.Deconstruction || data.TargetType == TargetTypes.Construction)
                {

                    IMySlimBlock slimBlock;
                    if (data.TargetType == TargetTypes.Projection)
                    {
                        IMyProjector projector = entity as IMyProjector;
                        slimBlock = projector.ProjectedGrid.GetCubeBlock(new Vector3I(data.PositionI.X, data.PositionI.Y, data.PositionI.Z)) as IMySlimBlock;
                        if (slimBlock != null && slimBlock.FatBlock != null && slimBlock.FatBlock.Closed)
                        {
                            Logging.Instance.WriteLine(string.Format("SyncAddTarget Error: Can't get projection target block: {0}", data.PositionI.ToString()));
                            return;
                        }
                        Logging.Instance.WriteLine(string.Format("SyncAddTarget: Found block: {0}", slimBlock.Position.ToString()));
                    }
                    else
                    {
                        MyCubeGrid grid = (MyCubeGrid)entity;
                        slimBlock = grid.GetCubeBlock(new Vector3I(data.PositionI.X, data.PositionI.Y, data.PositionI.Z)) as IMySlimBlock;
                        if (slimBlock != null && slimBlock.FatBlock != null && slimBlock.FatBlock.Closed)
                        {
                            Logging.Instance.WriteLine(string.Format("SyncAddTarget Error: Can't get target block: {0}", data.PositionI.ToString()));
                            return;
                        }
                    }

                    if (data.TargetType == TargetTypes.Projection && !GetTarget<NaniteProjectionTargets>().TargetList.Contains(slimBlock))
                        GetTarget<NaniteProjectionTargets>().TargetList.Add(slimBlock);

                    else if (data.TargetType == TargetTypes.Deconstruction && !GetTarget<NaniteDeconstructionTargets>().TargetList.Contains(slimBlock))
                        GetTarget<NaniteDeconstructionTargets>().TargetList.Add(slimBlock);

                    else if (!GetTarget<NaniteConstructionTargets>().TargetList.Contains(slimBlock))
                        GetTarget<NaniteConstructionTargets>().TargetList.Add(slimBlock);
                }
                else if (data.TargetType == TargetTypes.Floating && !GetTarget<NaniteFloatingTargets>().TargetList.Contains(entity))
                    GetTarget<NaniteFloatingTargets>().TargetList.Add(entity);
            }
            finally
            {
                CleanupTargets();
            }
        }

        public void SendCompleteTarget(IMySlimBlock target, TargetTypes targetType, long projectorId = 0)
        {
            TargetData data = new TargetData();
            data.EntityId = ConstructionBlock.EntityId;
            data.TargetId = target.CubeGrid.EntityId;

            if (projectorId > 0)
                data.TargetId = projectorId;

            data.PositionI = target.Position;
            data.TargetType = targetType;
            SendToPlayerInSyncRange(8952, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
        }

        public void SendCompleteTarget(IMyEntity target)
        {
            TargetData data = new TargetData();
            data.EntityId = ConstructionBlock.EntityId;
            data.TargetId = target.EntityId;
            data.TargetType = TargetTypes.Floating;
            SendToPlayerInSyncRange(8952, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
        }

        public void SendCompleteTarget(NaniteMiningItem target)
        {
            TargetData data = new TargetData();
            data.EntityId = ConstructionBlock.EntityId;
            data.TargetId = target.VoxelId;
            data.PositionD = target.Position;
            data.TargetType = TargetTypes.Voxel;
            SendToPlayerInSyncRange(8952, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
        }

        public void SendCompleteTarget(IMyPlayer target)
        {
            TargetData data = new TargetData();
            data.EntityId = ConstructionBlock.EntityId;
            data.TargetId = target.IdentityId;
            data.TargetType = TargetTypes.Medical;
            SendToPlayerInSyncRange(8952, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
        }

        public void SyncCompleteTarget(TargetData data)
        {
            try
            {
                Logging.Instance.WriteLine(string.Format("SYNCCOMPLETE Target: {0} - {1} | {2} - {3}", data.EntityId, data.PositionI, data.PositionD, data.TargetType.ToString()));

                if (data.TargetType == TargetTypes.Floating)
                {
                    GetTarget<NaniteFloatingTargets>().CompleteTarget(data.TargetId);
                    return;
                }
                else if (data.TargetType == TargetTypes.Medical)
                {
                    List<IMyPlayer> players = new List<IMyPlayer>();
                    MyAPIGateway.Players.GetPlayers(players);
                    IMyPlayer playerTarget = null;
                    foreach (var item in players)
                        if (item.IdentityId == data.TargetId)
                        {
                            playerTarget = item;
                            break;
                        }

                    if (playerTarget != null)
                        GetTarget<NaniteMedicalTargets>().CompleteTarget(playerTarget);

                    return;
                }
                else if (data.TargetType == TargetTypes.Voxel)
                {
                    var target = GetTarget<NaniteMiningTargets>().TargetList.FirstOrDefault(x => ((NaniteMiningItem)x).Position == new Vector3D(data.PositionD.X, data.PositionD.Y, data.PositionD.Z)) as NaniteMiningItem;
                    if (target != null)
                        GetTarget<NaniteMiningTargets>().CompleteTarget(target);

                    return;
                }
                else if (data.TargetType == TargetTypes.Deconstruction)
                {
                    foreach (IMySlimBlock item in GetTarget<NaniteDeconstructionTargets>().TargetList.ToList())
                        if (item.CubeGrid.EntityId == data.TargetId && item.Position == new Vector3I(data.PositionI.X, data.PositionI.Y, data.PositionI.Z))
                        {
                            GetTarget<NaniteDeconstructionTargets>().CompleteTarget(item);
                            return;
                        }

                    Logging.Instance.WriteLine(string.Format("PROBLEM Unable to remove (dec): {0} - {1} | {2}", data.EntityId, data.PositionI, data.PositionD));
                    return;
                }
                else if (data.TargetType == TargetTypes.Projection)
                {
                    foreach (IMySlimBlock item in GetTarget<NaniteProjectionTargets>().TargetList.ToList())
                        if (item.Position == new Vector3I(data.PositionI.X, data.PositionI.Y, data.PositionI.Z) && NaniteProjectionTargets.GetProjectorByBlock(item) == data.TargetId)
                        {
                            GetTarget<NaniteProjectionTargets>().CompleteTarget(item);
                            return;
                        }
                }

                IMySlimBlock block = null;
                Vector3I position = new Vector3I(data.PositionI.X, data.PositionI.Y, data.PositionI.Z);

                block = GetTarget<NaniteConstructionTargets>().TargetList.FirstOrDefault(x => ((IMySlimBlock)x).Position == position && ((IMySlimBlock)x).CubeGrid.EntityId == data.TargetId) as IMySlimBlock;
                if (block != null)
                {
                    GetTarget<NaniteConstructionTargets>().CompleteTarget(block);
                    return;
                }

                if (block == null)
                    Logging.Instance.WriteLine(string.Format("PROBLEM Unable to remove (oth): {0} - {1} | {2}", data.EntityId, data.PositionI, data.PositionD));
            }
            finally
                {CleanupTargets();}
        }

        public void SendCancelTarget(IMySlimBlock target, TargetTypes targetType, long projectorId = 0)
        {
            TargetData data = new TargetData();
            data.EntityId = ConstructionBlock.EntityId;
            data.TargetId = target.CubeGrid.EntityId;

            if (projectorId > 0)
                data.TargetId = projectorId;

            data.PositionI = target.Position;
            data.TargetType = targetType;
            SendToPlayerInSyncRange(8953, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
        }

        public void SendCancelTarget(IMyEntity target)
        {
            TargetData data = new TargetData();
            data.EntityId = ConstructionBlock.EntityId;
            data.TargetId = target.EntityId;
            data.TargetType = TargetTypes.Floating;
            SendToPlayerInSyncRange(8953, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
        }

        public void SendCancelTarget(IMyPlayer target)
        {
            TargetData data = new TargetData();
            data.EntityId = ConstructionBlock.EntityId;
            data.TargetId = target.IdentityId;
            data.TargetType = TargetTypes.Medical;
            SendToPlayerInSyncRange(8953, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
        }

        public void SyncCancelTarget(TargetData data)
        {
            Logging.Instance.WriteLine(string.Format("SYNCCANCEL Target: {0} - {1} | {2} - {3}", data.EntityId, data.PositionI, data.PositionD, data.TargetType.ToString()));
            try
            {
                if (data.TargetType == TargetTypes.Floating)
                {
                    var floatingTarget = GetTarget<NaniteFloatingTargets>();
                    if (floatingTarget != null)
                        floatingTarget.CancelTarget(data.TargetId);

                    return;
                }
                else if (data.TargetType == TargetTypes.Voxel)
                {
                    var target = GetTarget<NaniteMiningTargets>().TargetList.FirstOrDefault(x => ((NaniteMiningItem)x).Position == new Vector3D(data.PositionD.X, data.PositionD.Y, data.PositionD.Z)) as NaniteMiningItem;
                    if (target != null)
                        GetTarget<NaniteMiningTargets>().CompleteTarget(target);

                    return;
                }
                else if (data.TargetType == TargetTypes.Medical)
                {
                    List<IMyPlayer> players = new List<IMyPlayer>();
                    MyAPIGateway.Players.GetPlayers(players);
                    IMyPlayer playerTarget = null;
                    foreach (var item in players)
                        if (item.IdentityId == data.TargetId)
                        {
                            playerTarget = item;
                            break;
                        }

                    if (playerTarget != null)
                        GetTarget<NaniteMedicalTargets>().CancelTarget(playerTarget);

                    return;
                }
                else if (data.TargetType == TargetTypes.Deconstruction)
                {
                    foreach (IMySlimBlock item in m_targets.First(x => x is NaniteDeconstructionTargets).TargetList.ToList())
                        if (item.CubeGrid.EntityId == data.TargetId && item.Position == new Vector3I(data.PositionI.X, data.PositionI.Y, data.PositionI.Z))
                        {
                            var deconstructTarget = GetTarget<NaniteDeconstructionTargets>();
                            if (deconstructTarget != null)
                                deconstructTarget.CancelTarget(item);

                            return;
                        }

                    Logging.Instance.WriteLine(string.Format("PROBLEM Unable to remove (dec): {0} - {1} | {2}", data.EntityId, data.PositionI));
                    return;
                }
                else if (data.TargetType == TargetTypes.Projection)
                    foreach (IMySlimBlock item in GetTarget<NaniteProjectionTargets>().TargetList.ToList())
                        if (item.Position == new Vector3I(data.PositionI.X, data.PositionI.Y, data.PositionI.Z) && NaniteProjectionTargets.GetProjectorByBlock(item) == data.TargetId)
                        {
                            GetTarget<NaniteProjectionTargets>().CancelTarget(item);
                            return;
                        }

                IMySlimBlock block = null;
                Vector3I position = new Vector3I(data.PositionI.X, data.PositionI.Y, data.PositionI.Z);

                block = GetTarget<NaniteConstructionTargets>().TargetList.FirstOrDefault(x => ((IMySlimBlock)x).Position == position && ((IMySlimBlock)x).CubeGrid.EntityId == data.TargetId) as IMySlimBlock;
                if (block != null)
                {
                    GetTarget<NaniteConstructionTargets>().CancelTarget(block);
                    return;
                }

                Logging.Instance.WriteLine(string.Format("PROBLEM Unable to remove (oth): {0} - {1} | {2}", data.EntityId, data.PositionI, data.PositionD));
            }
            finally
                {CleanupTargets();}
        }

        private void SendDetails()
        {
            DetailData data = new DetailData();
            data.EntityId = ConstructionBlock.EntityId;
            data.Details = m_syncDetails.ToString();
            SendToPlayerInSyncRange(8954, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
        }

        private void SendFactoryGroup()
        {
            try
            {
                FactoryGroupData data = new FactoryGroupData();
                data.EntityId = ConstructionBlock.EntityId;
                data.FactoryGroup = new List<long>();

                foreach (var factory in FactoryGroup)
                    data.FactoryGroup.Add(factory.EntityId);
                
                SendToPlayerInSyncRange(8974, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
            }
            catch (Exception e)
                {VRage.Utils.MyLog.Default.WriteLineAndConsole($"NaniteConstructionBlock.SendFactoryGroup exception: {e}");}
        }

        public void SyncDetails(DetailData data)
        {
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                m_syncDetails.Clear();
                m_syncDetails.Append(data.Details);
            });
        }

        public void SyncFactoryGroup(FactoryGroupData data)
        {
            List<NaniteConstructionBlock> factoryGroup = new List<NaniteConstructionBlock>();
            foreach (long factoryID in data.FactoryGroup)
                try
                    {factoryGroup.Add(NaniteConstructionManager.NaniteBlocks[factoryID]);}
                catch (Exception e)
                    {VRage.Utils.MyLog.Default.WriteLineAndConsole($"NaniteConstructionBlock.SyncFactoryGroup exception: {e}");}
                
            FactoryGroup = factoryGroup.ToList();
        }

        public void SendStartParticleEffect(long entityId, Vector3I position, int effectId)
        {
            ParticleData data = new ParticleData();
            data.EntityId = ConstructionBlock.EntityId;
            data.TargetId = entityId;
            data.PositionX = position.X;
            data.PositionY = position.Y;
            data.PositionZ = position.Z;
            data.EffectId = effectId;
            MyAPIGateway.Multiplayer.SendMessageToOthers(8958, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
        }

        private void CleanupTargets()
        { // When splits happen, targets grid and position change, which isn't updating properly, this will just remove the target on the client.
            try
            {
                MyAPIGateway.Parallel.Start(() =>
                {
                    foreach (var item in m_targets.ToList())
                    {
                        if (item.TargetList == null || item.TargetList.Count < 1)
                            continue;

                        foreach (var targetItem in item.TargetList.ToList())
                        {
                            if (!(targetItem is IMySlimBlock))
                                continue;

                            var target = targetItem as IMySlimBlock;
                            if (target == null)
                                continue;

                            if (item is NaniteDeconstructionTargets && (target.IsDestroyed || target.IsFullyDismounted 
                            || (target.CubeGrid != null && target.CubeGrid.GetCubeBlock(target.Position) == null) || (target.FatBlock != null && target.FatBlock.Closed)))
                                MyAPIGateway.Utilities.InvokeOnGameThread(() => {item.CompleteTarget(target);});

                            else if (target.IsDestroyed || target.IsFullyDismounted || (target.CubeGrid != null && target.CubeGrid.GetCubeBlock(target.Position) == null) 
                            || (target.FatBlock != null && target.FatBlock.Closed))
                                MyAPIGateway.Utilities.InvokeOnGameThread(() => {item.CancelTarget(target);});

                            else if(item is NaniteConstructionTargets && target.IsFullIntegrity && !target.HasDeformation)
                                MyAPIGateway.Utilities.InvokeOnGameThread(() => {item.CompleteTarget(target);});

                            //else if(!item.IsEnabled())
                                //MyAPIGateway.Utilities.InvokeOnGameThread(() => {item.CancelTarget(target);});
                        }
                    }
                });
            }
            catch(Exception ex)
                {VRage.Utils.MyLog.Default.WriteLineAndConsole($"NaniteConstructionBlock.CleanupTargets Exception: {ex.ToString()}");}
        }

        public T GetTarget<T>() where T : NaniteTargetBlocksBase
        {
            foreach (var item in m_targets)
                if (item is T)
                    return (T)item;

            return null;
        }

        public void SyncTerminalSettings(NaniteTerminalSettings settings)
        {
            if (!NaniteConstructionManager.TerminalSettings.ContainsKey(m_constructionBlock.EntityId))
                NaniteConstructionManager.TerminalSettings.Add(m_constructionBlock.EntityId, settings);

            NaniteConstructionManager.TerminalSettings[m_constructionBlock.EntityId] = settings;
        }

        private void SendToPlayerInSyncRange(ushort id, byte[] bytes)
        {
            MyAPIGateway.Parallel.Start(() =>
            {
                var distSq = MyAPIGateway.Session.SessionSettings.SyncDistance;
                distSq += 1000; // some safety padding, avoid desync
                distSq *= distSq;

                var syncPosition = ConstructionBlock.GetPosition();
                var players = new List<IMyPlayer>();
                MyAPIGateway.Players.GetPlayers(players);

                foreach (var p in players.ToList())
                    if (p != null && p.SteamUserId != MyAPIGateway.Multiplayer.MyId && Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= distSq)
                        MyAPIGateway.Utilities.InvokeOnGameThread(() => {MyAPIGateway.Multiplayer.SendMessageTo(id, bytes, p.SteamUserId);});
            });
        }
        #endregion
    }

    public class MyCubeBlockEmissive : MyCubeBlock
    { // Class used to set emissives on a block dynamically
        public static void SetEmissiveParts(MyEntity entity, float emissivity, Color emissivePartColor, Color displayPartColor)
        {
            if (entity != null)
                UpdateEmissiveParts(entity.Render.RenderObjectIDs[0], emissivity, emissivePartColor, displayPartColor);
        }
    }
}
