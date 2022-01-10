using MapAssist.Automation.Identification;
using MapAssist.Files;
using MapAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapAssist.Settings
{
    public class IdentifyConfiguration
    {
        public static Dictionary<string, List<IdentificationFilter>> Filters { get; set; }

        public static void Load()
        {
            Filters = ConfigurationParser<Dictionary<string, List<IdentificationFilter>>>.ParseConfigurationFile($"./{MapAssistConfiguration.Loaded.ItemLog.IdentifyFileName}");
        }
    }

    public class IdentificationFilter
    {
        public ItemQuality[] Qualities { get; set; }
        public bool? Ethereal { get; set; }
        public int[] Sockets { get; set; }
        public List<StatFilter>[] Stats { get; set; }
    }
}
