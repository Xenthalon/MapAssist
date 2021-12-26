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

        public static int[] BeltSlotsOpen = new int[] { 4, 4, 4, 4 };

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
        }

        public static int GetNextPotionSlotToUse()
        {
            var result = -1;

            for (var i = 0; i < 4; i++)
            {
                if (BeltSlotsOpen[i] < 4)
                {
                    result = i + 1;
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
