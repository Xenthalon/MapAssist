using MapAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using static MapAssist.Types.Stats;

namespace MapAssist.Automation.Identification
{
    public sealed class IdentifyYamlTypeConverter : IYamlTypeConverter
    {
        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();

        public bool Accepts(Type type)
        {
            return type == typeof(List<StatFilter>[]);
        }

        public object ReadYaml(IParser parser, Type type)
        {
            if (parser.TryConsume<Scalar>(out var scalar))
            {
                var items = new List<string>() { scalar.Value };
                return ParseStatFilters(items);
            }

            if (parser.TryConsume<SequenceStart>(out var _))
            {
                var items = new List<string>();
                while (parser.TryConsume<Scalar>(out var scalarItem))
                {
                    items.Add(scalarItem.Value);
                }

                parser.Consume<SequenceEnd>();
                return ParseStatFilters(items);
            }

            return null;
        }

        private List<StatFilter>[] ParseStatFilters(List<string> quality)
        {
            return quality.Select(q =>
            {
                var success = false;
                var result = new List<StatFilter>();
                try
                {
                    foreach (var section in q.ToUpper().Split('&'))
                    {
                        var parts = section.Trim().Split(' ');
                        var stat = (Stat)Enum.ToObject(typeof(Stat), int.Parse(parts[0]));

                        StatFilterType type = StatFilterType.NONE;

                        switch (parts[1])
                        {
                            case "=":
                                type = StatFilterType.EQ;
                                break;
                            case ">=":
                                type = StatFilterType.GTE;
                                break;
                            case ">":
                                type = StatFilterType.GT;
                                break;
                            case "<":
                                type = StatFilterType.LT;
                                break;
                            case "<=":
                                type = StatFilterType.LTE;
                                break;
                        }

                        result.Add(new StatFilter { Stat = stat, StatFilterType = type, Value = int.Parse(parts[2]) });
                        success = type != StatFilterType.NONE;
                    }
                }
                catch (Exception e) { _log.Error(e, "Couldn't parse node."); }
                return new { success, result };
            }).Where(x => x.success).Select(x => x.result).ToArray();
        }

        public void WriteYaml(IEmitter emitter, object value, Type type)
        {
            throw new NotImplementedException();
        }
    }
}
