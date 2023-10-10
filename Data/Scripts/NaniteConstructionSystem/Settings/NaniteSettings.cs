using System;
using ProtoBuf;
using Sandbox.ModAPI;

namespace NaniteConstructionSystem.Settings
{
    [ProtoContract]
    public class NaniteSettings
    {
		static public string SettingsFile = "Config.xml";

        [ProtoMember(1)]
        public bool ConstructionEnabled { get; set; }
        [ProtoMember(2)]
        public int ConstructionMaxStreams { get; set; }
        [ProtoMember(3)]
        public float ConstructionEfficiency { get; set; }
        [ProtoMember(4)]
        public float ConstructionPowerPerStream { get; set; }
        [ProtoMember(5)]
        public float ConstructionMinTravelTime { get; set; }
        [ProtoMember(6)]
        public float ConstructionDistanceDivisor { get; set; }
        [ProtoMember(7)]
        public int ConstructionNanitesPerUpgrade { get; set; }
        [ProtoMember(8)]
        public int ConstructionNanitesNoUpgrade { get; set; }
        [ProtoMember(9)]
        public float ConstructionMaxBeaconDistance { get; set; }
        [ProtoMember(10)]
        public bool ProjectionEnabled { get; set; }
        [ProtoMember(11)]
        public int ProjectionMaxStreams { get; set; }
        [ProtoMember(12)]
        public float ProjectionPowerPerStream { get; set; }
        [ProtoMember(13)]
        public float ProjectionMinTravelTime { get; set; }
        [ProtoMember(14)]
        public float ProjectionDistanceDivisor { get; set; }
        [ProtoMember(15)]
        public int ProjectionNanitesPerUpgrade { get; set; }
        [ProtoMember(16)]
        public int ProjectionNanitesNoUpgrade { get; set; }
        [ProtoMember(17)]
        public float ProjectionMaxBeaconDistance { get; set; }
        [ProtoMember(18)]
        public bool CleanupEnabled { get; set; }
        [ProtoMember(19)]
        public int CleanupMaxStreams { get; set; }
        [ProtoMember(20)]
        public float CleanupCarryVolume { get; set; }
        [ProtoMember(21)]
        public float CleanupMaxDistance { get; set; }
        [ProtoMember(22)]
        public float CleanupPowerPerStream { get; set; }
        [ProtoMember(23)]
        public float CleanupMinTravelTime { get; set; }
        [ProtoMember(24)]
        public float CleanupDistanceDivisor { get; set; }
        [ProtoMember(25)]
        public int CleanupNanitesPerUpgrade { get; set; }
        [ProtoMember(26)]
        public int CleanupNanitesNoUpgrade { get; set; }
        [ProtoMember(27)]
        public bool DeconstructionEnabled { get; set; }
        [ProtoMember(28)]
        public int DeconstructionMaxStreams { get; set; }
        [ProtoMember(29)]
        public float DeconstructionMaxDistance { get; set; }
        [ProtoMember(30)]
        public float DeconstructionEfficiency { get; set; }
        [ProtoMember(31)]
        public float DeconstructionPowerPerStream { get; set; }
        [ProtoMember(32)]
        public float DeconstructionMinTravelTime { get; set; }
        [ProtoMember(33)]
        public float DeconstructionDistanceDivisor { get; set; }
        [ProtoMember(34)]
        public bool DeconstructionPerformanceFriendly { get; set; }
        [ProtoMember(35)]
        public int DeconstructionNanitesPerUpgrade { get; set; }
        [ProtoMember(36)]
        public int DeconstructionNanitesNoUpgrade { get; set; }
        [ProtoMember(37)]
        public bool MiningEnabled { get; set; }
        [ProtoMember(38)]
        public int MiningMaxStreams { get; set; }
        [ProtoMember(39)]
        public float MiningMaxDistance { get; set; }
        [ProtoMember(40)]
        public float MiningPowerPerStream { get; set; }
        [ProtoMember(41)]
        public float MiningMinTravelTime { get; set; }
        [ProtoMember(42)]
        public float MiningDistanceDivisor { get; set; }
        [ProtoMember(43)]
        public int MiningNanitesPerUpgrade { get; set; }
        [ProtoMember(44)]
        public int MiningNanitesNoUpgrade { get; set; }
        [ProtoMember(47)]
        public bool LifeSupportEnabled { get; set; }
        [ProtoMember(48)]
        public int LifeSupportMaxStreams { get; set; }
        [ProtoMember(49)]
        public float LifeSupportMaxDistance { get; set; }
        [ProtoMember(50)]
        public float LifeSupportPowerPerStream { get; set; }
        [ProtoMember(51)]
        public float LifeSupportMinTravelTime { get; set; }
        [ProtoMember(52)]
        public float LifeSupportDistanceDivisor { get; set; }
        [ProtoMember(53)]
        public int LifeSupportNanitesPerUpgrade { get; set; }
        [ProtoMember(54)]
        public int LifeSupportNanitesNoUpgrade { get; set; }
        [ProtoMember(55)]
        public int LifeSupportSecondsPerTick { get; set; }
        [ProtoMember(56)]
        public float LifeSupportHealthPerTick { get; set; }
        [ProtoMember(59)]
        public float SpeedIncreasePerUpgrade { get; set; }
        [ProtoMember(60)]
        public float MinTravelTimeReductionPerUpgrade { get; set; }
        [ProtoMember(61)]
        public float PowerDecreasePerUpgrade { get; set; }
        [ProtoMember(62)]
        public float FactoryComponentMultiplier { get; set; }
        [ProtoMember(63)]
        public float UpgradeComponentMultiplier { get; set; }
        [ProtoMember(65)]
        public int DebugLogging { get; set; }
        [ProtoMember(66)]
        public float MasterSlaveDistance { get; set; }
        [ProtoMember(74)]
        public int BlocksScannedPerSecond { get; set; }
        [ProtoMember(77)]
        public float LifeSupportOxygenPerTick { get; set; }
        [ProtoMember(78)]
        public float LifeSupportHydrogenPerTick { get; set; }
        [ProtoMember(79)]
        public float LifeSupportOxygenRefillLevel { get; set; }
        [ProtoMember(80)]
        public float LifeSupportHydrogenRefillLevel { get; set; }
        [ProtoMember(81)]
        public float LifeSupportEnergyRefillLevel { get; set; }
        [ProtoMember(82)]
        public float LifeSupportEnergyPerTick { get; set; }

        public string Version { get; set; }

        public NaniteSettings()
        {
            ConstructionEnabled = true;
            ConstructionMaxStreams = 15;
            ConstructionEfficiency = 0.5f;
            ConstructionPowerPerStream = 26f;
            ConstructionMinTravelTime = 16f;
            ConstructionDistanceDivisor = 5f;
            ConstructionNanitesPerUpgrade = 3;
            ConstructionNanitesNoUpgrade = 3;
            ConstructionMaxBeaconDistance = 800;
            ProjectionEnabled = true;
            ProjectionMaxStreams = 15;
            ProjectionPowerPerStream = 26f;
            ProjectionMinTravelTime = 16f;
            ProjectionDistanceDivisor = 5f;
            ProjectionNanitesPerUpgrade = 3;
            ProjectionNanitesNoUpgrade = 1;
            ProjectionMaxBeaconDistance = 800;
            CleanupEnabled = true;
            CleanupMaxStreams = 15;
            CleanupCarryVolume = 2.5f;
            CleanupMaxDistance = 800f;
            CleanupPowerPerStream = 13f;
            CleanupMinTravelTime = 14f;
            CleanupDistanceDivisor = 15f;
            CleanupNanitesPerUpgrade = 3;
            CleanupNanitesNoUpgrade = 1;
            DeconstructionEnabled = true;
            DeconstructionMaxStreams = 15;
            DeconstructionMaxDistance = 800f;
            DeconstructionEfficiency = 0.5f;
            DeconstructionPowerPerStream = 26f;
            DeconstructionMinTravelTime = 16f;
            DeconstructionDistanceDivisor = 5f;
            DeconstructionNanitesPerUpgrade = 3;
            DeconstructionNanitesNoUpgrade = 1;
            DeconstructionPerformanceFriendly = true;
            MiningEnabled = true;
            MiningMaxStreams = 15;
            MiningMaxDistance = 1000f;
            MiningPowerPerStream = 13f;
            MiningMinTravelTime = 10f;
            MiningDistanceDivisor = 15f;
            MiningNanitesPerUpgrade = 3;
            MiningNanitesNoUpgrade = 1;
            LifeSupportEnabled = true;
            LifeSupportMaxStreams = 15;
            LifeSupportMaxDistance = 800f;
            LifeSupportPowerPerStream = 13f;
            LifeSupportMinTravelTime = 16f;
            LifeSupportDistanceDivisor = 5f;
            LifeSupportNanitesPerUpgrade = 3;
            LifeSupportNanitesNoUpgrade = 1;
            LifeSupportSecondsPerTick = 1;
            LifeSupportHealthPerTick = 5f;
            SpeedIncreasePerUpgrade = 5f;
            MinTravelTimeReductionPerUpgrade = 1f;
            PowerDecreasePerUpgrade = 2f;
            FactoryComponentMultiplier = 1f;
            UpgradeComponentMultiplier = 1f;
            DebugLogging = 0;
            MasterSlaveDistance = 300f;
            BlocksScannedPerSecond = 500;
            LifeSupportOxygenPerTick = 0.05f;
            LifeSupportHydrogenPerTick = 0.05f;
            LifeSupportOxygenRefillLevel = 0.5f;
            LifeSupportHydrogenRefillLevel = 0.5f;
            LifeSupportEnergyRefillLevel = 0.5f;
            LifeSupportEnergyPerTick = 0.05f;
            Version = "2.1";
        }

        public static NaniteSettings Load()
        {
			if (MyAPIGateway.Utilities.FileExistsInWorldStorage(SettingsFile, typeof(NaniteSettings)))
            {
                try
                {
                    Logging.Instance.WriteLine("Loading Settings");
                    NaniteSettings settings;
                    using (var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(SettingsFile, typeof(NaniteSettings)))
                        settings = MyAPIGateway.Utilities.SerializeFromXML<NaniteSettings>(reader.ReadToEnd());
                    try
                    {
                        UpdateSettings(settings);
                        Save(settings);
                    }
                    catch { }

                    return settings;
                }
                catch (Exception ex)
                {
                    Logging.Instance.WriteLine(string.Format("Error loading settings file.  Using defaults. ({0})", ex.ToString()));
                }
            }

            NaniteSettings result = new NaniteSettings();
            Save(result);
            return result;
        }

        private static void UpdateSettings(NaniteSettings settings)
        {
            //var originalVersion = settings.Version;

            //TODO config migration

            //if (settings.Version != originalVersion)
            //    SendNotification();
        }

        public static void Save(NaniteSettings settings){
            using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(SettingsFile, typeof(NaniteSettings)))
                writer.Write(MyAPIGateway.Utilities.SerializeToXML<NaniteSettings>(settings));
        }

   //     private static void SendNotification()
   //     {
   //         if (MyAPIGateway.Utilities != null)
   //         {
   //             MyAPIGateway.Utilities.ShowMessage("[Nanite Control Factory]", "Nanite control factory has been updated.  Type /nanite to see what's new.");
   //             MyAPIGateway.Utilities.ShowNotification("Nanite control factory has been updated.  Type /nanite to see what's new.", 20000, VRage.Game.MyFontEnum.Green);
			
   //         }
			//if(MyAPIGateway.Utilities == null) return;
   //     }
    }
}
