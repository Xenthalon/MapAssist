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
        private static string itemTemplate = "<tr><td colspan=2></td><td colspan=4 style=\"color: {{color}}\">{{name}}</td></tr>";

        public static void ExportPlayerInventory(UnitPlayer player, UnitItem[] itemAry)
        {
            var template = ReadResource("InventoryExportTemplate.html");
            var outputfile = player.Name + ".html";

            template = template.Replace("{{player-name}}", player.Name);

            var items = itemAry.ToList();

            var equippedItems = items.Where(x => x.ItemData.dwOwnerID == player.UnitId && x.ItemData.InvPage == InvPage.NULL && x.ItemData.BodyLoc != BodyLoc.NONE);

            template = template.Replace("{{equipped-items}}", GetItemList(equippedItems));

            var inventoryItems = items.Where(x => x.ItemData.dwOwnerID == player.UnitId && x.ItemData.InvPage == InvPage.INVENTORY);

            template = template.Replace("{{inventory-items}}", GetItemList(inventoryItems));

            var stashItems = items.Where(x => x.ItemData.dwOwnerID == player.UnitId && x.ItemData.InvPage == InvPage.STASH);

            template = template.Replace("{{stash-items}}", GetItemList(stashItems));

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

            return itemTemplate.Replace("{{color}}", ColorTranslator.ToHtml(item.ItemBaseColor)).Replace("{{name}}", itemName);
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
