using ProtoBuf;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using static NaniteConstructionSystem.Entities.Detectors.NaniteOreDetector;
using NaniteConstructionSystem.Extensions;

namespace NaniteConstructionSystem
{
    public enum MessageSide
    {
        ServerSide,
        ClientSide
    }

    class MessageHub
    {
        public static List<byte> Client_MessageCache = new List<byte>();
        public static Dictionary<ulong, List<byte>> Server_MessageCache = new Dictionary<ulong, List<byte>>();

        public static readonly ushort MessageId = 8956;

        public static void SendMessageToServer(MessageBase message)
        {
            message.Side = MessageSide.ServerSide;
            if (MyAPIGateway.Session.Player != null)
                message.SenderSteamId = MyAPIGateway.Session.Player.SteamUserId;
            var byteData = MyAPIGateway.Utilities.SerializeToBinary<MessageBase>(message);
            Logging.Instance.WriteLine(string.Format("SendMessageToServer {0} {1} {2}, {3}b", message.SenderSteamId, message.Side, message.GetType().Name, byteData.Length), 1);
            MyAPIGateway.Multiplayer.SendMessageToServer(MessageId, byteData);
        }

        /// <summary>
        /// Sends a message to all players within sync range of a given position
        /// </summary>
        /// <param name="content"></param>
        public static void SendToPlayerInSyncRange(MessageBase messageContainer, Vector3D syncPosition)
        {
            MyAPIGateway.Parallel.Start(() =>
            {
                var distSq = MyAPIGateway.Session.SessionSettings.SyncDistance;
                distSq += 1000; // some safety padding, avoid desync
                distSq *= distSq;

                var players = new List<IMyPlayer>();
                MyAPIGateway.Players.GetPlayers(players);

                foreach (var p in players.ToList())
                    if (p != null && p.SteamUserId != MyAPIGateway.Multiplayer.MyId && Vector3D.DistanceSquared(p.GetPosition(), syncPosition) <= distSq)
                        MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                            {SendMessageToPlayer(p.SteamUserId, messageContainer);});
            });
        }

        /// <summary>
        /// Creates and sends an entity with the given information for the server and all players.
        /// </summary>
        /// <param name="content"></param>
        public static void SendMessageToAll(MessageBase message, bool syncAll = true)
        {
            if (MyAPIGateway.Session.Player != null)
                message.SenderSteamId = MyAPIGateway.Session.Player.SteamUserId;

            if (syncAll || !MyAPIGateway.Multiplayer.IsServer)
                SendMessageToServer(message);
            SendMessageToAllPlayers(message);
        }

        public static void SendMessageToAllPlayers(MessageBase messageContainer)
        {
            //MyAPIGateway.Multiplayer.SendMessageToOthers(StandardClientId, System.Text.Encoding.Unicode.GetBytes(ConvertData(content))); <- does not work as expected ... so it doesn't work at all?
            List<IMyPlayer> players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players, p => p != null && !MyAPIGateway.Multiplayer.IsServerPlayer(p.Client));
            foreach (IMyPlayer player in players)
                SendMessageToPlayer(player.SteamUserId, messageContainer);
        }

        public static void SendMessageToAllOtherPlayers(ulong ownSteamId, MessageBase messageContainer)
        {
            //MyAPIGateway.Multiplayer.SendMessageToOthers(StandardClientId, System.Text.Encoding.Unicode.GetBytes(ConvertData(content))); <- does not work as expected ... so it doesn't work at all?
            List<IMyPlayer> players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players, p => p != null && !MyAPIGateway.Multiplayer.IsServerPlayer(p.Client));
            foreach (IMyPlayer player in players)
            {
                if (player.SteamUserId == ownSteamId)
                    continue;

                SendMessageToPlayer(player.SteamUserId, messageContainer);
            }
        }

        public static void SendMessageToPlayer(ulong steamId, MessageBase message)
        {
            message.Side = MessageSide.ClientSide;
            var byteData = MyAPIGateway.Utilities.SerializeToBinary(message);

            Logging.Instance.WriteLine(string.Format("SendMessageToPlayer {0} {1} {2}, {3}b", steamId, message.Side, message.GetType().Name, byteData.Length), 1);

            MyAPIGateway.Multiplayer.SendMessageTo(MessageId, byteData, steamId);
        }

        public static void HandleMessage(ushort handlerId, byte[] data, ulong steamId, bool isServer )
        {
            try
            {
                var message = MyAPIGateway.Utilities.SerializeFromBinary<MessageBase>(data);

                Logging.Instance.WriteLine("HandleMessage()", 1);
                if (message != null)
                {
                    Logging.Instance.WriteLine(string.Format("HandleMessage() {0} {1} {2}, {3}b", message.SenderSteamId, message.Side, message.GetType().Name, data.Length), 1);
                    message.InvokeProcessing();
                }
                return;
            }
            catch (Exception e)
            {
                // Don't warn the user of an exception, this can happen if two mods with the same message id receive an unknown message
                Logging.Instance.WriteLine(string.Format("Processing message exception. Exception: {0}", e.ToString()));
                //Logger.Instance.LogException(e);
            }

        }
    }

    [ProtoContract]
    [ProtoInclude(5001, typeof(MessageClientConnected))]
    [ProtoInclude(5002, typeof(MessageConfig))]
    [ProtoInclude(5003, typeof(MessageOreDetectorSettings))]
    [ProtoInclude(5004, typeof(MessageOreDetectorStateChange))]
    [ProtoInclude(5005, typeof(MessageOreDetectorScanProgress))]
    [ProtoInclude(5006, typeof(MessageOreDetectorScanComplete))]
    public abstract class MessageBase
    {
        /// <summary>
        /// The SteamId of the message's sender. Note that this will be set when the message is sent, so there is no need for setting it otherwise.
        /// </summary>
        [ProtoMember(1)]
        public ulong SenderSteamId;

        /// <summary>
        /// Defines on which side the message should be processed. Note that this will be set when the message is sent, so there is no need for setting it otherwise.
        /// </summary>
        [ProtoMember(2, IsRequired = true)]
        public MessageSide Side = MessageSide.ClientSide;

        /// <summary>
        /// Name of mod. Used to determine if message belongs to us.
        /// </summary>
        [ProtoMember(3)]
        public string ModName = "Foo";

        public void InvokeProcessing()
        {
            if (ModName != "Foo")
            {
                Logging.Instance.WriteLine("Message came from another mod (" + ModName + "), ignored.");
                return;
            }

            switch (Side)
            {
                case MessageSide.ClientSide:
                    InvokeClientProcessing();
                    break;
                case MessageSide.ServerSide:
                    InvokeServerProcessing();
                    break;
            }
        }

        private void InvokeClientProcessing()
        {
            Logging.Instance.WriteLine(string.Format("START - Processing [Client] {0}", this.GetType().Name), 1);
            try
            {
                ProcessClient();
            }
            catch (Exception ex)
            {
                Logging.Instance.WriteLine(ex.ToString());
            }
            Logging.Instance.WriteLine(string.Format("END - Processing [Client] {0}", this.GetType().Name), 1);
        }

        private void InvokeServerProcessing()
        {
            Logging.Instance.WriteLine(string.Format("START - Processing [Server] {0}", this.GetType().Name), 1);

            try
            {
                ProcessServer();
            }
            catch (Exception ex)
            {
                Logging.Instance.WriteLine(ex.ToString());
            }

            Logging.Instance.WriteLine(string.Format("END - Processing [Server] {0}", this.GetType().Name), 1);
        }

        public abstract void ProcessClient();
        public abstract void ProcessServer();
    }

    [ProtoContract]
    public class MessageConfig : MessageBase
    {
        [ProtoMember(10)]
        public Settings.NaniteSettings Settings;

        public override void ProcessClient()
        {
            NaniteConstructionManager.Settings = Settings;
            Logging.Instance.WriteLine(string.Format("Received Settings Data - {0}x {1}x", NaniteConstructionManager.Settings.FactoryComponentMultiplier, NaniteConstructionManager.Settings.UpgradeComponentMultiplier), 1);

            foreach (var item in NaniteConstructionManager.NaniteBlocks)
            {
                IMySlimBlock slimBlock = ((MyCubeBlock)item.Value.ConstructionBlock).SlimBlock as IMySlimBlock;
                Logging.Instance.WriteLine(string.Format("Here: {0} / {1}", slimBlock.BuildIntegrity, slimBlock.MaxIntegrity), 1);
            }

            var def = MyDefinitionManager.Static.GetCubeBlockDefinition(new MyDefinitionId(typeof(MyObjectBuilder_ShipWelder), "LargeNaniteControlFacility"));
            foreach (var item in def.Components)
            {
                item.Count = (int)((float)item.Count * NaniteConstructionManager.Settings.FactoryComponentMultiplier);
                if (item.Count < 1)
                    item.Count = 1;
            }

            var def2 = MyDefinitionManager.Static.GetCubeBlockDefinition(new MyDefinitionId(typeof(MyObjectBuilder_ShipWelder), "SmallNaniteControlFacility"));
            foreach (var item in def2.Components)
            {
                item.Count = (int)((float)item.Count * NaniteConstructionManager.Settings.FactoryComponentMultiplier);
                if (item.Count < 1)
                    item.Count = 1;
            }

            foreach (var item in NaniteConstructionManager.NaniteBlocks)
            {
                IMySlimBlock slimBlock = ((MyCubeBlock)item.Value.ConstructionBlock).SlimBlock as IMySlimBlock;
                Logging.Instance.WriteLine(string.Format("Here: {0} / {1}", slimBlock.BuildIntegrity, slimBlock.MaxIntegrity), 1);
            }

            NaniteConstructionManager.Instance.UpdateSettingsChanges();

            if (Sync.IsClient && !Sync.IsServer)
                NaniteConstructionManager.Instance.InitializeControls();
        }

        public override void ProcessServer()
        {
        }
    }

    [ProtoContract]
    public class MessageClientConnected : MessageBase
    {
        public override void ProcessClient()
        {
        }

        public override void ProcessServer()
        {
            Logging.Instance.WriteLine(string.Format("Sending config to new client: {0}", SenderSteamId), 1);
            // Send new clients the configuration
            MessageHub.SendMessageToPlayer(SenderSteamId, new MessageConfig() { Settings = NaniteConstructionManager.Settings });
        }
    }

    [ProtoContract]
    public class MessageOreDetectorSettings : MessageBase
    {
        [ProtoMember(10)]
        public long EntityId;

        [ProtoMember(11)]
        public Entities.Detectors.ProtoNaniteOreDetectorSettings Settings;

        public override void ProcessClient()
        {
            IMyEntity ent;
            if (!MyAPIGateway.Entities.TryGetEntityById(EntityId, out ent) || ent.Closed)
                return;

            var logic = ent.GameLogic.GetAs<Entities.Detectors.LargeNaniteOreDetectorLogic>();
            if (logic == null)
                return;

            logic.Detector.Settings.Settings = Settings;
        }

        public override void ProcessServer()
        {
            IMyEntity ent;
            if (!MyAPIGateway.Entities.TryGetEntityById(EntityId, out ent) || ent.Closed)
                return;

            var logic = ent.GameLogic.GetAs<Entities.Detectors.LargeNaniteOreDetectorLogic>();
            if (logic == null)
                return;


            if (Settings == null)
            {
                // Client request settings{
                Logging.Instance.WriteLine(string.Format("Sending ore detector settings to client: {0}", SenderSteamId), 1);
                MessageHub.SendMessageToPlayer(SenderSteamId, new MessageOreDetectorSettings()
                {
                    EntityId = ent.EntityId,
                    Settings = logic.Detector.Settings.Settings
                });
                // Send state update as well
                MessageHub.SendMessageToPlayer(SenderSteamId, new MessageOreDetectorStateChange()
                {
                    EntityId = ent.EntityId,
                    State = logic.Detector.m_detectorState
                });
            }
            else
            {
                // Client update Settings
                logic.Detector.Settings.Settings = Settings;
                Logging.Instance.WriteLine(string.Format("Sending ore detector settings to other client: {0}", SenderSteamId), 1);
                MessageHub.SendMessageToAllOtherPlayers(SenderSteamId, new MessageOreDetectorSettings()
                {
                    EntityId = ent.EntityId,
                    Settings = logic.Detector.Settings.Settings
                });
            }
        }
    }

    [ProtoContract]
    public class MessageOreDetectorStateChange : MessageBase
    {
        [ProtoMember(10)]
        public long EntityId;

        [ProtoMember(11)]
        public DetectorStates State;

        [ProtoMember(12)]
        public bool TooClose;

        public override void ProcessClient()
        {
            IMyEntity ent;
            if (!MyAPIGateway.Entities.TryGetEntityById(EntityId, out ent) || ent.Closed)
                return;

            var logic = ent.GameLogic.GetAs<Entities.Detectors.LargeNaniteOreDetectorLogic>();
            if (logic == null)
                return;

            logic.Detector.m_detectorState = State;
            logic.Detector.m_tooCloseToOtherDetector = TooClose;
        }

        public override void ProcessServer()
        {
        }
    }

    [ProtoContract]
    public class MessageOreDetectorScanProgress : MessageBase
    {
        [ProtoMember(10)]
        public long EntityId;

        [ProtoMember(11)]
        public float Progress;

        public override void ProcessClient()
        {
            IMyEntity ent;
            if (!MyAPIGateway.Entities.TryGetEntityById(EntityId, out ent) || ent.Closed)
                return;

            var logic = ent.GameLogic.GetAs<Entities.Detectors.LargeNaniteOreDetectorLogic>();
            if (logic == null) return;

            logic.Detector.m_scanProgress = Progress;
        }

        public override void ProcessServer()
        {
        }
    }

    [ProtoContract]
    public class MessageOreDetectorScanComplete : MessageBase
    {
        [ProtoMember(10)]
        public long EntityId;

        [ProtoMember(11)]
        public string OreListCache;

        public override void ProcessClient()
        {
            IMyEntity ent;
            if (!MyAPIGateway.Entities.TryGetEntityById(EntityId, out ent) || ent.Closed)
                return;

            var logic = ent.GameLogic.GetAs<Entities.Detectors.LargeNaniteOreDetectorLogic>();
            if (logic == null) return;

            logic.Detector.OreListCache = new StringBuilder(OreListCache);
        }

        public override void ProcessServer()
        {
        }
    }
}
