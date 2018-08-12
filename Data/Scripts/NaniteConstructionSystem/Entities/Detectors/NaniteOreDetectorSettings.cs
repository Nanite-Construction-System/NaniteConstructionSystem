using NaniteConstructionSystem.Extensions;
using ProtoBuf;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;

namespace NaniteConstructionSystem.Entities.Detectors
{
    class NaniteOreDetectorSettings
    {
        public ProtoNaniteOreDetectorSettings Settings = new ProtoNaniteOreDetectorSettings();
        internal readonly IMyFunctionalBlock Detector;
        internal float DefaultRange = 0f;
        internal NaniteOreDetectorSettings(IMyFunctionalBlock detector, float defaultRange)
        {
            Detector = detector;
            DefaultRange = defaultRange;
        }

        public void Save()
        {
            if (Detector.Storage == null)
                Detector.Storage = new MyModStorageComponent();

            Detector.Storage[NaniteConstructionManager.Instance.OreDetectorSettingsGuid] = MyAPIGateway.Utilities.SerializeToXML(Settings);

            if (Sync.IsClient)
                MessageHub.SendMessageToServer(new MessageOreDetectorSettings()
                {
                    EntityId = Detector.EntityId,
                    Settings = Settings
                });
        }

        public bool Load()
        {
            if (Detector.Storage == null) return false;

            string rawData;
            bool success = false;

            if (Detector.Storage.TryGetValue(NaniteConstructionManager.Instance.OreDetectorSettingsGuid, out rawData))
            {
                ProtoNaniteOreDetectorSettings loadedSettings = null;

                try
                {
                    loadedSettings = MyAPIGateway.Utilities.SerializeFromXML<ProtoNaniteOreDetectorSettings>(rawData);
                }
                catch (Exception e)
                {
                    loadedSettings = null;
                    Logging.Instance.WriteLine($"OreDetectorId:{Detector.EntityId.ToString()} - Error loading settings!\n{e}");
                }

                if (loadedSettings != null)
                {
                    Settings = loadedSettings;
                    success = true;
                }
            }
            else
            {
                Settings.Range = DefaultRange;
            }

            return success;
        }
    }

    [ProtoContract]
    public class ProtoNaniteOreDetectorSettings
    {
        [ProtoMember(1)]
        public float Range;

        [ProtoMember(2)]
        public List<string> OreList;

        [ProtoMember(3)]
        public bool ShowScanRadius;
    }
}
