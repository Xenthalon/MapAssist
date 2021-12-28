using GameOverlay.Drawing;
using MapAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapAssist.Automation
{
    public enum RunType { KillTarget, ClearArea };

    class RunProfile
    {
        public string Name { get; set; }
        public Area[] AreaPath { get; set; }
        public RunType Type { get; set; }
        public Point KillSpot { get; set; }
        public Npc MonsterType { get; set; } = Npc.NpcNotApplicable;

        // add cows, diablo and baal here as specials
    }
}
