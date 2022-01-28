namespace MapAssist.Automation
{
    public class BotConfiguration
    {
        public RunProfile[] RunProfiles { get; set; }

        public CharacterConfiguration Character { get; set; }

        public BotSettingsConfiguration Settings { get; set; }
    }

    public class CharacterConfiguration
    {
        public CombatSkill[] Skills { get; set; }
        public CombatSkill[] BuffSkills { get; set; }

        public bool HasTeleport { get; set; }
        public string KeyTeleport { get; set; }
        public bool UseCta { get; set; }
        public string KeyWeaponSwitch { get; set; }
        public string[] KeysPotion { get; set; }
        public double PotionLifePercent { get; set; }
        public double PotionMercLifePercent { get; set; }
        public int PotionWaitInterval { get; set; }
        public double RepairPercent { get; set; }
        public int GambleGoldStart { get; set; }
        public int GambleGoldStop { get; set; }
        public string GambleItem { get; set; }
        public string KeyInventory { get; set; }
        public int[][] Inventory { get; set; }
        public string KeyTownPortal { get; set; }
        public string KeyForceMove { get; set; }
    }

    public class BotSettingsConfiguration
    {
        public bool Autostart { get; set; }
        public int MaxGameLength { get; set; }
        public int MaxRetries { get; set; }
        public int DetectionRange { get; set; }
        public int CombatRange { get; set; }
        public int ChestRange { get; set; }
        public int TooCloseRange { get; set; }
        public int EscapeCooldown { get; set; }
        public int MaxAttackAttempts { get; set; }
        public int ShortSleep { get; set; }
        public int LongSleep { get; set; }
        public int GameChangeVerificationAttempts { get; set; }
        public int AreaChangeVerificationAttempts { get; set; }
        public int WindowX { get; set; }
        public int WindowY { get; set; }
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }
        public string WebApiUrl { get; set; }

        public PathingConfiguration Pathing;
    }

    public class PathingConfiguration
    {
        public int StinkSampleSize { get; set; }
        public int StinkRange { get; set; }
        public int StinkMaximum { get; set; }
        public double StinkMaxSaturation { get; set; }
        public int RangeInvalid { get; set; }
        public int RangeTp { get; set; }
        public int RangeBlock { get; set; }
    }
}
