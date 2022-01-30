using MapAssist.Settings;
using MapAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapAssist.Automation.Identification
{
    static class IdentificationFilter
    {
        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();

        public static bool HasEntry(UnitItem item)
        {
            var baseName = item.ItemBaseName;
            var itemQuality = item.ItemData.ItemQuality;
            var isEth = (item.ItemData.ItemFlags & ItemFlags.IFLAG_ETHEREAL) == ItemFlags.IFLAG_ETHEREAL;

            return Filter(baseName, itemQuality, isEth);
        }

        public static bool IsKeeper(UnitItem item)
        {
            item.IsCached = false;
            item = item.Update();

            var baseName = item.ItemBaseName;
            var itemQuality = item.ItemData.ItemQuality;
            var isEth = (item.ItemData.ItemFlags & ItemFlags.IFLAG_ETHEREAL) == ItemFlags.IFLAG_ETHEREAL;
            var stats = item.Stats;

            if (stats.Count() == 0)
            {
                _log.Warn(itemQuality + " " + baseName + " has no stats, keeping it!");
                return true;
            }

            return FilterDeep(baseName, itemQuality, isEth, stats);
        }

        private static bool Filter(string baseName, ItemQuality itemQuality, bool isEth)
        {
            //populate a list of filter rules by combining rules from "Any" and the item base name
            //use only one list or the other depending on if "Any" exists
            var matches =
                IdentifyConfiguration.Filters
                    .Where(f => f.Key == "Any" || f.Key == baseName).ToList();

            //scan the list of rules
            foreach (var item in matches.SelectMany(kv => kv.Value))
            {
                var qualityReqMet = item.Qualities == null || item.Qualities.Length == 0 ||
                                    item.Qualities.Contains(itemQuality);

                var ethReqMet = (item.Ethereal == null || item.Ethereal == isEth);
                if (qualityReqMet && ethReqMet) { return true; }
            }

            return false;
        }

        private static bool FilterDeep(string baseName, ItemQuality itemQuality, bool isEth, Dictionary<Stat, int> stats)
        {
            //populate a list of filter rules by combining rules from "Any" and the item base name
            //use only one list or the other depending on if "Any" exists
            var matches =
                IdentifyConfiguration.Filters
                    .Where(f => f.Key == "Any" || f.Key == baseName).ToList();

            //scan the list of rules
            foreach (var filterRule in matches.SelectMany(kv => kv.Value))
            {
                var qualityReqMet = filterRule.Qualities == null || filterRule.Qualities.Length == 0 ||
                                    filterRule.Qualities.Contains(itemQuality);

                var ethReqMet = (filterRule.Ethereal == null || filterRule.Ethereal == isEth);

                if (qualityReqMet && ethReqMet)
                {
                    _log.Info($"Evaluating {itemQuality} {baseName} with:");
                    foreach (KeyValuePair<Stat, int> entry in stats)
                    {
                        _log.Info($"{entry.Key}: {entry.Value}");
                    }

                    foreach (List<StatFilter> statRule in filterRule.Stats)
                    {
                        var success = MatchFilterRule(stats, statRule);

                        if (success)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool MatchFilterRule(Dictionary<Stat, int> stats, List<StatFilter> statFilterRule)
        {
            var matches = false;

            var allContained = statFilterRule.Select(x => x.Stat).Intersect(stats.Keys).Count() == statFilterRule.Select(x => x.Stat).Count();

            if (allContained)
            {
                var allLinesMatch = true;

                foreach (StatFilter filter in statFilterRule)
                {
                    var value = -1;

                    stats.TryGetValue(filter.Stat, out value);

                    if (filter.Stat == Stat.MaxLife || filter.Stat == Stat.MaxMana)
                    {
                        value = Chicken.ConvertHexHealthToInt(value);
                    }

                    _log.Info(filter.Stat.ToString() + " is " + value + ", wanted " + filter.StatFilterType.ToString() + " " + filter.Value);

                    switch (filter.StatFilterType)
                    {
                        case StatFilterType.EQ:
                            allLinesMatch = filter.Value == value;
                            break;
                        case StatFilterType.GT:
                            allLinesMatch = value > filter.Value;
                            break;
                        case StatFilterType.GTE:
                            allLinesMatch = value >= filter.Value;
                            break;
                        case StatFilterType.LT:
                            allLinesMatch = value < filter.Value;
                            break;
                        case StatFilterType.LTE:
                            allLinesMatch = value <= filter.Value;
                            break;
                    }

                    if (allLinesMatch == false)
                        break;
                }

                matches = allLinesMatch;
            }

            return matches;
        }
    }
}
