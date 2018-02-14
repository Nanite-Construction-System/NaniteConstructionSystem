using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRageMath;
//using Ingame = VRage.Game.ModAPI.Ingame;
using Ingame = Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game;
using Sandbox.Definitions;
using VRage;

using NaniteConstructionSystem.Extensions;

namespace NaniteConstructionSystem.Entities.Tools
{
    public class NaniteToolBaseOld
    {
        private IMySlimBlock m_targetBlock = null;
        public IMySlimBlock TargetBlock
        {
            get { return m_targetBlock; }
        }

        private int m_startTime = 0;
        public int StartTime
        {
            get { return m_startTime; }
        }

        private int m_waitTime = 0;
        public int WaitTime
        {
            get { return m_waitTime; }
        }

        private IMyFunctionalBlock m_tool = null;
        public IMyFunctionalBlock ToolBlock
        {
            get { return m_tool; }
        }

        private IMyEntity m_toolEntity;
        private NaniteConstructionBlock m_constructionBlock;
        private bool m_started = false;
        private int m_lastToolUse = 0;
        private int m_lastEnabled = 0;
        private bool m_completed = false;
        private bool m_performanceFriendly;
        private int m_completeTime;
        private Dictionary<string, int> m_missingComponents;
        private Dictionary<string, MyTuple<MyFixedPoint, MyObjectBuilder_PhysicalObject>> m_inventory = new Dictionary<string, MyTuple<MyFixedPoint, MyObjectBuilder_PhysicalObject>>();
        private bool m_isGrinder;
        private long m_cubeEntityId;
        private Vector3I m_position;
        private bool m_removed;

        public NaniteToolBaseOld(NaniteConstructionBlock constructionBlock, IMySlimBlock block, int waitTime, string toolBuilderText, bool performanceFriendly, bool isGrinder)
        {
            if (block == null)
            {
                Logging.Instance.WriteLine("Block is null!");
                return;
            }

            m_targetBlock = block;
            Vector3D position = Vector3D.Zero;
            if (block.FatBlock == null)
            {
                position = Vector3D.Transform(block.Position * (block.CubeGrid.GridSizeEnum == MyCubeSize.Small ? 0.5f : 2.5f), block.CubeGrid.WorldMatrix);
            }
            else
            {
                position = block.FatBlock.GetPosition();
            }

            m_startTime = (int)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
            m_waitTime = waitTime;
            m_constructionBlock = constructionBlock;
            m_performanceFriendly = performanceFriendly;
            m_isGrinder = isGrinder;
            m_missingComponents = new Dictionary<string, int>();
            m_inventory = new Dictionary<string, MyTuple<MyFixedPoint, MyObjectBuilder_PhysicalObject>>();
            m_removed = false;

            if (!performanceFriendly)
                CreateTool(block.CubeGrid.GridSizeEnum, toolBuilderText);
            else
                CreatePerformanceTool();

            //Logging.Instance.WriteLine("Init");
        }

        public virtual void Close()
        {
            //Logging.Instance.WriteLine(string.Format("Close"));

            int pos = 0;
            try
            {
                if(m_performanceFriendly)
                {
                   /* if (Sync.IsClient)
                        NaniteConstructionManager.ParticleManager.RemoveParticle(m_cubeEntityId, m_position);
                    else
                        m_constructionBlock.SendRemoveParticleEffect(m_cubeEntityId, m_position);
                    */
                    if (m_isGrinder && m_removed)
                    {
                        TransferRemainingComponents();
                        Logging.Instance.WriteLine(string.Format("GRINDING completed.  Target block: {0} - (EntityID: {1} Elapsed: {2})", m_targetBlock.FatBlock != null ? m_targetBlock.FatBlock.GetType().Name : m_targetBlock.GetType().Name, m_targetBlock.FatBlock != null ? m_targetBlock.FatBlock.EntityId : 0, m_completeTime + m_waitTime));
                    }

                    m_completed = true;
                    return;
                }

                if(m_constructionBlock != null && m_constructionBlock.ConstructionBlock != null)
                {
                    var toolInventory = ((MyEntity)m_tool).GetInventory(0);

                    // Since grinding in creative gives no components.  Insert hack.
                    if(MyAPIGateway.Session.CreativeMode && m_tool is Sandbox.ModAPI.Ingame.IMyShipGrinder)
                    {
                        MyObjectBuilder_CubeBlock block = (MyObjectBuilder_CubeBlock)m_targetBlock.GetObjectBuilder();
                        MyCubeBlockDefinition blockDefinition;
                        if(MyDefinitionManager.Static.TryGetCubeBlockDefinition(block.GetId(), out blockDefinition))
                        {
                            foreach(var item in blockDefinition.Components)
                            {
                                var inventoryItem = (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject(item.DeconstructItem.Id);
                                toolInventory.AddItems(item.Count, inventoryItem);
                            }
                        }
                    }

                    if (toolInventory.GetItemsCount() > 0)
                    {
                        TransferFromTarget((MyCubeBlock)m_tool);
                    }
                }

                m_completed = true;
                if (m_tool != null)
                {
                    m_tool.Enabled = false;
                }

                if (!m_toolEntity.Closed)
                    m_toolEntity.Close();
            }
            catch (Exception ex)
            {
                Logging.Instance.WriteLine(string.Format("Close() {1}: {0}", ex.ToString(), pos));
            }
        }

        private void TransferRemainingComponents()
        {
            MyObjectBuilder_CubeBlock block;
            try
            {
                if (m_targetBlock.FatBlock == null)
                    block = m_targetBlock.GetObjectBuilder();
                else
                    block = m_targetBlock.FatBlock.GetObjectBuilderCubeBlock();
            }
            catch (Exception ex)
            {
                Logging.Instance.WriteLine(string.Format("ERROR getting cubeblock object builder (1): {0} {1} - {2}", m_targetBlock.IsDestroyed, m_targetBlock.FatBlock != null ? m_targetBlock.FatBlock.GetType().Name : m_targetBlock.GetType().Name, ex.ToString()));
                return;
            }

            MyCubeBlockDefinition blockDefinition;
            if (MyDefinitionManager.Static.TryGetCubeBlockDefinition(block.GetId(), out blockDefinition))
            {
                Dictionary<string, MyTuple<int, MyPhysicalItemDefinition>> components = new Dictionary<string, MyTuple<int, MyPhysicalItemDefinition>>();
                foreach (var item in blockDefinition.Components)
                {
                    var inventoryItem = (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject(item.DeconstructItem.Id);
                    if (!components.ContainsKey(item.DeconstructItem.Id.SubtypeName))
                        components.Add(item.DeconstructItem.Id.SubtypeName, new MyTuple<int, MyPhysicalItemDefinition>(item.Count, item.DeconstructItem));
                    else
                        components[item.DeconstructItem.Id.SubtypeName] = new MyTuple<int, MyPhysicalItemDefinition>(components[item.DeconstructItem.Id.SubtypeName].Item1 + item.Count, item.DeconstructItem);
                }

                foreach (var item in m_missingComponents)
                {
                    if (components.ContainsKey(item.Key))
                    {
                        components[item.Key] = new MyTuple<int, MyPhysicalItemDefinition>(components[item.Key].Item1 - item.Value, components[item.Key].Item2);
                    }

                    if (components[item.Key].Item1 <= 0)
                        components.Remove(item.Key);
                }

                foreach (var item in components)
                {                    
                    TransferFromItem((MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject(item.Value.Item2.Id), item.Value.Item1);
                }
            }

            foreach(var item in m_inventory)
            {
                MyFloatingObjects.Spawn(new MyPhysicalInventoryItem(item.Value.Item1, item.Value.Item2), Vector3D.Transform(m_targetBlock.Position * m_targetBlock.CubeGrid.GridSize, m_targetBlock.CubeGrid.WorldMatrix), m_targetBlock.CubeGrid.WorldMatrix.Forward, m_targetBlock.CubeGrid.WorldMatrix.Up);
            }
        }

        private void TransferFromItem(MyObjectBuilder_PhysicalObject item, int count)
        {
            MyInventory targetInventory = ((MyCubeBlock)m_constructionBlock.ConstructionBlock).GetInventory();
            if(targetInventory.CanItemsBeAdded(count, item.GetId()))
            {
                targetInventory.AddItems(count, item);
                return;
            }

            if (!GridHelper.FindFreeCargo((MyCubeBlock)m_constructionBlock.ConstructionBlock, item, count))
                return;

            var inventoryItem = new MyPhysicalInventoryItem(count, item);            
            MyFloatingObjects.Spawn(inventoryItem, Vector3D.Transform(m_targetBlock.Position * m_targetBlock.CubeGrid.GridSize, m_targetBlock.CubeGrid.WorldMatrix), m_targetBlock.CubeGrid.WorldMatrix.Forward, m_targetBlock.CubeGrid.WorldMatrix.Up);
        }

        private void TransferFromTarget(MyCubeBlock target)
        {
            MyInventory targetInventory = ((MyCubeBlock)m_constructionBlock.ConstructionBlock).GetInventory();
            MyInventory sourceInventory = target.GetInventory();

            foreach(var item in sourceInventory.GetItems().ToList())
            {                
                if(targetInventory.ItemsCanBeAdded(item.Amount, item))
                {
                    targetInventory.TransferItemsFrom(sourceInventory, item, item.Amount);
                }
                else
                {
                    int amountFits = (int)targetInventory.ComputeAmountThatFits(new MyDefinitionId(item.Content.TypeId, item.Content.SubtypeId));
                    if(amountFits > 0f)
                    {
                        targetInventory.TransferItemsFrom(sourceInventory, item, amountFits);
                    }
                }
            }

            if (sourceInventory.GetItems().Count < 1)
                return;

            if (GridHelper.FindFreeCargo(target, (MyCubeBlock)m_constructionBlock.ConstructionBlock))
                return;

            // We have left over inventory, drop it
            foreach(var item in sourceInventory.GetItems().ToList())
            {
                sourceInventory.RemoveItems(item.ItemId, item.Amount, spawn: true);
            }
        }

        public virtual void Update()
        {
            if (m_performanceFriendly)
            {
                if (!m_started && MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - m_startTime > m_waitTime)
                {
                    m_started = true;

                    m_cubeEntityId = m_targetBlock.CubeGrid.EntityId;
                    m_position = m_targetBlock.Position;

                    if (Sync.IsClient)
                        NaniteConstructionManager.ParticleManager.AddParticle(m_cubeEntityId, m_position, MyParticleEffectsNameEnum.WelderFlame);
                    else
                        m_constructionBlock.SendStartParticleEffect(m_cubeEntityId, m_position, 27);
                }

                if (MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - m_startTime > m_waitTime + m_completeTime)
                {
                    Complete(!m_isGrinder);
                }

                return;
            }

            if (m_targetBlock.IsDestroyed)
            {
                if (m_tool != null && !m_tool.Closed && m_tool.Enabled)
                    m_tool.Enabled = false;

                return;
            }

            if (m_toolEntity.Closed)
                return;

            // This moves the tool to the position of the target even if the parent cube moves.
            Vector3D targetBlockPosition;
            if (m_targetBlock.FatBlock != null)
                targetBlockPosition = m_targetBlock.FatBlock.PositionComp.GetPosition();
            else
                targetBlockPosition = Vector3D.Transform(m_targetBlock.Position * (m_targetBlock.CubeGrid.GridSizeEnum == MyCubeSize.Small ? 0.5f : 2.5f), m_targetBlock.CubeGrid.WorldMatrix);

            if (m_toolEntity != null && !m_toolEntity.Closed && m_toolEntity.PositionComp.GetPosition() != targetBlockPosition)
            {
                m_toolEntity.SetPosition(targetBlockPosition);
            }

            if (m_completed)
                return;

            if (m_started) //(int)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - m_lastToolUse > 30 && m_started)
            {
                m_lastToolUse = (int)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
                ToggleToolStatus();
            }

            if (MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - m_startTime > m_waitTime)
            {
                if (m_started)
                    return;

                m_started = true;
            }
        }

        private void ToggleToolStatus()
        {
            if (m_tool.Closed)
                return;

            var efficiency = NaniteConstructionManager.Settings.ConstructionEfficiency;
            if (m_tool is Sandbox.ModAPI.Ingame.IMyShipGrinder)
                efficiency = NaniteConstructionManager.Settings.DeconstructionEfficiency;

            //Logging.Instance.WriteLine(string.Format("Efficiency: {0}", efficiency));

            if(efficiency >= 1f || m_lastEnabled == 0)
            {
                m_lastEnabled = (int)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
                if (!m_tool.Enabled)
                {
                    m_tool.Enabled = true;
                }

                return;
            }

            var onTime = (1f * efficiency) * 1000f + 250f;
            var offTime = (1f - (1f * efficiency)) * 1000f + 250f;

            //Logging.Instance.WriteLine(string.Format("OnTime: {0}  OffTime: {1}  Elapsed: {2}", onTime, offTime, MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - m_lastEnabled));
            if(!m_tool.Enabled && MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - m_lastEnabled > offTime)
            {
                m_lastEnabled = (int)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
                m_tool.Enabled = true;
            }
            else if(m_tool.Enabled && MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - m_lastEnabled > onTime)
            {
                m_lastEnabled = (int)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
                m_tool.Enabled = false;
            }
        }

        private Vector3D GetTargetPosition()
        {
            Vector3D targetBlockPosition;
            if (m_targetBlock.FatBlock != null)
                targetBlockPosition = m_targetBlock.FatBlock.PositionComp.GetPosition();
            else
                targetBlockPosition = Vector3D.Transform(m_targetBlock.Position * (m_targetBlock.CubeGrid.GridSizeEnum == MyCubeSize.Small ? 0.5f : 2.5f), m_targetBlock.CubeGrid.WorldMatrix);

            return targetBlockPosition;
        }

        private void CreateTool(MyCubeSize size, string toolBuilderText)
        {
            var toolObject = toolBuilderText;
            if (size == MyCubeSize.Large)
                toolObject = string.Format(toolObject, "Small");
            else
                toolObject = string.Format(toolObject, "Tiny");

            MyObjectBuilder_CubeGrid cubeGrid = MyAPIGateway.Utilities.SerializeFromXML<MyObjectBuilder_CubeGrid>(toolObject);
            foreach (var item in cubeGrid.CubeBlocks)
                item.Owner = m_constructionBlock.ConstructionBlock.OwnerId;

            m_toolEntity = MyAPIGateway.Entities.CreateFromObjectBuilder(cubeGrid);
            m_toolEntity.PositionComp.Scale = 0.001f;
            m_toolEntity.PositionComp.SetPosition(GetTargetPosition());
            m_toolEntity.Physics.Enabled = false;
            m_toolEntity.Save = false;

            var toolGrid = (MyCubeGrid)m_toolEntity;
            toolGrid.IsSplit = true;
            toolGrid.IsPreview = true;

            List<IMySlimBlock> blocks = new List<IMySlimBlock>();
            ((IMyCubeGrid)m_toolEntity).GetBlocks(blocks);
            foreach (var slimBlock in blocks)
            {
                if (slimBlock.FatBlock == null)
                    continue;

                var block = slimBlock.FatBlock;
                if (block is Sandbox.ModAPI.Ingame.IMyShipWelder || block is Sandbox.ModAPI.Ingame.IMyShipGrinder)
                {
                    MyCubeBlock toolBlock = (MyCubeBlock)block;
                    toolBlock.IDModule.ShareMode = MyOwnershipShareModeEnum.Faction;
                    IMyFunctionalBlock tool = (IMyFunctionalBlock)block;
                    m_tool = tool;
                    break;
                }
            }

            MyAPIGateway.Entities.AddEntity(m_toolEntity);
            //m_toolEntity.RemoveFromGamePruningStructure();
        }

        private void CreatePerformanceTool()
        {
            MyObjectBuilder_CubeBlock block;
            try
            {
                if (m_targetBlock.FatBlock == null)
                    block = m_targetBlock.GetObjectBuilder();
                else
                    block = m_targetBlock.FatBlock.GetObjectBuilderCubeBlock();
            }
            catch (Exception ex)
            {
                Logging.Instance.WriteLine(string.Format("ERROR getting cubeblock object builder (2): {0} {1} - {2}", m_targetBlock.IsDestroyed, m_targetBlock.FatBlock != null ? m_targetBlock.FatBlock.GetType().Name : m_targetBlock.GetType().Name, ex.ToString()));
                return;
            }

            MyCubeBlockDefinition blockDefinition;
            if (MyDefinitionManager.Static.TryGetCubeBlockDefinition(block.GetId(), out blockDefinition))
            {
                m_completeTime = (int)(m_targetBlock.BuildIntegrity / blockDefinition.IntegrityPointsPerSec * 1000f / MyAPIGateway.Session.GrinderSpeedMultiplier / NaniteConstructionManager.Settings.DeconstructionEfficiency);
            }

            Logging.Instance.WriteLine(string.Format("GRINDING started.  Target block: {0} - {1}ms", m_targetBlock.FatBlock != null ? m_targetBlock.FatBlock.GetType().Name : m_targetBlock.GetType().Name, m_completeTime));
        }

        private void Complete(bool replace = false)
        {
            if (replace)
            {
                /*
                VRage.Game.ModAPI.Interfaces.IMyDestroyableObject obj = (VRage.Game.ModAPI.Interfaces.IMyDestroyableObject)m_targetBlock;
                var dmg = m_targetBlock.MaxIntegrity - m_targetBlock.BuildIntegrity;
                obj.DoDamage(-dmg, VRage.Utils.MyStringHash.GetOrCompute("NaniteRepair"), true);
                m_targetBlock.ApplyAccumulatedDamage();
                MyCubeGrid grid = (MyCubeGrid)m_targetBlock.CubeGrid;
                grid.SendIntegrityChanged((Sandbox.Game.Entities.Cube.MySlimBlock)m_targetBlock, MyCubeGrid.MyIntegrityChangeEnum.Repair, 0);
                grid.OnIntegrityChanged((Sandbox.Game.Entities.Cube.MySlimBlock)m_targetBlock);
                m_targetBlock.UpdateVisual();
                */
            }
            else
            {
                if (m_targetBlock.IsDestroyed || m_targetBlock.CubeGrid.GetCubeBlock(m_targetBlock.Position) == null || (m_targetBlock.FatBlock != null && m_targetBlock.FatBlock.Closed))
                    return;

                if (m_targetBlock.CubeGrid.Closed)
                    return;

                MyCubeGrid grid = (MyCubeGrid)m_targetBlock.CubeGrid;
                MyObjectBuilder_CubeBlock block;
                try
                {
                    if (m_targetBlock.FatBlock == null)
                        block = m_targetBlock.GetObjectBuilder();
                    else
                        block = m_targetBlock.FatBlock.GetObjectBuilderCubeBlock();
                }
                catch (Exception ex)
                {
                    Logging.Instance.WriteLine(string.Format("ERROR getting cubeblock object builder (3): {0} {1} - {2}", m_targetBlock.IsDestroyed, m_targetBlock.FatBlock != null ? m_targetBlock.FatBlock.GetType().Name : m_targetBlock.GetType().Name, ex.ToString()));
                }

                // Target block contains inventory, spawn it
                if(m_targetBlock.FatBlock != null && ((MyEntity)m_targetBlock.FatBlock).HasInventory)
                {
                    for(int r = 0; r < ((MyEntity)m_targetBlock.FatBlock).InventoryCount; r++)
                    {
                        var inventory = ((MyEntity)m_targetBlock.FatBlock).GetInventoryBase(r);
                        if (inventory == null)
                            continue;

                        foreach(var item in inventory.GetItems())
                        {
                            Logging.Instance.WriteLine(string.Format("INVENTORY found.  Target block contains inventory: {0} {1}", item.Amount, item.Content.SubtypeId));
                            if(!m_inventory.ContainsKey(item.Content.SubtypeName))
                                m_inventory.Add(item.Content.SubtypeName, new MyTuple<MyFixedPoint, MyObjectBuilder_PhysicalObject>(item.Amount, item.Content));
                            else
                                m_inventory[item.Content.SubtypeName] = new MyTuple<MyFixedPoint, MyObjectBuilder_PhysicalObject>(m_inventory[item.Content.SubtypeName].Item1 + item.Amount, m_inventory[item.Content.SubtypeName].Item2);
                        }
                    }
                }

                m_targetBlock.GetMissingComponents(m_missingComponents);
                //grid.RemoveBlock((Sandbox.Game.Entities.Cube.MySlimBlock)m_targetBlock, true);
                grid.RazeBlock(m_targetBlock.Position);
                m_removed = true;
            }
        }
    }
}