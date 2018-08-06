using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.ModAPI;
using VRageMath;
using Sandbox.Common.ObjectBuilders;
using System.Text.RegularExpressions;
using Sandbox.Common;
using Sandbox.Game.Entities;
using Ingame = Sandbox.ModAPI.Ingame;

using VRage;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Game;
using VRage.Game.ModAPI;

namespace NaniteConstructionSystem.Extensions
{
    public static class Conveyor
    {
        private static Dictionary<long, HashSet<long>> m_conveyorCache = new Dictionary<long, HashSet<long>>(10000);
        private static Dictionary<long, long[]> m_conveyorConnected = new Dictionary<long, long[]>();
        private static Dictionary<long, HashSet<long>> m_creatingCache = new Dictionary<long, HashSet<long>>(10000);
        private static DateTime m_lastRebuild = DateTime.MinValue;
        private static FastResourceLock m_lock = new FastResourceLock();
        private static FastResourceLock m_busyLock = new FastResourceLock();

        public static DateTime LastRebuild
        {
            get { return m_lastRebuild; }
        }

        /// <summary>
        /// Rebuilds our conveyor dictionary.  This lets us check if two entities are connected by conveyors quickly.
        /// </summary>
        /// <param name="entities"></param>
        public static void RebuildConveyorList(HashSet<IMyEntity> entities)
        {
            if (!m_busyLock.TryAcquireExclusive())
            {
                Logging.Instance.WriteLine(string.Format("REBUILD Busy.  Last Rebuild: {0}s", (DateTime.Now - m_lastRebuild).TotalSeconds));
                return;
            }

            m_lastRebuild = DateTime.Now;
            DateTime start = DateTime.Now;
            try
            {
                m_conveyorCache.Clear();
                m_conveyorConnected.Clear();
                foreach (IMyEntity entity in entities)
                {
                    if (!(entity is IMyCubeGrid))
                        continue;

                    MyCubeGrid grid = (MyCubeGrid)entity;
                    if (grid.Closed || grid.Physics == null)
                        continue;

                    MyObjectBuilder_CubeGrid gridObject = (MyObjectBuilder_CubeGrid)grid.GetObjectBuilder();

                    if (gridObject == null || gridObject.ConveyorLines == null)
                        continue;

                    foreach (MyObjectBuilder_ConveyorLine line in gridObject.ConveyorLines)
                    {
                        IMySlimBlock slimBlockStart = grid.GetCubeBlock((Vector3I)line.StartPosition);
                        if (slimBlockStart == null || slimBlockStart.FatBlock == null || !slimBlockStart.FatBlock.IsFunctional)
                        {
                            continue;
                        }

                        IMySlimBlock slimBlockEnd = grid.GetCubeBlock((Vector3I)line.EndPosition);
                        if (slimBlockEnd == null || slimBlockEnd.FatBlock == null || !slimBlockEnd.FatBlock.IsFunctional)
                        {
                            continue;
                        }

                        ConnectConveyorBlocks(slimBlockStart, slimBlockEnd);
                    }

                    if (m_conveyorConnected.ContainsKey(grid.EntityId))
                    {
                        long[] connectedBlockId = m_conveyorConnected[grid.EntityId];
                        m_conveyorConnected.Remove(grid.EntityId);
                        ConnectConveyorBlocks(connectedBlockId);
                    }
                }

                foreach (KeyValuePair<long, long[]> p in m_conveyorConnected)
                {
                    ConnectConveyorBlocks(p.Value);
                }

                var creatingCache = new Dictionary<long, HashSet<long>>(m_conveyorCache);
                using (m_lock.AcquireExclusiveUsing())
                {
                    m_creatingCache = creatingCache;
                }
            }
            catch (Exception ex)
            {
                Logging.Instance.WriteLine(String.Format("RebuildConveyorList: {0}", ex.ToString()));
            }
            finally
            {
                m_busyLock.ReleaseExclusive();
                Logging.Instance.WriteLine(string.Format("REBUILD Inventory: {0}ms", (DateTime.Now - start).TotalMilliseconds));
            }
        }

        private static void ConnectConveyorBlocks(IMySlimBlock slimBlockStart, IMySlimBlock slimBlockEnd)
        {
            HashSet<long> startList = GetLocalConveyorListFromEntity(slimBlockStart.FatBlock);
            HashSet<long> endList = GetLocalConveyorListFromEntity(slimBlockEnd.FatBlock);

            if (startList != null && endList != null && startList == endList)
                return;

            if (startList != null && endList != null)
            {
                // No AddList()?  Damn you extensions!
                foreach (long item in endList)
                {
                    if(!startList.Contains(item))
                        startList.Add(item);

                    m_conveyorCache[item] = startList;
                }

                return;
            }

            if (startList != null)
            {
                if(!startList.Contains(slimBlockEnd.FatBlock.EntityId))
                    startList.Add(slimBlockEnd.FatBlock.EntityId);

                if(!m_conveyorCache.ContainsKey(slimBlockEnd.FatBlock.EntityId))
                    m_conveyorCache.Add(slimBlockEnd.FatBlock.EntityId, startList);
            }
            else if (endList != null)
            {
                if(!endList.Contains(slimBlockStart.FatBlock.EntityId))
                    endList.Add(slimBlockStart.FatBlock.EntityId);

                if(!m_conveyorCache.ContainsKey(slimBlockStart.FatBlock.EntityId))
                    m_conveyorCache.Add(slimBlockStart.FatBlock.EntityId, endList);
            }
            else
            {
                HashSet<long> newList = new HashSet<long>();
                newList.Add(slimBlockStart.FatBlock.EntityId);
                if(!m_conveyorCache.ContainsKey(slimBlockStart.FatBlock.EntityId))
                    m_conveyorCache.Add(slimBlockStart.FatBlock.EntityId, newList);

                newList.Add(slimBlockEnd.FatBlock.EntityId);
                if(!m_conveyorCache.ContainsKey(slimBlockEnd.FatBlock.EntityId))
                    m_conveyorCache.Add(slimBlockEnd.FatBlock.EntityId, newList);
            }

            CheckGridConnection(slimBlockStart.FatBlock);
            CheckGridConnection(slimBlockEnd.FatBlock);
        }

        private static void ConnectConveyorBlocks(long[] connectedBlockId)
        {
            IMyEntity startEntity = null;
            IMyEntity endEntity = null;
            if (!MyAPIGateway.Entities.TryGetEntityById(connectedBlockId[0], out startEntity))
            {
                return;
            }

            if (!MyAPIGateway.Entities.TryGetEntityById(connectedBlockId[1], out endEntity))
            {
                return;
            }

            HashSet<long> startList = GetLocalConveyorListFromEntity(startEntity);
            HashSet<long> endList = GetLocalConveyorListFromEntity(endEntity);

            if (startList != null && endList != null && startList == endList)
            {
                return;
            }

            if (startList != null && endList != null)
            {
                // No AddList()?  Damn you extensions!
                foreach (long item in endList)
                {
                    startList.Add(item);
                    m_conveyorCache[item] = startList;
                }
                return;
            }
        }

        private static void CheckGridConnection(IMyEntity block)
        {
            IMyCubeBlock cubeBlock = block as IMyCubeBlock;
            if (cubeBlock == null)
                return;

            if (cubeBlock is IMyPistonBase)
            {
                IMyPistonBase pistonBase = cubeBlock as IMyPistonBase;
                if (pistonBase != null && pistonBase.IsAttached && pistonBase.Top != null && pistonBase.Top.Parent != null)
                {
                    if (!m_conveyorConnected.ContainsKey(pistonBase.Top.Parent.EntityId))
                        m_conveyorConnected.Add(pistonBase.Top.Parent.EntityId, new long[] { pistonBase.Top.EntityId, pistonBase.EntityId });
                }
            }
            else if (cubeBlock is IMyShipConnector)
            {
                Ingame.IMyShipConnector connector = cubeBlock as IMyShipConnector;
                if (connector != null && connector.Status == Sandbox.ModAPI.Ingame.MyShipConnectorStatus.Connected && connector.OtherConnector != null && connector.OtherConnector.CubeGrid != null)
                {
                    if (!m_conveyorConnected.ContainsKey(connector.OtherConnector.CubeGrid.EntityId))
                        m_conveyorConnected.Add(connector.OtherConnector.CubeGrid.EntityId, new long[] { connector.OtherConnector.EntityId, connector.EntityId });
                }
            }
            else if (cubeBlock is IMyAttachableTopBlock)
            {
                var motorRotor = cubeBlock as IMyAttachableTopBlock;
                if (motorRotor != null && motorRotor.IsAttached && motorRotor.Base != null && motorRotor.Parent != null)
                {
                    if (!m_conveyorConnected.ContainsKey(motorRotor.Base.Parent.EntityId))
                        m_conveyorConnected.Add(motorRotor.Base.Parent.EntityId, new long[] { motorRotor.Base.EntityId, motorRotor.EntityId });
                }
            }
            else if (cubeBlock is IMyMechanicalConnectionBlock)
            {
                var motorBase = cubeBlock as IMyMechanicalConnectionBlock;
                if(motorBase != null && motorBase.IsAttached && motorBase.Top != null && motorBase.Top.Parent != null)
                {
                    if (!m_conveyorConnected.ContainsKey(motorBase.Top.Parent.EntityId))
                        m_conveyorConnected.Add(motorBase.Top.Parent.EntityId, new long[] { motorBase.Top.EntityId, motorBase.EntityId });
                }
            }
        }

        private static HashSet<long> GetLocalConveyorListFromEntity(IMyEntity entity)
        {
            if (m_conveyorCache.ContainsKey(entity.EntityId))
                return m_conveyorCache[entity.EntityId];

            return null;
        }

        /// <summary>
        /// Gets the list of blocks connected with this block
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public static HashSet<long> GetConveyorListFromEntity(IMyEntity entity)
        {
            HashSet<long> result = null;

            if(m_creatingCache != null)
            {
                using (m_lock.AcquireExclusiveUsing())
                {
                    if (m_creatingCache.ContainsKey(entity.EntityId))
                        result = m_creatingCache[entity.EntityId];
                }
            }

            if(result == null)
                return new HashSet<long>() { entity.EntityId };

            return result;
        }

        /// <summary>
        /// Returns true if both blocks are connected via conveyor, otherwise false
        /// </summary>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <returns></returns>
        public static bool AreEntitiesConnected(IMyEntity first, IMyEntity second)
        {
            if(m_creatingCache != null)
            {
                using (m_lock.AcquireExclusiveUsing())
                {
                    if (m_creatingCache.ContainsKey(first.EntityId))
                        return m_creatingCache[first.EntityId].Contains(second.EntityId);
                }
            }

            return false;
        }
    }
}
