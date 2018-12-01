using Sandbox.Common.ObjectBuilders;
using VRage.Game.Components;
using VRage.ObjectBuilders;
using VRage.Game.ModAPI;
using Sandbox.Game.Entities;

namespace NaniteConstructionSystem.Entities
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ShipWelder), true, "LargeNaniteControlFacility")]
    public class LargeControlFacilityLogic : MyGameLogicComponent
    {
        private NaniteConstructionBlock m_block = null;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();

            m_block = new NaniteConstructionBlock(Entity);

            if (!NaniteConstructionManager.NaniteBlocks.ContainsKey(Entity.EntityId))
                NaniteConstructionManager.NaniteBlocks.Add(Entity.EntityId, m_block);

            m_block.UpdateCount += NaniteConstructionManager.NaniteBlocks.Count * 30;
            // Adds some gap between factory processing so they don't all process their targets at once.

            IMySlimBlock slimBlock = ((MyCubeBlock)m_block.ConstructionBlock).SlimBlock as IMySlimBlock;
            Logging.Instance.WriteLine(string.Format("ADDING Nanite Factory: conid={0} physics={1} ratio={2}", 
              Entity.EntityId, m_block.ConstructionBlock.CubeGrid.Physics == null, slimBlock.BuildLevelRatio));

            if (NaniteConstructionManager.NaniteSync != null)
                NaniteConstructionManager.NaniteSync.SendNeedTerminalSettings(Entity.EntityId);
        }

        public override void UpdateBeforeSimulation()
        {
            try
                {m_block.Update();}
            catch (System.Exception e)
                {VRage.Utils.MyLog.Default.WriteLineAndConsole($"LargeControlFacilityLogic.UpdateBeforeSimulation Exception: {e.ToString()}");}
        }

        public override void Close()
        {
            if (NaniteConstructionManager.NaniteBlocks != null && Entity != null)
            {
                NaniteConstructionManager.NaniteBlocks.Remove(Entity.EntityId);
                Logging.Instance.WriteLine(string.Format("REMOVING Nanite Factory: {0}", Entity.EntityId));
            }

            if (m_block != null)
                m_block.Unload();
        }
    }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Projector), true)]
    public class NaniteProjectorLogic : MyGameLogicComponent
    {
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            if (!NaniteConstructionManager.ProjectorBlocks.ContainsKey(Entity.EntityId))
                NaniteConstructionManager.ProjectorBlocks.Add(Entity.EntityId, (IMyCubeBlock)Entity);
        }

        /// <summary>
        /// GetObjectBuilder on a block is always null
        /// </summary>
        /// <param name="copy"></param>
        /// <returns></returns>
        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return null;
        }

        public override void Close()
        {
            if (NaniteConstructionManager.ProjectorBlocks == null)
                return;

            if (NaniteConstructionManager.ProjectorBlocks.ContainsKey(Entity.EntityId))
                NaniteConstructionManager.ProjectorBlocks.Remove(Entity.EntityId);
        }
    }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Assembler), true)]
    public class NaniteAssemblerLogic : MyGameLogicComponent
    {
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            if (!NaniteConstructionManager.AssemblerBlocks.ContainsKey(Entity.EntityId))
            {
                NaniteConstructionManager.AssemblerBlocks.Add(Entity.EntityId, (IMyCubeBlock)Entity);
                if (NaniteConstructionManager.NaniteSync != null)
                    NaniteConstructionManager.NaniteSync.SendNeedAssemblerSettings(Entity.EntityId);
            }
        }

        /// <summary>
        /// GetObjectBuilder on a block is always null
        /// </summary>
        /// <param name="copy"></param>
        /// <returns></returns>
        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return null;
        }

        public override void Close()
        {
            if (NaniteConstructionManager.AssemblerBlocks == null)
                return;

            if (NaniteConstructionManager.AssemblerBlocks.ContainsKey(Entity.EntityId))
                NaniteConstructionManager.AssemblerBlocks.Remove(Entity.EntityId);
        }
    }
}
