using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NaniteConstructionSystem.Entities.Effects.LightningBolt
{
    public class LightningBoltInstance
    {
        public LightningBoltItem Bolt { get; set; }
        public LightningBoltPath Path { get; set; }
        public int Position { get; set; }
        public int MaxPosition { get; set; }

        public LightningBoltInstance(LightningBoltItem bolt, LightningBoltPath path, int maxPosition)
        {
            Bolt = bolt;
            Path = path;
            Position = 0;
            MaxPosition = maxPosition;
        }
    }

}
