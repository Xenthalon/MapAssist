using MapAssist.Automation.Identification;
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
        private static readonly double REPAIR_THRESHOLD = 0.3;
        private static readonly int GAMBLE_START_AT = 2000000;

        private static bool _needsRepair = false;

        public static int[][] InventoryOpen = new int[][] {
            new int[] { 1, 1, 1, 1, 0, 0, 0, 0, 0, 0 },
            new int[] { 1, 1, 1, 1, 0, 0, 0, 0, 0, 0 },
            new int[] { 1, 1, 1, 1, 0, 0, 0, 0, 0, 0 },
            new int[] { 1, 1, 1, 1, 0, 0, 0, 0, 0, 0 }
        };

        public static int[] BeltSlotsOpen = new int[] { 4, 4, 4, 4 };
        public static int TPScrolls = 20;
        public static int Gold = 0;
        public static int Freespace = 0;
        public static UnitAny IDScroll = new UnitAny(IntPtr.Zero);
        public static bool NeedsRepair => _needsRepair;
        public static bool NeedsGamble => Gold > GAMBLE_START_AT;

        public static IEnumerable<UnitAny> ItemsToStash = new HashSet<UnitAny>();
        public static IEnumerable<UnitAny> ItemsToIdentify = new HashSet<UnitAny>();
        public static IEnumerable<UnitAny> ItemsToTrash = new HashSet<UnitAny>();
        public static IEnumerable<UnitAny> ItemsToBelt = new HashSet<UnitAny>();

        public static bool AnyItemsToStash => ItemsToStash.Count() > 0;
        public static bool AnyItemsToIdentify => ItemsToIdentify.Count() > 0;
        public static bool AnyItemsToTrash => ItemsToTrash.Count() > 0;
        public static bool AnyItemsToBelt => ItemsToBelt.Count() > 0;

        public static void Update(UnitAny playerUnit, HashSet<UnitAny> items)
        {
            var playerUnitId = playerUnit.UnitId;

            var equippedItems = items.Where(x => x.ItemData.dwOwnerID == playerUnitId && x.ItemData.InvPage == InvPage.NULL && x.ItemData.BodyLoc != BodyLoc.NONE);

            _needsRepair = false;
            foreach (var item in equippedItems)
            {
                var durability = -1;
                var maxDurability = -1;

                item.Stats.TryGetValue(Stat.Durability, out durability);
                item.Stats.TryGetValue(Stat.MaxDurability, out maxDurability);

                if (durability > -1 && maxDurability > -1 &&
                    durability/(double)maxDurability < REPAIR_THRESHOLD)
                {
                    _needsRepair = true;
                    break;
                }
            }

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

            ItemsToStash = inventoryItems.Where(x => InventoryOpen[x.Y][x.X] == 1 && LootFilter.Filter(x).Item1);
            ItemsToIdentify = ItemsToStash.Where(x => (x.ItemData.ItemFlags & ItemFlags.IFLAG_IDENTIFIED) != ItemFlags.IFLAG_IDENTIFIED && IdentificationFilter.HasEntry(x));
            ItemsToTrash = inventoryItems.Where(x => InventoryOpen[x.Y][x.X] == 1 && !LootFilter.Filter(x).Item1);
            ItemsToBelt = inventoryItems.Where(x => InventoryOpen[x.Y][x.X] == 1 && 
                (Items.ItemName(x.TxtFileNo) == "Full Rejuvenation Potion" || Items.ItemName(x.TxtFileNo) == "Rejuvenation Potion"));

            var tpTome = inventoryItems.Where(x => x.TxtFileNo == 518).FirstOrDefault() ?? new UnitAny(IntPtr.Zero);

            if (tpTome.IsValidUnit())
            {
                tpTome.Stats.TryGetValue(Stat.Quantity, out TPScrolls);
            }

            IDScroll = inventoryItems.Where(x => x.TxtFileNo == 530).FirstOrDefault() ?? new UnitAny(IntPtr.Zero);

            playerUnit.Stats.TryGetValue(Stat.StashGold, out Gold);

            var free = 0;

            foreach (var line in InventoryOpen)
            {
                foreach (var element in line)
                {
                    if (element == 1)
                    {
                        free += 1;
                    }
                }
            }

            foreach (var item in ItemsToStash)
            {
                var size = GetItemSize(item);

                free -= size.width * size.height;
            }

            if (free != Freespace)
            {
                Freespace = free;
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

        public static int GetItemTotalSize(UnitAny item)
        {
            var size = GetItemSize(item);

            return size.height * size.width;
        }

        private static (int width, int height) GetItemSize(UnitAny item)
        {
            var itemName = Items.ItemName(item.TxtFileNo);

            if (itemName == "Ring")
            {
                return (1, 1);
            }
            else if (itemName == "Amulet")
            {
                return (1, 1);
            }

            // TODO: get weapon, armor and misc sizes from .txt files ugh

            return (1, 1);
        }
    }
}
