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
        public static void TryMoveToFreeCargo(MyCubeBlock target, List<IMyInventory> connectedInventory, bool ignoreOtherFactories = false)
        {
            MyInventory sourceInventory = target.GetInventory();
            foreach (IMyInventory inv in connectedInventory.OrderByDescending(x => (float)x.MaxVolume - (float)x.CurrentVolume))
            {
                MyInventory targetInventory = inv as MyInventory;
                List<VRage.Game.Entity.MyPhysicalInventoryItem> items = sourceInventory.GetItems();
                for (int i = 0; i < items.Count; i++)
                {
                    IMyInventoryItem subItem = items[i] as IMyInventoryItem;
                    if (subItem == null) 
                    {
                        VRage.Utils.MyLog.Default.WriteLineAndConsole("WARNING: IMyInventoryItem subItem was NULL: NaniteConstructionSystem.Extensions.GridHelper.TryMoveToFreeCargo");
                        continue;
                    }
                    if (targetInventory.ItemsCanBeAdded(subItem.Amount, subItem)) 
                        targetInventory.TransferItemFrom(sourceInventory, i, null, null, subItem.Amount);
                    else
                    {
                        int amountFits = (int)targetInventory.ComputeAmountThatFits(new MyDefinitionId(subItem.Content.TypeId, subItem.Content.SubtypeId));
                        if(amountFits > 0f) 
                            targetInventory.TransferItemFrom(sourceInventory, i, null, null, amountFits);
                    }
                }
            }
        }
    }
}
