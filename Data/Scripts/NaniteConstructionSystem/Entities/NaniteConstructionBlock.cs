using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;
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

namespace NaniteConstructionSystem.Entities
{
    public class NaniteConstructionBlock
    {
        public enum FactoryStates
        {
            Disabled,
            Enabled,
            SpoolingUp,
            SpoolingDown,
            MissingParts,
            MissingPower,
            InvalidTargets,
            Active
        }

        private IMyShipWelder m_constructionBlock;
        public IMyShipWelder ConstructionBlock
        {
            get { return m_constructionBlock; }
        }

        private MyCubeBlock m_constructionCubeBlock;

        private List<NaniteTargetBlocksBase> m_targets;
        public List<NaniteTargetBlocksBase> Targets
        {
            get { return m_targets; }
        }

        private bool m_initialize;
        public bool Initialized
        {
            get { return m_initialize; }
        }

        private NaniteParticleManager m_particleManager;
        public NaniteParticleManager ParticleManager
        {
            get { return m_particleManager; }
        }

        private NaniteToolManager m_toolManager;
        public NaniteToolManager ToolManager
        {
            get { return m_toolManager; }
        }

        private NaniteConstructionInventory m_inventoryManager;
        public NaniteConstructionInventory InventoryManager
        {
            get { return m_inventoryManager; }
        }

        private FactoryStates m_factoryState;
        public FactoryStates FactoryState
        {
            get { return m_factoryState; }
        }

        private int m_userDefinedNaniteLimit;
        public int UserDefinedNaniteLimit
        {
            get { return m_userDefinedNaniteLimit; }
        }

        private bool m_updateConnectedInventory;
        public bool UpdateConnectedInventory
        {
            get { return m_updateConnectedInventory; }
            set { m_updateConnectedInventory = value; }
        }

        internal MyResourceSinkInfo ResourceInfo;
        internal MyResourceSinkComponent Sink;

        private float _power = 0.0001f;
        public float Power
        {
            get { return Sink.CurrentInputByType(MyResourceDistributorComponent.ElectricityId); }
        }

        private List<NaniteBlockEffectBase> m_effects;
        private MySoundPair m_soundPair;
        private MyEntity3DSoundEmitter m_soundEmitter;
        private int m_updateCount;
        private FactoryStates m_lastState;
        private int m_spoolPosition;
        private StringBuilder m_syncDetails;
        private Dictionary<MyDefinitionId, MyBlueprintDefinitionBase> m_defCache;
        private StringBuilder m_targetDetails;
        private StringBuilder m_invalidTargetDetails;
        private StringBuilder m_missingComponentsDetails;
        private int m_potentialTargetsCount;
        private int m_targetsCount;
        private bool m_clientEmissivesUpdate;
        private bool m_forceProcessState;
        private List<IMyCubeGrid> GridGroup = new List<IMyCubeGrid>();
        private MyInventory m_constructionBlockInventory = null;

        private const int m_spoolingTime = 3000;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="entity">The IMyEntity of the block</param>
        public NaniteConstructionBlock(IMyEntity entity)
        {
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

        /// <summary>
        /// Actual init.  This occurs once modapi is ready and updating.
        /// </summary>
        private void Initialize()
        {
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
            //m_constructionCubeBlock.CubeGrid.OnBlockRemoved += OnBlockRemoved;
            //m_constructionCubeBlock.CubeGrid.OnBlockAdded += OnBlockAdded;

            Sink = ((MyEntity)m_constructionBlock).Components.Get<MyResourceSinkComponent>();

            CheckGridGroup();
        }

        /// <summary>
        /// Main update loop
        /// </summary>
        public void Update()
        {
            if (ConstructionBlock.Closed)
                return;
            
            m_updateCount++;

            if (!m_initialize)
                Initialize();

            if (m_updateCount % 1800 == 0)
            {
                MyAPIGateway.Parallel.Start(() =>
                {
                    string upgrades = "";
                    foreach (var item in ((MyCubeBlock)m_constructionBlock).UpgradeValues)
                        upgrades += string.Format("({0} - {1}) ", item.Key, item.Value);

                    Logging.Instance.WriteLine(string.Format("STATUS Nanite Factory: {0} - (t: {1}  pt: {2}  pw: {3} st: {4}) - {5}", 
                      ConstructionBlock.EntityId, m_targetsCount, m_potentialTargetsCount, _power, m_factoryState, upgrades));
                });
            }

            if (Sync.IsServer && ConstructionBlock.IsFunctional)
            {
                if (m_updateCount % 60 == 0)
                {
                    ToolManager.Update();
                    InventoryManager.TakeRequiredComponents();
                }

                if (m_updateCount % 300 == 0 && m_updateConnectedInventory)
                {
                    m_updateConnectedInventory = false;
                    CheckGridGroup();
                }
                
                ProcessState();
                ScanForTargets();
            }            
            
            UpdateSpoolPosition();
            ParticleManager.Update();
            DrawEffects();

            if (m_updateCount % 60 == 0)
                ProcessTargetItems();

            if (m_updateCount % 120 == 0)
                m_userDefinedNaniteLimit = NaniteConstructionManager.TerminalSettings[m_constructionBlock.EntityId].MaxNanites;

            if (m_updateCount % 180 == 0)
            {
                ((IMyTerminalBlock)m_constructionBlock).RefreshCustomInfo();
                if (Sync.IsClient)
                {
                    CleanupTargets();
                    if (MyAPIGateway.Gui?.GetCurrentScreen == MyTerminalPageEnum.ControlPanel)
                    {                    
                        ((IMyTerminalBlock)m_constructionBlock).ShowInToolbarConfig = false;
                        ((IMyTerminalBlock)m_constructionBlock).ShowInToolbarConfig = true;
                    }
                }
            }
        }

        public void Unload()
        {
            if (m_effects != null)
                foreach (var item in m_effects)
                    item.Unload();

            if (m_soundEmitter != null)
                m_soundEmitter.StopSound(true);
        }

        public bool IsUserDefinedLimitReached()
        {
            //var totalTargets = ;
            if (m_userDefinedNaniteLimit != 0 && Targets.Sum(x => x.TargetList.Count) >= m_userDefinedNaniteLimit)
                return true;

            return false;
        }

        private void UpdatePower()
        {
            if (!m_constructionBlock.Enabled || !m_constructionBlock.IsFunctional)
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(() => 
                    {Sink.SetRequiredInputByType(MyResourceDistributorComponent.ElectricityId, 0.0001f);});

                return;
            }

            float totalPowerRequired = m_targets.Sum(x => (x.TargetList.Count) * x.GetPowerUsage());

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

        private void ProcessInventory()
        {
            var inventory = ((MyCubeBlock)m_constructionBlock).GetInventory();
            if(inventory.VolumeFillFactor > 0.75f && (GetTarget<NaniteDeconstructionTargets>().TargetList.Count > 0 
              || GetTarget<NaniteFloatingTargets>().TargetList.Count > 0 || GetTarget<NaniteMiningTargets>().TargetList.Count > 0))
            {
                GridHelper.TryMoveToFreeCargo((MyCubeBlock)m_constructionBlock, InventoryManager.connectedInventory, true);
                Logging.Instance.WriteLine($"PUSHING Factory inventory over 75% full: {m_constructionBlock.EntityId}");
            }
        }

        private void OnBlockAdded(IMySlimBlock block)
        {
            Logging.Instance.WriteLine("Block added to grid. Attempting to add to inventory.");
            MyAPIGateway.Parallel.Start(() =>
                {TryAddToInventoryGroup(block);});
        }

        private void TryAddToInventoryGroup(object block)
        {
            IMyInventory inv = null;
            if (GridHelper.IsValidInventoryConnection(m_constructionBlockInventory, block, out inv))
            {
                Logging.Instance.WriteLine("BEEEEEEEEEEEEEEEEEEEEEEEEEP.");
                if (!InventoryManager.connectedInventory.Contains(inv))
                {
                    Logging.Instance.WriteLine("Adding inventory block to connected inventory.");
                    lock (InventoryManager.connectedInventory)
                        InventoryManager.connectedInventory.Add(inv);
                }
            }
        }

        // Checks if the grid group has changed and quickly scans/adds any inventory blocks. Removes event handlers
        private void CheckGridGroup()
        {
            MyAPIGateway.Parallel.Start(() =>
            {
                try
                {   
                    List<IMyCubeGrid> removalList = new List<IMyCubeGrid>();
                    List<IMyCubeGrid> newGroup = new List<IMyCubeGrid>(MyAPIGateway.GridGroups.GetGroup((IMyCubeGrid)m_constructionCubeBlock.CubeGrid, GridLinkTypeEnum.Physical));
                    foreach(IMyCubeGrid grid in GridGroup)
                    {
                        if (!newGroup.Contains(grid))
                        {
                            Logging.Instance.WriteLine("Removing disconnected grid from grid group.");
                            removalList.Add(grid);
                            grid.OnBlockAdded -= OnBlockAdded;
                        }
                    }
                    foreach(IMyCubeGrid grid in removalList)
                        GridGroup.Remove(grid);

                    foreach (IMyCubeGrid grid in newGroup)
                    {
                        if (!GridGroup.Contains(grid))
                        {
                            Logging.Instance.WriteLine("Adding new grid to grid group.");
                            GridGroup.Add(grid);
                            BuildConnectedInventory(grid);
                            grid.OnBlockAdded += OnBlockAdded;
                        }
                    }
                }
                catch (Exception ex)
                    {VRage.Utils.MyLog.Default.WriteLineAndConsole($"CheckGridGroup() Error: {ex.ToString()}");}
            });
        }

        //Scans and adds inventory blocks to a grid in the gridgroup. Adds an event handlers
        public void BuildConnectedInventory(IMyCubeGrid grid)
        {
            MyAPIGateway.Parallel.StartBackground(() =>
            {
                try
                {                   
                    ConcurrentBag<IMySlimBlock> slimBlocks = new ConcurrentBag<IMySlimBlock>(((MyCubeGrid)grid).GetBlocks());
                    foreach (IMySlimBlock SlimBlock in slimBlocks)
                    {
                        TryAddToInventoryGroup(SlimBlock);
                    }
                }
                catch (Exception ex)
                    {VRage.Utils.MyLog.Default.WriteLineAndConsole($"BuildConnectedInventory() Error: {ex.ToString()}");}
            });
        }

        private void ProcessAssemblerQueue()
        {
            if (!NaniteConstructionManager.TerminalSettings.ContainsKey(m_constructionBlock.EntityId) 
              || !NaniteConstructionManager.TerminalSettings[m_constructionBlock.EntityId].UseAssemblers 
              || InventoryManager.ComponentsRequired.Count < 1) 
                return;

            List<IMyProductionBlock> assemblerList = new List<IMyProductionBlock>();

            lock (InventoryManager.connectedInventory)
            {
                foreach (var inv in InventoryManager.connectedInventory)
                {
                    IMyEntity entity = inv.Owner as IMyEntity;
                    if (entity == null) 
                        continue;

                    IMyAssembler assembler = entity as IMyAssembler;
                    if (assembler == null || assembler.Mode == Sandbox.ModAPI.Ingame.MyAssemblerMode.Disassembly) 
                        continue;

                    assemblerList.Add((IMyProductionBlock)assembler);
                }
            }

            if (assemblerList.Count < 1) 
                return;

            Dictionary<string, int> missing = new Dictionary<string, int>();
            Dictionary<string, int> available = new Dictionary<string, int>();
            InventoryManager.GetAvailableComponents(ref available);
            GetMissingComponentsPotentialTargets<NaniteConstructionTargets>(missing, available);
            GetMissingComponentsPotentialTargets<NaniteProjectionTargets>(missing, available);

            foreach (var item in missing)
            {
                var def = MyDefinitionManager.Static.TryGetBlueprintDefinitionByResultId(new MyDefinitionId(typeof(MyObjectBuilder_Component), item.Key));
                if (def == null) 
                    continue;

                // If this is some sort of weird modded definition, then we need to find the vanilla definition (ug)
                if (def.Results != null && def.Results[0].Amount > 1)
                {
                    if (m_defCache.ContainsKey(new MyDefinitionId(typeof(MyObjectBuilder_Component), item.Key))) 
                        def = m_defCache[new MyDefinitionId(typeof(MyObjectBuilder_Component), item.Key)]; 

                    else
                    {
                        foreach (var defTest in MyDefinitionManager.Static.GetBlueprintDefinitions())
                        {
                            if (defTest.Results != null && defTest.Results[0].Amount == 1 && defTest.Results[0].Id == new MyDefinitionId(typeof(MyObjectBuilder_Component), item.Key))
                            {
                                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                                    {m_defCache.Add(new MyDefinitionId(typeof(MyObjectBuilder_Component), item.Key), defTest);});
                                break;
                            }
                        }
                    }
                }

                if (ItemIsAlreadyQueuedInAssembler(def, item.Value, assemblerList))
                    continue;

                int blueprintCount = assemblerList.Sum(x => x.GetQueue().Sum(y => y.Blueprint == def ? (int)y.Amount : 0));
                int availableCount = 0;

                if (available.ContainsKey(item.Key)) 
                    availableCount = available[item.Key];

                if (blueprintCount >= item.Value - availableCount) 
                    continue;

                var assemblers = assemblerList.Where(x => NaniteConstructionManager.AssemblerSettings.ContainsKey(x.EntityId) 
                  && NaniteConstructionManager.AssemblerSettings[x.EntityId].AllowFactoryUsage);

                foreach (var target in assemblers.ToList())
                {
                    int amount = (int)Math.Max(((float)(item.Value - blueprintCount) / (float)assemblers.Count()), 1f);

                    Logging.Instance.WriteLine(string.Format("ASSEMBLER Queuing {0} {1} for factory {2} ({3})", 
                      amount, def.Id, m_constructionBlock.CustomName, blueprintCount));

                    MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                        {target.InsertQueueItem(0, def, amount);});
                }
            }
        }

        private bool ItemIsAlreadyQueuedInAssembler(object def, int amountNeeded, List<IMyProductionBlock> assemblerList)
        {
            try
            {
                foreach (var assembler in assemblerList)
                    foreach (var queueItem in assembler.GetQueue())
                        if (queueItem.Blueprint == def && (int)queueItem.Amount >= amountNeeded)
                            return true;
                return false;
            }
            catch (InvalidOperationException ex)
            {
                Logging.Instance.WriteLine("NaniteConstructionBlock.ItemIsAlreadyQueuedInAssembler: A list was modified. Retrying.");
                return ItemIsAlreadyQueuedInAssembler(def, amountNeeded, assemblerList);
            }           
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
        private void ScanForTargets()
        {
            if (m_factoryState != FactoryStates.Disabled && m_factoryState != FactoryStates.MissingPower && m_updateCount % 300 == 0)
            {
                MyAPIGateway.Parallel.StartBackground(() =>
                {
                    DateTime start = DateTime.Now;
                    try
                    {
                        ProcessTargetsParallel();
                        ProcessAssemblerQueue();
                        ProcessTargets();
                        Logging.Instance.WriteLine($"ScanForTargets {ConstructionBlock.EntityId}: {(DateTime.Now - start).TotalMilliseconds}ms");
                    }
                    catch (InvalidOperationException ex)
                    {
                        Logging.Instance.WriteLine("ScanForTargets InvalidOperationException: "
                          + "This is likely due to a list being modified during enumeration in a parallel thread, "
                          + $"which is probably harmless.\n{ex.ToString()}");
                    }
                    catch (Exception ex) when (ex.ToString().Contains("IndexOutOfRangeException")) //because Keen thinks we shouldn't have access to this exception ...
                    {
                        Logging.Instance.WriteLine("ScanForTargets IndexOutOfRangeException: "
                          + "This is likely due to a list being modified during enumeration in a parallel thread, "
                          + $"which is probably harmless.\n{ex.ToString()}");
                    }
                    catch (Exception ex)
                    {
                        VRage.Utils.MyLog.Default.WriteLineAndConsole($"NaniteConstructionBlock.ScanForTargets Error: {ex.ToString()}");
                    }
                });
            }
        }

        /// <summary>
        /// Processes targets (construction and projection)
        /// </summary>
        private void ProcessTargetItems()
        {
            try
            {
                foreach (var item in m_targets)
                    item.Update();
            }
            catch (Exception ex)
            {
                VRage.Utils.MyLog.Default.WriteLineAndConsole($"NaniteConstructionBlock.ProcessTargetItems() Exception: {ex.ToString()}");
            }
        }

        /// <summary>
        /// Draws effects (lightning and center spinning orb)
        /// </summary>
        private void DrawEffects()
        {
            if (!Sync.IsClient)
                return;

            foreach (var item in m_effects)
            {
                if (m_factoryState == FactoryStates.Active)
                    item.ActiveUpdate();
                else if (m_factoryState == FactoryStates.SpoolingUp)
                    item.ActivatingUpdate(m_spoolPosition, m_spoolingTime);
                else if (m_factoryState == FactoryStates.SpoolingDown)
                    item.DeactivatingUpdate(m_spoolPosition, m_spoolingTime);
                else
                    item.InactiveUpdate();
            }
        }

        /// <summary>
        /// Walking the grid looking for target blocks. All done in a thread
        /// </summary>
        private void ProcessTargetsParallel()
        {
            try
            {
                List<IMyCubeGrid> grids = MyAPIGateway.GridGroups.GetGroup((IMyCubeGrid)m_constructionCubeBlock.CubeGrid, GridLinkTypeEnum.Physical);
                List<IMySlimBlock> blocks = new List<IMySlimBlock>();

                try
                {
                    foreach (IMyCubeGrid grid in grids)
                        grid.GetBlocks(blocks);     
                }
                catch (InvalidOperationException ex)
                {
                    Logging.Instance.WriteLine("NaniteConstructionBlock.ProcessTargetsParallel InvalidOperationException: "
                          + "This is likely due to a list being modified during enumeration in a parallel thread, "
                          + $"which is probably harmless.\n{ex.ToString()}");
                }

                foreach (var item in m_targets)
                    item.ParallelUpdate(grids, blocks);
            }
            catch (Exception ex) 
                {VRage.Utils.MyLog.Default.WriteLineAndConsole($"ProcessTargetsParallel() Error {ex.ToString()}");}
        }

        /// <summary>
        /// Processes found targets by the factory and also moves inventory. Processed mostly in parallel
        /// </summary>
        private void ProcessTargets()
        {   
            int pos = 0;
            try
            {
                Dictionary<string, int> availableComponents = new Dictionary<string, int>();
                InventoryManager.GetAvailableComponents(ref availableComponents);
                
                foreach (var item in m_targets.ToList())
                    if (item is NaniteConstructionTargets || item is NaniteProjectionTargets) 
                        InventoryManager.SubtractAvailableComponents(item.TargetList.Cast<IMySlimBlock>().ToList(), ref availableComponents, item is NaniteProjectionTargets);            

                pos = 1;
                var factoryBlockList = NaniteConstructionManager.GetConstructionBlocks((IMyCubeGrid)ConstructionBlock.CubeGrid);

                pos = 2;
                foreach (var item in m_targets.ToList())
                    item.FindTargets(ref availableComponents, factoryBlockList);

                availableComponents = new Dictionary<string, int>();
                pos = 3;
                InventoryManager.GetAvailableComponents(ref availableComponents);

                pos = 4;
                foreach (var item in m_targets.ToList())
                    if ((item is NaniteConstructionTargets) || (item is NaniteProjectionTargets))
                        InventoryManager.SubtractAvailableComponents(item.TargetList.Cast<IMySlimBlock>().ToList(), ref availableComponents, item is NaniteProjectionTargets);

                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                    {InventoryManager.ComponentsRequired.Clear();});
                
                pos = 5;
                foreach (var item in m_targets.ToList())
                    if ((item is NaniteConstructionTargets) || (item is NaniteProjectionTargets)) 
                        InventoryManager.SetupRequiredComponents(item.TargetList.Cast<IMySlimBlock>().ToList(), 
                          item.PotentialTargetList.Cast<IMySlimBlock>().ToList(), item.GetMaximumTargets(), 
                          ref availableComponents, item is NaniteProjectionTargets);
            }
            catch (Exception ex) 
            {
                VRage.Utils.MyLog.Default.WriteLineAndConsole($"Nanite Construction Factory: Exception at NaniteConstructionBlock.ProcessTargets at pos {pos}. \n{ex.ToString()}");
            }
        }

        /// <summary>
        /// Update custom info of the block
        /// </summary>
        /// <param name="block"></param>
        /// <param name="details"></param>
        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder details)
        {
            if (m_factoryState == FactoryStates.Disabled || m_updateCount % 180 != 0)
                return;
            
            details.Clear();

            if (Sync.IsServer)
            {   
                MyAPIGateway.Parallel.Start(() =>
                {
                    StringBuilder targetDetailsParallel = new StringBuilder();
                    StringBuilder invalidTargetDetailsParallel = new StringBuilder();
                    StringBuilder missingComponentsDetailsParallel = new StringBuilder();
                    bool invalidTitleAppended = false;
                    bool missingCompTitleAppended = false;

                    foreach (var item in m_targets.ToList())
                    {
                        targetDetailsParallel.Append("-----\r\n"
                          + $"{item.TargetName} Nanites\r\n"
                          + "-----\r\n"
                          + $"Possible Targets: {item.PotentialTargetList.Count}\r\n"
                          + $"Current Targets: {item.TargetList.Count}\r\n"
                          + $"Max Streams: {item.GetMaximumTargets()}\r\n"
                          + $"MW/Stream: {item.GetPowerUsage()} MW\r\n"
                          + $"Min. Travel Time: {item.GetMinTravelTime()} s\r\n"
                          + $"Travel Speed: {item.GetSpeed()} m/s\r\n");

                        if (item.LastInvalidTargetReason != null && item.LastInvalidTargetReason != "")
                        {
                            if (!invalidTitleAppended)
                            {
                                invalidTargetDetailsParallel.Append("\nTarget information:\r\n");
                                invalidTitleAppended = true;
                            }
                            invalidTargetDetailsParallel.Append($"\n- ({item.TargetName}) " + item.LastInvalidTargetReason);
                        }
                    }

                    if (InventoryManager.ComponentsRequired.Count > 0) 
                    {
                        foreach (var component in InventoryManager.ComponentsRequired.ToList())
                        {
                            if (component.Value > 0)
                            {
                                if (!missingCompTitleAppended)
                                {
                                    missingComponentsDetailsParallel.Append("\r\nMissing components:\r\n");
                                    missingCompTitleAppended = true;
                                }
                                missingComponentsDetailsParallel.Append($"{component.Key}: {component.Value}\r\n");
                            }
                        }
                    }

                    MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                    {
                        m_targetDetails = targetDetailsParallel;
                        m_invalidTargetDetails = invalidTargetDetailsParallel;
                        m_missingComponentsDetails = missingComponentsDetailsParallel;
                    });
                });

                details.Append(m_targetDetails
                  + "-----\r\n"
                  + $"Current Power Required: {_power} MW\r\n"
                  + $"Status: {m_factoryState.ToString()}\r\n"
                  + $"Active Nanites: {m_particleManager.Particles.Count}\r\n");

                if (m_userDefinedNaniteLimit > 0)
                    details.Append($"Maximum Nanites: {m_userDefinedNaniteLimit}\r\n");

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

        
        private void UpdateSpoolPosition()
        {
            if (m_factoryState == FactoryStates.SpoolingUp && (m_spoolPosition += (int)(1000f / 60f)) >= m_spoolingTime)
            {
                m_spoolPosition = m_spoolingTime;
                m_forceProcessState = true;
            }

            else if (m_factoryState == FactoryStates.SpoolingDown && (m_spoolPosition -= (int)(1000f / 60f)) <= 0)
                m_spoolPosition = 0;
        }

        /// <summary>
        /// Change color of emissives on the block model to appropriate color. Client only.
        /// </summary>
        private void UpdateClientEmissives()
        {
            float emissivity = 1.0f;
            IMyFunctionalBlock blockEntity = (IMyFunctionalBlock)m_constructionBlock;
            if (!blockEntity.Enabled || !blockEntity.IsFunctional)
            {
                m_soundEmitter.StopSound(true);
                MyCubeBlockEmissive.SetEmissiveParts((MyEntity)m_constructionBlock, emissivity, Color.Red, Color.White);
            }
            else if (m_factoryState == FactoryStates.Active)
                MyCubeBlockEmissive.SetEmissiveParts((MyEntity)m_constructionBlock, emissivity, 
                  Color.FromNonPremultiplied(new Vector4(0.05f, 0.05f, 0.35f, 0.75f)) 
                  * (((float)m_spoolPosition / m_spoolingTime) + 0.1f), Color.White);

            else if (m_factoryState == FactoryStates.SpoolingUp)
            {
                if (m_spoolPosition >= m_spoolingTime)
                    m_soundEmitter.PlaySound(m_soundPair, true);

                MyCubeBlockEmissive.SetEmissiveParts((MyEntity)m_constructionBlock, emissivity, 
                  Color.FromNonPremultiplied(new Vector4(0.05f, 0.05f, 0.35f, 0.75f)) 
                  * (((float)m_spoolPosition / m_spoolingTime) + 0.1f), Color.White);
            }
            else if (m_factoryState == FactoryStates.SpoolingDown)
            {
                m_soundEmitter.StopSound(true);
                MyCubeBlockEmissive.SetEmissiveParts((MyEntity)m_constructionBlock, emissivity, 
                  Color.FromNonPremultiplied(new Vector4(0.05f, 0.05f, 0.35f, 0.75f)) 
                  * (((float)m_spoolPosition / m_spoolingTime) + 0.1f), Color.White);
            }
            else if (m_factoryState == FactoryStates.MissingPower)
            {
                emissivity = (float)MathHelper.Clamp(0.5 * (1 + Math.Sin(2 * 3.14 * m_updateCount * 8)), 0.0, 1.0);
                MyCubeBlockEmissive.SetEmissiveParts((MyEntity)m_constructionBlock, emissivity, Color.DarkGoldenrod * emissivity, Color.White);
            }
            else if (m_factoryState == FactoryStates.MissingParts)
            {
                emissivity = (float)MathHelper.Clamp(0.5 * (1 + Math.Sin(2 * 3.14 * m_updateCount * 8)), 0.0, 1.0);
                MyCubeBlockEmissive.SetEmissiveParts((MyEntity)m_constructionBlock, emissivity, Color.DeepPink * emissivity, Color.White);
            }
            else if (m_factoryState == FactoryStates.InvalidTargets)
            {
                emissivity = (float)MathHelper.Clamp(0.5 * (1 + Math.Sin(2 * 3.14 * m_updateCount * 8)), 0.0, 1.0);
                MyCubeBlockEmissive.SetEmissiveParts((MyEntity)m_constructionBlock, emissivity, Color.Lime * emissivity, Color.White);
            }
            else if (m_factoryState == FactoryStates.Enabled)
                MyCubeBlockEmissive.SetEmissiveParts((MyEntity)m_constructionBlock, emissivity, Color.Green, Color.White);
            else
            {
                m_soundEmitter.StopSound(true);
                MyCubeBlockEmissive.SetEmissiveParts((MyEntity)m_constructionBlock, emissivity, Color.Red, Color.White);
            }
            m_clientEmissivesUpdate = false;
        }

        /// <summary>
        /// Check state of block (used to be just emissives, but emissive state has been turned into block state.  Will refactor names later)
        /// </summary>
        private void ProcessState()
        {
            if (m_forceProcessState)
                m_forceProcessState = false;
            else if (m_updateCount % 120 != 0)
                return;

            MyAPIGateway.Parallel.Start(() =>
            {
                ProcessInventory();
                UpdatePower();
                IMyFunctionalBlock blockEntity = (IMyFunctionalBlock)ConstructionBlock;
                int targetsCount = m_targets.Sum(x => x.TargetList.Count);
                int potentialTargetsCount = m_targets.Sum(x => x.PotentialTargetList.Count);

                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                {
                    m_targetsCount = targetsCount;
                    m_potentialTargetsCount = potentialTargetsCount;
                });

                if (!blockEntity.Enabled || !blockEntity.IsFunctional)
                    MyAPIGateway.Utilities.InvokeOnGameThread(() => 
                        {m_factoryState = FactoryStates.Disabled;});

                if ((targetsCount > 0) && IsPowered() || m_particleManager.Particles.Count > 0)
                {
                    if (m_spoolPosition == m_spoolingTime)
                    {
                        MyAPIGateway.Utilities.InvokeOnGameThread(() => 
                        {
                            m_factoryState = FactoryStates.Active;
                            if (m_lastState != m_factoryState)
                                m_updateConnectedInventory = true;
                        });
                    }
                    else
                        MyAPIGateway.Utilities.InvokeOnGameThread(() => 
                            {m_factoryState = FactoryStates.SpoolingUp;});
                }
                else if (targetsCount == 0 && potentialTargetsCount > 0 && !IsPowered())
                    MyAPIGateway.Utilities.InvokeOnGameThread(() => 
                        {m_factoryState = FactoryStates.MissingPower;});

                else if (targetsCount == 0 && potentialTargetsCount > 0)
                {
                    MyAPIGateway.Utilities.InvokeOnGameThread(() => 
                        {m_factoryState = FactoryStates.InvalidTargets;});

                    foreach(var item in InventoryManager.ComponentsRequired.ToList())
                        if (item.Value <= 0)
                            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                                {InventoryManager.ComponentsRequired.Remove(item.Key);});

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
                    {
                        MyAPIGateway.Utilities.InvokeOnGameThread(() => 
                        {
                            m_spoolPosition = 0;
                            m_factoryState = FactoryStates.Enabled;
                        });
                    }
                    else
                        MyAPIGateway.Utilities.InvokeOnGameThread(() => 
                            {m_factoryState = FactoryStates.SpoolingDown;});
                }
                else
                    MyAPIGateway.Utilities.InvokeOnGameThread(() => 
                        {m_factoryState = FactoryStates.Disabled;});

                MyAPIGateway.Utilities.InvokeOnGameThread(() => 
                {
                    if (m_factoryState != FactoryStates.Active && m_factoryState != FactoryStates.SpoolingUp 
                      && m_factoryState != FactoryStates.SpoolingDown && m_spoolPosition > 0f)
                        m_factoryState = FactoryStates.SpoolingDown;

                    SendStateUpdate(m_factoryState);

                    if (m_lastState != m_factoryState)
                    {
                        m_lastState = m_factoryState;
                        
                        if (!MyAPIGateway.Multiplayer.MultiplayerActive)
                            UpdateClientEmissives(); //updates emissives for a singleplayer game
                    }
                });
            });
        }

        #region Multiplayer Sync Functions

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
            {
                UpdateClientEmissives();
                m_lastState = m_factoryState;
            }
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
            //data.SubTargetId = target.MiningHammer.MiningBlock.EntityId;
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
                        {
                            if (item.IdentityId == data.TargetId)
                            {
                                playerTarget = item;
                                break;
                            }
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
                    {
                        if (item.IdentityId == data.TargetId)
                        {
                            playerTarget = item;
                            break;
                        }
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
                    {
                        if (item.CubeGrid.EntityId == data.TargetId && item.Position == new Vector3I(data.PositionI.X, data.PositionI.Y, data.PositionI.Z))
                        {
                            GetTarget<NaniteDeconstructionTargets>().CompleteTarget(item);
                            return;
                        }
                    }

                    Logging.Instance.WriteLine(string.Format("PROBLEM Unable to remove (dec): {0} - {1} | {2}", data.EntityId, data.PositionI, data.PositionD));
                    return;
                }
                else if (data.TargetType == TargetTypes.Projection)
                {
                    foreach (IMySlimBlock item in GetTarget<NaniteProjectionTargets>().TargetList.ToList())
                    {
                        if (item.Position == new Vector3I(data.PositionI.X, data.PositionI.Y, data.PositionI.Z) && NaniteProjectionTargets.GetProjectorByBlock(item) == data.TargetId)
                        {
                            GetTarget<NaniteProjectionTargets>().CompleteTarget(item);
                            return;
                        }
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
            {
                CleanupTargets();
            }
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
                    {
                        if (item.IdentityId == data.TargetId)
                        {
                            playerTarget = item;
                            break;
                        }
                    }

                    if (playerTarget != null)
                        GetTarget<NaniteMedicalTargets>().CancelTarget(playerTarget);

                    return;
                }
                else if (data.TargetType == TargetTypes.Deconstruction)
                {
                    foreach (IMySlimBlock item in m_targets.First(x => x is NaniteDeconstructionTargets).TargetList.ToList())
                    {
                        if (item.CubeGrid.EntityId == data.TargetId && item.Position == new Vector3I(data.PositionI.X, data.PositionI.Y, data.PositionI.Z))
                        {
                            var deconstructTarget = GetTarget<NaniteDeconstructionTargets>();
                            if (deconstructTarget != null)
                                deconstructTarget.CancelTarget(item);

                            return;
                        }
                    }

                    Logging.Instance.WriteLine(string.Format("PROBLEM Unable to remove (dec): {0} - {1} | {2}", data.EntityId, data.PositionI));
                    return;
                }
                else if (data.TargetType == TargetTypes.Projection)
                {
                    foreach (IMySlimBlock item in GetTarget<NaniteProjectionTargets>().TargetList.ToList())
                    {
                        if (item.Position == new Vector3I(data.PositionI.X, data.PositionI.Y, data.PositionI.Z) && NaniteProjectionTargets.GetProjectorByBlock(item) == data.TargetId)
                        {
                            GetTarget<NaniteProjectionTargets>().CancelTarget(item);
                            return;
                        }
                    }
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
            {
                CleanupTargets();
            }
        }

        private void SendDetails()
        {
            DetailData data = new DetailData();
            data.EntityId = ConstructionBlock.EntityId;
            data.Details = m_syncDetails.ToString();
            SendToPlayerInSyncRange(8954, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
        }

        public void SyncDetails(DetailData data)
        {
            m_syncDetails.Clear();
            m_syncDetails.Append(data.Details);
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

        /// <summary>
        /// When splits happen, targets grid and position change, which isn't updating properly ?  This will just remove the target on the client.  It's hacky
        /// but it works
        /// </summary>
        private void CleanupTargets()
        {
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

                            else if(!item.IsEnabled())
                                MyAPIGateway.Utilities.InvokeOnGameThread(() => {item.CancelTarget(target);});
                        }
                    }
                });
            }
            catch(Exception ex)
            {
                VRage.Utils.MyLog.Default.WriteLineAndConsole($"NaniteConstructionBlock.CleanupTargets Exception: {ex.ToString()}");
            }
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

    /// <summary>
    /// Class used to set emissives on a block dynamically
    /// </summary>
    public class MyCubeBlockEmissive : MyCubeBlock
    {
        public static void SetEmissiveParts(MyEntity entity, float emissivity, Color emissivePartColor, Color displayPartColor)
        {
            if (entity != null)
                UpdateEmissiveParts(entity.Render.RenderObjectIDs[0], emissivity, emissivePartColor, displayPartColor);
        }
    }
}
