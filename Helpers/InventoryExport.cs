using MapAssist.Types;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MapAssist.Helpers
{
    public class InventoryExport
    {
        private static string itemTemplate = "<div class=\"item\"><div class=\"item-name\" style=\"color: {{color}}\">{{name}}</div>{{stats}}</div>";
        private static string statTemplate = "<div class=\"stat\" style=\"color:#4169E1\">{{text}}</div>";

        public static void ExportPlayerInventory(UnitPlayer player, UnitItem[] itemAry)
        {
            var template = ReadResource("InventoryExportTemplate.html");
            var outputfile = player.Name + ".html";

            template = template.Replace("{{player-name}}", player.Name);

            var items = itemAry.ToList();

            var equippedItems = items.Where(x => x.ItemData.dwOwnerID == player.UnitId && x.ItemData.InvPage == InvPage.NULL && x.ItemData.BodyLoc != BodyLoc.NONE);

            if (equippedItems.Count() > 0)
            {
                template = template.Replace("{{show-equipped}}", "show");
                template = template.Replace("{{equipped-items}}", GetItemList(equippedItems));
            }

            var inventoryItems = items.Where(x => x.ItemData.dwOwnerID == player.UnitId && x.ItemData.InvPage == InvPage.INVENTORY);

            if (inventoryItems.Count() > 0)
            {
                template = template.Replace("{{show-inventory}}", "show");
                template = template.Replace("{{inventory-items}}", GetItemList(inventoryItems));
            }

            var stashItems = items.Where(x => x.ItemData.dwOwnerID == player.UnitId && x.ItemData.InvPage == InvPage.STASH);

            if (stashItems.Count() > 0)
            {
                template = template.Replace("{{show-stash}}", "show");
                template = template.Replace("{{stash-items}}", GetItemList(stashItems));
            }

            File.WriteAllText(outputfile, template);
        }

        private static string GetItemList(IEnumerable<UnitItem> items)
        {
            var result = "";

            foreach (UnitItem item in items)
            {
                result += GetItemHtml(item);
            }

            return result;
        }

        private static string GetItemHtml(UnitItem item)
        {
            var itemName = Items.ItemLogDisplayName(item, new Settings.ItemFilter());

            if (item.ItemData.ItemQuality > ItemQuality.SUPERIOR && item.IsIdentified)
            {
                itemName = itemName.Replace("[Identified] ", "");
            }

            var itemText = itemTemplate.Replace("{{color}}", ColorTranslator.ToHtml(item.ItemBaseColor)).Replace("{{name}}", itemName);
            var statText = "";

            if (item.ItemData.ItemQuality > ItemQuality.SUPERIOR && !item.IsIdentified)
            {
                statText += statTemplate.Replace("{{text}}", "Unidentified").Replace("4169E1", "DD0000");

                if (item.Stats.ContainsKey(Stat.Defense))
                {
                    int defense;

                    item.Stats.TryGetValue(Stat.Defense, out defense);

                    statText += statTemplate.Replace("{{text}}", Stat.Defense.ToString() + ": " + defense);
                }
            }
            else
            {
                foreach (var stat in item.Stats)
                {
                    var name = stat.Key.ToString();
                    var value = stat.Value;

                    if (stat.Key == Stat.MaxLife || stat.Key == Stat.MaxMana)
                    {
                        value = ConvertHexHealthToInt(stat.Value);
                    }

                    if (stat.Key == Stat.AddClassSkills)
                    {
                        var classSkills = Items.GetItemStatAddClassSkills(item, Structs.PlayerClass.Any);
                        name = classSkills.Item1.ToString();
                        value = classSkills.Item2;
                    }

                    if (stat.Key == Stat.AddSkillTab)
                    {
                        var skillTree = Items.GetItemStatAddSkillTreeSkills(item, SkillTree.Any);
                        name = skillTree.Item1.ToString() + " Skills";
                        value = skillTree.Item2;
                    }

                    statText += statTemplate.Replace("{{text}}", name + ": " + value);
                }
            }


            itemText = itemText.Replace("{{stats}}", statText);

            return itemText;
        }

        private static int ConvertHexHealthToInt(int hexHealth)
        {
            var hexValue = hexHealth.ToString("X");
            hexValue = hexValue.Substring(0, hexValue.Length - 2);
            return int.Parse(hexValue, System.Globalization.NumberStyles.HexNumber);
        }

        private static string ReadResource(string name)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourcePath = assembly.GetManifestResourceNames()
                    .Single(str => str.EndsWith(name));

            using (Stream stream = assembly.GetManifestResourceStream(resourcePath))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
