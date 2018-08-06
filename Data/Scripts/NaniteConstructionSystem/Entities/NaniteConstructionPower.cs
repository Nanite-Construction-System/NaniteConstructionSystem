using System;
using System.Linq;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
//using Ingame = VRage.Game.ModAPI.Ingame;
//using VRage.Game.ModAPI;

using NaniteConstructionSystem.Entities.Targets;

namespace NaniteConstructionSystem.Entities
{
    public static class NaniteConstructionPower
    {
        internal static bool HasRequiredPower(IMyFunctionalBlock constructionBlock, float powerRequired)
        {
            var resourceSink = constructionBlock.Components.Get<MyResourceSinkComponent>();
            if (resourceSink != null)
            {
                return resourceSink.IsPowerAvailable(new MyDefinitionId(typeof(VRage.Game.ObjectBuilders.Definitions.MyObjectBuilder_GasProperties), "Electricity"), powerRequired);
            }

            return true;            
        }

        internal static void SetPowerRequirements(IMyFunctionalBlock block, Func<float> requiredInputFunc)
        {
            var resourceSink = block.Components.Get<MyResourceSinkComponent>();
            if (resourceSink != null)
            {
                resourceSink.SetRequiredInputFuncByType(new MyDefinitionId(typeof(VRage.Game.ObjectBuilders.Definitions.MyObjectBuilder_GasProperties), "Electricity"), requiredInputFunc);
            }
        }

        internal static bool HasRequiredPowerForNewTarget(IMyFunctionalBlock constructionBlock, NaniteTargetBlocksBase target)
        {
            if (!NaniteConstructionManager.NaniteBlocks.ContainsKey(constructionBlock.EntityId))
                return false;

            NaniteConstructionBlock block = NaniteConstructionManager.NaniteBlocks[constructionBlock.EntityId];
            var powerRequired = block.Targets.Sum(x => x.TargetList.Count* x.GetPowerUsage()) + target.GetPowerUsage();
            return HasRequiredPower(constructionBlock, powerRequired);
        }

        internal static bool HasRequiredPowerForCurrentTarget(IMyFunctionalBlock constructionBlock)
        {
            if (!NaniteConstructionManager.NaniteBlocks.ContainsKey(constructionBlock.EntityId))
                return false;

            NaniteConstructionBlock block = NaniteConstructionManager.NaniteBlocks[constructionBlock.EntityId];
            var powerRequired = block.Targets.Sum(x => x.TargetList.Count * x.GetPowerUsage());
            return HasRequiredPower(constructionBlock, powerRequired);
        }
    }
}
