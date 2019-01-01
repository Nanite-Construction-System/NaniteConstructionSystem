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
    public class NaniteToolBase
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

        //private IMyEntity m_toolEntity;
        private NaniteConstructionBlock m_constructionBlock;
        private bool m_started = false;
        private bool m_performanceFriendly;
        private int m_completeTime;
        private bool m_removed = false;
        private Dictionary<string, int> m_missingComponents;
        private Dictionary<string, MyTuple<MyFixedPoint, MyObjectBuilder_PhysicalObject>> m_inventory;
        private bool m_isGrinder;
        private long m_cubeEntityId;
        private Vector3I m_position;
        private int m_updateCount;

        public NaniteToolBase(NaniteConstructionBlock constructionBlock, IMySlimBlock block, int waitTime, string toolBuilderText, bool performanceFriendly, bool isGrinder)
        {
            if (block == null)
            {
                Logging.Instance.WriteLine("Block is null!");
                return;
            }

            m_targetBlock = block;
            m_startTime = (int)MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds;
            m_waitTime = waitTime;
            m_constructionBlock = constructionBlock;
            m_performanceFriendly = performanceFriendly;
            m_isGrinder = isGrinder;
            m_updateCount = 0;

            m_missingComponents = new Dictionary<string, int>();
            m_inventory = new Dictionary<string, MyTuple<MyFixedPoint, MyObjectBuilder_PhysicalObject>>();

            CreateTool();
        }

        public virtual void Close()
        {
            int pos = 0;
            try
            {
                if (m_isGrinder && m_removed)
                {
                    TransferRemainingComponents();
                    Logging.Instance.WriteLine(string.Format("GRINDER completed.  Target block: {0} - (EntityID: {1} Elapsed: {2})", 
                      m_targetBlock.FatBlock != null ? m_targetBlock.FatBlock.GetType().Name : m_targetBlock.GetType().Name, m_targetBlock.FatBlock != null ? m_targetBlock.FatBlock.EntityId : 0, m_completeTime + m_waitTime));
                    return;
                }

                Logging.Instance.WriteLine(string.Format("TOOL completed.  Target block: {0} - (EntityID: {1} Elapsed: {2})", 
                  m_targetBlock.FatBlock != null ? m_targetBlock.FatBlock.GetType().Name : m_targetBlock.GetType().Name, m_targetBlock.FatBlock != null ? m_targetBlock.FatBlock.EntityId : 0, m_completeTime + m_waitTime));
            }
            catch (Exception ex)
            {
                Logging.Instance.WriteLine(string.Format("Close() {1}: {0}", ex.ToString(), pos));
            }
        }

        private void TransferRemainingComponents()
        {
            int pos = 0;
            try
            {
                MyCubeBlockDefinition blockDefinition = (MyCubeBlockDefinition)m_targetBlock.BlockDefinition;
                Dictionary<string, MyTuple<int, MyPhysicalItemDefinition>> components = new Dictionary<string, MyTuple<int, MyPhysicalItemDefinition>>();
                foreach (var item in blockDefinition.Components)
                {
                    var inventoryItem = (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject(item.DeconstructItem.Id);
                    if (!components.ContainsKey(item.DeconstructItem.Id.SubtypeName))
                        components.Add(item.DeconstructItem.Id.SubtypeName, new MyTuple<int, MyPhysicalItemDefinition>(item.Count, item.DeconstructItem));
                    else
                        components[item.DeconstructItem.Id.SubtypeName] = new MyTuple<int, MyPhysicalItemDefinition>(components[item.DeconstructItem.Id.SubtypeName].Item1 + item.Count, item.DeconstructItem);
                }
                pos = 1;

                foreach (var item in m_missingComponents)
                {
                    if (components.ContainsKey(item.Key))
                        components[item.Key] = new MyTuple<int, MyPhysicalItemDefinition>(components[item.Key].Item1 - item.Value, components[item.Key].Item2);

                    if (components[item.Key].Item1 <= 0)
                        components.Remove(item.Key);
                }
                pos = 2;
                foreach (var item in components)
                {
                    TransferFromItem((MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject(item.Value.Item2.Id), item.Value.Item1);
                }
                pos = 3;
                foreach (var item in m_inventory)
                {
                    MyFloatingObjects.Spawn(new MyPhysicalInventoryItem(item.Value.Item1, item.Value.Item2), Vector3D.Transform(m_targetBlock.Position * m_targetBlock.CubeGrid.GridSize, m_targetBlock.CubeGrid.WorldMatrix), m_targetBlock.CubeGrid.WorldMatrix.Forward, m_targetBlock.CubeGrid.WorldMatrix.Up);
                }
            }
            catch (Exception ex)
            {
                Logging.Instance.WriteLine(string.Format("Error {0}: {1}", pos, ex.ToString()));
            }
        }

        private void TransferFromItem(MyObjectBuilder_PhysicalObject item, int count)
        {
            MyInventory targetInventory = ((MyCubeBlock)m_constructionBlock.ConstructionBlock).GetInventory();
            if (targetInventory.CanItemsBeAdded(count, item.GetId()))
            {
                targetInventory.AddItems(count, item);
                return;
            }

            var inventoryItem = new MyPhysicalInventoryItem(count, item);
            MyFloatingObjects.Spawn(inventoryItem, Vector3D.Transform(m_targetBlock.Position * m_targetBlock.CubeGrid.GridSize, m_targetBlock.CubeGrid.WorldMatrix), m_targetBlock.CubeGrid.WorldMatrix.Forward, m_targetBlock.CubeGrid.WorldMatrix.Up);
        }

        public virtual void Update()
        {
            m_updateCount++;
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

            if (MyAPIGateway.Session.ElapsedPlayTime.TotalMilliseconds - m_startTime > m_waitTime + m_completeTime && m_isGrinder)
            {
                Complete();
                return;
            }

            if (m_started && m_updateCount % 4 == 0 && !m_isGrinder)
                WeldTarget();
        }

        private void Complete()
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
            if (m_targetBlock.FatBlock != null && ((MyEntity)m_targetBlock.FatBlock).HasInventory)
            {
                for (int r = 0; r < ((MyEntity)m_targetBlock.FatBlock).InventoryCount; r++)
                {
                    var inventory = ((MyEntity)m_targetBlock.FatBlock).GetInventoryBase(r);
                    if (inventory == null)
                        continue;

                    foreach (var item in inventory.GetItems())
                    {
                        Logging.Instance.WriteLine(string.Format("INVENTORY found.  Target block contains inventory: {0} {1}", item.Amount, item.Content.SubtypeId));
                        if (!m_inventory.ContainsKey(item.Content.SubtypeName))
                            m_inventory.Add(item.Content.SubtypeName, new MyTuple<MyFixedPoint, MyObjectBuilder_PhysicalObject>(item.Amount, item.Content));
                        else
                            m_inventory[item.Content.SubtypeName] = new MyTuple<MyFixedPoint, MyObjectBuilder_PhysicalObject>(m_inventory[item.Content.SubtypeName].Item1 + item.Amount, m_inventory[item.Content.SubtypeName].Item2);
                    }
                }
            }

            m_targetBlock.GetMissingComponents(m_missingComponents);
            grid.RazeBlock(m_targetBlock.Position);
            m_removed = true;
        }

        private void GrindTarget()
        {
            float damage = (MyAPIGateway.Session.GrinderSpeedMultiplier * MyShipGrinderConstants.GRINDER_AMOUNT_PER_SECOND) * 4f * NaniteConstructionManager.Settings.DeconstructionEfficiency;
            IMyInventory inventory = ((MyEntity)m_constructionBlock.ConstructionBlock).GetInventory();
            m_targetBlock.DecreaseMountLevel(damage, inventory);
            m_targetBlock.MoveItemsFromConstructionStockpile(inventory);

            if (m_targetBlock.IsFullyDismounted)
                m_targetBlock.CubeGrid.RazeBlock(m_targetBlock.Position);
        }

        private void WeldTarget()
        {
            try
            {
                if (m_targetBlock == null)
                    return;
            
                if (m_targetBlock.HasDeformation)
                    m_targetBlock.FixBones(0f, 1f);

                float damage = (MyAPIGateway.Session.WelderSpeedMultiplier * MyShipGrinderConstants.GRINDER_AMOUNT_PER_SECOND) * 8f * NaniteConstructionManager.Settings.ConstructionEfficiency;
                IMyInventory inventory = ((MyEntity)m_constructionBlock.ConstructionBlock).GetInventory();
                if (m_targetBlock.CanContinueBuild(inventory) || MyAPIGateway.Session.CreativeMode)
                {
                    var functional = m_targetBlock as IMyFunctionalBlock;
                    if (functional != null && !functional.Enabled)
                        functional.Enabled = true;

                    m_targetBlock.MoveItemsToConstructionStockpile(inventory);
                    m_targetBlock.IncreaseMountLevel(damage, m_constructionBlock.ConstructionBlock.OwnerId, inventory, maxAllowedBoneMovement: 1.0f);
                }
            }
            catch (Exception e)
            {
                Logging.Instance.WriteLine($"{e}");
            }
        }

        private void CreateTool()
        {
            MyCubeBlockDefinition blockDefinition = (MyCubeBlockDefinition)m_targetBlock.BlockDefinition;
            var grindPerUpdate = (MyAPIGateway.Session.GrinderSpeedMultiplier 
              * NaniteConstructionManager.Settings.DeconstructionEfficiency / blockDefinition.DisassembleRatio) 
              * blockDefinition.IntegrityPointsPerSec;

            m_completeTime = (int)(m_targetBlock.BuildIntegrity / grindPerUpdate * 1000f);

            Logging.Instance.WriteLine(string.Format("TOOL started.  Target block: {0} - {1}ms - {2} {3} {4}", 
              blockDefinition.Id, m_completeTime, blockDefinition.IntegrityPointsPerSec, m_targetBlock.BuildIntegrity, grindPerUpdate));
        }    
    }
}
