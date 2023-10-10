using System;
using System.IO;
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
        public bool AllowLifeSupport { get; set; }
        public int MaxNanites { get; set; }
        public bool UseAssemblers { get; set; }

        public NaniteTerminalSettings()
        {
            AllowRepair = true;
            AllowProjection = true;
            AllowCleanup = true;
            AllowDeconstruct = true;
            AllowMining = true;
            AllowLifeSupport = true;
            MaxNanites = 0;
            UseAssemblers = false;
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
            SaveTerminalSettings("FactoryTerminalSettings.dat", terminalResult);

            List<SerializableKeyValuePair<long, NaniteAssemblerSettings>> assemblerResult = new List<SerializableKeyValuePair<long, NaniteAssemblerSettings>>();
            foreach (var item in NaniteConstructionManager.AssemblerSettings)
            {
                SerializableKeyValuePair<long, NaniteAssemblerSettings> pair = new SerializableKeyValuePair<long, NaniteAssemblerSettings>(item.Key, item.Value);
                assemblerResult.Add(pair);
            }
            SaveTerminalSettings("AssemblerTerminalSettings.dat", assemblerResult);
        }

        public void Load()
        {
            var terminalResult = LoadTerminalSettings<List<SerializableKeyValuePair<long, NaniteTerminalSettings>>>("FactoryTerminalSettings.dat");
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

            var assemblerResult = LoadTerminalSettings<List<SerializableKeyValuePair<long, NaniteAssemblerSettings>>>("AssemblerTerminalSettings.dat");
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
        }

        private void SaveTerminalSettings<T>(string fileName, T settings) {
            using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(fileName, typeof(NaniteSettings))){
                writer.Write(MyAPIGateway.Utilities.SerializeToXML(settings));
            }
        }

        private T LoadTerminalSettings<T>(string fileName){
			if (MyAPIGateway.Utilities.FileExistsInWorldStorage(fileName, typeof(NaniteSettings))){
                try{
                    Logging.Instance.WriteLine("Loading: " + fileName);

					using (var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(fileName, typeof(NaniteSettings)))
						return MyAPIGateway.Utilities.SerializeFromXML<T>(reader.ReadToEnd());

                } catch(Exception ex){
                    Logging.Instance.WriteLine(string.Format("Error loading terminal settings file: {0}", ex.ToString()));
                }
            }

            return default(T);
        }
    }
}
