using GameOverlay.Drawing;
using MapAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapAssist.Automation
{
    public enum RunType { KillTarget, ClearArea, Explore };
    public enum KillType { Everything, Bosses, Nothing };

    public class RunProfile
    {
        public string Name { get; set; }
        public RunArea[] AreaPath { get; set; }
        public RunType Type { get; set; }
        public Point KillSpot { get; set; }
        public string Target { get; set; }
        public Npc MonsterType { get; set; } = Npc.NpcNotApplicable;
        public bool Reposition { get; set; } = true;
        public bool GoSuperchest { get; set; } = false;

        // add cows, diablo and baal here as specials
    }

    public class RunArea
    {
        public Area Area { get; set; }
        public KillType Kill { get; set; } = KillType.Nothing;

    }
}
