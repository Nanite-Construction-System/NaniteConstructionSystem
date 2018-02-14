using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.ModAPI;
using Sandbox.Game.Entities;
using VRage;
using VRageMath;
using VRage.Game;
using Sandbox.Definitions;
using VRage.Voxels;
using VRage.Game.ModAPI;

using NaniteConstructionSystem.Extensions;
using NaniteConstructionSystem.Entities.Effects;
using NaniteConstructionSystem.Settings;

namespace NaniteConstructionSystem.Entities.Beacons
{
    public class NaniteMining
    {
        const int CELL_SIZE = 16;
        const int VOXEL_CLAMP_BORDER_DISTANCE = 2;
        
        private DateTime m_lastUpdate;
        private bool m_busy;
        private int m_updateCount;

        private IMyTerminalBlock m_block;
        public IMyTerminalBlock MiningBlock
        {
            get { return m_block; }
        }

        private FastResourceLock m_lock;
        public FastResourceLock Lock
        {
            get { return m_lock; }
        }

        private FastResourceLock m_locationLock;
        public FastResourceLock LocationLock
        {
            get { return m_locationLock; }
        }

        private bool m_working;
        public bool IsWorking
        {
            get { return m_working; }
        }

        private Dictionary<MyVoxelMaterialDefinition, List<NaniteMiningItem>> m_oreList;
        public Dictionary<MyVoxelMaterialDefinition, List<NaniteMiningItem>> OreList
        {
            get { return m_oreList; }
        }

        private List<NaniteBlockEffectBase> m_effects;
        private bool m_initialize;
        private StringBuilder m_syncDetails;
        private DateTime m_syncLastUpdate;
        private DateTime m_lastRefresh;
        private StringBuilder m_oreListCache;
        //private Dictionary<Vector3D, NaniteMiningItem> m_oreLocations;

        public NaniteMining(IMyTerminalBlock block)
        {
            m_block = block;
            m_busy = false;
            m_lastUpdate = DateTime.MinValue;
            m_lastRefresh = DateTime.MinValue;
            m_lock = new FastResourceLock();
            m_locationLock = new FastResourceLock();
            m_initialize = false;
            m_working = false;
            m_syncDetails = new StringBuilder();
            m_updateCount = 0;
            m_oreListCache = new StringBuilder();
            //m_oreLocations = new Dictionary<Vector3D, NaniteMiningItem>();

            MiningBlock.AppendingCustomInfo += MiningBlock_AppendingCustomInfo;
        }

        private void MiningBlock_AppendingCustomInfo(IMyTerminalBlock block, StringBuilder sb)
        {
            if (MyAPIGateway.Session == null)
                return;

            sb.Clear();

            if (Sync.IsServer)
            {
                sb.Append("=========\r\n");
                sb.Append("Ore Detected:\r\n");
                sb.Append("=========\r\n");

                sb.Append(m_oreListCache);

                /*
                using (Lock.AcquireExclusiveUsing())
                {
                    if (m_oreList != null)
                    {
                        foreach (var item in m_oreList)
                        {
                            sb.Append(string.Format("{0} - {1:N0} Kg\r\n", item.Key.MinedOre, item.Value.Sum(x => CalculateAmount(item.Key, x.Amount))));
                        }
                    }
                }
                */
                sb.Append("\r\n=========\r\n");
                sb.Append("Valid Ore Types:\r\n");
                sb.Append("=========\r\n");

                if (!NaniteConstructionManager.HammerTerminalSettings.ContainsKey(m_block.EntityId))
                    NaniteConstructionManager.HammerTerminalSettings.Add(m_block.EntityId, new NaniteHammerTerminalSettings(true));

                var allowedOreList = NaniteConstructionManager.HammerTerminalSettings[m_block.EntityId].SelectedOres;
                foreach(var item in allowedOreList)
                {
                    sb.Append(item + "\r\n");
                }

                if (m_syncDetails.Length != sb.Length)
                {
                    m_syncLastUpdate = DateTime.Now;
                    m_syncDetails.Clear();
                    m_syncDetails.Append(sb);
                    SendDetails();
                }
            }
            else
            {
                sb.Append(m_syncDetails);
            }
        }

        private void SendDetails()
        {
            DetailData data = new DetailData();
            data.EntityId = MiningBlock.EntityId;
            data.Details = m_syncDetails.ToString();
            MyAPIGateway.Multiplayer.SendMessageToOthers(8954, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
        }

        public void SyncDetails(DetailData data)
        {
            m_syncDetails.Clear();
            m_syncDetails.Append(data.Details);
            //m_block.RefreshCustomInfo();
            // Trigger a refresh
            var detector = m_block as Sandbox.ModAPI.Ingame.IMyOreDetector;
            var action = detector.GetActionWithName("BroadcastUsingAntennas");
            action.Apply(m_block);
            action.Apply(m_block);
        }

        public void Update()
        {
            /*
            // Debug shows mining area
            var position = m_block.PositionComp.GetPosition();
            var direction = Vector3D.Normalize(-m_block.PositionComp.WorldMatrix.Up);
            var color = Color.Red;
            for (int r = 0; r < NaniteConstructionManager.Settings.MiningDepth; r++)
            {
                MatrixD matrix = MatrixD.CreateTranslation(position + (direction * NaniteConstructionManager.Settings.MiningRadius * r));
                MySimpleObjectDraw.DrawTransparentSphere(ref matrix, NaniteConstructionManager.Settings.MiningRadius, ref color, MySimpleObjectRasterizer.Solid, 20);
            }
            */

            m_updateCount++;

            if(!m_initialize)
            {
                m_initialize = true;
                Initialize();

                if (!CheckWorking())
                    return;
            }

            if (Sync.IsClient)
            {
                foreach (var item in m_effects)
                {
                    if (IsWorking)
                        item.ActiveUpdate();
                    else
                        item.InactiveUpdate();
                }
            }

            if (Sync.IsServer)
            {
                if (DateTime.Now - m_syncLastUpdate > TimeSpan.FromSeconds(3))
                {
                    m_syncLastUpdate = DateTime.Now;
                    SendDetails();
                }
            }

            if(m_updateCount % 240 == 0)
            {
                if (!CheckWorking())
                {
                    m_working = false;
                    return;
                }

                m_working = true;
            }

            if (!m_working)
            {
                using(Lock.AcquireExclusiveUsing())
                    m_oreList = null;

                //using(LocationLock.AcquireExclusiveUsing())
                //    m_oreLocations.Clear();

                return;
            }

            if (DateTime.Now - m_lastUpdate > TimeSpan.FromSeconds(30) && !m_busy)
            {
                m_lastUpdate = DateTime.Now;
                m_working = true;
                m_busy = true;
                //m_block.SetEmissiveParts("Emissive-Beacon", Color.FromNonPremultiplied(new Vector4(0.3f, 0.15f, 0.0f, 1f)), 1f);
                //MyCubeBlockEmissive.SetEmissiveParts((MyEntity)m_block, 1.0f, Color.FromNonPremultiplied(new Vector4(0.3f, 0.15f, 0.0f, 1f)), Color.White);

                if (Sync.IsServer)
                    MyAPIGateway.Parallel.Start(ScanVoxelWork, ScanVoxelComplete);
            }

            //((IMyFunctionalBlock)m_block).RefreshCustomInfo();
        }

        public void RemoveVoxel(Vector3D pos)
        {
            /*
            MyAPIGateway.Parallel.Start(() =>
            {
                NaniteMiningItem item = null;
                if (!m_oreLocations.TryGetValue(pos, out item))
                    return;

                using(LocationLock.AcquireExclusiveUsing())
                    m_oreLocations.Remove(pos);

                var voxelDef = MyDefinitionManager.Static.GetVoxelMaterialDefinition(item.VoxelMaterial);
                if (!m_oreList.ContainsKey(voxelDef))
                    return;

                using (Lock.AcquireExclusiveUsing())
                    m_oreList[voxelDef].Remove(item);
            });
            */
        }

        private bool CheckWorking()
        {
            if (!m_block.IsWorking)
                return false;

            if (!IsInVoxel(m_block))
            {
                Logging.Instance.WriteLine(string.Format("Block '{0}' not in voxel. ({1})", m_block.CustomName, Sandbox.Game.MyPerGameSettings.Destruction));
                return false;
            }

            /*
            BoundingSphereD sphere = new BoundingSphereD(m_block.GetPosition(), NaniteConstructionManager.Settings.MiningRadius);
            foreach (var item in MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere))
            {
                if (item == m_block)
                    continue;

                var terminalBlock = item as IMyTerminalBlock;
                if (terminalBlock == null)
                    continue;

                if (terminalBlock.BlockDefinition.SubtypeName == "NaniteUltrasonicHammer")
                {
                    var cubeBlock = terminalBlock as MyCubeBlock;
                    m_working = false;
                    Logging.Instance.WriteLine(string.Format("Found hammer near another hammer!"));
                    //MyAPIGateway.Utilities.ShowMessage("[Nanite Control Factory]", "An ultrasonic hammer has been removed due to it being too close to another hammer");
                    return false;
                }
            }
            */
            //m_block.SetEmissiveParts("Emissive-Beacon", Color.FromNonPremultiplied(new Vector4(0.3f, 0.15f, 0.0f, 1f)), 1f);
            //MyCubeBlockEmissive.SetEmissiveParts((MyEntity)m_block, 1.0f, Color.FromNonPremultiplied(new Vector4(0.3f, 0.15f, 0.0f, 1f)), Color.White);
            return true;
        }

        private void Initialize()
        {            
            m_effects = new List<NaniteBlockEffectBase>();
            m_effects.Add(new MiningHammerEffect((MyCubeBlock)m_block));
            m_effects.Add(new NaniteBeaconEffect((MyCubeBlock)m_block, new Vector3(0f, 3.6f, -0.6f), new Vector4(0.3f, 0.15f, 0.0f, 1f)));

            //if (!MyCubeGrid.IsInVoxels(((MyCubeBlock)m_block).SlimBlock))
            if(!IsInVoxel(m_block))
            {
                //MyAPIGateway.Utilities.ShowMessage("[Nanite Control Factory]", string.Format("The NUHOL '{0}' was detected to not be embedded inside a planet or asteroid.  Please be sure part of the block extends INTO the asteroid or planet.", m_block.CustomName));
            }
        }

        public void Close()
        {
            Logging.Instance.WriteLine(string.Format("Closing"));
            if (m_effects != null)
            {
                foreach (var item in m_effects)
                    item.Unload();

                m_effects.Clear();
            }

            // I'm putting this here because for some reason the Logic component is not running it's closed method??? wtf.
            if (NaniteConstructionManager.MiningList.Contains(this))
            {
                NaniteConstructionManager.MiningList.Remove(this);
            }

            //m_oreLocations = null;
            m_oreList = null;
        }

        private void ScanVoxelWork()
        {
            try
            {
                if (m_oreList != null && DateTime.Now - m_lastRefresh < TimeSpan.FromMinutes(5))
                    return;

                m_lastRefresh = DateTime.Now;
                Vector3D position = m_block.GetPosition();
                BoundingSphereD sphere = new BoundingSphereD(position, 2f);
                var entities = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);

                if (!NaniteConstructionManager.HammerTerminalSettings.ContainsKey(m_block.EntityId))
                    NaniteConstructionManager.HammerTerminalSettings.Add(m_block.EntityId, new NaniteHammerTerminalSettings(true));

                var allowedOreList = new HashSet<string>(NaniteConstructionManager.HammerTerminalSettings[m_block.EntityId].SelectedOres);

                DateTime start = DateTime.Now;
                Logging.Instance.WriteLine(string.Format("MINING Hammer Start Scan"));
                Dictionary<Vector3D, NaniteMiningItem> miningItems = new Dictionary<Vector3D, NaniteMiningItem>(100000);
                foreach (var item in entities)
                {
                    var voxelMap = item as MyVoxelBase;
                    if (voxelMap == null)
                        continue;

                    if (item.GetType().Name == "MyVoxelPhysics")
                        continue;

                    Logging.Instance.WriteLine(string.Format("Item: {0}:{1} - {2}", item.GetType().Name, item.EntityId, item.GetPosition()));
                    //var direction = Vector3D.Normalize(item.GetPosition() - position);
                    var direction = Vector3D.Normalize(-m_block.PositionComp.WorldMatrix.Up);
                    for (int r = NaniteConstructionManager.Settings.MiningDepth - 1; r >= 0; r--)
                    {
                        DateTime readStart = DateTime.Now;
                        ReadVoxel(voxelMap, position + (direction * NaniteConstructionManager.Settings.MiningRadius * r), miningItems, allowedOreList);
                        //Logging.Instance.WriteLine(string.Format("Read Time: {0}", (DateTime.Now - readStart).TotalMilliseconds));
                        //ReadVoxel(voxelMap, position, position + (direction * NaniteConstructionManager.Settings.MiningRadius * NaniteConstructionManager.Settings.MiningDepth), miningItems, allowedOreList);
                    }
                }

                Logging.Instance.WriteLine(string.Format("MINING Hammer Read Voxel Complete: {0}ms", (DateTime.Now - start).TotalMilliseconds));

                Dictionary<MyVoxelMaterialDefinition, List<NaniteMiningItem>> oreList = new Dictionary<MyVoxelMaterialDefinition, List<NaniteMiningItem>>();
                Dictionary<Vector3D, NaniteMiningItem> oreLocations = new Dictionary<Vector3D, NaniteMiningItem>();

                foreach (var item in miningItems.Values)
                {
                    var def = MyDefinitionManager.Static.GetVoxelMaterialDefinition(item.VoxelMaterial);
                    if (!oreList.ContainsKey(def))
                        oreList.Add(def, new List<NaniteMiningItem>());

                    //if (oreList[def].Count >= 1000)
                    //    continue;

                    oreList[def].Add(item);
                    //oreLocations.Add(item.Position, item);
                }

                m_oreListCache.Clear();
                if (oreList != null)
                {
                    foreach (var item in oreList)
                    {
                        m_oreListCache.Append(string.Format("{0} - {1:N0} Kg\r\n", item.Key.MinedOre, CalculateAmount(item.Key, item.Value.Sum(x => x.Amount))));
                    }
                }

                //using (LocationLock.AcquireExclusiveUsing())
                //m_oreLocations = oreLocations;

                using (Lock.AcquireExclusiveUsing())
                    m_oreList = oreList;

                Logging.Instance.WriteLine(string.Format("MINING Hammer Scan Complete: {0}ms ({1} groups, {2} items)", (DateTime.Now - start).TotalMilliseconds, oreList.Count, oreList.Sum(x => x.Value.Count)));
            }
            catch (Exception ex)
            {
                Logging.Instance.WriteLine(string.Format("ScanVoxelWork() Error: {0}", ex.ToString()));
            }
        }

        private void ScanVoxelComplete()
        {
            m_block.RefreshCustomInfo();
            m_lastUpdate = DateTime.Now;
            m_busy = false;
        }

        private void ReadVoxel(IMyVoxelBase voxel, Vector3D position, Dictionary<Vector3D, NaniteMiningItem> targets, HashSet<string> allowedOreList = null)
        {
            var m_cache = new MyStorageData();
            NaniteShapeSphere shapeSphere = new NaniteShapeSphere();
            shapeSphere.Center = position;
            shapeSphere.Radius = NaniteConstructionManager.Settings.MiningRadius / 2;

            //NaniteShapeCapsule shapeCapsule = new NaniteShapeCapsule();
            //shapeCapsule.A = positionA;
            //shapeCapsule.B = positionB;
            //shapeCapsule.Radius = NaniteConstructionManager.Settings.MiningRadius;

            Vector3I minCorner, maxCorner, numCells;
            GetVoxelShapeDimensions(voxel, shapeSphere, out minCorner, out maxCorner, out numCells);

            var cacheMin = minCorner - 1;
            var cacheMax = maxCorner + 1;

            //bool bRareOnly = true;
            //if (allowedOreList != null && allowedOreList.Contains("Stone"))
            //    bRareOnly = false;

            m_cache.Resize(cacheMin, cacheMax);
            m_cache.ClearContent(0);
            m_cache.ClearMaterials(0);
            var flags = MyVoxelRequestFlags.AdviseCache;
            voxel.Storage.ReadRange(m_cache, MyStorageDataTypeFlags.ContentAndMaterial, 0, cacheMin, cacheMax, ref flags);

            //voxel.Storage.PinAndExecute(() =>
            {
                Vector3I pos;
                for (pos.X = minCorner.X; pos.X <= maxCorner.X; ++pos.X)
                    for (pos.Y = minCorner.Y; pos.Y <= maxCorner.Y; ++pos.Y)
                        for (pos.Z = minCorner.Z; pos.Z <= maxCorner.Z; ++pos.Z)
                        {
                            // get original amount
                            var relPos = pos - cacheMin;
                            var lin = m_cache.ComputeLinear(ref relPos);

                            //var relPos = pos - cacheMin; // Position of voxel in local space
                            var original = m_cache.Content(lin); // Content at this position

                            if (original == MyVoxelConstants.VOXEL_CONTENT_EMPTY)
                                continue;

                            var material = m_cache.Material(lin); // Material at this position
                            Vector3D vpos;
                            MyVoxelCoordSystems.VoxelCoordToWorldPosition(voxel.PositionLeftBottomCorner, ref pos, out vpos);
                            if (targets.ContainsKey(vpos))
                                continue;

                            /*
                            var volume = shapeSphere.GetVolume(ref vpos);
                            if (volume == 0f) // Shape and voxel do not intersect at this position, so continue
                                continue;
                            */

                            // Pull information about voxel required for later processing
                            var voxelMat = MyDefinitionManager.Static.GetVoxelMaterialDefinition(material);
                            //if ((bRareOnly && voxelMat.IsRare) || !bRareOnly)
                            //if(voxelMat.IsRare)
                            if (allowedOreList != null)// || (allowedOreList == null && bRareOnly && voxelMat.IsRare))
                            {
                                if (allowedOreList != null && !allowedOreList.Contains(voxelMat.MinedOre))
                                    continue;


                                NaniteMiningItem miningItem = new NaniteMiningItem();
                                miningItem.Position = vpos;
                                miningItem.VoxelMaterial = material;
                                miningItem.VoxelId = voxel.EntityId;
                                miningItem.Amount = original; // * 3.9f;
                                miningItem.MiningHammer = this;
                                targets.Add(vpos, miningItem);
                                //count++;
                            }

                            //m_cache.Content(lin, 0);
                            //m_cache.Material(lin, 0);
                        }


                //voxel.Storage.WriteRange(m_cache, MyStorageDataTypeFlags.ContentAndMaterial, cacheMin, cacheMax);
            };

        /*
        int count = 0;
        for (var itCells = new Vector3I_RangeIterator(ref Vector3I.Zero, ref numCells); itCells.IsValid(); itCells.MoveNext())
        {
            Vector3I cellMinCorner, cellMaxCorner;
            GetCellCorners(ref minCorner, ref maxCorner, ref itCells, out cellMinCorner, out cellMaxCorner);

            var cacheMin = cellMinCorner - 1;
            var cacheMax = cellMaxCorner + 1;
            voxel.Storage.ClampVoxel(ref cacheMin);
            voxel.Storage.ClampVoxel(ref cacheMax);

            m_cache.Resize(cacheMin, cacheMax);
            voxel.Storage.ReadRange(m_cache, MyStorageDataTypeFlags.ContentAndMaterial, 0, cacheMin, cacheMax);

            for (var it = new Vector3I_RangeIterator(ref cellMinCorner, ref cellMaxCorner); it.IsValid(); it.MoveNext())
            {
                var relPos = it.Current - cacheMin; // Position of voxel in local space
                var original = m_cache.Content(ref relPos); // Content at this position
                var material = m_cache.Material(ref relPos); // Material at this position

                if (original == MyVoxelConstants.VOXEL_CONTENT_EMPTY)
                    continue;

                Vector3D vpos;
                MyVoxelCoordSystems.VoxelCoordToWorldPosition(voxel.PositionLeftBottomCorner, ref it.Current, out vpos);
                if (targets.ContainsKey(vpos))
                    continue;

                var volume = shapeSphere.GetVolume(ref vpos);
                if (volume == 0f) // Shape and voxel do not intersect at this position, so continue
                    continue;

                // Pull information about voxel required for later processing
                var voxelMat = MyDefinitionManager.Static.GetVoxelMaterialDefinition(m_cache.Material(ref relPos));
                //if ((bRareOnly && voxelMat.IsRare) || !bRareOnly)
                //if(voxelMat.IsRare)
                if (allowedOreList != null)// || (allowedOreList == null && bRareOnly && voxelMat.IsRare))
                {
                    if (allowedOreList != null && !allowedOreList.Contains(voxelMat.MinedOre))
                        continue;

                    NaniteMiningItem miningItem = new NaniteMiningItem();
                    miningItem.Position = vpos;
                    miningItem.VoxelMaterial = material;
                    miningItem.VoxelId = voxel.EntityId;
                    miningItem.Amount = original; // * 3.9f;
                    miningItem.MiningHammer = this;
                    targets.Add(vpos, miningItem);
                    count++;
                }                   
            }                
        }
        */

        //Logging.Instance.WriteLine(string.Format("Voxels Read: {0} - {1}", voxel.GetType().Name, count));
        }

        public static void RemoveVoxelContent(long voxelId, Vector3D position, out byte materialRemoved, out float amountOfMaterial)
        {
            materialRemoved = 0;
            amountOfMaterial = 0f;

            IMyEntity entity;
            if (!MyAPIGateway.Entities.TryGetEntityById(voxelId, out entity))
                return;

            var voxel = entity as IMyVoxelBase;
            var targetMin = position;
            var targetMax = position;
            Vector3I minVoxel, maxVoxel;
            MyVoxelCoordSystems.WorldPositionToVoxelCoord(voxel.PositionLeftBottomCorner, ref targetMin, out minVoxel);
            MyVoxelCoordSystems.WorldPositionToVoxelCoord(voxel.PositionLeftBottomCorner, ref targetMax, out maxVoxel);

            MyVoxelBase voxelBase = voxel as MyVoxelBase;
            minVoxel += voxelBase.StorageMin;
            maxVoxel += voxelBase.StorageMin + 1;

            voxel.Storage.ClampVoxel(ref minVoxel);
            voxel.Storage.ClampVoxel(ref maxVoxel);

            MyStorageData cache = new MyStorageData();
            cache.Resize(minVoxel, maxVoxel);
            var flag = MyVoxelRequestFlags.AdviseCache;
            cache.ClearContent(0);
            cache.ClearMaterials(0);

            byte original = 0;
            byte material = 0;
            // I don't really think pinning is necessary since I'm in the main thread, but this hasn't been working for awhile, so I'll keep it here.
            voxel.Storage.PinAndExecute(() =>
            {
                voxel.Storage.ReadRange(cache, MyStorageDataTypeFlags.ContentAndMaterial, 0, minVoxel, maxVoxel, ref flag);

                // Grab content and material
                original = cache.Content(0);
                material = cache.Material(0);

                if (original == MyVoxelConstants.VOXEL_CONTENT_EMPTY)
                {
                    Logging.Instance.WriteLine(string.Format("Content is empty"));
                    return;
                }

                // Remove Content
                Logging.Instance.WriteLine($"Material: SizeLinear: {cache.SizeLinear}, Size3D: {cache.Size3D}, AboveISO: {cache.ContainsVoxelsAboveIsoLevel()}");
                cache.Content(0, 0);
                voxel.Storage.WriteRange(cache, MyStorageDataTypeFlags.ContentAndMaterial, minVoxel, maxVoxel);
            });

            // Calculate Material Mined
            var voxelMat = MyDefinitionManager.Static.GetVoxelMaterialDefinition(material);
            materialRemoved = material;
            amountOfMaterial = CalculateAmount(voxelMat, original * 3.9f);

            // This will sync the clients.  Apparently voxel writes do not sync, lovely.
            if(Sync.IsServer)
            {
                VoxelRemovalData data = new VoxelRemovalData();
                data.VoxelID = voxelId;
                data.Position = position;
                MyAPIGateway.Multiplayer.SendMessageToOthers(8969, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
            }
        }

        public static bool CheckVoxelContent(long voxelId, Vector3D position)
        {
            IMyEntity entity;
            if (!MyAPIGateway.Entities.TryGetEntityById(voxelId, out entity))
                return false;

            var voxel = entity as IMyVoxelBase;
            var targetMin = position;
            var targetMax = position;
            Vector3I minVoxel, maxVoxel;
            MyVoxelCoordSystems.WorldPositionToVoxelCoord(voxel.PositionLeftBottomCorner, ref targetMin, out minVoxel);
            MyVoxelCoordSystems.WorldPositionToVoxelCoord(voxel.PositionLeftBottomCorner, ref targetMax, out maxVoxel);

            MyVoxelBase voxelBase = voxel as MyVoxelBase;
            minVoxel += voxelBase.StorageMin;
            maxVoxel += voxelBase.StorageMin;

            voxel.Storage.ClampVoxel(ref minVoxel);
            voxel.Storage.ClampVoxel(ref maxVoxel);

            MyStorageData cache = new MyStorageData();
            cache.Resize(minVoxel, maxVoxel);
            voxel.Storage.ReadRange(cache, MyStorageDataTypeFlags.Content, 0, minVoxel, maxVoxel);

            // Grab content and material
            var original = cache.Content(0);
            //var material = cache.Material(0);

            if (original == MyVoxelConstants.VOXEL_CONTENT_EMPTY)
            {
                //Logging.Instance.WriteLine(string.Format("Content is empty"));
                return false;
            }

            return true;
        }

        private static float CalculateAmount(MyVoxelMaterialDefinition material, float amount)
        {
            var oreObjBuilder = VRage.ObjectBuilders.MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Ore>(material.MinedOre);
            oreObjBuilder.MaterialTypeName = material.Id.SubtypeId;
            float amountCubicMeters = (float)(((float)amount / (float)MyVoxelConstants.VOXEL_CONTENT_FULL) * MyVoxelConstants.VOXEL_VOLUME_IN_METERS * Sandbox.Game.MyDrillConstants.VOXEL_HARVEST_RATIO);
            amountCubicMeters *= (float)material.MinedOreRatio;
            var physItem = MyDefinitionManager.Static.GetPhysicalItemDefinition(oreObjBuilder);
            MyFixedPoint amountInItemCount = (MyFixedPoint)(amountCubicMeters / physItem.Volume);
            return (float)amountInItemCount;
        }

        public abstract class NaniteShape
        {
            protected MatrixD m_transformation = MatrixD.Identity;
            protected MatrixD m_inverse = MatrixD.Identity;
            protected bool m_inverseIsDirty = false;

            public MatrixD Transformation
            {
                get { return m_transformation; }
                set
                {
                    m_transformation = value;
                    m_inverseIsDirty = true;
                }
            }

            public abstract BoundingBoxD GetWorldBoundaries();

            /// <summary>
            /// Gets volume of intersection of shape and voxel
            /// </summary>
            /// <param name="voxelPosition">Left bottom point of voxel</param>
            /// <returns>Normalized volume of intersection</returns>
            public abstract float GetVolume(ref Vector3D voxelPosition);

            /// <returns>Recomputed density value from signed distance</returns>
            protected float SignedDistanceToDensity(float signedDistance)
            {
                const float TRANSITION_SIZE = MyVoxelConstants.VOXEL_SIZE_IN_METRES;
                const float NORMALIZATION_CONSTANT = 1 / (2 * MyVoxelConstants.VOXEL_SIZE_IN_METRES);
                return MathHelper.Clamp(-signedDistance, -TRANSITION_SIZE, TRANSITION_SIZE) * NORMALIZATION_CONSTANT + 0.5f;
            }
        }

        public partial class NaniteShapeCapsule : NaniteShape
        {
            public Vector3D A;
            public Vector3D B;
            public float Radius;

            public override BoundingBoxD GetWorldBoundaries()
            {
                var bbox = new BoundingBoxD(A - Radius, B + Radius);
                return bbox.TransformSlow(Transformation);
            }

            public override float GetVolume(ref Vector3D voxelPosition)
            {
                if (m_inverseIsDirty)
                {
                    m_inverse = MatrixD.Invert(m_transformation);
                    m_inverseIsDirty = false;
                }

                voxelPosition = Vector3D.Transform(voxelPosition, m_inverse);

                var pa = voxelPosition - A;
                var ba = B - A;
                var h = MathHelper.Clamp(pa.Dot(ref ba) / ba.LengthSquared(), 0.0, 1.0);
                var sd = (float)((pa - ba * h).Length() - Radius);
                return SignedDistanceToDensity(sd);
            }
        }

        public class NaniteShapeSphere : NaniteShape
        {
            public Vector3D Center; // in World space
            public float Radius;

            public override BoundingBoxD GetWorldBoundaries()
            {
                var bbox = new BoundingBoxD(Center - Radius, Center + Radius);
                return bbox.TransformSlow(Transformation);
            }

            public override float GetVolume(ref Vector3D voxelPosition)
            {
                if (m_inverseIsDirty) { MatrixD.Invert(ref m_transformation, out m_inverse); m_inverseIsDirty = false; }
                Vector3D.Transform(ref voxelPosition, ref m_inverse, out voxelPosition);
                float dist = (float)(voxelPosition - Center).Length();
                float diff = dist - Radius;
                return SignedDistanceToDensity(diff);
            }
        }

        private static void ComputeShapeBounds(IMyVoxelBase voxelMap, ref BoundingBoxD shapeAabb, Vector3D voxelMapMinCorner, Vector3I storageSize, out Vector3I voxelMin, out Vector3I voxelMax)
        {
            MyVoxelCoordSystems.WorldPositionToVoxelCoord(voxelMapMinCorner, ref shapeAabb.Min, out voxelMin);
            MyVoxelCoordSystems.WorldPositionToVoxelCoord(voxelMapMinCorner, ref shapeAabb.Max, out voxelMax);

            MyVoxelBase voxelBase = voxelMap as MyVoxelBase;

            voxelMin += voxelBase.StorageMin;
            voxelMax += voxelBase.StorageMin + 1;

            storageSize -= 1;
            Vector3I.Clamp(ref voxelMin, ref Vector3I.Zero, ref storageSize, out voxelMin);
            Vector3I.Clamp(ref voxelMax, ref Vector3I.Zero, ref storageSize, out voxelMax);
        }

        private static void GetVoxelShapeDimensions(IMyVoxelBase voxelMap, NaniteShape shape, out Vector3I minCorner, out Vector3I maxCorner, out Vector3I numCells)
        {
            {
                var bbox = shape.GetWorldBoundaries();
                ComputeShapeBounds(voxelMap, ref bbox, voxelMap.PositionLeftBottomCorner, voxelMap.Storage.Size, out minCorner, out maxCorner);
            }
            numCells = new Vector3I((maxCorner.X - minCorner.X) / CELL_SIZE, (maxCorner.Y - minCorner.Y) / CELL_SIZE, (maxCorner.Z - minCorner.Z) / CELL_SIZE);
        }

        private static void GetCellCorners(ref Vector3I minCorner, ref Vector3I maxCorner, ref Vector3I_RangeIterator it, out Vector3I cellMinCorner, out Vector3I cellMaxCorner)
        {
            cellMinCorner = new Vector3I(minCorner.X + it.Current.X * CELL_SIZE, minCorner.Y + it.Current.Y * CELL_SIZE, minCorner.Z + it.Current.Z * CELL_SIZE);
            cellMaxCorner = new Vector3I(Math.Min(maxCorner.X, cellMinCorner.X + CELL_SIZE), Math.Min(maxCorner.Y, cellMinCorner.Y + CELL_SIZE), Math.Min(maxCorner.Z, cellMinCorner.Z + CELL_SIZE));
        }

        private bool IsInVoxel(IMyTerminalBlock block)
        {
            BoundingBoxD blockWorldAABB = block.PositionComp.WorldAABB;
            List<MyVoxelBase> voxelList = new List<MyVoxelBase>();
            MyGamePruningStructure.GetAllVoxelMapsInBox(ref blockWorldAABB, voxelList);

            var cubeSize = block.CubeGrid.GridSize;

            BoundingBoxD localAABB = new BoundingBoxD(cubeSize * ((Vector3D)block.Min - 1), cubeSize * ((Vector3D)block.Max + 1));
            var gridWorldMatrix = block.CubeGrid.WorldMatrix;

            //Logging.Instance.WriteLine($"Total Voxels: {voxelList.Count}.  CubeSize: {cubeSize}.  localAABB: {localAABB}");

            foreach (var map in voxelList)
            {
                if (map.IsAnyAabbCornerInside(ref gridWorldMatrix, localAABB))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public class NaniteMiningItem
    {
        public byte VoxelMaterial { get; set; }
        public Vector3D Position { get; set; }
        public long VoxelId { get; set; }
        public float Amount { get; set; }
        public NaniteMining MiningHammer { get; set; }
    }
}
