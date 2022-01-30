using GameOverlay.Drawing;
using MapAssist.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace MapAssist.Automation
{
    class Combat
    {
        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();

        private short DETECTION_RANGE_DEFAULT;
        private short DETECTION_RANGE;
        private short COMBAT_RANGE_DEFAULT;
        private short COMBAT_RANGE;
        private short TOO_CLOSE_RANGE;
        private short MAX_ATTACK_ATTEMPTS;
        private int REPOSITION_COOLDOWN;
        private bool HAS_TELEPORT = false;
        private short CHEST_RANGE;
        private int SLEEP_SHORT;
        private int SLEEP_LONG;
        private List<CombatSkill> COMBAT_SKILLS = new List<CombatSkill>();

        private BackgroundWorker _combatWorker;
        private bool _fighting = false;
        private Chicken _chicken;
        private Input _input;
        private Movement _movement;
        private Pathing _pathing;
        private bool _defenseActive = false;
        private bool _reposition = true;
        private Point _playerPosition;
        private UnitPlayer _playerUnit;
        private HashSet<UnitMonster> _monsters;
        private IEnumerable<UnitObject> _chests;
        private UnitMonster _target;
        private Point _areaToClear = new Point(0, 0);
        private List<uint> _blacklist = new List<uint>();
        private int _attackAttempts = 0;
        private int _targetLastHealth = int.MaxValue;
        private long _lastReposition = 0;

        public bool Busy => _fighting;
        public int GroupSize { get; set; } = 1;
        public bool HasTeleport => HAS_TELEPORT;
        public bool HuntBosses { get; set; } = false;

        public bool IsSafe => !Busy && !_combatWorker.IsBusy &&
                            ((HuntBosses && !_monsters.Any(x => IsBoss(x) &&
                                                                !_blacklist.Contains(x.UnitId) &&
                                                                Automaton.GetDistance(_playerPosition, x.Position) <= COMBAT_RANGE) &&
                                            _monsters.Where(x => !_blacklist.Contains(x.UnitId) &&
                                                                 (MonsterFilter.Count == 0 || MonsterFilter.Contains((Npc)x.TxtFileNo)) &&
                                                                 Automaton.GetDistance(_playerPosition, x.Position) <= COMBAT_RANGE)
                                                     .Count() < GroupSize) ||
                             (!HuntBosses && _monsters.Where(x => !_blacklist.Contains(x.UnitId) &&
                                                                 (MonsterFilter.Count == 0 || MonsterFilter.Contains((Npc)x.TxtFileNo)) &&
                                                                 Automaton.GetDistance(_playerPosition, x.Position) <= COMBAT_RANGE)
                                                     .Count() < GroupSize));
        // maybe we can trim the second condition
        public List<Npc> MonsterFilter = new List<Npc>();
        public bool OpenChests { get; set; } = false;


        public Combat(BotConfiguration config, Chicken chicken, Input input, Movement movement, Pathing pathing)
        {
            COMBAT_RANGE = (short)config.Settings.CombatRange;
            COMBAT_RANGE_DEFAULT = (short)config.Settings.CombatRange;
            DETECTION_RANGE = (short)config.Settings.DetectionRange;
            DETECTION_RANGE_DEFAULT = (short)config.Settings.DetectionRange;
            TOO_CLOSE_RANGE = (short)config.Settings.TooCloseRange;
            MAX_ATTACK_ATTEMPTS = (short)config.Settings.MaxAttackAttempts;
            REPOSITION_COOLDOWN = config.Settings.EscapeCooldown;
            CHEST_RANGE = (short)config.Settings.ChestRange;
            HAS_TELEPORT = config.Character.HasTeleport;
            SLEEP_SHORT = config.Settings.ShortSleep;
            SLEEP_LONG = config.Settings.LongSleep;

            COMBAT_SKILLS.AddRange(config.Character.Skills);

            _chicken = chicken;
            _input = input;
            _movement = movement;
            _pathing = pathing;

            _combatWorker = new BackgroundWorker();
            _combatWorker.DoWork += new DoWorkEventHandler(Fight);
            _combatWorker.WorkerSupportsCancellation = true;

            _target = new UnitMonster(IntPtr.Zero);
        }

        public void Kill(string name, bool clearArea = false, bool reposition = true)
        {
            foreach (var monster in _monsters.Where(x => (x.MonsterData.MonsterType & Structs.MonsterTypeFlags.SuperUnique) == Structs.MonsterTypeFlags.SuperUnique))
            {
                var internalName = Types.NPC.SuperUniques.Where(y => y.Value == monster.MonsterStats.Name).First().Key;
                _log.Info("Type: " + internalName);
                var localizedName = NpcExtensions.LocalizedName(internalName);
                _log.Info("Resolved: " + localizedName);

                if (localizedName.Contains(name))
                {
                    _log.Info("Found " + name + ": " + monster.UnitId);

                    if (!_fighting && !_combatWorker.IsBusy)
                    {
                        _target = monster;

                        if (clearArea)
                        {
                            _log.Info("Set areaToClear in Kill");
                            _areaToClear = monster.Position;
                        }

                        _fighting = true;
                        _reposition = reposition;
                        _combatWorker.RunWorkerAsync();
                    }
                }
            }
        }

        public void Kill(uint unitTxtFileNo, bool clearArea = false, bool reposition = true)
        {
            if (!_fighting && !_combatWorker.IsBusy &&
                _monsters.Any(x => x.TxtFileNo == unitTxtFileNo))
            {
                _target = _monsters.Where(x => x.TxtFileNo == unitTxtFileNo).First();

                if (clearArea)
                {
                    _log.Info("Set areaToClear in Kill2");
                    _areaToClear = _target.Position;
                }

                _fighting = true;
                _reposition = reposition;
                _combatWorker.RunWorkerAsync();
            }
        }

        public void ClearArea(Point location, bool reposition = true)
        {
            if (!_fighting && !_combatWorker.IsBusy &&
                    ((HuntBosses && _monsters.Any(x => IsBoss(x) &&
                                                    !_blacklist.Contains(x.UnitId) &&
                                                    ((reposition && Automaton.GetDistance(_playerPosition, x.Position) <= DETECTION_RANGE) ||
                                                    (!reposition && Automaton.GetDistance(_playerPosition, x.Position) <= COMBAT_RANGE)))) ||
                    _monsters.Where(x => !_blacklist.Contains(x.UnitId) &&
                                        (MonsterFilter.Count == 0 || MonsterFilter.Contains((Npc)x.TxtFileNo)) &&
                                        ((reposition && Automaton.GetDistance(_playerPosition, x.Position) <= DETECTION_RANGE) ||
                                        (!reposition && Automaton.GetDistance(_playerPosition, x.Position) <= COMBAT_RANGE)))
                            .Count() >= GroupSize))
            {
                _log.Info($"Clearing baddies around {location.X}/{location.Y}.");
                _areaToClear = location;
                _fighting = true;
                _reposition = reposition;
                _combatWorker.RunWorkerAsync();
            }
            else if (_fighting)
            {
                // emergency abort
                _fighting = false;
            }
        }

        public void CheckChests()
        {
            if (!OpenChests)
                return;

            if (IsSafe)
            {
                var interactRange = 5.0;

                var telekinesis = COMBAT_SKILLS.Where(x => x.IsTelekinesis).FirstOrDefault();

                if (telekinesis != null)
                {
                    interactRange = telekinesis.MaxRange;
                }

                foreach (var chest in _chests.Where(x => Automaton.GetDistance(x.Position, _playerPosition) <= CHEST_RANGE))
                {
                    if (Automaton.GetDistance(chest.Position, _playerPosition) > interactRange)
                    {
                        GetInLOSRange(chest.Position, 1, (short)(interactRange));
                    }

                    if (telekinesis != null)
                    {
                        _input.DoInputAtWorldPosition(telekinesis.Key, chest.Position);
                        telekinesis.LastUsage = Now;
                        System.Threading.Thread.Sleep(telekinesis.Cooldown);
                    }
                    else
                    {
                        _input.DoInputAtWorldPosition("{LMB}", chest.Position);
                        System.Threading.Thread.Sleep(SLEEP_LONG * 2);
                    }

                    _log.Info("Opened chest " + chest.UnitId + ".");
                }
            }
            else
            {
                _log.Info("Too dangerous to look for chests!");
            }
        }

        /// <summary>
        /// Activates combat aura or things like venom maybe?
        /// </summary>
        public void PrepareForCombat()
        {
            if (COMBAT_SKILLS.Any(x => x.IsAura && x.IsMainSkill))
            {
                var combatAura = COMBAT_SKILLS.Where(x => x.IsAura && x.IsMainSkill).First();

                if (!_playerUnit.StateList.Contains(combatAura.BuffState))
                {
                    _input.DoInputAtWorldPosition(combatAura.Key, _playerPosition);
                    System.Threading.Thread.Sleep(SLEEP_LONG);
                }
            }

            _defenseActive = false;
        }

        /// <summary>
        /// Activates town buffs, like Vigor, or assassin movement buffs maybe?
        /// </summary>
        public void PrepareForTown()
        {
            if (COMBAT_SKILLS.Any(x => x.IsAura && x.BuffState == State.STATE_STAMINA))
            {
                var vigorAura = COMBAT_SKILLS.Where(x => x.IsAura && x.BuffState == State.STATE_STAMINA).First();

                if (!_playerUnit.StateList.Contains(vigorAura.BuffState))
                {
                    _input.DoInputAtWorldPosition(vigorAura.Key, _playerPosition);
                    System.Threading.Thread.Sleep(SLEEP_LONG);
                }
            }
        }

        /// <summary>
        /// Selects a mitigation strategy against a specified element if possible
        /// </summary>
        /// <param name="resist"></param>
        public void DefendAgainst(Resist resist)
        {
            CombatSkill mitigationSkill = null;

            if ((resist == Resist.FIRE || resist == Resist.COLD || resist == Resist.LIGHTNING || resist == Resist.POISON) &&
                COMBAT_SKILLS.Any(x => x.IsAura && x.BuffState == State.STATE_RESISTALL))
            {
                mitigationSkill = COMBAT_SKILLS.Where(x => x.IsAura && x.BuffState == State.STATE_RESISTALL).First();
            }

            if (resist == Resist.FIRE &&
                COMBAT_SKILLS.Any(x => x.IsAura && x.BuffState == State.STATE_RESISTFIRE))
            {
                mitigationSkill = COMBAT_SKILLS.Where(x => x.IsAura && x.BuffState == State.STATE_RESISTFIRE).First();
            }

            if (resist == Resist.COLD &&
                COMBAT_SKILLS.Any(x => x.IsAura && x.BuffState == State.STATE_RESISTCOLD))
            {
                mitigationSkill = COMBAT_SKILLS.Where(x => x.IsAura && x.BuffState == State.STATE_RESISTCOLD).First();
            }

            if (resist == Resist.LIGHTNING &&
                COMBAT_SKILLS.Any(x => x.IsAura && x.BuffState == State.STATE_RESISTLIGHTNING))
            {
                mitigationSkill = COMBAT_SKILLS.Where(x => x.IsAura && x.BuffState == State.STATE_RESISTLIGHTNING).First();
            }

            if (mitigationSkill != null && !_playerUnit.StateList.Contains(mitigationSkill.BuffState))
            {
                _input.DoInputAtWorldPosition(mitigationSkill.Key, _playerPosition);
                System.Threading.Thread.Sleep(SLEEP_LONG);
                _defenseActive = true;
            }
        }

        /// <summary>
        /// Use Redemption or other skills to heal up.
        /// </summary>
        public void HealUp()
        {
            if (COMBAT_SKILLS.Any(x => x.IsAura && x.BuffState == State.STATE_REDEMPTION) &&
                _chicken.PlayerLifePercentage < 0.7)
            {
                var lastPercentage = _chicken.PlayerLifePercentage;

                var redemption = COMBAT_SKILLS.Where(x => x.IsAura && x.BuffState == State.STATE_REDEMPTION).First();

                _input.DoInputAtWorldPosition(redemption.Key, _playerPosition);

                do
                {
                    System.Threading.Thread.Sleep(300);
                }
                while (_chicken.PlayerLifePercentage == lastPercentage);
            }
        }

        /// <summary>
        /// Use Cleansing or other skills to get rid of status effects.
        /// </summary>
        public void RemoveDebuffs()
        {
            if (COMBAT_SKILLS.Any(x => x.IsAura && x.BuffState == State.STATE_CLEANSING) &&
                _playerUnit.StateList.Any(x => States.DebuffStates.Contains(x)))
            {
                var cleansing = COMBAT_SKILLS.Where(x => x.IsAura && x.BuffState == State.STATE_CLEANSING).First();

                _input.DoInputAtWorldPosition(cleansing.Key, _playerPosition);

                _log.Info("Waiting for curses to lift");

                do
                {
                    System.Threading.Thread.Sleep(100);
                }
                while (_playerUnit.StateList.Any(x => States.DebuffStates.Contains(x)));
            }
        }

        public void PrecastMainSkillForAt(int duration, Point worldPosition)
        {
            if (COMBAT_SKILLS.Any(x => x.IsMainSkill))
            {
                var skill = COMBAT_SKILLS.Where(x => x.IsMainSkill).First();

                var start = Now;

                do
                {
                    _input.DoInputAtWorldPosition(skill.Key, worldPosition);

                    System.Threading.Thread.Sleep(skill.Cooldown);
                }
                while (Now - start < duration);
            }
        }

        public void Update(GameData gameData)
        {
            if (gameData != null && gameData.PlayerUnit.IsValidPointer && gameData.PlayerUnit.IsValidUnit)
            {
                _chests = gameData.Objects.Where(x => x.IsChest &&
                                                (x.ObjectData.InteractType & ((byte)Chest.InteractFlags.Locked)) == ((byte)Chest.InteractFlags.None)); // only non-locked chests
                _monsters = gameData.Monsters.ToHashSet();
                _playerPosition = gameData.PlayerPosition;
                _playerUnit = gameData.PlayerUnit;

                if (_target != null && _target.IsValidPointer)
                {
                    _target = _monsters.Where(x => x.UnitId == _target.UnitId).FirstOrDefault() ?? new UnitMonster(IntPtr.Zero);
                    //_target = _monsters.Where(x => x.UnitId == _target.UnitId).Select(x => x.Update()).FirstOrDefault() ?? new UnitMonster(IntPtr.Zero);

                    if (_target == null || !_target.IsValidPointer)
                    {
                        _log.Info("Killed!");
                        _log.Info("Area to clear: " + _areaToClear);

                        if (_areaToClear.X == 0 && _areaToClear.Y == 0)
                        {
                            _fighting = false;
                        }
                    }
                }

                if (_chicken.Dead)
                {
                    Reset();
                }

                if (_fighting && !_combatWorker.IsBusy)
                {
                    _combatWorker.RunWorkerAsync();
                }
            }
        }

        public void SetCombatRange(short range)
        {
            COMBAT_RANGE = range;
            DETECTION_RANGE = range;
        }

        public void Reset()
        {
            _fighting = false;
            _defenseActive = false;
            _areaToClear = new Point(0, 0);
            _reposition = true;
            _lastReposition = 0;
            _blacklist = new List<uint>();
            _monsters = new HashSet<UnitMonster>();
            _chests = new List<UnitObject>();
            _attackAttempts = 0;
            _targetLastHealth = int.MaxValue;
            _target = new UnitMonster(IntPtr.Zero);
            _combatWorker.CancelAsync();
            HuntBosses = false;
            GroupSize = 1;
            OpenChests = false;
            MonsterFilter = new List<Npc>();
            COMBAT_RANGE = COMBAT_RANGE_DEFAULT;
            DETECTION_RANGE = DETECTION_RANGE_DEFAULT;
        }

        private void Fight(object sender, DoWorkEventArgs e)
        {
            if (_areaToClear.X != 0 && _areaToClear.Y != 0 && (_target == null || !_target.IsValidPointer))
            {
                var monstersInArea = _monsters.Where(x => !_blacklist.Contains(x.UnitId) &&
                        (MonsterFilter.Count == 0 || MonsterFilter.Contains((Npc)x.TxtFileNo)) &&
                        ((_reposition && Automaton.GetDistance(_areaToClear, x.Position) <= DETECTION_RANGE) ||
                        (!_reposition && Automaton.GetDistance(_areaToClear, x.Position) <= COMBAT_RANGE)));

                if ((HuntBosses && monstersInArea.Any(x => IsBoss(x))) || monstersInArea.Count() >= GroupSize)
                {
                    _fighting = true;

                    if (!_defenseActive)
                        PrepareForCombat();

                    _target = GetNextVictim(monstersInArea);
                }
                else
                {
                    _log.Info("Killed them all!");
                    _fighting = false;
                    _areaToClear = new Point(0, 0);
                    _reposition = true;
                }
            }

            if (_target != null && _target.IsValidPointer)
            {
                var castLocation = new Point(_target.Position.X, _target.Position.Y);

                if (_target.Struct.Mode != 0 && _target.Struct.Mode != 12) // if not dying or dead
                {
                    var targetLife = 0;

                    _target.Stats.TryGetValue(Stat.Life, out targetLife);

                    if (targetLife < _targetLastHealth)
                    {
                        _targetLastHealth = targetLife;
                        _attackAttempts = 0;
                    }

                    var targetLifePercentage = targetLife / 32768.0;

                    if (COMBAT_SKILLS.Any(x => x.IsAoe && Now - x.LastUsage > x.Cooldown))
                    {
                        CombatSkill skillToUse = COMBAT_SKILLS.Where(x => x.IsAoe && Now - x.LastUsage > x.Cooldown).First();
                        System.Threading.Thread.Sleep(SLEEP_SHORT);
                        _input.DoInputAtWorldPosition(skillToUse.Key, castLocation);
                        skillToUse.LastUsage = Now;
                        System.Threading.Thread.Sleep(SLEEP_SHORT);
                    }

                    if ((_target.TxtFileNo == (uint)Npc.Mephisto || _target.TxtFileNo == (uint)Npc.Diablo || _target.TxtFileNo == (uint)Npc.BaalThrone) &&
                        targetLifePercentage > 0.6 &&
                        COMBAT_SKILLS.Any(x => x.IsStatic))
                    {
                        CombatSkill staticSkill = COMBAT_SKILLS.Where(x => x.IsStatic && Now - x.LastUsage > x.Cooldown).FirstOrDefault();

                        // means static is still on cooldown
                        if (staticSkill != null)
                        {
                            if (Automaton.GetDistance(_target.Position, _playerPosition) > staticSkill.MaxRange || !_pathing.HasLineOfSight(_playerPosition, _target.Position))
                            {
                                _log.Info("Want to use " + staticSkill.Name + ", lets get closer.");
                                GetInLOSRange(_target.Position, 1, (short)(staticSkill.MaxRange - 1));
                            }

                            _input.DoInput(staticSkill.Key);
                            staticSkill.LastUsage = Now;
                            System.Threading.Thread.Sleep(SLEEP_SHORT);
                        }
                    }
                    else if (COMBAT_SKILLS.Any(x => x.IsRanged && !x.IsAoe && !x.IsStatic && !x.IsTelekinesis && !x.IsAura && (_target.Immunities == null || !_target.Immunities.Contains(x.DamageType))))
                    {
                        // ranged combat
                        CombatSkill skillToUse = COMBAT_SKILLS.Where(x => x.IsRanged && !x.IsAoe && !x.IsStatic && !x.IsTelekinesis && !x.IsAura && (_target.Immunities == null || !_target.Immunities.Contains(x.DamageType))).First();
                        
                        if (_reposition &&
                            Automaton.GetDistance(_target.Position, _playerPosition) < TOO_CLOSE_RANGE &&
                            Now - _lastReposition > REPOSITION_COOLDOWN)
                        {
                            _log.Info("This is a bit personal, lets get away.");
                            GetInLOSRange(_target.Position, TOO_CLOSE_RANGE, (short)(skillToUse.MaxRange - 1));
                            _lastReposition = Now;
                        }

                        if (Now - skillToUse.LastUsage > skillToUse.Cooldown)
                        {
                            Attack(skillToUse, castLocation);
                            _attackAttempts += 1;
                        }
                    }
                    else if (COMBAT_SKILLS.Any(x => !x.IsRanged && !x.IsAoe && !x.IsStatic && !x.IsTelekinesis && !x.IsAura && (_target.Immunities == null || !_target.Immunities.Contains(x.DamageType))))
                    {
                        // melee combat
                        CombatSkill skillToUse = COMBAT_SKILLS.Where(x => !x.IsRanged && !x.IsAoe && !x.IsStatic && !x.IsTelekinesis && !x.IsAura && (_target.Immunities == null || !_target.Immunities.Contains(x.DamageType))).First();

                        if (Now - skillToUse.LastUsage > skillToUse.Cooldown)
                        {
                            AttackFor(1000, skillToUse, castLocation);
                            _attackAttempts += 3;
                        }
                    }
                }

                if (_attackAttempts >= MAX_ATTACK_ATTEMPTS)
                {
                    _blacklist.Add(_target.UnitId);
                    _target = new UnitMonster(IntPtr.Zero);
                    _attackAttempts = 0;
                }
                //else
                //{
                //    _target.IsCached = false;
                //    _target = _target.Update();
                //}
            }

            //if (_areaToClear.X == 0 && _areaToClear.Y == 0 && (_target == null || !_target.IsValidPointer))
            //{
            //    _log.Info("Dead!");
            //    _fighting = false;
            //}
        }

        private void AttackFor(int milliseconds, CombatSkill skill, Point worldPosition)
        {
            var end = Now + milliseconds;

            while (Now < end)
            {
                Attack(skill, worldPosition);
            }
        }

        private void Attack(CombatSkill skill, Point worldPosition)
        {
            if (_reposition &&
                ((Automaton.GetDistance(worldPosition, _playerPosition) > skill.MaxRange && Now - _lastReposition > REPOSITION_COOLDOWN) ||
                ((skill.IsAoe || skill.IsRanged) && !_pathing.HasLineOfSight(_playerPosition, worldPosition))))
            {
                _log.Info("Want to use " + skill.Name + ", lets get closer.");
                GetInLOSRange(worldPosition, TOO_CLOSE_RANGE, skill.MaxRange);
                _log.Info("Arrived!");
                _lastReposition = Now;
            }

            _log.Info("Casting " + skill.Name + " at " + worldPosition);
            _input.DoInputAtWorldPosition(skill.Key, worldPosition);
            skill.LastUsage = Now;
            System.Threading.Thread.Sleep(SLEEP_SHORT);
        }

        private void GetInLOSRange(Point target, short minRange, short maxRange)
        {
            System.Threading.Thread.Sleep(SLEEP_LONG);
            _movement.GetInLOSRange(target, minRange, maxRange - 1, HAS_TELEPORT);
            System.Threading.Thread.Sleep(SLEEP_LONG);
        }

        private UnitMonster GetNextVictim(IEnumerable<UnitMonster> monsters)
        {
            _log.Debug("Looking for next victim.");

            _targetLastHealth = int.MaxValue;
            var mainSkill = COMBAT_SKILLS.Where(x => x.IsMainSkill).FirstOrDefault();
            var fallbackSkill = COMBAT_SKILLS.Where(x => !x.IsMainSkill && !x.IsAoe && !x.IsBuff && !x.IsStatic).FirstOrDefault();
            var victim = new UnitMonster(IntPtr.Zero);

            var supers = monsters.Where(x => (x.MonsterData.MonsterType & Structs.MonsterTypeFlags.SuperUnique) == Structs.MonsterTypeFlags.SuperUnique ||
                                            (x.MonsterData.MonsterType & Structs.MonsterTypeFlags.Unique) == Structs.MonsterTypeFlags.Unique ||
                                            (x.MonsterData.MonsterType & Structs.MonsterTypeFlags.Champion) == Structs.MonsterTypeFlags.Champion ||
                                            (x.MonsterData.MonsterType & Structs.MonsterTypeFlags.Ghostly) == Structs.MonsterTypeFlags.Ghostly ||
                                            (x.MonsterData.MonsterType & Structs.MonsterTypeFlags.Possessed) == Structs.MonsterTypeFlags.Possessed);

            // get closest super not immune to us with line of sight
            if (supers.Any(x => !x.Immunities.Contains(mainSkill.DamageType) && _pathing.HasLineOfSight(_playerPosition, x.Position)))
            {
                victim = supers.Where(x => !x.Immunities.Contains(mainSkill.DamageType) && _pathing.HasLineOfSight(_playerPosition, x.Position))
                                .OrderBy(x => Automaton.GetDistance(_playerPosition, x.Position)).First();
            }

            // get closest priority enemy (Shamans, Defilers, Spawners etc) not immune to us with line of sight
            if (!victim.IsValidPointer)
            {
                victim = monsters.Where(x => !x.Immunities.Contains(mainSkill.DamageType) && PriorityEnemies.Contains((Npc)x.TxtFileNo) && _pathing.HasLineOfSight(_playerPosition, x.Position))
                                .OrderBy(x => Automaton.GetDistance(_playerPosition, x.Position)).FirstOrDefault() ?? new UnitMonster(IntPtr.Zero);
            }

            // get closest enemy not immune to us with line of sight
            if (!victim.IsValidPointer)
            { 
                victim = monsters.Where(x => !x.Immunities.Contains(mainSkill.DamageType) && _pathing.HasLineOfSight(_playerPosition, x.Position))
                                .OrderBy(x => Automaton.GetDistance(_playerPosition, x.Position)).FirstOrDefault() ?? new UnitMonster(IntPtr.Zero);
            }

            // get closest enemy not immune to our fallback with line of sight
            if (!victim.IsValidPointer && fallbackSkill != null)
            {
                victim = monsters.Where(x => !x.Immunities.Contains(fallbackSkill.DamageType) && _pathing.HasLineOfSight(_playerPosition, x.Position))
                                .OrderBy(x => Automaton.GetDistance(_playerPosition, x.Position)).FirstOrDefault() ?? new UnitMonster(IntPtr.Zero);
            }

            // get closest enemy not immune to us
            if (!victim.IsValidPointer)
            {
                victim = monsters.Where(x => !x.Immunities.Contains(mainSkill.DamageType))
                                .OrderBy(x => Automaton.GetDistance(_playerPosition, x.Position)).FirstOrDefault() ?? new UnitMonster(IntPtr.Zero);
            }

            // get closest priority enemy (Shamans, Defilers, Spawners etc) not immune to our fallback
            if (!victim.IsValidPointer)
            {
                victim = monsters.Where(x => !x.Immunities.Contains(fallbackSkill.DamageType) && PriorityEnemies.Contains((Npc)x.TxtFileNo))
                                .OrderBy(x => Automaton.GetDistance(_playerPosition, x.Position)).FirstOrDefault() ?? new UnitMonster(IntPtr.Zero);
            }

            // get closest enemy not immune to our fallback
            if (!victim.IsValidPointer && fallbackSkill != null)
            {
                victim = monsters.Where(x => !x.Immunities.Contains(fallbackSkill.DamageType))
                                .OrderBy(x => Automaton.GetDistance(_playerPosition, x.Position)).FirstOrDefault() ?? new UnitMonster(IntPtr.Zero);
            }

            return victim;
        }

        private bool IsBoss(UnitMonster monster)
        {
            return (monster.MonsterData.MonsterType & Structs.MonsterTypeFlags.SuperUnique) == Structs.MonsterTypeFlags.SuperUnique ||
                   (monster.MonsterData.MonsterType & Structs.MonsterTypeFlags.Unique) == Structs.MonsterTypeFlags.Unique ||
                   (monster.MonsterData.MonsterType & Structs.MonsterTypeFlags.Champion) == Structs.MonsterTypeFlags.Champion ||
                   (monster.MonsterData.MonsterType & Structs.MonsterTypeFlags.Ghostly) == Structs.MonsterTypeFlags.Ghostly ||
                   (monster.MonsterData.MonsterType & Structs.MonsterTypeFlags.Minion) == Structs.MonsterTypeFlags.Minion ||
                   (monster.MonsterData.MonsterType & Structs.MonsterTypeFlags.Possessed) == Structs.MonsterTypeFlags.Possessed;
        }

        private List<Npc> PriorityEnemies = new List<Npc> {
            // shamans
            Npc.CarverShaman,
            Npc.CarverShaman2,
            Npc.DarkShaman,
            Npc.DarkShaman2,
            Npc.DevilkinShaman,
            Npc.DevilkinShaman2,
            Npc.FallenShaman,
            Npc.WarpedShaman,
            // mummy revivers
            Npc.HollowOne,
            Npc.Guardian,
            Npc.Guardian2,
            Npc.Unraveler,
            Npc.Unraveler2,
            Npc.HoradrimAncient,
            Npc.HoradrimAncient2,
            Npc.HoradrimAncient3,
            // maggots, more?
            Npc.SandMaggot,
            // those spawner dudes from act 4?
            // also missing: Nests, Mummy spawners
        };

        private long Now => DateTimeOffset.Now.ToUnixTimeMilliseconds();
    }
}
