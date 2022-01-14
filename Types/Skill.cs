/**
 *   Copyright (C) 2021 okaygo
 *
 *   https://github.com/misterokaygo/MapAssist/
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <https://www.gnu.org/licenses/>.
 **/

using MapAssist.Helpers;
using MapAssist.Interfaces;
using MapAssist.Structs;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MapAssist.Types
{
    public class Skills : IUpdatable<Skills>
    {
        private readonly IntPtr _pSkills;
        private Dictionary<Skill, SkillPoints> _allSkills;
        private Skill _rightSkillId;
        private Skill _leftSkillId;
        private Skill _usedSkillId;

        public Skills(IntPtr pSkills)
        {
            _pSkills = pSkills;
            Update();
        }

        public Skills Update()
        {
            using (var processContext = GameManager.GetProcessContext())
            {
                var skillList = processContext.Read<SkillList>(_pSkills);

                var skill = processContext.Read<SkillStrc>(skillList.pRightSkill);
                var skillTxt = processContext.Read<SkillTxt>(skill.SkillTxt);
                _rightSkillId = skillTxt.Id;

                skill = processContext.Read<SkillStrc>(skillList.pLeftSkill);
                skillTxt = processContext.Read<SkillTxt>(skill.SkillTxt);
                _leftSkillId = skillTxt.Id;

                skill = processContext.Read<SkillStrc>(skillList.pUsedSkill);
                skillTxt = processContext.Read<SkillTxt>(skill.SkillTxt);
                _usedSkillId = skillTxt.Id;

                var allSkills = new Dictionary<Skill, SkillPoints>();
                var skillPtr = skillList.pFirstSkill;
                while (true)
                {
                    skill = processContext.Read<SkillStrc>(skillPtr);
                    skillTxt = processContext.Read<SkillTxt>(skill.SkillTxt);
                    allSkills.Add(skillTxt.Id, new SkillPoints()
                    {
                        HardPoints = skill.HardPoints,
                        Quantity = skill.Quantity,
                        Charges = skill.Charges
                    });

                    skillPtr = skill.pNextSkill;
                    if (skillPtr == IntPtr.Zero) break;
                }

                _allSkills = allSkills;
            }

            return this;
        }

        public Dictionary<Skill, SkillPoints> AllSkills => _allSkills;
        public Skill RightSkillId => _rightSkillId;
        public Skill LeftSkillId => _leftSkillId;
        public Skill UsedSkillId => _usedSkillId;
    }

    public enum Skill : short
    {
        Unset = -1,
        Attack = 0,
        Kick,
        Throw,
        Unsummon,
        LeftHandThrow,
        LeftHandSwing,
        MagicArrow,
        FireArrow,
        InnerSight,
        CriticalStrike,
        Jab,
        ColdArrow,
        MultipleShot,
        Dodge,
        PowerStrike,
        PoisonJavelin,
        ExplodingArrow,
        SlowMissiles,
        Avoid,
        Impale,
        LightningBolt,
        IceArrow,
        GuidedArrow,
        Penetrate,
        ChargedStrike,
        PlagueJavelin,
        Strafe,
        ImmolationArrow,
        Dopplezon,
        Evade,
        Fend,
        FreezingArrow,
        Valkyrie,
        Pierce,
        LightningStrike,
        LightningFury,
        FireBolt,
        Warmth,
        ChargedBolt,
        IceBolt,
        FrozenArmor,
        Inferno,
        StaticField,
        Telekinesis,
        FrostNova,
        IceBlast,
        Blaze,
        FireBall,
        Nova,
        Lightning,
        ShiverArmor,
        FireWall,
        Enchant,
        ChainLightning,
        Teleport,
        GlacialSpike,
        Meteor,
        ThunderStorm,
        EnergyShield,
        Blizzard,
        ChillingArmor,
        FireMastery,
        Hydra,
        LightningMastery,
        FrozenOrb,
        ColdMastery,
        AmplifyDamage,
        Teeth,
        BoneArmor,
        SkeletonMastery,
        RaiseSkeleton,
        DimVision,
        Weaken,
        PoisonDagger,
        CorpseExplosion,
        ClayGolem,
        IronMaiden,
        Terror,
        BoneWall,
        GolemMastery,
        RaiseSkeletalMage,
        Confuse,
        LifeTap,
        PoisonExplosion,
        BoneSpear,
        BloodGolem,
        Attract,
        Decrepify,
        BonePrison,
        SummonResist,
        IronGolem,
        LowerResist,
        PoisonNova,
        BoneSpirit,
        FireGolem,
        Revive,
        Sacrifice,
        Smite,
        Might,
        Prayer,
        ResistFire,
        HolyBolt,
        HolyFire,
        Thorns,
        Defiance,
        ResistCold,
        Zeal,
        Charge,
        BlessedAim,
        Cleansing,
        ResistLightning,
        Vengeance,
        BlessedHammer,
        Concentration,
        HolyFreeze,
        Vigor,
        Conversion,
        HolyShield,
        HolyShock,
        Sanctuary,
        Meditation,
        FistOfTheHeavens,
        Fanaticism,
        Conviction,
        Redemption,
        Salvation,
        Bash,
        SwordMastery,
        AxeMastery,
        MaceMastery,
        Howl,
        FindPotion,
        Leap,
        DoubleSwing,
        PoleArmMastery,
        ThrowingMastery,
        SpearMastery,
        Taunt,
        Shout,
        Stun,
        DoubleThrow,
        IncreasedStamina,
        FindItem,
        LeapAttack,
        Concentrate,
        IronSkin,
        BattleCry,
        Frenzy,
        IncreasedSpeed,
        BattleOrders,
        GrimWard,
        Whirlwind,
        Berserk,
        NaturalResistance,
        WarCry,
        BattleCommand,
        FireHit,
        UnHolyBolt,
        SkeletonRaise,
        MaggotEgg,
        ShamanFire,
        MagottUp,
        MagottDown,
        MagottLay,
        AndrialSpray,
        Jump,
        SwarmMove,
        Nest,
        QuickStrike,
        VampireFireball,
        VampireFirewall,
        VampireMeteor,
        GargoyleTrap,
        SpiderLay,
        VampireHeal,
        VampireRaise,
        Submerge,
        FetishAura,
        FetishInferno,
        ZakarumHeal,
        Emerge,
        Resurrect,
        Bestow,
        MissileSkill1,
        MonTeleport,
        PrimeLightning,
        PrimeBolt,
        PrimeBlaze,
        PrimeFirewall,
        PrimeSpike,
        PrimeIceNova,
        PrimePoisonball,
        PrimePoisonNova,
        DiabLight,
        DiabCold,
        DiabFire,
        FingerMageSpider,
        DiabWall,
        DiabRun,
        DiabPrison,
        PoisonBallTrap,
        AndyPoisonBolt,
        HireableMissile,
        DesertTurret,
        ArcaneTower,
        MonBlizzard,
        Mosquito,
        CursedBallTrapRight,
        CursedBallTrapLeft,
        MonFrozenArmor,
        MonBoneArmor,
        MonBoneSpirit,
        MonCurseCast,
        HellMeteor,
        RegurgitatorEat,
        MonFrenzy,
        QueenDeath,
        ScrollOfIdentify,
        BookOfIdentify,
        ScrollOfTownportal,
        BookOfTownportal,
        Raven,
        PlaguePoppy,
        Wearwolf,
        ShapeShifting,
        Firestorm,
        OakSage,
        SummonSpiritWolf,
        Wearbear,
        MoltenBoulder,
        ArcticBlast,
        CycleOfLife,
        FeralRage,
        Maul,
        Eruption,
        CycloneArmor,
        HeartOfWolverine,
        SummonFenris,
        Rabies,
        FireClaws,
        Twister,
        Vines,
        Hunger,
        ShockWave,
        Volcano,
        Tornado,
        SpiritOfBarbs,
        SummonGrizzly,
        Fury,
        Armageddon,
        Hurricane,
        FireTrauma,
        ClawMastery,
        PsychicHammer,
        TigerStrike,
        DragonTalon,
        ShockField,
        BladeSentinel,
        Quickness,
        FistsOfFire,
        DragonClaw,
        ChargedBoltSentry,
        WakeOfFireSentry,
        WeaponBlock,
        CloakOfShadows,
        CobraStrike,
        BladeFury,
        Fade,
        ShadowWarrior,
        ClawsOfThunder,
        DragonTail,
        LightningSentry,
        InfernoSentry,
        MindBlast,
        BladesOfIce,
        DragonFlight,
        DeathSentry,
        BladeShield,
        Venom,
        ShadowMaster,
        RoyalStrike,
        WakeOfDestructionSentry,
        ImpInferno,
        ImpFireball,
        BaalTaunt,
        BaalCorpseExplode,
        BaalMonsterSpawn,
        CatapultChargedBall,
        CatapultSpikeBall,
        SuckBlood,
        CryHelp,
        HealingVortex,
        Teleport2,
        SelfResurrect,
        VineAttack,
        OverseerWhip,
        BarbsAura,
        WolverineAura,
        OakSageAura,
        ImpFireMissile,
        Impregnate,
        SiegeBeastStomp,
        MinionSpawner,
        CatapultBlizzard,
        CatapultPlague,
        CatapultMeteor,
        BoltSentry,
        CorpseCycler,
        DeathMaul,
        DefenseCurse,
        BloodMana,
        monInfernoSentry,
        monDeathSentry,
        sentryLightning,
        fenrisRage,
        BaalTentacle,
        BaalNova,
        BaalInferno,
        BaalColdMissiles,
        MegademonInferno,
        EvilHutSpawner,
        CountessFirewall,
        ImpBolt,
        HorrorArcticBlast,
        deathSentryLtng,
        VineCycler,
        BearSmite,
        Resurrect2,
        BloodLordFrenzy,
        BaalTeleport,
        ImpTeleport,
        BaalCloneTeleport,
        ZakarumLightning,
        VampireMissile,
        MephistoMissile,
        DoomKnightMissile,
        RogueMissile,
        HydraMissile,
        NecromageMissile,
        MonBow,
        MonFireArrow,
        MonColdArrow,
        MonExplodingArrow,
        MonFreezingArrow,
        MonPowerStrike,
        SuccubusBolt,
        MephFrostNova,
        MonIceSpear,
        ShamanIce,
        Diablogeddon,
        DeleriumChange,
        NihlathakCorpseExplosion,
        SerpentCharge,
        TrapNova,
        UnHolyBoltEx,
        ShamanFireEx,
        ImpFireMissileEx,
        FixedSiegeBeastStomp
    };

    public enum ClassTabs
    {
        AmazonBowAndCrossbow = 0,
        AmazonPassiveAndMagic = 1,
        AmazonJavelinAndSpear = 2,
        SorceressFire = 8,
        SorceressLightning = 9,
        SorceressCold = 10,
        NecromancerCurses = 16,
        NecromancerPoisonAndBone = 17,
        NecromancerSummoning = 18,
        PaladinCombatSkills = 24,
        PaladinOffensiveAuras = 25,
        PaladinDefensiveAuras = 26,
        BarbarianCombatSkills = 32,
        BarbarianMasteries = 33,
        BarbarianWarcries = 34,
        DruidSummoning = 40,
        DruidShapeShifting = 41,
        DruidElemental = 42,
        AssassinTraps = 48,
        AssassinShadowDisciplines = 49,
        AssassinMartialArts = 50,
    }

    public class SkillPoints
    {
        public uint HardPoints;
        public uint Quantity;
        public uint Charges;
    }

    public static class SkillExtensions
    {
        public static string Name(this ClassTabs classTab)
        {
            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
            return string.Concat(classTab.ToString().Select((x, j) => j > 0 && char.IsUpper(x) ? " " + x.ToString() : x.ToString()));
        }

        public static string Name(this Skill skill)
        {
            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
            return string.Concat(skill.ToString().Select((x, j) => j > 0 && char.IsUpper(x) ? " " + x.ToString() : x.ToString()));
        }
    }
}
