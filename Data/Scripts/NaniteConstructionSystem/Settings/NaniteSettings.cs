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
        [ProtoMember(45)]
        public float MiningRadius { get; set; }
        [ProtoMember(46)]
        public int MiningDepth { get; set; }
        [ProtoMember(47)]
        public bool MedicalEnabled { get; set; }
        [ProtoMember(48)]
        public int MedicalMaxStreams { get; set; }
        [ProtoMember(49)]
        public float MedicalMaxDistance { get; set; }
        [ProtoMember(50)]
        public float MedicalPowerPerStream { get; set; }
        [ProtoMember(51)]
        public float MedicalMinTravelTime { get; set; }
        [ProtoMember(52)]
        public float MedicalDistanceDivisor { get; set; }
        [ProtoMember(53)]
        public int MedicalNanitesPerUpgrade { get; set; }
        [ProtoMember(54)]
        public int MedicalNanitesNoUpgrade { get; set; }
        [ProtoMember(55)]
        public int MedicalSecondsPerHealTick { get; set; }
        [ProtoMember(56)]
        public float MedicalHealthPerHealTick { get; set; }
        [ProtoMember(57)]
        public float AreaBeaconMaxSize { get; set; }
        [ProtoMember(58)]
        public float AreaBeaconMinSize { get; set; }
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
        [ProtoMember(64)]
        public float OreDetectorPowerMultiplicator { get; set; }
        [ProtoMember(65)]
        public int DebugLogging { get; set; }
        [ProtoMember(66)]
        public float MasterSlaveDistance { get; set; }
        [ProtoMember(67)]
        public float OreDetectorToNaniteFactoryCommunicationDistance { get; set; }
        [ProtoMember(68)]
        public float OreDetectorRangePerUpgrade { get; set; }
        [ProtoMember(69)]
        public float OreDetectorPowerIncreasePerRangeUpgrade { get; set; }
        [ProtoMember(70)]
        public float OreDetectorPowerIncreasePerFilterUpgrade { get; set; }
        [ProtoMember(71)]
        public float OreDetectorPowerPercentIncreasedPerScanningUpgrade { get; set; }
        [ProtoMember(72)]
        public float OreDetectorPowerPercentReducedPerEfficiencyUpgrade { get; set; }
        [ProtoMember(73)]
        public float AreaBeaconMaxDistanceFromNaniteFacility { get; set; }        

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
            ConstructionMaxBeaconDistance = 300;
            ProjectionEnabled = true;
            ProjectionMaxStreams = 15;
            ProjectionPowerPerStream = 26f;
            ProjectionMinTravelTime = 16f;
            ProjectionDistanceDivisor = 5f;
            ProjectionNanitesPerUpgrade = 3;
            ProjectionNanitesNoUpgrade = 1;
            ProjectionMaxBeaconDistance = 300;
            CleanupEnabled = true;
            CleanupMaxStreams = 15;
            CleanupCarryVolume = 2.5f;
            CleanupMaxDistance = 300f;
            CleanupPowerPerStream = 26f;
            CleanupMinTravelTime = 14f;
            CleanupDistanceDivisor = 15f;
            CleanupNanitesPerUpgrade = 3;
            CleanupNanitesNoUpgrade = 1;
            DeconstructionEnabled = true;
            DeconstructionMaxStreams = 15;
            DeconstructionMaxDistance = 300f;
            DeconstructionEfficiency = 0.5f;
            DeconstructionPowerPerStream = 26f;
            DeconstructionMinTravelTime = 16f;
            DeconstructionDistanceDivisor = 5f;
            DeconstructionNanitesPerUpgrade = 3;
            DeconstructionNanitesNoUpgrade = 1;
            DeconstructionPerformanceFriendly = true;
            MiningEnabled = true;
            MiningMaxStreams = 15;
            MiningMaxDistance = 500f;
            MiningPowerPerStream = 26f;
            MiningMinTravelTime = 25f;
            MiningDistanceDivisor = 15f;
            MiningNanitesPerUpgrade = 3;
            MiningNanitesNoUpgrade = 1;
            MedicalEnabled = true;
            MedicalMaxStreams = 15;
            MedicalMaxDistance = 300f;
            MedicalPowerPerStream = 26f;
            MedicalMinTravelTime = 16f;
            MedicalDistanceDivisor = 5f;
            MedicalNanitesPerUpgrade = 3;
            MedicalNanitesNoUpgrade = 1;
            MedicalSecondsPerHealTick = 4;
            MedicalHealthPerHealTick = 5f;
            AreaBeaconMaxSize = 200f;
            AreaBeaconMinSize = 10f;
            SpeedIncreasePerUpgrade = 5f;
            MinTravelTimeReductionPerUpgrade = 1f;
            PowerDecreasePerUpgrade = 2f;
            FactoryComponentMultiplier = 1f;
            UpgradeComponentMultiplier = 1f;
            OreDetectorPowerMultiplicator = 1f;
            DebugLogging = 0;
            MasterSlaveDistance = 300f;
            OreDetectorToNaniteFactoryCommunicationDistance = 300f;
            OreDetectorRangePerUpgrade = 50f;
            OreDetectorPowerIncreasePerRangeUpgrade = 0.125f;
            OreDetectorPowerIncreasePerFilterUpgrade = 0.1f;
            OreDetectorPowerPercentIncreasedPerScanningUpgrade = 1f;
            OreDetectorPowerPercentReducedPerEfficiencyUpgrade = 0.1f;
            AreaBeaconMaxDistanceFromNaniteFacility = 300f;
            Version = "2.0";
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
