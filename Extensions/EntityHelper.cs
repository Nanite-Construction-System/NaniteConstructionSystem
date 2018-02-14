using System;
using VRageMath;
using VRage.Game.ModAPI;
using VRage.Game;
using System.Collections.Generic;
using Sandbox.ModAPI;
using Ingame = Sandbox.ModAPI.Ingame;
using Sandbox.Game;
using System.Linq;
using VRage.ModAPI;
//using Ingame = VRage.Game.ModAPI.Ingame;
using Sandbox.Game.Entities;
using VRage.ObjectBuilders;

namespace NaniteConstructionSystem.Extensions
{

    public static class EntityHelper
    {
        public static double GetDistanceBetweenBlockAndSlimblock(IMyCubeBlock block, IMySlimBlock slimBlock)
        {
            return Vector3D.Distance(block.GetPosition(), GetBlockPosition(slimBlock));
        }

        public static Vector3D GetBlockPosition(IMySlimBlock slimBlock)
        {
            Vector3D slimBlockPosition = Vector3D.Zero;
            if (slimBlock.FatBlock != null)
                slimBlockPosition = slimBlock.FatBlock.GetPosition();
            else
            {
                var size = slimBlock.CubeGrid.GridSizeEnum == MyCubeSize.Small ? 0.5f : 2.5f;
                slimBlockPosition = Vector3D.Transform(new Vector3D(slimBlock.Position * size), slimBlock.CubeGrid.WorldMatrix);
            }

            return slimBlockPosition;
        }

        public static MatrixD GetBlockWorldMatrix(IMySlimBlock slimBlock)
        {
            if (slimBlock.FatBlock != null)
                return slimBlock.FatBlock.WorldMatrix;
            else
            {
                var size = slimBlock.CubeGrid.GridSizeEnum == MyCubeSize.Small ? 0.5f : 2.5f;
                return MatrixD.CreateWorld(Vector3D.Transform(slimBlock.Position * size, slimBlock.CubeGrid.WorldMatrix), slimBlock.CubeGrid.WorldMatrix.Forward, slimBlock.CubeGrid.WorldMatrix.Up);
            }
        }

    }

    public static class GridHelper
    {
        public static List<IMyCubeGrid> GetGridGroup(IMyCubeGrid grid)
        {
            List<IMyCubeGrid> gridList = new List<IMyCubeGrid>();
            gridList.Add(grid);

            //int pos = 0;
            try
            {
                List<Ingame.IMyTerminalBlock> terminalBlocks = new List<Ingame.IMyTerminalBlock>();
                Ingame.IMyGridTerminalSystem system = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid);
                if (system != null)
                {
                    system.GetBlocks(terminalBlocks);
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
                            if (connector.Status == Sandbox.ModAPI.Ingame.MyShipConnectorStatus.Connected && connector.OtherConnector != null)
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
                }
            }
            catch(Exception ex)
            {
                Logging.Instance.WriteLine(string.Format("GetGridGroup Error: {0}", ex.ToString()));
            }

            return gridList;
        }

        public static bool FindFreeCargo(MyCubeBlock target, MyCubeBlock startBlock, bool ignoreOtherFactories = false)
        {
            var list = Conveyor.GetConveyorListFromEntity(startBlock);
            if (list == null)
                return false;

            List<MyInventory> inventoryList = new List<MyInventory>();
            foreach (var item in list)
            {
                IMyEntity entity;
                if (MyAPIGateway.Entities.TryGetEntityById(item, out entity))
                {
                    if (!(entity is IMyCubeBlock))
                        continue;

                    if (target == (MyCubeBlock)entity)
                        continue;

                    if (entity is Ingame.IMyRefinery || entity is Ingame.IMyAssembler)
                        continue;

                    if (ignoreOtherFactories && ((IMyCubeBlock)entity).BlockDefinition.SubtypeName == "LargeNaniteFactory")
                        continue;

                    MyCubeBlock block = (MyCubeBlock)entity;
                    if (!block.HasInventory)
                        continue;

                    inventoryList.Add(block.GetInventory());
                }
            }

            MyInventory sourceInventory = target.GetInventory();
            MyInventory targetInventory = null;
            foreach (var item in inventoryList.OrderByDescending(x => (float)x.MaxVolume - (float)x.CurrentVolume))
            {
                targetInventory = item;

                foreach (var subItem in sourceInventory.GetItems().ToList())
                {
                    if (targetInventory.ItemsCanBeAdded(subItem.Amount, subItem))
                    {
                        targetInventory.TransferItemsFrom(sourceInventory, subItem, subItem.Amount);
                    }
                    else
                    {
                        int amountFits = (int)targetInventory.ComputeAmountThatFits(new MyDefinitionId(subItem.Content.TypeId, subItem.Content.SubtypeId));
                        if (amountFits > 0f)
                        {
                            targetInventory.TransferItemsFrom(sourceInventory, subItem, amountFits);
                        }
                    }
                }
            }

            if (sourceInventory.GetItems().Count < 1)
                return true;

            return false;
        }

        public static bool FindFreeCargo(MyCubeBlock startBlock, MyObjectBuilder_Base item, int count, bool order = true)
        {
            var list = Conveyor.GetConveyorListFromEntity(startBlock);
            if (list == null)
            {
                Logging.Instance.WriteLine(string.Format("Conveyor list is null!"));
                return false;
            }

            if (!list.Contains(startBlock.EntityId))
                list.Add(startBlock.EntityId);

            List<MyInventory> inventoryList = new List<MyInventory>();
            foreach (var inventoryItem in list)
            {
                IMyEntity entity;
                if (MyAPIGateway.Entities.TryGetEntityById(inventoryItem, out entity))
                {
                    if (!(entity is IMyCubeBlock))
                        continue;

                    if (entity is Ingame.IMyRefinery || entity is Ingame.IMyAssembler)
                        continue;

                    MyCubeBlock block = (MyCubeBlock)entity;
                    if (!block.HasInventory)
                        continue;

                    if (block.EntityId == startBlock.EntityId)
                        inventoryList.Insert(0, block.GetInventory());
                    else
                        inventoryList.Add(block.GetInventory());
                }
            }

            MyInventory targetInventory = null;

            List<MyInventory> modifiedList;
            if (order)
                modifiedList = inventoryList.OrderByDescending(x => (float)x.MaxVolume - (float)x.CurrentVolume).ToList();
            else
                modifiedList = inventoryList;

            foreach (var inventoryItem in modifiedList)
            {
                targetInventory = inventoryItem;
                if(targetInventory.CanItemsBeAdded(count, item.GetId()))
                {
                    var ownerName = targetInventory.Owner as IMyTerminalBlock;
                    if(ownerName != null)
                        Logging.Instance.WriteLine(string.Format("TRANSFER Adding {0} {1} to {2}", count, item.GetId().SubtypeName, ownerName.CustomName));

                    targetInventory.AddItems(count, item);
                    return true;
                }
            }

            return false;
        }
    }
}
