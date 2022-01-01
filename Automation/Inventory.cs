using MapAssist.Helpers;
using MapAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapAssist.Automation
{
    class Inventory
    {
        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();

        public static int[][] InventoryOpen = new int[][] {
            new int[] { 1, 1, 1, 1, 0, 0, 0, 0, 0, 0 },
            new int[] { 1, 1, 1, 1, 0, 0, 0, 0, 0, 0 },
            new int[] { 1, 1, 1, 1, 0, 0, 0, 0, 0, 0 },
            new int[] { 1, 1, 1, 1, 0, 0, 0, 0, 0, 0 }
        };

        public static int[] BeltSlotsOpen = new int[] { 4, 4, 4, 4 };
        public static int TPScrolls = 20;

        public static IEnumerable<UnitAny> ItemsToStash = new HashSet<UnitAny>();
        public static IEnumerable<UnitAny> ItemsToTrash = new HashSet<UnitAny>();
        public static IEnumerable<UnitAny> ItemsToBelt = new HashSet<UnitAny>();

        public static bool AnyItemsToStash => ItemsToStash.Count() > 0;
        public static bool AnyItemsToTrash => ItemsToTrash.Count() > 0;
        public static bool AnyItemsToBelt => ItemsToBelt.Count() > 0;

        public static void Update(uint playerUnitId, HashSet<UnitAny> items)
        {
            for (var i = 0; i < 4; i++)
            {
                BeltSlotsOpen[i] = 4;
            }

            foreach (var line in items.Where(x => x.ItemData.dwOwnerID == playerUnitId && x.ItemData.InvPage == InvPage.NULL && x.ItemData.BodyLoc == BodyLoc.NONE)
                                    .GroupBy(x => x.X % 4)
                                    .Select(group => new {
                                        Slot = group.Key,
                                        Count = group.Count()
                                    })
                                    .OrderBy(x => x.Slot))
            {
                BeltSlotsOpen[line.Slot] = BeltSlotsOpen[line.Slot] - line.Count;
            }

            var inventoryItems = items.Where(x => x.ItemData.dwOwnerID == playerUnitId && x.ItemData.InvPage == InvPage.INVENTORY);

            var itemsToHandle = inventoryItems.Where(x => InventoryOpen[x.Y][x.X] == 1);

            ItemsToStash = inventoryItems.Where(x => InventoryOpen[x.Y][x.X] == 1 && LootFilter.Filter(x));
            ItemsToTrash = inventoryItems.Where(x => InventoryOpen[x.Y][x.X] == 1 && !LootFilter.Filter(x));
            ItemsToBelt = inventoryItems.Where(x => InventoryOpen[x.Y][x.X] == 1 && 
                (Items.ItemName(x.TxtFileNo) == "Full Rejuvenation Potion" || Items.ItemName(x.TxtFileNo) == "Rejuvenation Potion"));

            var tpTome = inventoryItems.Where(x => x.TxtFileNo == 518).FirstOrDefault() ?? new UnitAny(IntPtr.Zero);

            if (tpTome.IsValidUnit())
            {
                tpTome.Stats.TryGetValue(Stat.STAT_QUANTITY, out TPScrolls);
            }

            //foreach (var item in ItemsToStash)
            //{
            //    _log.Debug($"Gotta stash {item.TxtFileNo} from {item.X}/{item.Y}!");
            //}
        }

        public static int GetNextPotionSlotToUse()
        {
            var result = -1;

            for (var i = 0; i < 4; i++)
            {
                if (BeltSlotsOpen[i] < 4)
                {
                    result = i;
                    break;
                }
            }

            return result;
        }

        public static bool IsBeltFull()
        {
            return BeltSlotsOpen[0] == 0 && BeltSlotsOpen[1] == 0 && BeltSlotsOpen[2] == 0 && BeltSlotsOpen[3] == 0;
        }
    }
}
