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
            if (Sync.IsClient)
            {
                MyAPIGateway.Multiplayer.RegisterMessageHandler(8950, HandleUpdateState);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(8951, HandleAddTarget);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(8952, HandleCompleteTarget);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(8953, HandleCancelTarget);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(8954, HandleDetails);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(8960, HandleTerminalSettings);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(8962, HandleAssemblerSettings);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(8974, HandleFactoryGroup);
            }
            else if (Sync.IsServer)
            {
                MyAPIGateway.Multiplayer.RegisterMessageHandler(8961, HandleNeedTerminalSettings);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(8963, HandleNeedAssemblerSettings);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(8964, HandleTerminalSettings);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(8965, HandleAssemblerSettings);
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
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(8960, HandleTerminalSettings);
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(8962, HandleAssemblerSettings);
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(8961, HandleNeedTerminalSettings);
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(8963, HandleNeedAssemblerSettings);
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(8964, HandleTerminalSettings);
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(8965, HandleAssemblerSettings);
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
                Logging.Instance.WriteLine(string.Format("UpdateState: {0}", state.State.ToString()), 1);
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
                Logging.Instance.WriteLine(string.Format("HandleUpdateState Error: {0}", ex.ToString()));
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
                Logging.Instance.WriteLine(string.Format("HandleAddTarget: {0}", target.ToString()), 1);
                foreach (var item in NaniteConstructionManager.NaniteBlocks)
                {
                    if (item.Key == target.EntityId)
                        item.Value.SyncAddTarget(target);
                }
            }
            catch (Exception ex)
            {
                Logging.Instance.WriteLine(string.Format("HandleAddTarget Error: {0}", ex.ToString()));
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
                Logging.Instance.WriteLine(string.Format("HandleCompleteTarget Error: {0}", ex.ToString()));
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
                Logging.Instance.WriteLine(string.Format("HandleCancelTarget Error: {0}", ex.ToString()));
            }
        }

        private void HandleDetails(byte[] data)
        {
            MyAPIGateway.Parallel.Start(() => {
                try {
                    if (MyAPIGateway.Session == null)
                        return;

                    if (NaniteConstructionManager.NaniteBlocks == null)
                        return;

                    DetailData details = MyAPIGateway.Utilities.SerializeFromXML<DetailData>(ASCIIEncoding.ASCII.GetString(data));

                    if (details == null)
                        return;
                    //Logging.Instance.WriteLine(string.Format("HandleDetails: {0}", details.EntityId));

                    foreach (var item in NaniteConstructionManager.NaniteBlocks)
                        if (item.Key == details.EntityId && item.Value.Initialized)
                        {
                            //Logging.Instance.WriteLine(string.Format("Details for Factory: {0}", details.EntityId));
                            item.Value.SyncDetails(details);
                            break;
                        }
                } catch (Exception ex) {
                    Logging.Instance.WriteLine(string.Format("HandleDetails() Error: {0}", ex.ToString()));
                }
            });
        }

        private void HandleFactoryGroup(byte[] data)
        {
            MyAPIGateway.Parallel.Start(() => {
                try {
                    if (MyAPIGateway.Session == null)
                        return;

                    if (NaniteConstructionManager.NaniteBlocks == null)
                        return;

                    FactoryGroupData details = MyAPIGateway.Utilities.SerializeFromXML<FactoryGroupData>(ASCIIEncoding.ASCII.GetString(data));

                    foreach (var item in NaniteConstructionManager.NaniteBlocks) {
                        if (item.Key == details.EntityId && item.Value.Initialized) {
                            item.Value.SyncFactoryGroup(details);
                            break;
                        }
                    }
                } catch (Exception ex) {
                    Logging.Instance.WriteLine(string.Format("HandleFactoryGroup() Error: {0}", ex.ToString()));
                }
            });
        }

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

            if (Sync.IsClient)
            {
                Logging.Instance.WriteLine("SendTerminalSettings -> Server", 2);
                MyAPIGateway.Multiplayer.SendMessageToServer(8964, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(settings)));
            }
            else if (Sync.IsServer)
            {
                Logging.Instance.WriteLine("SendTerminalSettings -> Others", 2);
                MyAPIGateway.Multiplayer.SendMessageToOthers(8960, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(settings)));
            }
        }

        private void HandleTerminalSettings(byte[] data)
        {
            Logging.Instance.WriteLine("HandleTerminalSettings", 2);

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
                    SendTerminalSettings(settings.Key);
            }
            catch (Exception ex)
            {
                Logging.Instance.WriteLine(string.Format("HandleTerminalSettings() Error: {0}", ex.ToString()));
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

            if (Sync.IsClient)
                MyAPIGateway.Multiplayer.SendMessageToServer(8965, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(settings)));
            else if (Sync.IsServer)
                MyAPIGateway.Multiplayer.SendMessageToOthers(8962, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(settings)));
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
                Logging.Instance.WriteLine(string.Format("HandleAssemblerSettings() Error: {0}", ex.ToString()));
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
                Logging.Instance.WriteLine(string.Format("HandleNeedTerminalSettings() Error: {0}", ex.ToString()));
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
                Logging.Instance.WriteLine(string.Format("HandleNeedAssemblerSettings() Error: {0}", ex.ToString()));
            }

        }

        public void SendNeedAssemblerSettings(long blockId)
        {
            if (MyAPIGateway.Multiplayer == null)
                return;

            Logging.Instance.WriteLine($"Requesting Assembler Settings -> {blockId}", 2);
            MyAPIGateway.Multiplayer.SendMessageToServer(8963, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(blockId)));
        }

    }
}
