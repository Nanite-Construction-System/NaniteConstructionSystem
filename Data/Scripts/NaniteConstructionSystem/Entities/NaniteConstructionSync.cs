using System;
using System.Text;
using Sandbox.ModAPI;
using VRage.Utils;
using VRage.ModAPI;

using NaniteConstructionSystem.Extensions;
using NaniteConstructionSystem.Settings;

namespace NaniteConstructionSystem.Entities
{
    public class NaniteConstructionManagerSync
    {
        private bool m_init;
        public NaniteConstructionManagerSync()
        {
            m_init = false;
        }

        public void Initialize()
        {
            if (!Sync.IsServer) // Client, why do I have to be so difficult
            {
                MyAPIGateway.Multiplayer.RegisterMessageHandler(8950, HandleUpdateState);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(8951, HandleAddTarget);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(8952, HandleCompleteTarget);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(8953, HandleCancelTarget);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(8954, HandleDetails);
              //*  MyAPIGateway.Multiplayer.RegisterMessageHandler(8958, HandleStartParticle);
              //*  MyAPIGateway.Multiplayer.RegisterMessageHandler(8959, HandleRemoveParticle);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(8960, HandleTerminalSettings);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(8962, HandleAssemblerSettings);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(8966, HandleHammerTerminalSettings);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(8969, HandleVoxelRemoval);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(8971, HandleBeaconTerminalSettings);
            }
            else
            {
                MyAPIGateway.Multiplayer.RegisterMessageHandler(8961, HandleNeedTerminalSettings);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(8963, HandleNeedAssemblerSettings);
                // Same function but server.  I think SendMessageToOthers sends to self, which will stack overflow
                MyAPIGateway.Multiplayer.RegisterMessageHandler(8964, HandleTerminalSettings);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(8965, HandleAssemblerSettings);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(8967, HandleHammerTerminalSettings);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(8968, HandleNeedHammerTerminalSettings);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(8972, HandleBeaconTerminalSettings);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(8973, HandleNeedBeaconTerminalSettings);
            }

            m_init = true;
        }

        public void Unload()
        {
            if (m_init)
            {
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(8950, HandleUpdateState);
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(8951, HandleAddTarget);
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(8952, HandleCompleteTarget);
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(8953, HandleCancelTarget);
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(8954, HandleDetails);
              //*  MyAPIGateway.Multiplayer.UnregisterMessageHandler(8958, HandleStartParticle);
              //*  MyAPIGateway.Multiplayer.UnregisterMessageHandler(8959, HandleRemoveParticle);
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(8960, HandleTerminalSettings);
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(8962, HandleAssemblerSettings);
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(8961, HandleNeedTerminalSettings);
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(8963, HandleNeedAssemblerSettings);
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(8964, HandleTerminalSettings);
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(8965, HandleAssemblerSettings);
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(8967, HandleHammerTerminalSettings);
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(8968, HandleNeedHammerTerminalSettings);
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(8972, HandleBeaconTerminalSettings);
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(8973, HandleNeedBeaconTerminalSettings);
            }
        }

        /// <summary>
        /// Multiplayer packet handlers - Direct to the proper block handler
        /// </summary>
        /// <param name="data"></param>
        private void HandleUpdateState(byte[] data)
        {
            try
            {
                if (MyAPIGateway.Session == null)
                    return;

                StateData state = MyAPIGateway.Utilities.SerializeFromXML<StateData>(ASCIIEncoding.ASCII.GetString(data));
                //MyLog.Default.WriteLine(string.Format("UpdateState: {0}", state.State.ToString()));
                if (NaniteConstructionManager.NaniteBlocks == null)
                    return;

                foreach (var item in NaniteConstructionManager.NaniteBlocks)
                {
                    if (item.Key == state.EntityId)
                        item.Value.SyncUpdateState(state);
                }
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine(string.Format("HandleUpdateState Error: {0}", ex.ToString()));
            }
        }

        // HandleAddTarget
        private void HandleAddTarget(byte[] data)
        {
            try
            {
                if (MyAPIGateway.Session == null)
                    return;

                if (NaniteConstructionManager.NaniteBlocks == null)
                    return;

                TargetData target = MyAPIGateway.Utilities.SerializeFromXML<TargetData>(ASCIIEncoding.ASCII.GetString(data));
                Logging.Instance.WriteLine(string.Format("HandleAddTarget: {0}", target.ToString()));
                foreach (var item in NaniteConstructionManager.NaniteBlocks)
                {
                    if (item.Key == target.EntityId)
                        item.Value.SyncAddTarget(target);
                }
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine(string.Format("HandleAddTarget Error: {0}", ex.ToString()));
            }
        }

        // HandleCompleteTarget
        private void HandleCompleteTarget(byte[] data)
        {
            try
            {
                if (MyAPIGateway.Session == null)
                    return;

                if (NaniteConstructionManager.NaniteBlocks == null)
                    return;

                TargetData target = MyAPIGateway.Utilities.SerializeFromXML<TargetData>(ASCIIEncoding.ASCII.GetString(data));
                foreach (var item in NaniteConstructionManager.NaniteBlocks)
                {
                    if (item.Key == target.EntityId)
                        item.Value.SyncCompleteTarget(target);
                }
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine(string.Format("HandleCompleteTarget Error: {0}", ex.ToString()));
            }
        }

        // HandleCancelTarget
        private void HandleCancelTarget(byte[] data)
        {
            try
            {
                if (MyAPIGateway.Session == null)
                    return;

                if (NaniteConstructionManager.NaniteBlocks == null)
                    return;

                TargetData target = MyAPIGateway.Utilities.SerializeFromXML<TargetData>(ASCIIEncoding.ASCII.GetString(data));
                foreach (var item in NaniteConstructionManager.NaniteBlocks)
                {
                    if (item.Key == target.EntityId)
                        item.Value.SyncCancelTarget(target);
                }
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine(string.Format("HandleCancelTarget Error: {0}", ex.ToString()));
            }
        }

        private void HandleDetails(byte[] data)
        {
            try
            {
                if (MyAPIGateway.Session == null)
                    return;

                if (NaniteConstructionManager.NaniteBlocks == null)
                    return;

                DetailData details = MyAPIGateway.Utilities.SerializeFromXML<DetailData>(ASCIIEncoding.ASCII.GetString(data));
                //Logging.Instance.WriteLine(string.Format("HandleDetails: {0}", details.EntityId));

                bool found = false;
                foreach (var item in NaniteConstructionManager.NaniteBlocks)
                {
                    if (item.Key == details.EntityId && item.Value.Initialized)
                    {
                        //Logging.Instance.WriteLine(string.Format("Details for Factory: {0}", details.EntityId));
                        item.Value.SyncDetails(details);
                        found = true;
                        break;
                    }
                }

                if(!found)
                {
                    foreach(var item in NaniteConstructionManager.MiningList)
                    {
                        if(item.MiningBlock.EntityId == details.EntityId)
                        {
                            //Logging.Instance.WriteLine(string.Format("Details for Hammer: {0}", details.EntityId));
                            item.SyncDetails(details);
                            found = true;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine(string.Format("HandleDetails() Error: {0}", ex.ToString()));
            }
        }

        public void SendLogin()
        {
            LoginData data = new LoginData();

            if (MyAPIGateway.Session.Player != null)
                data.SteamId = MyAPIGateway.Session.Player.SteamUserId;
            else
                data.SteamId = 0;

            MyAPIGateway.Multiplayer.SendMessageToServer(8955, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(data)));
        }

       /* private void HandleStartParticle(byte[] data)
        {
            try
            {
                if (MyAPIGateway.Session == null)
                    return;

                ParticleData target = MyAPIGateway.Utilities.SerializeFromXML<ParticleData>(ASCIIEncoding.ASCII.GetString(data));
                foreach (var item in NaniteConstructionManager.NaniteBlocks)
                {
                    if (item.Key == target.EntityId && item.Value.Initialized)
                        item.Value.SyncStartParticleEffect(target);
                }
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine(string.Format("HandleSettings() Error: {0}", ex.ToString()));
            }
        }

        private void HandleRemoveParticle(byte[] data)
        {
            try
            {
                if (MyAPIGateway.Session == null)
                    return;

                ParticleData target = MyAPIGateway.Utilities.SerializeFromXML<ParticleData>(ASCIIEncoding.ASCII.GetString(data));
                foreach (var item in NaniteConstructionManager.NaniteBlocks)
                {
                    if (item.Key == target.EntityId && item.Value.Initialized)
                        item.Value.SyncRemoveParticleEffect(target);
                }
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine(string.Format("HandleSettings() Error: {0}", ex.ToString()));
            }
        }
        */
        public void SendTerminalSettings(IMyTerminalBlock block)
        {
            SendTerminalSettings(block.EntityId);
        }

        public void SendTerminalSettings(long blockId)
        {
            if (NaniteConstructionManager.NaniteBlocks == null)
                return;

            if (!NaniteConstructionManager.NaniteBlocks.ContainsKey(blockId))
                return;

            if (!NaniteConstructionManager.TerminalSettings.ContainsKey(blockId))
                NaniteConstructionManager.TerminalSettings.Add(blockId, new NaniteTerminalSettings());

            SerializableKeyValuePair<long, NaniteTerminalSettings> settings = new SerializableKeyValuePair<long, NaniteTerminalSettings>(blockId, NaniteConstructionManager.TerminalSettings[blockId]);

            if (!Sync.IsServer)
            {
                //Logging.Instance.WriteLine("SendTerminalSettings -> Server");
                MyAPIGateway.Multiplayer.SendMessageToServer(8964, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(settings)));
            }
            else
            {
                //Logging.Instance.WriteLine("SendTerminalSettings -> Others");
                MyAPIGateway.Multiplayer.SendMessageToOthers(8960, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(settings)));
            }
        }

        private void HandleTerminalSettings(byte[] data)
        {
            //Logging.Instance.WriteLine("HandleTerminalSettings");

            try
            {
                if (MyAPIGateway.Session == null)
                    return;

                var settings = MyAPIGateway.Utilities.SerializeFromXML<SerializableKeyValuePair<long, NaniteTerminalSettings>>(ASCIIEncoding.ASCII.GetString(data));
                if (!NaniteConstructionManager.TerminalSettings.ContainsKey(settings.Key))
                    NaniteConstructionManager.TerminalSettings.Add(settings.Key, settings.Value);
                else
                    NaniteConstructionManager.TerminalSettings[settings.Key] = settings.Value;

                if (Sync.IsServer)
                {
                    SendTerminalSettings(settings.Key);
                }
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine(string.Format("HandleTerminalSettings() Error: {0}", ex.ToString()));
            }
        }

        public void SendAssemblerSettings(IMyTerminalBlock block)
        {
            SendAssemblerSettings(block.EntityId);
        }

        public void SendAssemblerSettings(long blockId)
        {
            if (!NaniteConstructionManager.AssemblerSettings.ContainsKey(blockId))
                NaniteConstructionManager.AssemblerSettings.Add(blockId, new NaniteAssemblerSettings());

            SerializableKeyValuePair<long, NaniteAssemblerSettings> settings = new SerializableKeyValuePair<long, NaniteAssemblerSettings>(blockId, NaniteConstructionManager.AssemblerSettings[blockId]);

            if (!Sync.IsServer)
            {
                //Logging.Instance.WriteLine("SendAssemblerSettings -> Server");
                MyAPIGateway.Multiplayer.SendMessageToServer(8965, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(settings)));
            }
            else
            {
                //Logging.Instance.WriteLine("SendAssemblerSettings -> Others");
                MyAPIGateway.Multiplayer.SendMessageToOthers(8962, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(settings)));
            }
        }

        private void HandleAssemblerSettings(byte[] data)
        {
            try
            {
                if (MyAPIGateway.Session == null)
                    return;

                var settings = MyAPIGateway.Utilities.SerializeFromXML<SerializableKeyValuePair<long, NaniteAssemblerSettings>>(ASCIIEncoding.ASCII.GetString(data));
                if (!NaniteConstructionManager.AssemblerSettings.ContainsKey(settings.Key))
                    NaniteConstructionManager.AssemblerSettings.Add(settings.Key, settings.Value);

                NaniteConstructionManager.AssemblerSettings[settings.Key] = settings.Value;
                if (Sync.IsServer)
                    SendAssemblerSettings(settings.Key);
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine(string.Format("HandleAssemblerSettings() Error: {0}", ex.ToString()));
            }
        }

        private void HandleNeedTerminalSettings(byte[] data)
        {
            try
            {
                if (MyAPIGateway.Session == null)
                    return;

                var settings = MyAPIGateway.Utilities.SerializeFromXML<long>(ASCIIEncoding.ASCII.GetString(data));
                foreach (var item in NaniteConstructionManager.NaniteBlocks)
                {
                    if (item.Key == settings)
                    {
                        SendTerminalSettings(settings);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine(string.Format("HandleNeedTerminalSettings() Error: {0}", ex.ToString()));
            }
        }

        public void SendNeedTerminalSettings(long blockId)
        {
            if (MyAPIGateway.Multiplayer == null)
                return;

            MyAPIGateway.Multiplayer.SendMessageToServer(8961, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(blockId)));
        }

        private void HandleNeedAssemblerSettings(byte[] data)
        {
            try
            {
                if (MyAPIGateway.Session == null)
                    return;

                var settings = MyAPIGateway.Utilities.SerializeFromXML<long>(ASCIIEncoding.ASCII.GetString(data));
                foreach (var item in NaniteConstructionManager.AssemblerBlocks)
                {
                    if (item.Key == settings)
                    {
                        SendAssemblerSettings(settings);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine(string.Format("HandleNeedAssemblerSettings() Error: {0}", ex.ToString()));
            }

        }

        public void SendNeedAssemblerSettings(long blockId)
        {
            if (MyAPIGateway.Multiplayer == null)
                return;

            //Logging.Instance.WriteLine(string.Format("Requesting Assembler Settings -> {0}", blockId));
            MyAPIGateway.Multiplayer.SendMessageToServer(8963, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(blockId)));
        }

        public void SendHammerTerminalSettings(long blockId)
        {
            if (!NaniteConstructionManager.HammerTerminalSettings.ContainsKey(blockId))
                NaniteConstructionManager.HammerTerminalSettings.Add(blockId, new NaniteHammerTerminalSettings());

            SerializableKeyValuePair<long, NaniteHammerTerminalSettings> settings = new SerializableKeyValuePair<long, NaniteHammerTerminalSettings>(blockId, NaniteConstructionManager.HammerTerminalSettings[blockId]);

            if (!Sync.IsServer)
            {
                //Logging.Instance.WriteLine("SendAssemblerSettings -> Server");
                MyAPIGateway.Multiplayer.SendMessageToServer(8967, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(settings)));
            }
            else
            {
                //Logging.Instance.WriteLine("SendAssemblerSettings -> Others");
                MyAPIGateway.Multiplayer.SendMessageToOthers(8966, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(settings)));
            }
        }

        private void HandleHammerTerminalSettings(byte[] data)
        {
            try
            {
                if (MyAPIGateway.Session == null)
                    return;

                var settings = MyAPIGateway.Utilities.SerializeFromXML<SerializableKeyValuePair<long, NaniteHammerTerminalSettings>>(ASCIIEncoding.ASCII.GetString(data));
                if (!NaniteConstructionManager.HammerTerminalSettings.ContainsKey(settings.Key))
                    NaniteConstructionManager.HammerTerminalSettings.Add(settings.Key, settings.Value);

                NaniteConstructionManager.HammerTerminalSettings[settings.Key] = settings.Value;

                IMyEntity entity;
                if(MyAPIGateway.Entities.TryGetEntityById(settings.Key, out entity))
                {
                    var block = entity as IMyTerminalBlock;
                    block.RefreshCustomInfo();
                }
                
                if (Sync.IsServer)
                    SendHammerTerminalSettings(settings.Key);
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine(string.Format("HandleHammerTerminalSettings() Error: {0}", ex.ToString()));
            }
        }

        public void SendNeedHammerTerminalSettings(long blockId)
        {
            if (MyAPIGateway.Multiplayer == null)
                return;

            //Logging.Instance.WriteLine(string.Format("Requesting Assembler Settings -> {0}", blockId));
            MyAPIGateway.Multiplayer.SendMessageToServer(8968, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(blockId)));
        }

        private void HandleNeedHammerTerminalSettings(byte[] data)
        {
            try
            {
                if (MyAPIGateway.Session == null)
                    return;

                var settings = MyAPIGateway.Utilities.SerializeFromXML<long>(ASCIIEncoding.ASCII.GetString(data));
                foreach (var item in NaniteConstructionManager.HammerTerminalSettings)
                {
                    if (item.Key == settings)
                    {
                        SendHammerTerminalSettings(item.Key);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine(string.Format("HandleNeedHammerTerminalSettings() Error: {0}", ex.ToString()));
            }
        }

        public void SendBeaconTerminalSettings(long blockId)
        {
            if (!NaniteConstructionManager.BeaconTerminalSettings.ContainsKey(blockId))
                NaniteConstructionManager.BeaconTerminalSettings.Add(blockId, new NaniteBeaconTerminalSettings());

            SerializableKeyValuePair<long, NaniteBeaconTerminalSettings> settings = new SerializableKeyValuePair<long, NaniteBeaconTerminalSettings>(blockId, NaniteConstructionManager.BeaconTerminalSettings[blockId]);

            if (!Sync.IsServer)
            {
                //Logging.Instance.WriteLine("SendAssemblerSettings -> Server");
                Logging.Instance.WriteLine("SendBeaconTerminalSettings -> Server: {blockId}");
                MyAPIGateway.Multiplayer.SendMessageToServer(8972, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(settings)));
            }
            else
            {
                //Logging.Instance.WriteLine("SendAssemblerSettings -> Others");
                Logging.Instance.WriteLine("SendBeaconTerminalSettings -> Client: {blockId}");
                MyAPIGateway.Multiplayer.SendMessageToOthers(8971, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(settings)));
            }
        }

        private void HandleBeaconTerminalSettings(byte[] data)
        {
            try
            {
                if (MyAPIGateway.Session == null)
                    return;

                var settings = MyAPIGateway.Utilities.SerializeFromXML<SerializableKeyValuePair<long, NaniteBeaconTerminalSettings>>(ASCIIEncoding.ASCII.GetString(data));
                if (!NaniteConstructionManager.BeaconTerminalSettings.ContainsKey(settings.Key))
                    NaniteConstructionManager.BeaconTerminalSettings.Add(settings.Key, settings.Value);

                NaniteConstructionManager.BeaconTerminalSettings[settings.Key] = settings.Value;
                Logging.Instance.WriteLine("Receieved Beacon Terminal Settings Update: {settings.Key}");

                if (Sync.IsServer)
                    SendBeaconTerminalSettings(settings.Key);
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine(string.Format("HandleBeaconTerminalSettings() Error: {0}", ex.ToString()));
            }
        }

        public void SendNeedBeaconTerminalSettings(long blockId)
        {
            if (MyAPIGateway.Multiplayer == null)
                return;

            Logging.Instance.WriteLine("SendNeedBeaconTerminalSettings: {blockId}");
            MyAPIGateway.Multiplayer.SendMessageToServer(8973, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(blockId)));
        }

        private void HandleNeedBeaconTerminalSettings(byte[] data)
        {
            try
            {
                if (MyAPIGateway.Session == null)
                    return;

                var settings = MyAPIGateway.Utilities.SerializeFromXML<long>(ASCIIEncoding.ASCII.GetString(data));
                Logging.Instance.WriteLine("HandleNeedBeaconTerminalSettings: {settings}");

                foreach (var item in NaniteConstructionManager.BeaconTerminalSettings)
                {
                    if (item.Key == settings)
                    {
                        Logging.Instance.WriteLine("SendBeaconTerminalSettings: {settings}");
                        SendBeaconTerminalSettings(item.Key);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine(string.Format("HandleNeedBeaconTerminalSettings() Error: {0}", ex.ToString()));
            }
        }


        private void HandleVoxelRemoval(byte[] data)
        {
            try
            {
                if (MyAPIGateway.Session == null)
                    return;

                var settings = MyAPIGateway.Utilities.SerializeFromXML<VoxelRemovalData>(ASCIIEncoding.ASCII.GetString(data));
                byte materialRemoved = 0;
                float amountOfMaterial = 0f;
                Beacons.NaniteMining.RemoveVoxelContent(settings.VoxelID, settings.Position, out materialRemoved, out amountOfMaterial);
            }
            catch(Exception ex)
            {
                MyLog.Default.WriteLine("HandleVoxelRemoval(): " + ex.ToString());
            }
        }
    }
}
