using System.Collections.Generic;
using VRageMath;
using Sandbox.ModAPI;
using VRage.ModAPI;
using VRage.Game;
using VRage.ObjectBuilders;
using VRage.Game.Entity;
using VRage.Utils;
using Sandbox.Game.Entities;
using System.IO;

using NaniteConstructionSystem.Extensions;
using NaniteConstructionSystem.Entities.Effects.LightningBolt;

namespace NaniteConstructionSystem.Entities.Effects
{
    public abstract class NaniteBlockEffectBase
    {
        protected static string m_modelsFolder;

        public NaniteBlockEffectBase()
        {
            if (m_modelsFolder == null)
                GetModelsFolder();
        }

        private void GetModelsFolder()
        {
            ulong publishID = 0;
            var mods = MyAPIGateway.Session.GetCheckpoint("null").Mods;
            foreach (var mod in mods)
            {
                if (mod.PublishedFileId == 219757726)
                    publishID = mod.PublishedFileId;
            }

            if (publishID != 0)
                m_modelsFolder = Path.GetFullPath(string.Format(@"{0}\{1}.sbm\Models\Cubes\large\", MyAPIGateway.Utilities.GamePaths.ModsPath, publishID.ToString()));
            else
                m_modelsFolder = Path.GetFullPath(string.Format(@"{0}\{1}\Models\Cubes\large\", MyAPIGateway.Utilities.GamePaths.ModsPath, "NaniteConstructionSystemLocal"));
        }

        public abstract void ActiveUpdate();
        public abstract void InactiveUpdate();
        public abstract void ActivatingUpdate(int position, int maxPosition);
        public abstract void DeactivatingUpdate(int position, int maxPosition);
        public abstract void Unload();
    }

}
