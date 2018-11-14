using System;
using VRageMath;
using VRage.Game.ModAPI;
using VRage.Game;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Sandbox.ModAPI;
using Ingame = Sandbox.ModAPI.Ingame;
using Sandbox.Game;
using System.Linq;
using VRage.ModAPI;
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
        public static void TryMoveToFreeCargo(MyCubeBlock source, ConcurrentBag<IMyInventory> connectedInventory, bool ignoreOtherFactories = false)
        {
            try
            {
                if (source == null)
                    return;

                MyInventory sourceInventory = source.GetInventory();
                foreach (IMyInventory inv in connectedInventory.OrderByDescending(x => (float)x.MaxVolume - (float)x.CurrentVolume))
                {
                    MyInventory targetInventory = inv as MyInventory;
                    List<VRage.Game.Entity.MyPhysicalInventoryItem> items = sourceInventory.GetItems();
                    for (int i = 0; i < items.Count; i++)
                    {
                        IMyInventoryItem subItem = items[i] as IMyInventoryItem;
                        if (subItem == null) 
                            continue;

                        MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                        {
                            if (subItem == null)
                                return;

                            if (targetInventory.ItemsCanBeAdded(subItem.Amount, subItem))
                            {
                                targetInventory.Add(subItem, subItem.Amount);
                                sourceInventory.Remove(subItem, subItem.Amount);
                            }
                            else
                            {
                                int amountFits = (int)targetInventory.ComputeAmountThatFits(new MyDefinitionId(subItem.Content.TypeId, subItem.Content.SubtypeId));
                                if (amountFits > 0f) 
                                {
                                    targetInventory.Add(subItem, (MyFixedPoint)amountFits);
                                    sourceInventory.Remove(subItem, (MyFixedPoint)amountFits);
                                }
                            }
                        });
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                Logging.Instance.WriteLine("NaniteConstructionSystem.Extensions.GridHelper.TryMoveToFreeCargo: A list was modified. Retrying.");
                TryMoveToFreeCargo(source, connectedInventory, ignoreOtherFactories);
            }
            catch (Exception ex) when (ex.ToString().Contains("IndexOutOfRangeException")) //because Keen thinks we shouldn't have access to this exception ...
            {
                Logging.Instance.WriteLine("NaniteConstructionSystem.Extensions.GridHelper.TryMoveToFreeCargo: A list was modified. Retrying.");
                TryMoveToFreeCargo(source, connectedInventory, ignoreOtherFactories);
            }
        }
    }
}
