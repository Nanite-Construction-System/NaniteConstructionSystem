using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.Entity;
using VRage.Game;
using VRage.ModAPI;
using VRageMath;
using VRage.Utils;
using Ingame = Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI;
using Sandbox.Definitions;

using NaniteConstructionSystem.Particles;
using NaniteConstructionSystem.Entities.Targets;
using NaniteConstructionSystem.Entities.Effects;
using NaniteConstructionSystem.Entities.Effects.LightningBolt;
using NaniteConstructionSystem.Entities.Tools;
using NaniteConstructionSystem.Entities.Beacons;
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

        private IMyTerminalBlock m_constructionBlock;
        public IMyTerminalBlock ConstructionBlock
        {
            get { return m_constructionBlock; }
        }

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

        private List<NaniteBlockEffectBase> m_effects;
        private DateTime m_lastUpdate;
        private MySoundPair m_soundPair;
        private MyEntity3DSoundEmitter m_soundEmitter;
        private int m_updateCount;
        private FactoryStates m_lastState;
        private bool m_ready;
        private int m_spoolPosition;
        private StringBuilder m_syncDetails;
        private DateTime m_syncLastUpdate;
        private Dictionary<MyDefinitionId, MyBlueprintDefinitionBase> m_defCache;

        private const int m_spoolingTime = 3000;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="entity">The IMyEntity of the block</param>
        public NaniteConstructionBlock(IMyEntity entity)
        {
            m_constructionBlock = (IMyTerminalBlock)entity;
            var inventory = ((MyCubeBlock)m_constructionBlock).GetInventory();
            inventory.SetFlags(VRage.Game.MyInventoryFlags.CanReceive | VRage.Game.MyInventoryFlags.CanSend);
            m_constructionBlock.CustomNameChanged += CustomNameChanged;
            m_defCache = new Dictionary<MyDefinitionId, MyBlueprintDefinitionBase>();

            MyCubeBlock block = (MyCubeBlock)entity;
            block.UpgradeValues.Add("ConstructionNanites", 0f);
            block.UpgradeValues.Add("DeconstructionNanites", 0f);
            block.UpgradeValues.Add("ProjectionNanites", 0f);
            block.UpgradeValues.Add("CleanupNanites", 0f);
            block.UpgradeValues.Add("MiningNanites", 0f);
            block.UpgradeValues.Add("MedicalNanites", 0f);
            block.UpgradeValues.Add("SpeedNanites", 0f);
            block.UpgradeValues.Add("PowerNanites", 0f);
        }

        private void CustomNameChanged(IMyTerminalBlock block)
        {
            try
            {
                if (block.CustomName.ToLower().Contains("MaxNanites".ToLower()))
                {
                    Regex regexObj = new Regex(@".*?MaxNanites[ :](\d{1,4})", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    Match matchResults = regexObj.Match(block.CustomName);
                    if (matchResults.Success)
                    {
                        int value = 0;
                        if (int.TryParse(matchResults.Groups[1].Value, out value))
                        {
                            m_userDefinedNaniteLimit = value;
                        }
                    }
                }
                else
                    m_userDefinedNaniteLimit = 0;
            }
            catch(Exception ex)
            {
                Logging.Instance.WriteLine(string.Format("Parse error: {0}", ex.ToString()));
            }
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

            m_ready = true;
            m_lastUpdate = DateTime.MinValue;
            m_factoryState = FactoryStates.Disabled;
            m_lastState = FactoryStates.Disabled;
            m_syncDetails = new StringBuilder();
            m_syncLastUpdate = DateTime.Now;

            m_soundPair = new MySoundPair("ArcParticleElectrical");
            m_soundEmitter = new MyEntity3DSoundEmitter((MyEntity)m_constructionBlock);
            m_soundEmitter.CustomMaxDistance = 30f;
            m_soundEmitter.CustomVolume = 2f;

            m_inventoryManager = new NaniteConstructionInventory((MyEntity)m_constructionBlock);
            NaniteConstructionPower.SetPowerRequirements((IMyFunctionalBlock)m_constructionBlock, () =>
            {
                //if (!Sync.IsServer)
                //    return 0f;

                if (m_constructionBlock == null)
                    return 0f;

                IMyFunctionalBlock block = (IMyFunctionalBlock)m_constructionBlock;
                if (!block.Enabled || !block.IsFunctional)
                    return 0f;

                var required = 0.1f;
                var sum = m_targets.Sum(x => x.TargetList.Count * x.GetPowerUsage());
                if (sum > 0)
                    required = sum;

                return required;
            });
            
            ((IMyFunctionalBlock)m_constructionBlock).AppendingCustomInfo += AppendingCustomInfo;
        }

        /// <summary>
        /// Main update loop
        /// </summary>
        public void Update()
        {
            if (ConstructionBlock.Closed)
                return;

            if (!m_initialize)
                Initialize();

            if (m_updateCount % 1800 == 0)
            {
                string upgrades = "";
                MyCubeBlock block = (MyCubeBlock)m_constructionBlock;
                foreach(var item in block.UpgradeValues)
                {
                    upgrades += string.Format("({0} - {1}) ", item.Key, item.Value);
                }

                Logging.Instance.WriteLine(string.Format("STATUS Nanite Factory: {0} - (t: {1}  pt: {2}  pw: {3} st: {4}) - {5}", ConstructionBlock.EntityId, m_targets.Sum(x => x.TargetList.Count), m_targets.Sum(x => x.PotentialTargetList.Count), m_targets.Sum(x => x.TargetList.Count * x.GetPowerUsage()), m_factoryState, upgrades)); //, slimBlock.BuildIntegrity, slimBlock.MaxIntegrity, slimBlock.BuildLevelRatio, missing.Count));
            }

            // Server updates
            if (Sync.IsServer)
            {
                ProcessTools();

                UpdateState();

                ScanForTargets();

                ProcessPlayers();

                ProcessInventory();
            }

            // Client and server updates (though draws are mostly ignored by server)
            DrawEmissives();

            DrawParticles();

            DrawEffects();

            ProcessTargetItems();

            if(!Sync.IsServer && m_updateCount % 60 == 0)
                CleanupTargets();

            ((IMyFunctionalBlock)m_constructionBlock).RefreshCustomInfo();
        }

        /// <summary>
        /// Unload
        /// </summary>
        public void Unload()
        {
            if (m_effects != null)
            {
                foreach (var item in m_effects)
                {
                    item.Unload();
                }
            }

            if(m_soundEmitter != null)
                m_soundEmitter.StopSound(true);
        }

        public bool IsUserDefinedLimitReached()
        {
            if (m_userDefinedNaniteLimit == 0)
                return false;

            var totalTargets = Targets.Sum(x => x.TargetList.Count);
            if (totalTargets >= m_userDefinedNaniteLimit)
                return true;

            return false;
        }

        private void ProcessInventory()
        {
            if (m_updateCount % 120 != 0)
                return;

            if (m_constructionBlock.Closed)
                return;

            var inventory = ((MyCubeBlock)m_constructionBlock).GetInventory();
            if(inventory.VolumeFillFactor > 0.75f && (GetTarget<NaniteDeconstructionTargets>().TargetList.Count > 0 || GetTarget<NaniteFloatingTargets>().TargetList.Count > 0 || GetTarget<NaniteMiningTargets>().TargetList.Count > 0))
            {
                GridHelper.FindFreeCargo((MyCubeBlock)m_constructionBlock, (MyCubeBlock)m_constructionBlock);
                Logging.Instance.WriteLine(string.Format("PUSHING Factory inventory over 75% full: {0}", m_constructionBlock.EntityId));
            }
        }

        private void ProcessAssemblerQueue()
        {
            if (!NaniteConstructionManager.TerminalSettings.ContainsKey(m_constructionBlock.EntityId))
                return;

            if (!NaniteConstructionManager.TerminalSettings[m_constructionBlock.EntityId].UseAssemblers)
                return;

            if (InventoryManager.ComponentsRequired.Count < 1)
                return;

            List<IMyProductionBlock> assemblerList = new List<IMyProductionBlock>();
            var conveyorList = Conveyor.GetConveyorListFromEntity(m_constructionBlock);
            if (conveyorList == null)
                return;

            var list = conveyorList.ToList();
            foreach(var item in list)
            {
                IMyEntity entity;
                if (!MyAPIGateway.Entities.TryGetEntityById(item, out entity))
                    continue;

                Ingame.IMyAssembler assembler = entity as Ingame.IMyAssembler;
                if (assembler == null)
                    continue;

                if (assembler.Mode == Ingame.MyAssemblerMode.Disassembly)
                    continue;

                assemblerList.Add((IMyProductionBlock)assembler);
            }

            if (assemblerList.Count < 1)
                return;

            Dictionary<string, int> missing = new Dictionary<string, int>();
            Dictionary<string, int> available = new Dictionary<string, int>();
            InventoryManager.GetAvailableComponents(ref available);
            GetMissingComponentsPotentialTargets<NaniteConstructionTargets>(missing, available);
            GetMissingComponentsPotentialTargets<NaniteProjectionTargets>(missing, available);

            foreach(var item in missing)
            {
                var def = MyDefinitionManager.Static.TryGetBlueprintDefinitionByResultId(new MyDefinitionId(typeof(MyObjectBuilder_Component), item.Key));
                if (def == null)
                    continue;

                // If this is some sort of weird modded definition, then we need to find the vanilla definition (ug)
                if(def.Results != null && def.Results[0].Amount > 1)
                {
                    if (m_defCache.ContainsKey(new MyDefinitionId(typeof(MyObjectBuilder_Component), item.Key)))
                    {
                        def = m_defCache[new MyDefinitionId(typeof(MyObjectBuilder_Component), item.Key)];
                    }
                    else
                    {
                        foreach (var defTest in MyDefinitionManager.Static.GetBlueprintDefinitions())
                        {
                            if (defTest.Results != null && defTest.Results[0].Amount == 1 && defTest.Results[0].Id == new MyDefinitionId(typeof(MyObjectBuilder_Component), item.Key))
                            {
                                def = defTest;
                                m_defCache.Add(new MyDefinitionId(typeof(MyObjectBuilder_Component), item.Key), def);
                                break;
                            }
                        }
                    }
                }

                bool found = false;
                foreach (var assemblerTest in assemblerList)
                {
                    foreach (var queueItem in assemblerTest.GetQueue())
                    {
                        if (queueItem.Blueprint == def && (int)queueItem.Amount >= item.Value)
                        {
                            found = true;
                            break;
                        }
                    }
                }

                if (found)
                    continue;

                int blueprintCount = assemblerList.Sum(x => x.GetQueue().Sum(y => y.Blueprint == def ? (int)y.Amount : 0));
                int availableCount = 0;
                if (available.ContainsKey(item.Key))
                    availableCount = available[item.Key];

                if (blueprintCount >= item.Value - availableCount)
                {
                    continue;
                }

                var assemblers = assemblerList.Where(x => NaniteConstructionManager.AssemblerSettings.ContainsKey(x.EntityId) && NaniteConstructionManager.AssemblerSettings[x.EntityId].AllowFactoryUsage);
                foreach (var target in assemblers)
                {
                    //if (!NaniteConstructionManager.AssemblerSettings.ContainsKey(target.EntityId))
                    //    continue;

                    //if (!NaniteConstructionManager.AssemblerSettings[target.EntityId].AllowFactoryUsage)
                    //    continue;

                    int amount = (int)Math.Max(((float)(item.Value - blueprintCount) / (float)assemblers.Count()), 1f);
                    Logging.Instance.WriteLine(string.Format("ASSEMBLER Queuing {0} {1} for factory {2} ({3})", amount, def.Id, m_constructionBlock.CustomName, blueprintCount));
                    target.InsertQueueItem(0, def, amount);
                }
            }
        }

        private void GetMissingComponentsPotentialTargets<T>(Dictionary<string, int> addToDictionary, Dictionary<string, int> available) where T : NaniteTargetBlocksBase
        {
            using (GetTarget<T>().Lock.AcquireExclusiveUsing())
            {
                int count = 0;
                foreach (var item in GetTarget<T>().PotentialTargetList)
                {
                    var target = item as IMySlimBlock;
                    if (target == null)
                        continue;

                    if(typeof(T) == typeof(NaniteProjectionTargets))
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

                    count++;
                    if (count > GetTarget<T>().GetMaximumTargets())
                        break;
                }
            }
        }

        /// <summary>
        /// Processes players near a factory.  If they are inside, damage them
        /// </summary>
        private void ProcessPlayers()
        {
            if (!Sync.IsServer)
                return;

            if (m_factoryState != FactoryStates.Active)
                return;

            if (m_updateCount % 60 != 0)
                return;

            List<IMyPlayer> players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);
            foreach (var item in players)
            {
                if (item.Controller == null || item.Controller.ControlledEntity == null || item.Controller.ControlledEntity.Entity == null)
                    continue;

                // Transform by an offset of half a large block on the Y using the factory's worldmatrix
                var position = Vector3D.Transform(new Vector3D(0f, 1.25f, 0f), ConstructionBlock.WorldMatrix);

                // Create a matrix from the transform
                MatrixD matrix = MatrixD.CreateTranslation(position);

                // Get the local bounding box of the player and transform by the matrix
                var localBB = item.Controller.ControlledEntity.Entity.WorldAABB.TransformFast(MatrixD.Invert(matrix));
                
                // Local bounding box of the dangerous area
                var boundingBox = new BoundingBoxD(new Vector3D(-2.1f, -2.1f, -2.1f), new Vector3(2.1f, 2.1f, 2.1f));

                // See if the player bounding box intersects the dangerous area
                if (boundingBox.Contains(localBB) != ContainmentType.Disjoint)
                {
                    IMyCharacter character = (IMyCharacter)item.Controller.ControlledEntity.Entity;
                    VRage.Game.ModAPI.Interfaces.IMyDestroyableObject damage = (VRage.Game.ModAPI.Interfaces.IMyDestroyableObject)character;
                    damage.DoDamage(30f, MyStringHash.GetOrCompute("NaniteFactory"), true, null, m_constructionBlock.EntityId);
                }
            }
        }

        /// <summary>
        /// Scans for block targets including projections.  This can be intensive, so we're only doing it once every 5 seconds.  Walking the grid happens
        /// in parallel, but processing the actual targets need to happen in the game thread.
        /// </summary>
        private void ScanForTargets()
        {
            if (DateTime.Now - m_lastUpdate > TimeSpan.FromSeconds(5) && m_ready)
            {
                //Logging.Instance.WriteLine(string.Format("ScanForTargets"));
                m_ready = false;
                MyAPIGateway.Parallel.StartBackground(() =>
                {
                    DateTime start = DateTime.Now;

                    try
                    {
                        // Rebuild our conveyor cache
                        InventoryManager.RebuildConveyorList();
                        ProcessTargetsParallel();

                        // Process possible blocks
                        MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                        {
                            try
                            {
                                ProcessTargets();
                                ProcessRequiredComponents();
                                ProcessAssemblerQueue();
                            }
                            catch (Exception ex)
                            {
                                Logging.Instance.WriteLine(string.Format("Process Error: {0}", ex.ToString()));
                            }
                            finally
                            {
                                m_ready = true;
                                m_lastUpdate = DateTime.Now;
                            }
                        });
                    }
                    catch(Exception ex)
                    {
                        m_ready = true;
                        Logging.Instance.WriteLine($"ScanForTargets Error: {ex.ToString()}");
                    }
                    finally
                    {
                        Logging.Instance.WriteLine($"ScanForTargets: {(DateTime.Now - start).TotalMilliseconds}ms");
                    }
                });
            }
        }

        /// <summary>
        /// Processes targets (construction and projection)
        /// </summary>
        private void ProcessTargetItems()
        {
            foreach (var item in m_targets)
            {
                item.Update();
            }
        }

        /// <summary>
        /// Draws effects (lightning and center spinning orb)
        /// </summary>
        private void DrawEffects()
        {
            if (MyAPIGateway.Session.Player == null)
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
        /// Walking the grid looking for target blocks.  All done in a thread
        /// </summary>
        private void ProcessTargetsParallel()
        {
            int pos = 0;
            try
            {
                Ingame.IMyGridTerminalSystem system = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid((IMyCubeGrid)m_constructionBlock.CubeGrid);
                if (system == null)
                {
                    Logging.Instance.WriteLine(string.Format("Terminal System is null: {0}", m_constructionBlock.CubeGrid.EntityId));
                    return;
                }

                pos = 1;
                List<Ingame.IMyTerminalBlock> terminalBlocks = new List<Ingame.IMyTerminalBlock>();
                system.GetBlocks(terminalBlocks);
                List<IMyCubeGrid> gridList = new List<IMyCubeGrid>();
                gridList.Add(m_constructionBlock.CubeGrid);
                pos = 2;
                foreach (var item in terminalBlocks)
                {
                    if (!gridList.Contains((IMyCubeGrid)item.CubeGrid))
                        gridList.Add((IMyCubeGrid)item.CubeGrid);

                    if (item is IMyPistonBase)
                    {
                        IMyPistonBase pistonBase = (IMyPistonBase)item;
                        if (pistonBase.TopGrid != null && !gridList.Contains(pistonBase.TopGrid))
                            gridList.Add(pistonBase.TopGrid);
                    }

                    if (item is IMyMechanicalConnectionBlock)
                    {
                        var motorBase = item as IMyMechanicalConnectionBlock;
                        if (motorBase.TopGrid != null && !gridList.Contains(motorBase.TopGrid))
                            gridList.Add(motorBase.TopGrid);
                    }

                    if (item is Ingame.IMyShipConnector)
                    {
                        Ingame.IMyShipConnector connector = (Ingame.IMyShipConnector)item;
                        if (connector.Status == Ingame.MyShipConnectorStatus.Connected && connector.OtherConnector != null)
                        {
                            if (!gridList.Contains((IMyCubeGrid)connector.OtherConnector.CubeGrid))
                                gridList.Add((IMyCubeGrid)connector.OtherConnector.CubeGrid);
                        }
                    }

                    if (item is IMyAttachableTopBlock)
                    {
                        var motorRotor = item as IMyAttachableTopBlock;
                        if (motorRotor.IsAttached && motorRotor.Base != null)
                        {
                            if (!gridList.Contains((IMyCubeGrid)motorRotor.Base.CubeGrid))
                                gridList.Add((IMyCubeGrid)motorRotor.Base.CubeGrid);
                        }
                    }
                }
                pos = 3;

                List<IMySlimBlock> blocks = new List<IMySlimBlock>();
                foreach (var item in gridList)
                {
                    item.GetBlocks(blocks);
                }
                pos = 4;
                foreach (var item in m_targets)
                    item.ParallelUpdate(gridList, blocks);
                pos = 5;
            }
            catch(Exception ex)
            {
                Logging.Instance.WriteLine(string.Format("ProcessTargetsParallel() Error {1}: {0}", ex.ToString(), pos));
            }
        }

        /// <summary>
        /// Process our welding and grinding nanites
        /// </summary>
        private void ProcessTools()
        {
            ToolManager.Update();
        }

        /// <summary>
        /// Process and draw the particle effects of nanites
        /// </summary>
        private void DrawParticles()
        {
            ParticleManager.Update();
        }

        /// <summary>
        /// Process any required components by the factory
        /// </summary>
        internal void ProcessRequiredComponents()
        {
            try
            {
                if (InventoryManager.ComponentsRequired.Count < 1)
                    return;

                var list = Conveyor.GetConveyorListFromEntity(ConstructionBlock);
                if (list != null)
                {
                    foreach (var entityId in list)
                    {
                        if (InventoryManager.ComponentsRequired.Count < 1)
                            return;

                        if (entityId == ConstructionBlock.EntityId)
                            continue;

                        IMyEntity entity = null;
                        if (!MyAPIGateway.Entities.TryGetEntityById(entityId, out entity))
                            continue;

                        if (((MyEntity)entity).HasInventory && ((MyEntity)entity).GetInventory(((MyEntity)entity).InventoryCount - 1).GetItemsCount() > 0)
                        {
                            InventoryManager.TakeRequiredComponents((MyEntity)entity);
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Logging.Instance.WriteLine(string.Format("ProcessRequiredComponents() Error: {0}", ex.ToString()));
            }
        }

        /// <summary>
        /// Processes found targets by the factory and also moves inventory.  This is done in the game thread.
        /// </summary>
        private void ProcessTargets()
        {
            Dictionary<string, int> availableComponents = new Dictionary<string, int>();
            InventoryManager.GetAvailableComponents(ref availableComponents);

            foreach (var item in m_targets)
            {
                if (!(item is NaniteConstructionTargets) && !(item is NaniteProjectionTargets))
                    continue;

                InventoryManager.SubtractAvailableComponents(item.TargetList.Cast<IMySlimBlock>().ToList(), ref availableComponents, item is NaniteProjectionTargets);
            }

            foreach (var item in m_targets)
            {
                item.FindTargets(ref availableComponents);
            }

            availableComponents = new Dictionary<string, int>();
            InventoryManager.GetAvailableComponents(ref availableComponents);

            foreach (var item in m_targets)
            {
                if (!(item is NaniteConstructionTargets) && !(item is NaniteProjectionTargets))
                    continue;

                InventoryManager.SubtractAvailableComponents(item.TargetList.Cast<IMySlimBlock>().ToList(), ref availableComponents, item is NaniteProjectionTargets);
            }

            InventoryManager.ComponentsRequired.Clear();
            foreach (var item in m_targets)
            {
                if (!(item is NaniteConstructionTargets) && !(item is NaniteProjectionTargets))
                    continue;

                InventoryManager.SetupRequiredComponents(item.TargetList.Cast<IMySlimBlock>().ToList(), item.PotentialTargetList.Cast<IMySlimBlock>().ToList(), item.GetMaximumTargets(), ref availableComponents, item is NaniteProjectionTargets);
            }
        }

        /// <summary>
        /// Update custom info of the block
        /// </summary>
        /// <param name="block"></param>
        /// <param name="details"></param>
        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder details)
        {
            details.Clear();

            if (Sync.IsServer)
            {
                foreach (var item in m_targets)
                {
                    using (item.Lock.AcquireExclusiveUsing())
                    {
                        details.Append("-----\r\n");
                        details.Append(string.Format("{0} Nanites\r\n", item.TargetName));
                        details.Append("-----\r\n");
                        details.Append(string.Format("Possible {1} Targets: {0}\r\n", item.PotentialTargetList.Count, item.TargetName));
                        details.Append(string.Format("Current {1} Targets: {0}\r\n", item.TargetList.Count, item.TargetName));
                        details.Append(string.Format("Maximum {1} Streams: {0}\r\n", item.GetMaximumTargets(), item.TargetName));
                        details.Append(string.Format("{1} Power / Stream: {0}MW\r\n", item.GetPowerUsage(), item.TargetName));
                        details.Append(string.Format("{1} Min. Travel Time: {0}s\r\n", item.GetMinTravelTime(), item.TargetName));
                        details.Append(string.Format("{1} Travel Speed: {0}m/s\r\n", item.GetSpeed(), item.TargetName));
                    }
                }

                details.Append("-----\r\n");
                var powerRequired = string.Format("{0} MW", (int)m_targets.Sum(x => x.TargetList.Count * x.GetPowerUsage()));
                if (m_targets.Sum(x => x.TargetList.Count * x.GetPowerUsage())  == 0f)
                {
                    if (((IMyFunctionalBlock)block).IsFunctional && (((IMyFunctionalBlock)block).Enabled))
                        powerRequired = string.Format("100kW");
                    else
                        powerRequired = string.Format("0kW");
                }

                details.Append(string.Format("Current Power Required: {0}\r\n", powerRequired));
                details.Append(string.Format("Status: {0}\r\n", m_factoryState.ToString()));
                details.Append(string.Format("Active Nanites: {0}\r\n", m_particleManager.Particles.Count));

                if(m_factoryState == FactoryStates.InvalidTargets)
                {
                    details.Append("Last invalid target reasons:\r\n");

                    foreach(var item in m_targets)
                    {
                        if (item.LastInvalidTargetReason != null && item.LastInvalidTargetReason != "")
                            details.Append(item.LastInvalidTargetReason + string.Format(" ({0})\r\n", item.TargetName));
                    }
                }

                if(InventoryManager.ComponentsRequired.Count > 0)
                {
                    details.Append(string.Format("\r\nMissing components:\r\n"));
                    foreach (var component in InventoryManager.ComponentsRequired)
                    {
                        if (component.Value > 0)
                            details.Append(string.Format("{0}: {1}\r\n", component.Key, component.Value));
                    }
                }

                if(m_syncDetails.Length != details.Length || DateTime.Now - m_syncLastUpdate > TimeSpan.FromSeconds(3))
                {
                    m_syncLastUpdate = DateTime.Now;
                    m_syncDetails.Clear();
                    m_syncDetails.Append(details);
                    SendDetails();
                }
            }
            else
            {
                details.Append(m_syncDetails);
            }
        }

        internal float GetPowerRequired(NaniteTargetBlocksBase targetStream)
        {
            return targetStream.GetPowerUsage();
        }

        /// <summary>
        /// Change color of emissives on the block model to appropriate color
        /// </summary>
        private void DrawEmissives()
        {
            m_updateCount++;
            
            if (m_factoryState == FactoryStates.SpoolingUp)
            {
                m_spoolPosition += (int)(1000f / 60f);
                if (m_spoolPosition >= m_spoolingTime)
                    m_spoolPosition = m_spoolingTime;
            }
            else if (m_factoryState == FactoryStates.SpoolingDown)
            {
                m_spoolPosition -= (int)(1000f / 60f);
                if (m_spoolPosition <= 0)
                    m_spoolPosition = 0;
            }

            if (MyAPIGateway.Session.Player == null)
                return;

            float emissivity = 1.0f;
            IMyFunctionalBlock blockEntity = (IMyFunctionalBlock)m_constructionBlock;
            if(!blockEntity.Enabled || !blockEntity.IsFunctional)
            {
                m_soundEmitter.StopSound(true);
                MyCubeBlockEmissive.SetEmissiveParts((MyEntity)m_constructionBlock, emissivity, Color.Red, Color.White);
            }
            else if (m_factoryState == FactoryStates.Active)
            {
                MyCubeBlockEmissive.SetEmissiveParts((MyEntity)m_constructionBlock, emissivity, Color.FromNonPremultiplied(new Vector4(0.05f, 0.05f, 0.35f, 0.75f)) * (((float)m_spoolPosition / m_spoolingTime) + 0.1f), Color.White);
            }
            else if (m_factoryState == FactoryStates.SpoolingUp)
            {
                if (m_spoolPosition >= m_spoolingTime)
                {
                    m_soundEmitter.PlaySound(m_soundPair, true);
                }

                MyCubeBlockEmissive.SetEmissiveParts((MyEntity)m_constructionBlock, emissivity, Color.FromNonPremultiplied(new Vector4(0.05f, 0.05f, 0.35f, 0.75f)) * (((float)m_spoolPosition / m_spoolingTime) + 0.1f), Color.White);
            }
            else if (m_factoryState == FactoryStates.SpoolingDown)
            {
                m_soundEmitter.StopSound(true);
                MyCubeBlockEmissive.SetEmissiveParts((MyEntity)m_constructionBlock, emissivity, Color.FromNonPremultiplied(new Vector4(0.05f, 0.05f, 0.35f, 0.75f)) * (((float)m_spoolPosition / m_spoolingTime) + 0.1f), Color.White);
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
            {
                MyCubeBlockEmissive.SetEmissiveParts((MyEntity)m_constructionBlock, emissivity, Color.Green, Color.White);
            }
            else
            {
                m_soundEmitter.StopSound(true);
                MyCubeBlockEmissive.SetEmissiveParts((MyEntity)m_constructionBlock, emissivity, Color.Red, Color.White);
            }
        }

        private void UpdateState()
        {
            //if(DateTime.Now - m_lastEmissiveUpdate > TimeSpan.FromMilliseconds(1000))
            
            {
                //m_lastEmissiveUpdate = DateTime.Now;
                ProcessState();
            }
        }

        /// <summary>
        /// Check state of block (used to be just emissives, but emissive state has been turned into block state.  Will refactor names later)
        /// </summary>
        private void ProcessState()
        {
            IMyFunctionalBlock blockEntity = (IMyFunctionalBlock)ConstructionBlock;
            int totalTargets = m_targets.Sum(x => x.TargetList.Count);
            int totalPotentialTargets = m_targets.Sum(x => x.PotentialTargetList.Count);
            float totalPowerRequired = m_targets.Sum(x => x.TargetList.Count * x.GetPowerUsage());

            if(!blockEntity.Enabled || !blockEntity.IsFunctional)
            {
                m_factoryState = FactoryStates.Disabled;
            }
            if ((totalTargets > 0) && NaniteConstructionPower.HasRequiredPower(blockEntity, totalPowerRequired) || m_particleManager.Particles.Count > 0)
            {
                if (m_spoolPosition == m_spoolingTime)
                {
                    m_factoryState = FactoryStates.Active;
                }
                else
                {
                    m_factoryState = FactoryStates.SpoolingUp;
                }
            }
            else if (totalTargets == 0 && totalPotentialTargets > 0 && !NaniteConstructionPower.HasRequiredPower(blockEntity, m_targets.Min(x => x.GetPowerUsage())))
            {
                m_factoryState = FactoryStates.MissingPower;
            }
            else if (totalTargets == 0 && totalPotentialTargets > 0)
            {
                m_factoryState = FactoryStates.InvalidTargets;

                foreach(var item in InventoryManager.ComponentsRequired.ToList())
                {
                    if (item.Value <= 0)
                        InventoryManager.ComponentsRequired.Remove(item.Key);
                }

                if (InventoryManager.ComponentsRequired.Count > 0)
                    m_factoryState = FactoryStates.MissingParts;
            }
            else if (blockEntity.Enabled)
            {
                if (m_spoolPosition <= 0)
                {
                    m_spoolPosition = 0;
                    m_factoryState = FactoryStates.Enabled;
                }
                else
                    m_factoryState = FactoryStates.SpoolingDown;
            }
            else
            {
                m_factoryState = FactoryStates.Disabled;
            }

            if(m_factoryState != FactoryStates.Active && m_factoryState != FactoryStates.SpoolingUp && m_factoryState != FactoryStates.SpoolingDown)
            {
                if (m_spoolPosition > 0f)
                    m_factoryState = FactoryStates.SpoolingDown;
            }

            if (m_lastState != m_factoryState || m_updateCount % 120 == 0)
            {
                m_lastState = m_factoryState;
                SendStateUpdate(m_factoryState);
            }
        }

        /// <summary>
        /// Multiplayer Sync Functions
        /// </summary>
        private void SendStateUpdate(FactoryStates state)
        {
            StateData data = new StateData();
            data.EntityId = ConstructionBlock.EntityId;
            data.State = state;
            MyAPIGateway.Multiplayer.SendMessageToOthers(8950, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
        }

        public void SyncUpdateState(StateData data)
        {
            m_factoryState = data.State;
        }

        public void SendAddTarget(IMySlimBlock target, TargetTypes targetType, long projectorId = 0)
        {
            TargetData data = new TargetData();
            data.EntityId = ConstructionBlock.EntityId;
            data.TargetId = target.CubeGrid.EntityId;

            if(projectorId > 0)
            {
                data.TargetId = projectorId;
            }

            data.PositionI = target.Position;
            data.TargetType = targetType;
            MyAPIGateway.Multiplayer.SendMessageToOthers(8951, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
        }

        public void SendAddTarget(IMyEntity floating)
        {
            TargetData data = new TargetData();
            data.EntityId = ConstructionBlock.EntityId;
            data.TargetId = floating.EntityId;
            data.TargetType = TargetTypes.Floating;
            MyAPIGateway.Multiplayer.SendMessageToOthers(8951, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
        }

        public void SendAddTarget(NaniteMiningItem item)
        {
            TargetData data = new TargetData();
            data.EntityId = ConstructionBlock.EntityId;
            data.TargetId = item.VoxelId;
            data.TargetType = TargetTypes.Voxel;
            data.PositionD = item.Position;
            data.SubTargetId = item.MiningHammer.MiningBlock.EntityId;
            MyAPIGateway.Multiplayer.SendMessageToOthers(8951, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
        }

        public void SendAddTarget(IMyPlayer player)
        {
            TargetData data = new TargetData();
            data.EntityId = ConstructionBlock.EntityId;
            data.TargetId = player.IdentityId;
            data.TargetType = TargetTypes.Medical;
            MyAPIGateway.Multiplayer.SendMessageToOthers(8951, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
        }

        public void SyncAddTarget(TargetData data)
        {
            Logging.Instance.WriteLine(string.Format("SYNCADD Target: {0} - {1} | {2} - {3}", data.EntityId, data.PositionI, data.PositionD, data.TargetType.ToString()));

            try
            {
                if(data.TargetType == TargetTypes.Medical)
                {
                    var target = GetTarget<NaniteMedicalTargets>().TargetList.FirstOrDefault(x => ((IMyPlayer)x).IdentityId == data.TargetId);
                    if(target == null)
                    {
                        List<IMyPlayer> players = new List<IMyPlayer>();
                        MyAPIGateway.Players.GetPlayers(players);
                        IMyPlayer playerTarget = null;
                        foreach(var item in players)
                        {
                            if(item.IdentityId == data.TargetId)
                            {
                                playerTarget = item;
                                break;
                            }                            
                        }

                        if(playerTarget != null)
                        {
                            GetTarget<NaniteMedicalTargets>().TargetList.Add(playerTarget);
                        }
                    }
                    return;
                }

                if(data.TargetType == TargetTypes.Voxel)
                {
                    var target = GetTarget<NaniteMiningTargets>().TargetList.FirstOrDefault(x => ((NaniteMiningItem)x).Position == new Vector3D(data.PositionD.X, data.PositionD.Y, data.PositionD.Z));
                    if (target == null)
                    {
                        var miningHammer = NaniteConstructionManager.MiningList.FirstOrDefault(x => x.MiningBlock.EntityId == data.SubTargetId);
                        if (miningHammer == null)
                            return;

                        NaniteMiningItem item = new NaniteMiningItem();
                        item.VoxelId = data.TargetId;
                        item.Position = new Vector3D(data.PositionD.X, data.PositionD.Y, data.PositionD.Z);
                        item.MiningHammer = miningHammer;
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

                if (data.TargetType == TargetTypes.Projection ||
                    data.TargetType == TargetTypes.Deconstruction ||
                    data.TargetType == TargetTypes.Construction)
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

                    if (data.TargetType == TargetTypes.Projection)
                    {
                        if (!GetTarget<NaniteProjectionTargets>().TargetList.Contains(slimBlock))
                            GetTarget<NaniteProjectionTargets>().TargetList.Add(slimBlock);
                    }
                    else if (data.TargetType == TargetTypes.Deconstruction)
                    {
                        if (!GetTarget<NaniteDeconstructionTargets>().TargetList.Contains(slimBlock))
                            GetTarget<NaniteDeconstructionTargets>().TargetList.Add(slimBlock);
                    }
                    else
                    {
                        if (!GetTarget<NaniteConstructionTargets>().TargetList.Contains(slimBlock))
                            GetTarget<NaniteConstructionTargets>().TargetList.Add(slimBlock);
                    }
                }
                else if (data.TargetType == TargetTypes.Floating)
                {
                    if (!GetTarget<NaniteFloatingTargets>().TargetList.Contains(entity))
                        GetTarget<NaniteFloatingTargets>().TargetList.Add(entity);
                }
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
            MyAPIGateway.Multiplayer.SendMessageToOthers(8952, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
        }

        public void SendCompleteTarget(IMyEntity floating)
        {
            TargetData data = new TargetData();
            data.EntityId = ConstructionBlock.EntityId;
            data.TargetId = floating.EntityId;
            data.TargetType = TargetTypes.Floating;
            MyAPIGateway.Multiplayer.SendMessageToOthers(8952, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
        }

        public void SendCompleteTarget(NaniteMiningItem item)
        {
            TargetData data = new TargetData();
            data.EntityId = ConstructionBlock.EntityId;
            data.TargetId = item.VoxelId;
            data.PositionD = item.Position;
            data.TargetType = TargetTypes.Voxel;
            MyAPIGateway.Multiplayer.SendMessageToOthers(8952, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
        }

        public void SendCompleteTarget(IMyPlayer player)
        {
            TargetData data = new TargetData();
            data.EntityId = ConstructionBlock.EntityId;
            data.TargetId = player.IdentityId;
            data.TargetType = TargetTypes.Medical;
            MyAPIGateway.Multiplayer.SendMessageToOthers(8952, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
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
                else if(data.TargetType == TargetTypes.Medical)
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

                    if(playerTarget != null)
                    {
                        GetTarget<NaniteMedicalTargets>().CompleteTarget(playerTarget);
                    }

                    return;
                }
                else if(data.TargetType == TargetTypes.Voxel)
                {
                    var target = GetTarget<NaniteMiningTargets>().TargetList.FirstOrDefault(x => ((NaniteMiningItem)x).Position == new Vector3D(data.PositionD.X, data.PositionD.Y, data.PositionD.Z)) as NaniteMiningItem;
                    if(target != null)
                    {
                        GetTarget<NaniteMiningTargets>().CompleteTarget(target);
                    }
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
                else if(data.TargetType == TargetTypes.Projection)
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
                {
                    Logging.Instance.WriteLine(string.Format("PROBLEM Unable to remove (oth): {0} - {1} | {2}", data.EntityId, data.PositionI, data.PositionD));
                }
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
            MyAPIGateway.Multiplayer.SendMessageToOthers(8953, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
        }

        public void SendCancelTarget(IMyEntity floating)
        {
            TargetData data = new TargetData();
            data.EntityId = ConstructionBlock.EntityId;
            data.TargetId = floating.EntityId;
            data.TargetType = TargetTypes.Floating;
            MyAPIGateway.Multiplayer.SendMessageToOthers(8953, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
        }

        public void SendCancelTarget(IMyPlayer player)
        {
            TargetData data = new TargetData();
            data.EntityId = ConstructionBlock.EntityId;
            data.TargetId = player.IdentityId;
            data.TargetType = TargetTypes.Medical;
            MyAPIGateway.Multiplayer.SendMessageToOthers(8953, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
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
                    {
                        GetTarget<NaniteMiningTargets>().CompleteTarget(target);
                    }
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
                    {
                        GetTarget<NaniteMedicalTargets>().CancelTarget(playerTarget);
                    }

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
            MyAPIGateway.Multiplayer.SendMessageToOthers(8954, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
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

       /* public void SyncStartParticleEffect(ParticleData data)
        {
            NaniteConstructionManager.ParticleManager.AddParticle(data.TargetId, new Vector3I(data.PositionX, data.PositionY, data.PositionZ), data.EffectId);
        }
        
        public void SendRemoveParticleEffect(long entityId, Vector3I position)
        {
            ParticleData data = new ParticleData();
            data.EntityId = ConstructionBlock.EntityId;
            data.TargetId = entityId;
            data.PositionX = position.X;
            data.PositionY = position.Y;
            data.PositionZ = position.Z;
            MyAPIGateway.Multiplayer.SendMessageToOthers(8959, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
        }

        public void SyncRemoveParticleEffect(ParticleData data)
        {
            NaniteConstructionManager.ParticleManager.RemoveParticle(data.TargetId, new Vector3I(data.PositionX, data.PositionY, data.PositionZ));
        }
        */
        /// <summary>
        /// When splits happen, targets grid and position change, which isn't updating properly ?  This will just remove the target on the client.  It's hacky
        /// but it works
        /// </summary>
        private void CleanupTargets()
        {
            try
            {
                foreach (var item in m_targets)
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

                        if(item is NaniteDeconstructionTargets && (target.IsDestroyed || target.IsFullyDismounted || (target.CubeGrid != null && target.CubeGrid.GetCubeBlock(target.Position) == null) || (target.FatBlock != null && target.FatBlock.Closed)))
                        {
                            item.CompleteTarget(target);
                        }
                        else if (target.IsDestroyed || target.IsFullyDismounted || (target.CubeGrid != null && target.CubeGrid.GetCubeBlock(target.Position) == null) || (target.FatBlock != null && target.FatBlock.Closed))
                        {
                            item.CancelTarget(target);
                        }
                        else if(item is NaniteConstructionTargets && target.IsFullIntegrity && !target.HasDeformation)
                        {
                            item.CompleteTarget(target);
                        }
                        else if(!item.IsEnabled())
                        {
                            item.CancelTarget(target);
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Logging.Instance.WriteLine(string.Format("Cleanup Error: {0}", ex.ToString()));
            }
        }

        public T GetTarget<T>() where T : NaniteTargetBlocksBase
        {
            foreach(var item in m_targets)
            {
                if(item is T)
                {
                    return (T)item;
                }
            }

            return null;
        }

        public void SyncTerminalSettings(NaniteTerminalSettings settings)
        {
            if (!NaniteConstructionManager.TerminalSettings.ContainsKey(m_constructionBlock.EntityId))
                NaniteConstructionManager.TerminalSettings.Add(m_constructionBlock.EntityId, settings);

            NaniteConstructionManager.TerminalSettings[m_constructionBlock.EntityId] = settings;
        }
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
