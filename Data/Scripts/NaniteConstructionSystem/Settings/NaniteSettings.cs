using System;
using Sandbox.ModAPI;

namespace NaniteConstructionSystem.Settings
{
    public class NaniteSettings
    {
		static public string SettingsFile = "Config.xml";

        public bool ConstructionEnabled { get; set; } //
        public int ConstructionMaxStreams { get; set; } //
        public float ConstructionEfficiency { get; set; } //
        public float ConstructionPowerPerStream { get; set; } //
        public float ConstructionMinTravelTime { get; set; } //
        public float ConstructionDistanceDivisor { get; set; } //
        public int ConstructionNanitesPerUpgrade { get; set; } //
        public int ConstructionNanitesNoUpgrade { get; set; } //
        public float ConstructionMaxBeaconDistance { get; set; }
        public bool ProjectionEnabled { get; set; } //
        public int ProjectionMaxStreams { get; set; } //
        public float ProjectionPowerPerStream { get; set; } //
        public float ProjectionMinTravelTime { get; set; } //
        public float ProjectionDistanceDivisor { get; set; } //
        public int ProjectionNanitesPerUpgrade { get; set; } //
        public int ProjectionNanitesNoUpgrade { get; set; } //
        public float ProjectionMaxBeaconDistance { get; set; }
        public bool CleanupEnabled { get; set; } //
        public int CleanupMaxStreams { get; set; } //
        public float CleanupCarryVolume { get; set; } //
        public float CleanupMaxDistance { get; set; } //
        public float CleanupPowerPerStream { get; set; } //
        public float CleanupMinTravelTime { get; set; } //
        public float CleanupDistanceDivisor { get; set; } //
        public int CleanupNanitesPerUpgrade { get; set; } //
        public int CleanupNanitesNoUpgrade { get; set; } //
        public bool DeconstructionEnabled { get; set; } //
        public int DeconstructionMaxStreams { get; set; } //
        public float DeconstructionMaxDistance { get; set; } //
        public float DeconstructionEfficiency { get; set; } // Kind of
        public float DeconstructionPowerPerStream { get; set; } //
        public float DeconstructionMinTravelTime { get; set; } //
        public float DeconstructionDistanceDivisor { get; set; } //
        public bool DeconstructionPerformanceFriendly { get; set; }
        public int DeconstructionNanitesPerUpgrade { get; set; } //
        public int DeconstructionNanitesNoUpgrade { get; set; } //
        public bool MiningEnabled { get; set; } //
        public int MiningMaxStreams { get; set; }
        public float MiningMaxDistance { get; set; }
        public float MiningPowerPerStream { get; set; }
        public float MiningMinTravelTime { get; set; }
        public float MiningDistanceDivisor { get; set; }
        public int MiningNanitesPerUpgrade { get; set; }
        public int MiningNanitesNoUpgrade { get; set; }
        public float MiningRadius { get; set; }
        public int MiningDepth { get; set; }
        public bool MedicalEnabled { get; set; } //
        public int MedicalMaxStreams { get; set; }
        public float MedicalMaxDistance { get; set; }
        public float MedicalPowerPerStream { get; set; }
        public float MedicalMinTravelTime { get; set; }
        public float MedicalDistanceDivisor { get; set; }
        public int MedicalNanitesPerUpgrade { get; set; }
        public int MedicalNanitesNoUpgrade { get; set; }
        public int MedicalSecondsPerHealTick { get; set; }
        public float MedicalHealthPerHealTick { get; set; }
        public float AreaBeaconMaxSize { get; set; }
        public float AreaBeaconMinSize { get; set; }
        public float SpeedIncreasePerUpgrade { get; set; }
        public float MinTravelTimeReductionPerUpgrade { get; set; }
        public float PowerDecreasePerUpgrade { get; set; }
        public float FactoryComponentMultiplier { get; set; } //
        public float UpgradeComponentMultiplier { get; set; }
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
            CleanupMaxDistance = 500f;
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
            MiningRadius = 40f;
            MiningDepth = 50;
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
            Version = "1.0";
        }

        public static NaniteSettings Load() {
			bool updatedFile = MyAPIGateway.Utilities.FileExistsInWorldStorage(SettingsFile, typeof(NaniteSettings));

			// We can't just ignore & delete the file at the old location because what if someone has modified it and expects the settings to be the same under multiple saves?

			if (MyAPIGateway.Utilities.FileExistsInLocalStorage("settings.xml", typeof(NaniteSettings)) || updatedFile)
            {
                try
                {
                    Logging.Instance.WriteLine("Loading Settings");
                    NaniteSettings settings;
                    using (var reader = updatedFile ? MyAPIGateway.Utilities.ReadFileInWorldStorage(SettingsFile, typeof(NaniteSettings)) : MyAPIGateway.Utilities.ReadFileInLocalStorage("settings.xml", typeof(NaniteSettings)))
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
            var originalVersion = settings.Version;
            if (settings.Version == "1.0")
            {
                Logging.Instance.WriteLine(string.Format("Updating Settings 1.0 -> 1.1"));

                if (settings.ConstructionMaxStreams == 6)
                    settings.ConstructionMaxStreams = 15;
                if (settings.DeconstructionMaxStreams == 6)
                    settings.DeconstructionMaxStreams = 15;
                if (settings.CleanupMaxStreams == 6)
                    settings.CleanupMaxStreams = 15;
                if (settings.ProjectionMaxStreams == 6)
                    settings.ProjectionMaxStreams = 15;

                settings.Version = "1.1";
            }

            if (settings.Version == "1.1")
            {
                if (settings.ConstructionNanitesNoUpgrade == 0)
                    settings.ConstructionNanitesNoUpgrade = 1;

                if (settings.DeconstructionNanitesNoUpgrade == 0)
                    settings.DeconstructionNanitesNoUpgrade = 1;

                if (settings.ProjectionNanitesNoUpgrade == 0)
                    settings.ProjectionNanitesNoUpgrade = 1;

                if (settings.CleanupNanitesNoUpgrade == 0)
                    settings.CleanupNanitesNoUpgrade = 1;

                settings.Version = "1.2";
            }

            if (settings.Version == "1.2") // Just notify user we've updated
            {
                settings.Version = "1.3";
            }

            if (settings.Version == "1.3")
            {
                settings.Version = "1.4";
            }

            if (settings.Version == "1.4")
            {
                settings.Version = "1.5";
            }

            if (settings.Version == "1.5")
            {
                settings.Version = "1.6";
            }

            if(settings.Version == "1.6")
            {
                settings.Version = "1.7";
			}

			if (settings.Version == "1.7") {
				MyAPIUtilities utils = (MyAPIUtilities)MyAPIGateway.Utilities;

				// Clean out useless variables in Sandbox.sbc save file.
				utils.Variables.Remove("terminalsettings.xml");
				utils.Variables.Remove("assemblersettings.xml");
				utils.Variables.Remove("NaniteControlFactory.HammerTerminalSettings");
				utils.Variables.Remove("NaniteControlFactory.BeaconTerminalSettings");

				settings.Version = "1.8";
            }

            if (settings.Version == "1.8")
            {
                settings.Version = "1.9";
            }

            if (settings.Version != originalVersion)
                SendNotification();
        }

        public static void Save(NaniteSettings settings){
            using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(SettingsFile, typeof(NaniteSettings)))
                writer.Write(MyAPIGateway.Utilities.SerializeToXML<NaniteSettings>(settings));
        }

        private static void SendNotification()
        {
            if (MyAPIGateway.Utilities != null)
            {
                MyAPIGateway.Utilities.ShowMessage("[Nanite Control Factory]", "Nanite control factory has been updated.  Type /nanite to see what's new.");
                MyAPIGateway.Utilities.ShowNotification("Nanite control factory has been updated.  Type /nanite to see what's new.", 20000, VRage.Game.MyFontEnum.Green);
			
            }
			if(MyAPIGateway.Utilities == null) return;
        }
	
    }
}
