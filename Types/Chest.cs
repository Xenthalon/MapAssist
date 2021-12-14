using System;
using System.Collections.Generic;

namespace MapAssist.Types
{
    class Chest
    {
        public static List<string> Types = new List<string> {
            "chest", "Chest3", "coffin", "stash", "Casket", "skeleton", "corpse", "GuardCorpse",
            "Sarcophagus", "hidden stash", "skull pile", "loose rock", "loose boulder", "hollow log",
            "cocoon", "ratnest", "goo pile", "dead guard", "dead body", "BoneChest", "ChestL", "chestR",
            "ChestSL", "ChestSR", "woodchestL", "woodchestR", "woodchest2L", "woodchest2R", "hiddenstash",
            "burialchestL", "burialchestR", "mrbox", "tomb1L", "tomb2L", "tomb3L", "tomb1", "tomb2", "tomb3",
            "groundtombL", "deadperson2"
        };

        [Flags]
        public enum InteractFlags
        {
            None = 0x00,
            Trap = 0x04,
            Locked = 0x80
        }
    }
}
