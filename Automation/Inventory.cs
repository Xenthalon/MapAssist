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

        private readonly double REPAIR_THRESHOLD;
        private readonly int GAMBLE_START_AT;
        private int[][] InventoryOpen;

        private bool _needsRepair = false;
        private int _freeSpace = 0;
        private int _gold = 0;

        public int[] BeltSlotsOpen = new int[] { 4, 4, 4, 4 };
        public int TPScrolls = 20;
        public int Gold => _gold;
        public int Freespace => _freeSpace;
        public UnitAny IDScroll = new UnitItem(IntPtr.Zero);
        public bool NeedsRepair => _needsRepair;
        public bool NeedsGamble => Gold > GAMBLE_START_AT;

        public IEnumerable<UnitItem> ItemsToStash = new HashSet<UnitItem>();
        public IEnumerable<UnitItem> ItemsToIdentify = new HashSet<UnitItem>();
        public IEnumerable<UnitItem> ItemsToTrash = new HashSet<UnitItem>();
        public IEnumerable<UnitItem> ItemsToBelt = new HashSet<UnitItem>();

        public bool AnyItemsToStash => ItemsToStash.Count() > 0;
        public bool AnyItemsToIdentify => ItemsToIdentify.Count() > 0;
        public bool AnyItemsToTrash => ItemsToTrash.Count() > 0;
        public bool AnyItemsToBelt => ItemsToBelt.Count() > 0;

        public Inventory(BotConfiguration config)
        {
            REPAIR_THRESHOLD = config.Character.RepairPercent / 100;
            GAMBLE_START_AT = config.Character.GambleGoldStart;
            InventoryOpen = config.Character.Inventory;
        }

        public void Update(GameData gameData)
        {
            var playerUnit = gameData.PlayerUnit;
            var playerUnitId = playerUnit.UnitId;
            var items = gameData.AllItems;

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
            ItemsToIdentify = ItemsToStash.Where(x => !x.IsIdentified && IdentificationFilter.HasEntry(x));
            ItemsToTrash = inventoryItems.Where(x => InventoryOpen[x.Y][x.X] == 1 && !LootFilter.Filter(x).Item1);
            ItemsToBelt = inventoryItems.Where(x => InventoryOpen[x.Y][x.X] == 1 && 
                (x.ItemBaseName == "Full Rejuvenation Potion" || x.ItemBaseName == "Rejuvenation Potion"));

            var tpTome = inventoryItems.Where(x => x.TxtFileNo == 518).FirstOrDefault() ?? new UnitItem(IntPtr.Zero);

            if (tpTome.IsValidUnit)
            {
                tpTome.Stats.TryGetValue(Stat.Quantity, out TPScrolls);
            }

            IDScroll = inventoryItems.Where(x => x.TxtFileNo == 530).FirstOrDefault() ?? new UnitItem(IntPtr.Zero);

            playerUnit.Stats.TryGetValue(Stat.StashGold, out _gold);

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

            foreach (var item in ItemsToTrash)
            {
                var size = GetItemSize(item);

                free -= size.width * size.height;
            }

            if (free != _freeSpace)
            {
                _freeSpace = free;
            }

            //foreach (var item in ItemsToStash)
            //{
            //    _log.Debug($"Gotta stash {item.TxtFileNo} from {item.X}/{item.Y}!");
            //}
        }

        public int GetNextPotionSlotToUse()
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

        public bool IsBeltFull()
        {
            return BeltSlotsOpen[0] == 0 && BeltSlotsOpen[1] == 0 && BeltSlotsOpen[2] == 0 && BeltSlotsOpen[3] == 0;
        }

        public int GetItemTotalSize(UnitItem item)
        {
            var size = GetItemSize(item);

            return size.height * size.width;
        }

        private (int width, int height) GetItemSize(UnitItem item)
        {
            var itemName = item.ItemBaseName;

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
