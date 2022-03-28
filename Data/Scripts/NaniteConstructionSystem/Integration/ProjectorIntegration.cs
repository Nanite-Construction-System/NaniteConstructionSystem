using System.Collections.Generic;
using MultigridProjector.Api;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRageMath;
using VRage.Game.ModAPI;

namespace NaniteConstructionSystem.Integration
{
    public static class ProjectorIntegration
    {
        private static MultigridProjectorModAgent mgpAgent;
        public static MultigridProjectorModAgent MgpAgent => mgpAgent ?? (mgpAgent = new MultigridProjectorModAgent());

        public static void LogVersion()
        {
            var mgpVersion = MgpAgent.Available ? MgpAgent.Version : "Not available";
            Logging.Instance.WriteLine($"Multigrid Projector: {mgpVersion}");
        }

        public static bool TryGetSubgridIndex(IMyProjector projector, IMySlimBlock block, out int subgridIndex)
        {
            var blockGrid = block.CubeGrid;
            if (blockGrid == null || projector.ProjectedGrid == null)
            {
                subgridIndex = 0;
                return false;
            }

            if (!MgpAgent.Available)
            {
                subgridIndex = 0;
                return blockGrid == projector.ProjectedGrid;
            }

            var subgridCount = MgpAgent.GetSubgridCount(projector.EntityId);
            for (subgridIndex = 0; subgridIndex < subgridCount; subgridIndex++)
            {
                var previewGrid = MgpAgent.GetPreviewGrid(projector.EntityId, subgridIndex);
                if (previewGrid == blockGrid)
                    return true;
            }

            subgridIndex = 0;
            return false;
        }

        public static IMySlimBlock GetPreviewBlock(IMyProjector projector, int subgridIndex, Vector3I blockPosition)
        {
            if (!MgpAgent.Available)
                return projector.ProjectedGrid.GetCubeBlock(blockPosition);

            var previewGrid = MgpAgent.GetPreviewGrid(projector.EntityId, subgridIndex);
            return previewGrid?.GetCubeBlock(blockPosition);
        }

        public static IEnumerable<IMySlimBlock> IterBuildableBlocks(IMyProjector projector)
        {
            return MgpAgent.Available
                ? IterBuildableBlocksMgp(projector)
                : IterBuildableBlocksVanilla(projector);
        }

        private static IEnumerable<IMySlimBlock> IterBuildableBlocksVanilla(IMyProjector projector)
        {
            var grid = (MyCubeGrid) projector.ProjectedGrid;

            foreach (IMySlimBlock block in grid.GetBlocks())
            {
                if (projector.CanBuild(block, false) == BuildCheckResult.OK)
                    yield return block;
            }
        }

        private static readonly BoundingBoxI UnlimitedBoundingBoxI = new BoundingBoxI(Vector3I.MinValue, Vector3I.MaxValue);
        private const int ConstructionBlockStateMask = (int) BlockState.Buildable | (int) BlockState.BeingBuilt;

        private static IEnumerable<IMySlimBlock> IterBuildableBlocksMgp(IMyProjector projector)
        {
            var blockStates = new Dictionary<Vector3I, BlockState>();
            var subgridCount = MgpAgent.GetSubgridCount(projector.EntityId);
            for (var subgridIndex = 0; subgridIndex < subgridCount; subgridIndex++)
            {
                var previewGrid = MgpAgent.GetPreviewGrid(projector.EntityId, subgridIndex);
                if (previewGrid == null)
                    continue;

                if (!MgpAgent.GetBlockStates(blockStates, projector.EntityId, subgridIndex, UnlimitedBoundingBoxI, ConstructionBlockStateMask))
                    continue;

                foreach (var blockPosition in blockStates.Keys)
                {
                    var block = previewGrid.GetCubeBlock(blockPosition);
                    if (block == null)
                        continue;

                    yield return block;
                }

                blockStates.Clear();
            }
        }
    }
}
