using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRageMath;
using VRage.Utils;
using VRage.ObjectBuilders;
using Ingame = VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI;
using Sandbox.Definitions;

using NaniteConstructionSystem.Particles;
using NaniteConstructionSystem.Extensions;
using NaniteConstructionSystem.Entities.Tools;

namespace NaniteConstructionSystem.Entities
{
    public class NaniteConstructionInventory
    {
        public List<IMyInventory> connectedInventory = new List<IMyInventory>();

        private Dictionary<string, int> m_componentsRequired;
        public Dictionary<string, int> ComponentsRequired
        {
            get { return m_componentsRequired; }
        }

        private MyEntity m_constructionBlock;
        public NaniteConstructionInventory(MyEntity constructionBlock)
        {
            m_constructionBlock = constructionBlock;
            m_componentsRequired = new Dictionary<string, int>();
        }

        internal void TakeRequiredComponents()
        {
            if (MyAPIGateway.Session.CreativeMode || ComponentsRequired.Count < 1)
                    return;
            
            List<IMyInventory> removalList = new List<IMyInventory>();
            try
            {
                foreach (IMyInventory inventory in connectedInventory)
                {
                    IMyInventory inv = null;
                    IMyInventory constructionInventory = GetConstructionInventory();
                    
                    if (inventory == null || inventory.CurrentVolume == inventory.MaxVolume) 
                        continue;

                    if (!GridHelper.IsValidInventoryConnection(constructionInventory, inventory, out inv))
                    {
                        removalList.Add(inventory);
                        continue;
                    }

                    foreach (var inventoryItem in inventory.GetItems().ToList())
                    {
                        foreach (var componentNeeded in ComponentsRequired.ToList())
                        {
                            if (inventoryItem.Content.TypeId != typeof(MyObjectBuilder_Component) || componentNeeded.Value <= 0 
                            || (int)inventoryItem.Amount <= 0f || inventoryItem.Content.SubtypeName != componentNeeded.Key) 
                                continue;

                            var validAmount = GetMaxComponentAmount(componentNeeded.Key, (float)constructionInventory.MaxVolume - (float)constructionInventory.CurrentVolume); 

                            float amount;

                            if (inventoryItem.Amount >= componentNeeded.Value) 
                                amount = Math.Min(componentNeeded.Value, validAmount);
                            else 
                                amount = Math.Min((float)inventoryItem.Amount, validAmount);

                            if (!constructionInventory.CanItemsBeAdded((int)amount, new SerializableDefinitionId(typeof(MyObjectBuilder_Component), componentNeeded.Key))) 
                                continue;

                            MyAPIGateway.Utilities.InvokeOnGameThread(() => 
                            {
                                try
                                {
                                    inventory.RemoveItemsOfType((int)amount, (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject(typeof(MyObjectBuilder_Component), componentNeeded.Key));
                                    constructionInventory.AddItems((int)amount, (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject(typeof(MyObjectBuilder_Component), componentNeeded.Key));

                                    if (ComponentsRequired.ContainsKey(componentNeeded.Key)) 
                                        ComponentsRequired[componentNeeded.Key] -= (int)amount;
                                }
                                catch (Exception ex)
                                {
                                    Logging.Instance.WriteLine($"Nanite Control Factory: Exception in NaniteConstructionInventory.TakeRequiredComponents:\n{ex.ToString()}");
                                }
                            });
                        }
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                Logging.Instance.WriteLine("NaniteConstructionSystem.Extensions.GridHelper.TryMoveToFreeCargo: A list was modified. Aborting.", 1);
            }
            catch (Exception ex) when (ex.ToString().Contains("IndexOutOfRangeException")) //because Keen thinks we shouldn't have access to this exception ...
            {
                Logging.Instance.WriteLine("NaniteConstructionSystem.Extensions.GridHelper.TryMoveToFreeCargo: A list was modified. Aborting.", 1);
            }

            foreach (IMyInventory inv in removalList)
                MyAPIGateway.Utilities.InvokeOnGameThread(() => 
                    {connectedInventory.Remove(inv);});
        }

        private float GetMaxComponentAmount(string componentName, float remainingVolume)
        {
            var componentDef = MyDefinitionManager.Static.GetComponentDefinition(new MyDefinitionId(typeof(MyObjectBuilder_Component), componentName));
            return remainingVolume / componentDef.Volume;
        }

        internal void SetupRequiredComponents(List<IMySlimBlock> targetList, List<IMySlimBlock> possibleTargetList, int maxTargets, ref Dictionary<string, int> available, bool isProjection)
        {
            if (MyAPIGateway.Session.CreativeMode)
                return;

            Dictionary<string, int> missing = new Dictionary<string, int>();
            if (targetList.Count < maxTargets)
            {
                if (!isProjection)
                {
                    foreach(var item in targetList.ToList())
                    {
                        missing.Clear();
                        item.GetMissingComponents(missing);
                        CalculateComponentsRequired(ref missing, ref available);

                        foreach (var missingItem in missing)
                        {
                            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                            {
                                if (ComponentsRequired.ContainsKey(missingItem.Key))
                                    ComponentsRequired[missingItem.Key] += missingItem.Value;
                                else
                                    ComponentsRequired.Add(missingItem.Key, missingItem.Value);
                            });
                        }
                    }
                }

                foreach (var item in possibleTargetList.ToList())
                {
                    if (targetList.Contains(item))
                        continue;

                    missing.Clear();
                    if (!isProjection)
                    {
                        item.GetMissingComponents(missing);
                        CalculateComponentsRequired(ref missing, ref available);
                    }
                    else
                    {
                        var missingName = GetProjectionComponents(item).First().Key;
                        missing.Add(missingName, 1);
                    }

                    foreach (var missingItem in missing)
                    {
                        MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                        {
                            if (ComponentsRequired.ContainsKey(missingItem.Key))
                                ComponentsRequired[missingItem.Key] += missingItem.Value;
                            else
                                ComponentsRequired.Add(missingItem.Key, missingItem.Value);
                        });
                    }
                }
            }
        }

        internal bool CheckComponentsAvailable(ref Dictionary<string, int> missing, ref Dictionary<string, int> available)
        {
            if (MyAPIGateway.Session.CreativeMode)
                return true;

            var checkMissing = new Dictionary<string, int>(missing);
            var checkAvailable = new Dictionary<string, int>(available);

            bool result = false;
            foreach (var item in checkMissing)
            {
                if (checkAvailable.ContainsKey(item.Key))
                {
                    result = true;
                    break;
                }
            }

            foreach (var item in missing)
            {
                if (available.ContainsKey(item.Key))
                {
                    available[item.Key] -= item.Value;
                    if (available[item.Key] <= 0)
                        available.Remove(item.Key);
                }
            }

            return result;
        }

        internal void CalculateComponentsRequired(ref Dictionary<string, int> missing, ref Dictionary<string, int> available)
        {
            if (MyAPIGateway.Session.CreativeMode)
                return;

            var checkMissing = new Dictionary<string, int>(missing);
            var checkAvailable = new Dictionary<string, int>(available);
            var missingResult = new Dictionary<string, int>(missing);

            foreach (var item in checkMissing)
            {
                if (!checkAvailable.ContainsKey(item.Key))
                    continue;

                if (checkAvailable[item.Key] < item.Value)
                    missingResult[item.Key] -= checkAvailable[item.Key];

                else
                    missingResult[item.Key] -= item.Value;
            }

            missing.Clear();
            foreach (var item in missingResult)
                missing.Add(item.Key, item.Value);
        }

        internal Dictionary<string, int> GetProjectionComponents(IMySlimBlock block, bool firstOnly = true)
        {
            Dictionary<string, int> result = new Dictionary<string, int>();
            if (MyAPIGateway.Session.CreativeMode)
                return result;

            MyCubeBlockDefinition blockDefinition = (MyCubeBlockDefinition)block.BlockDefinition;
            if (firstOnly)
                result.Add(blockDefinition.Components[0].Definition.Id.SubtypeName, 1);

            else
                foreach (var item in blockDefinition.Components)
                    result.Add(item.Definition.Id.SubtypeName, item.Count);

            return result;
        }

        internal void GetAvailableComponents(ref Dictionary<string, int> result)
        {
            if (MyAPIGateway.Session.CreativeMode)
                return;

            IMyInventory inventory = GetConstructionInventory();
            if (inventory != null)
            {
                foreach (var item in inventory.GetItems())
                {
                    if ((int)item.Amount < 1)
                        continue;

                    if (!result.ContainsKey(item.Content.SubtypeName))
                        result.Add(item.Content.SubtypeName, (int)item.Amount);
                    else
                        result[item.Content.SubtypeName] += (int)item.Amount;
                }
            }
        }

        internal void SubtractAvailableComponents(List<IMySlimBlock> targetList, ref Dictionary<string, int> result, bool isProjection)
        {
            if (MyAPIGateway.Session.CreativeMode)
                return;

            Dictionary<string, int> missing = new Dictionary<string, int>();
            foreach (var item in targetList)
            {
                missing.Clear();

                if (isProjection)
                    item.GetMissingComponents(missing);
                else
                {
                    var missingName = GetProjectionComponents(item).First().Key;
                    missing.Add(missingName, 1);
                }

                foreach (var component in missing)
                    if (result.ContainsKey(component.Key))
                        result[component.Key] -= component.Value;
            }
        }

        internal bool ProcessMissingComponents(IMySlimBlock target)
        {
            if (MyAPIGateway.Session.CreativeMode)
                return true;

            try
            {
                IMyInventory inventory = GetConstructionInventory();
                if (inventory == null)
                {
                    Logging.Instance.WriteLine($"NaniteConstructionInventory.ProcessMissingComponents(): Inventory is null = {(inventory == null)}.");
                    return false;
                }

                Dictionary<string, int> missingComponents = new Dictionary<string, int>();
                // Target block is on a real grid
                if (target.CubeGrid.Physics != null)
                {
                    target.GetMissingComponents(missingComponents);
                    if (missingComponents.Count == 0)
                        return true;
                }
                else // Target block is projection, let's just put 1 item in it
                {
                    try
                    {
                        MyCubeBlockDefinition blockDefinition = (MyCubeBlockDefinition)target.BlockDefinition;
                        missingComponents.Add(blockDefinition.Components[0].Definition.Id.SubtypeName, 1);
                    }
                    catch (Exception ex)
                    {
                        Logging.Instance.WriteLine($"NaniteConstructionInventory.ProcessMissingComponents():\n{ex.ToString()}");
                        return false;
                    }

                }

                foreach (var item in inventory.GetItems().ToList())
                {
                    if (missingComponents.ContainsKey(item.Content.SubtypeName))
                    {
                        var amount = (float)missingComponents[item.Content.SubtypeName];
                        if (amount >= (float)item.Amount)
                            amount = (float)item.Amount;

                        missingComponents[item.Content.SubtypeName] -= (int)item.Amount;
                        if (missingComponents[item.Content.SubtypeName] <= 0)
                            missingComponents.Remove(item.Content.SubtypeName);

                        inventory.RemoveItemsOfType((int)amount, (MyObjectBuilder_PhysicalObject)item.Content);
                    }
                }

                if (missingComponents.Count == 0)
                    return true;

                return false;
            }
            catch(Exception ex)
            {
                Logging.Instance.WriteLine(string.Format("Exception: {0}", ex.ToString()));
                return false;
            }
        }

        private IMyInventory GetConstructionInventory()
        {
            return (IMyInventory)m_constructionBlock.GetInventoryBase(0);
        }
    }
}
