using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using VRage.ModAPI;
using Sandbox.Definitions;

namespace NaniteConstructionSystem.Settings
{
    [Serializable]
    public class SerializableKeyValuePair<K, V>
    {
        public K Key { get; set; }
        public V Value { get; set; }
        public SerializableKeyValuePair()
        {
            Key = default(K);
            Value = default(V);
        }

        public SerializableKeyValuePair(K key, V value)
        {
            Key = key;
            Value = value;
        }
    }

    public class NaniteAssemblerSettings
    {
        public bool AllowFactoryUsage { get; set; }

        public NaniteAssemblerSettings()
        {
            AllowFactoryUsage = false;
        }
    }

    public class NaniteTerminalSettings
    {
        public bool AllowRepair { get; set; }
        public bool AllowProjection { get; set; }
        public bool AllowCleanup { get; set; }
        public bool AllowDeconstruct { get; set; }
        public bool AllowMining { get; set; }
        public bool AllowMedical { get; set; }
        public int MaxNanites { get; set; }
        public bool UseAssemblers { get; set; }

        public NaniteTerminalSettings()
        {
            AllowRepair = true;
            AllowProjection = true;
            AllowCleanup = true;
            AllowDeconstruct = true;
            AllowMining = true;
            AllowMedical = true;
            MaxNanites = 0;
            UseAssemblers = false;
        }
    }
  
    public class NaniteHammerTerminalSettings
    {
        public List<string> SelectedOres { get; set; }

        public NaniteHammerTerminalSettings()
        {
            SelectedOres = new List<string>();
        }

        public NaniteHammerTerminalSettings(bool defaultAll = true)
        {
            SelectedOres = new List<string>();
            if (defaultAll)
            {
                foreach (var item in MyDefinitionManager.Static.GetVoxelMaterialDefinitions().Select(x => x.MinedOre).Distinct())
                {
                    if (item == "Stone")
                        continue;

                    SelectedOres.Add(item);
                }
            }
        }
    }

    public class NaniteBeaconTerminalSettings
    {
        public int Height { get; set; }
        public int Width { get; set; }
        public int Depth { get; set; }
        public int OffsetX { get; set; }
        public int OffsetY { get; set; }
        public int OffsetZ { get; set; }
        public int RotationX { get; set; }
        public int RotationY { get; set; }
        public int RotationZ { get; set; }
        public bool HighlightArea { get; set; }
        public bool AllowRepair { get; set; }
        public bool AllowProjection { get; set; }
        public bool AllowDeconstruction { get; set; }

        public NaniteBeaconTerminalSettings()
        {
            Width = 200;
            Height = 200;
            Depth = 200;
            OffsetX = 0;
            OffsetY = 0;
            OffsetZ = 0;
            RotationX = 0;
            RotationY = 0;
            RotationZ = 0;
            HighlightArea = true;
            AllowRepair = false;
            AllowProjection = false;
            AllowDeconstruction = false;
        }
    }

    public class TerminalSettings
    {
        public void Save()
        {
            List<SerializableKeyValuePair<long, NaniteTerminalSettings>> terminalResult = new List<SerializableKeyValuePair<long, NaniteTerminalSettings>>();
            foreach (var item in NaniteConstructionManager.TerminalSettings)
            {
                SerializableKeyValuePair<long, NaniteTerminalSettings> pair = new SerializableKeyValuePair<long, NaniteTerminalSettings>(item.Key, item.Value);
                terminalResult.Add(pair);
            }
            SaveTerminalSettings("terminalsettings.xml", terminalResult);

            List<SerializableKeyValuePair<long, NaniteAssemblerSettings>> assemblerResult = new List<SerializableKeyValuePair<long, NaniteAssemblerSettings>>();
            foreach (var item in NaniteConstructionManager.AssemblerSettings)
            {
                SerializableKeyValuePair<long, NaniteAssemblerSettings> pair = new SerializableKeyValuePair<long, NaniteAssemblerSettings>(item.Key, item.Value);
                assemblerResult.Add(pair);
            }
            SaveTerminalSettings("assemblersettings.xml", assemblerResult);

            List<SerializableKeyValuePair<long, NaniteHammerTerminalSettings>> hammerResult = new List<SerializableKeyValuePair<long, NaniteHammerTerminalSettings>>();
            foreach (var item in NaniteConstructionManager.HammerTerminalSettings)
            {
                SerializableKeyValuePair<long, NaniteHammerTerminalSettings> pair = new SerializableKeyValuePair<long, NaniteHammerTerminalSettings>(item.Key, item.Value);
                hammerResult.Add(pair);
            }
            SaveTerminalSettings("NaniteControlFactory.HammerTerminalSettings", hammerResult);

            List<SerializableKeyValuePair<long, NaniteBeaconTerminalSettings>> beaconResult = new List<SerializableKeyValuePair<long, NaniteBeaconTerminalSettings>>();
            foreach (var item in NaniteConstructionManager.BeaconTerminalSettings)
            {
                SerializableKeyValuePair<long, NaniteBeaconTerminalSettings> pair = new SerializableKeyValuePair<long, NaniteBeaconTerminalSettings>(item.Key, item.Value);
                beaconResult.Add(pair);
            }
            SaveTerminalSettings("NaniteControlFactory.BeaconTerminalSettings", beaconResult);
        }

        public void Load()
        {
            var terminalResult = LoadTerminalSettings<List<SerializableKeyValuePair<long, NaniteTerminalSettings>>>("terminalsettings.xml");
            if(terminalResult != null)
            {
                foreach (var item in terminalResult)
                {
                    IMyEntity entity;
                    if (!MyAPIGateway.Entities.TryGetEntityById(item.Key, out entity))
                        continue;

                    IMyTerminalBlock block = entity as IMyTerminalBlock;
                    if (block == null)
                        continue;

                    if (!NaniteConstructionManager.TerminalSettings.ContainsKey(block.EntityId))
                        NaniteConstructionManager.TerminalSettings.Add(block.EntityId, item.Value);
                }
            }

            var assemblerResult = LoadTerminalSettings<List<SerializableKeyValuePair<long, NaniteAssemblerSettings>>>("assemblersettings.xml");
            if(assemblerResult != null)
            {
                foreach (var item in assemblerResult)
                {
                    IMyEntity entity;
                    if (!MyAPIGateway.Entities.TryGetEntityById(item.Key, out entity))
                        continue;

                    IMyTerminalBlock block = entity as IMyTerminalBlock;
                    if (block == null)
                        continue;

                    if (!NaniteConstructionManager.AssemblerSettings.ContainsKey(block.EntityId))
                        NaniteConstructionManager.AssemblerSettings.Add(block.EntityId, item.Value);
                }
            }

            var hammerResult = LoadTerminalSettings<List<SerializableKeyValuePair<long, NaniteHammerTerminalSettings>>>("NaniteControlFactory.HammerTerminalSettings");
            if(hammerResult != null)
            {
                foreach(var item in hammerResult)
                {
                    IMyEntity entity;
                    if (!MyAPIGateway.Entities.TryGetEntityById(item.Key, out entity))
                        continue;

                    IMyTerminalBlock block = entity as IMyTerminalBlock;
                    if (block == null)
                        continue;

                    if (!NaniteConstructionManager.HammerTerminalSettings.ContainsKey(block.EntityId))
                        NaniteConstructionManager.HammerTerminalSettings.Add(block.EntityId, item.Value);
                }
            }

            var beaconResult = LoadTerminalSettings<List<SerializableKeyValuePair<long, NaniteBeaconTerminalSettings>>>("NaniteControlFactory.BeaconTerminalSettings");
            if (beaconResult != null)
            {
                foreach (var item in beaconResult)
                {
                    IMyEntity entity;
                    if (!MyAPIGateway.Entities.TryGetEntityById(item.Key, out entity))
                        continue;

                    IMyTerminalBlock block = entity as IMyTerminalBlock;
                    if (block == null)
                        continue;

                    if (!NaniteConstructionManager.BeaconTerminalSettings.ContainsKey(block.EntityId))
                        NaniteConstructionManager.BeaconTerminalSettings.Add(block.EntityId, item.Value);
                }
            }
        }

        private void SaveTerminalSettings<T>(string fileName, T settings)
        {
            /*
            using (var writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(fileName, typeof(NaniteSettings)))
            {
                writer.Write(MyAPIGateway.Utilities.SerializeToXML(settings));
            }
            */
			if(MyAPIGateway.Utilities == null) return;
            MyAPIGateway.Utilities.SetVariable(fileName, MyAPIGateway.Utilities.SerializeToXML(settings));

        }

        private T LoadTerminalSettings<T>(string fileName)
        {
            
             if (MyAPIGateway.Utilities.FileExistsInLocalStorage(fileName, typeof(NaniteSettings)))
            {
                try
                {
                    Logging.Instance.WriteLine(string.Format("Loading: {0}", fileName));
                    using (var reader = MyAPIGateway.Utilities.ReadFileInLocalStorage(fileName, typeof(NaniteSettings)))
                    {
                        string settingsData = reader.ReadToEnd();
                        T settings = MyAPIGateway.Utilities.SerializeFromXML<T>(settingsData);
                        return settings;
                    }
                }
                catch (Exception ex)
                {
                    Logging.Instance.WriteLine(string.Format("Error loading terminal settings file: {0}", ex.ToString()));
                }
            }

            return default(T);
            

            try
            {
                string data = "";
                if (MyAPIGateway.Utilities.GetVariable(fileName, out data))
                {
                    T settings = MyAPIGateway.Utilities.SerializeFromXML<T>(data);
                    return settings;
                }
            }
            catch(Exception ex)
            {
                Logging.Instance.WriteLine(string.Format("Loading Error: {0}", ex.ToString()));
            }

            return default(T); 
        }
    }
}
