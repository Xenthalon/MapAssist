﻿using MapAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapAssist.Automation
{
    public class CombatSkill
    {
        public string Name { get; set; }
        public string Key { get; set; }
        public Resist DamageType { get; set; } = Resist.Physical;
        public short MaxRange { get; set; }
        public int Cooldown { get; set; } = 500;
        public long LastUsage { get; set; } = 0;
        public bool IsMainSkill { get; set; } = false;
        public bool IsStatic { get; set; } = false;
        public bool IsRanged { get; set; } = false;
        public bool IsAoe { get; set; } = false;
        public bool IsBuff { get; set; } = false;
        public bool IsTelekinesis { get; set; } = false;
        public bool IsAura { get; set; } = false;
        public State BuffState { get; set; }
    }
}
