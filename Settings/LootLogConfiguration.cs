using MapAssist.Files;
using MapAssist.Types;

using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace MapAssist.Settings
{
    public class LootLogConfiguration
    {
        public static Dictionary<string, List<ItemFilter>> Filters { get; set; }

        public static void Load()
        {
            Filters = ConfigurationParser<Dictionary<string, List<ItemFilter>>>.ParseConfigurationFile($"./{MapAssistConfiguration.Loaded.ItemLog.FilterFileName}");
        }
    }

    public class ItemFilter
    {
        public ItemQuality[] Qualities { get; set; }
        public bool? Ethereal { get; set; }
        public int[] Sockets { get; set; }
        public int? Defense { get; set; }
        
        [YamlMember(Alias = "All Resist")]
        public int? AllResist { get; set; }
        
        [YamlMember(Alias = "All Skills")]
        public int? AllSkills { get; set; }
        
        [YamlMember(Alias = "Class Skills")]
        public Dictionary<Structs.PlayerClass, int?> ClassSkills { get; set; } = new Dictionary<Structs.PlayerClass, int?>();
        
        [YamlMember(Alias = "Class Skill Tree")]
        public Dictionary<ClassTabs, int?> ClassTabSkills { get; set; } = new Dictionary<ClassTabs, int?>();
        
        [YamlMember(Alias = "Skills")]
        public Dictionary<Skill, int?> Skills { get; set; } = new Dictionary<Skill, int?>();
    }
}
