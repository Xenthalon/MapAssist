/**
 *   Copyright (C) 2021 okaygo
 *
 *   https://github.com/misterokaygo/MapAssist/
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <https://www.gnu.org/licenses/>.
 **/

using MapAssist.Settings;
using MapAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using YamlDotNet.Serialization;

namespace MapAssist.Helpers
{
    public static class LootFilter
    {
        public static Dictionary<Stat, int> StatShifts = new Dictionary<Stat, int>()
        {
            [Stat.MaxLife] = 8,
            [Stat.MaxMana] = 8,
        };

        public static (bool, ItemFilter) Filter(UnitAny unitAny)
        {
            // Skip low quality items
            var lowQuality = (unitAny.ItemData.ItemFlags & ItemFlags.IFLAG_LOWQUALITY) == ItemFlags.IFLAG_LOWQUALITY;
            if (lowQuality) return (false, null);

            // Populate a list of filter rules by combining rules from "Any" and the item base name
            // Use only one list or the other depending on if "Any" exists
            var matches = LootLogConfiguration.Filters.Where(f => f.Key == Item.Any || (uint)f.Key == unitAny.TxtFileNo).ToList();

            // Early breakout
            // We know that there is an item in here without any actual filters
            // So we know that simply having the name match means we can return true
            if (matches.Any(kv => kv.Value == null)) return (true, null);

            // Scan the list of rules
            foreach (var rule in matches.SelectMany(kv => kv.Value))
            {
                // Requirement check functions
                var requirementsFunctions = new Dictionary<string, Func<bool>>()
                {
                    ["Qualities"] = () => rule.Qualities.Contains(unitAny.ItemData.ItemQuality),
                    ["Sockets"] = () => rule.Sockets.Contains(Items.GetItemStat(unitAny, Stat.NumSockets)),
                    ["Ethereal"] = () => ((unitAny.ItemData.ItemFlags & ItemFlags.IFLAG_ETHEREAL) == ItemFlags.IFLAG_ETHEREAL) == rule.Ethereal,
                    ["AllAttributes"] = () => Items.GetItemStatAllAttributes(unitAny) >= rule.AllAttributes,
                    ["AllResist"] = () => Items.GetItemStatAllResist(unitAny) >= rule.AllResist,
                    ["ClassSkills"] = () =>
                    {
                        if (rule.ClassSkills.Count() == 0) return true;
                        return rule.ClassSkills.All(subrule => Items.GetItemStatAddClassSkills(unitAny, subrule.Key) >= subrule.Value);
                    },
                    ["ClassTabSkills"] = () =>
                    {
                        if (rule.ClassTabSkills.Count() == 0) return true;
                        return rule.ClassTabSkills.All(subrule => Items.GetItemStatAddClassTabSkills(unitAny, subrule.Key) >= subrule.Value);
                    },
                    ["Skills"] = () =>
                    {
                        if (rule.Skills.Count() == 0) return true;
                        return rule.Skills.All(subrule => Items.GetItemStatSingleSkills(unitAny, subrule.Key) >= subrule.Value);
                    },
                    ["SkillCharges"] = () =>
                    {
                        if (rule.SkillCharges.Count() == 0) return true;
                        return rule.SkillCharges.All(subrule => Items.GetItemStatAddSkillCharges(unitAny, subrule.Key).Item1 >= subrule.Value);
                    },
                };

                foreach (var (stat, shift) in StatShifts.Select(x => (x.Key, x.Value)))
                {
                    requirementsFunctions.Add(stat.ToString(), () => Items.GetItemStatShifted(unitAny, stat, shift) >= (int)rule[stat]);
                }

                var requirementMet = true;
                foreach (var property in rule.GetType().GetProperties())
                {
                    if (property.PropertyType == typeof(object)) continue; // This is the item from Stat property

                    var propertyValue = rule.GetType().GetProperty(property.Name).GetValue(rule, null);
                    if (requirementsFunctions.TryGetValue(property.Name, out var requirementFunc))
                    {
                        requirementMet &= propertyValue == null || requirementFunc();
                    }
                    else if (Enum.TryParse<Stat>(property.Name, out var stat))
                    {
                        requirementMet &= propertyValue == null || Items.GetItemStat(unitAny, stat) >= (int)propertyValue;
                    }
                    if (!requirementMet) break;
                }
                if (!requirementMet) continue;

                // Item meets all filter requirements
                return (true, rule);
            }

            return (false, null);
        }
    }
}
