using System;
using VRageMath;
using VRage.Game.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
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
        public static bool IsValidInventoryConnection(object FactoryBlockInv, object TargetBlockInv, out IMyInventory inv)
        {
            inv = null;
            try
            {
                MyInventory FactoryInv = (MyInventory)FactoryBlockInv;
                MyEntity TargetInv = null;

                if (TargetBlockInv is IMySlimBlock && ((IMySlimBlock)TargetBlockInv).FatBlock != null)
                    TargetInv = (MyEntity)((IMyEntity)(((IMySlimBlock)TargetBlockInv).FatBlock));

                else if (TargetBlockInv is MyInventory || TargetBlockInv is IMyInventory)
                    TargetInv = ((MyInventory)TargetBlockInv).Owner;

                MyCubeBlock FactoryInvBlock = (MyCubeBlock)FactoryInv.Owner;
                if (TargetInv == null || FactoryInv == null || FactoryInvBlock == null || !TargetInv.HasInventory)
                    return false;

                MyCubeBlock InvBlock = TargetInv as MyCubeBlock;
                if (InvBlock == null)
                    return false;

                IMyProductionBlock prodblock = TargetInv as IMyProductionBlock; //assembler
                inv = (prodblock != null && prodblock.OutputInventory != null) ? prodblock.OutputInventory : ((IMyEntity)TargetInv).GetInventory();

                if (inv == null || !InvBlock.IsFunctional || NaniteConstructionManager.NaniteBlocks.ContainsKey(TargetInv.EntityId) 
                  || TargetInv is Sandbox.ModAPI.Ingame.IMyReactor || !inv.IsConnectedTo((IMyInventory)FactoryInv)
                  || !MyRelationsBetweenPlayerAndBlockExtensions.IsFriendly(FactoryInvBlock.GetUserRelationToOwner(InvBlock.OwnerId))) 
                    return false;

                return true;
            }
            catch (Exception e)
            {
                Logging.Instance.WriteLine($"IsValidInventoryConnection exception:\n{e}.");
                return false;
            }
        }

        public static void TryMoveToFreeCargo(MyCubeBlock source, List<IMyInventory> connectedInventory, bool ignoreOtherFactories = false)
        {
            try
            {
                List<IMyInventory> removalList = new List<IMyInventory>();
                MyInventory sourceInventory = source.GetInventory();
                foreach (IMyInventory inv in connectedInventory.OrderByDescending(x => (float)x.MaxVolume - (float)x.CurrentVolume))
                {
                    MyInventory targetInventory = inv as MyInventory;
                    IMyInventory outinv = null;
                    if (!IsValidInventoryConnection(sourceInventory, targetInventory, out outinv))
                    {
                        removalList.Add(inv);
                        continue;
                    }

                    if ((IMyEntity)(targetInventory.Owner) is IMyProductionBlock)
                        continue; // Dont push to assembler inventories

                    List<VRage.Game.Entity.MyPhysicalInventoryItem> items = sourceInventory.GetItems();
                    for (int i = 0; i < items.Count; i++)
                    {
                        IMyInventoryItem subItem = items[i] as IMyInventoryItem;
                        if (subItem == null) 
                            continue;

                        MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                        {
                            try
                            {
                                if (subItem == null || targetInventory == null || sourceInventory == null)
                                    return;

                                MyFixedPoint amountFits = targetInventory.ComputeAmountThatFits(new MyDefinitionId(subItem.Content.TypeId, subItem.Content.SubtypeId));
                                amountFits = (amountFits > subItem.Amount) ? subItem.Amount : amountFits;
                                
                                if (amountFits > (MyFixedPoint)0f && sourceInventory.Remove(subItem, amountFits))
                                    targetInventory.Add(subItem, amountFits);
                            }
                            catch (Exception e)
                                {Logging.Instance.WriteLine($"NaniteConstructionSystem.Extensions.GridHelper.TryMoveToFreeCargo:\n{e.ToString()}");}
                        });
                    }
                }
                foreach (IMyInventory inv in removalList)
                    MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                        {connectedInventory.Remove(inv);});
                
            }
            catch (InvalidOperationException ex)
            {
                Logging.Instance.WriteLine("NaniteConstructionSystem.Extensions.GridHelper.TryMoveToFreeCargo: A list was modified. Aborting.", 1);
            }
            catch (Exception ex) when (ex.ToString().Contains("IndexOutOfRangeException")) //because Keen thinks we shouldn't have access to this exception ...
            {
                Logging.Instance.WriteLine("NaniteConstructionSystem.Extensions.GridHelper.TryMoveToFreeCargo: A list was modified. Aborting.", 1);
            }
        }
    }
}
