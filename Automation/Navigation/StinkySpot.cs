using GameOverlay.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapAssist.Automation.Navigation
{
    public class StinkySpot
    {
        public bool IsWalkable { get; set; } = false;
        public double Stinkyness { get; set; } = 0;
        public Point Location { get; set; }
    }
}
