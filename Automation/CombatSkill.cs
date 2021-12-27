using MapAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapAssist.Automation
{
    class CombatSkill
    {
        public string Name { get; set; }
        public string Key { get; set; }
        public int Cooldown { get; set; }
        public long LastUsage { get; set; }
        public bool IsRanged { get; set; }
        public bool IsAoe { get; set; }
        public bool IsBuff { get; set; }
        public State BuffState { get; set; }
    }
}
