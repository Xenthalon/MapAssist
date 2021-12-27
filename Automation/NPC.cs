using GameOverlay.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapAssist.Automation
{
    class NPC
    {
        public string Name { get; set; }
        public Point Position { get; set; }
        public int Act { get; set; }
        public int TxtFileId { get; set; }
        public bool CanHeal { get; set; } = false;
        public bool CanRevive { get; set; } = false;
        public bool CanRepair { get; set; } = false;
        public bool CanGamble { get; set; } = false;
        public bool CanTrade { get; set; } = false;
        public bool HasScrolls { get; set; } = false;
        public bool HasPotions { get; set; } = false;
        public bool IsStash { get; set; } = false;
        public bool IsWaypoint { get; set; } = false;
        public bool IsPortalSpot { get; set; } = false;
    }
}
