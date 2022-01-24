﻿using GameOverlay.Drawing;
using MapAssist.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapAssist.Automation
{
    class Combat
    {
        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();

        private short DETECTION_RANGE = 30;
        private short COMBAT_RANGE = 20;
        private short TOO_CLOSE_RANGE = 7;
        private short MAX_ATTACK_ATTEMPTS = 10;
        private int ESCAPE_COOLDOWN = 3000;
        private bool OPEN_CHESTS = true;
        private bool HAS_TELEPORT = true;
        private short CHEST_RANGE = 20;
        private List<CombatSkill> COMBAT_SKILLS = new List<CombatSkill>();

        private BackgroundWorker _combatWorker;
        private bool _fighting = false;
        private Input _input;
        private Movement _movement;
        private Pathing _pathing;
        private bool _reposition = true;
        private Point _playerPosition;
        private HashSet<UnitAny> _monsters;
        private IEnumerable<UnitAny> _chests;
        private UnitAny _target;
        private Point? _areaToClear;
        private List<uint> _blacklist = new List<uint>();
        private int _attackAttempts = 0;
        private int _targetLastHealth = int.MaxValue;
        private long _lastEscapeAttempt = 0;

        public bool OnlyBosses { get; set; } = false;

        public bool IsSafe => !Busy && !_combatWorker.IsBusy &&
                            !_monsters.Where(x => !OnlyBosses || IsBoss(x)).Any(x => !_blacklist.Contains(x.UnitId) &&
                            Automaton.GetDistance(_playerPosition, x.Position) <= COMBAT_RANGE);
        public bool Busy => _fighting;

        public Combat(BotConfiguration config, Input input, Movement movement, Pathing pathing)
        {
            COMBAT_RANGE = (short)config.Settings.CombatRange;
            DETECTION_RANGE = (short)config.Settings.DetectionRange;
            TOO_CLOSE_RANGE = (short)config.Settings.TooCloseRange;
            MAX_ATTACK_ATTEMPTS = (short)config.Settings.MaxAttackAttempts;
            ESCAPE_COOLDOWN = config.Settings.EscapeCooldown;
            CHEST_RANGE = (short)config.Settings.ChestRange;
            OPEN_CHESTS = config.Character.OpenChests;
            HAS_TELEPORT = config.Character.HasTeleport;

            COMBAT_SKILLS.AddRange(config.Character.Skills);

            _input = input;
            _movement = movement;
            _pathing = pathing;

            _combatWorker = new BackgroundWorker();
            _combatWorker.DoWork += new DoWorkEventHandler(Fight);
            _combatWorker.WorkerSupportsCancellation = true;

            _target = new UnitAny(IntPtr.Zero);
        }

        public void Kill(string name)
        {

        }

        public void Kill(uint unitTxtFileNo)
        {
            if (!_fighting && !_combatWorker.IsBusy &&
                _monsters.Any(x => x.TxtFileNo == unitTxtFileNo))
            {
                _target = _monsters.Where(x => x.TxtFileNo == unitTxtFileNo).First();
                _fighting = true;
                _combatWorker.RunWorkerAsync();
            }
        }

        public void ClearArea(Point location, bool reposition = true)
        {
            if (!_fighting && !_combatWorker.IsBusy &&
                _monsters.Where(x => !OnlyBosses || IsBoss(x))
                        .Any(x => !_blacklist.Contains(x.UnitId) &&
                        ((reposition && Automaton.GetDistance(location, x.Position) <= DETECTION_RANGE) ||
                        (!reposition && Automaton.GetDistance(location, x.Position) <= COMBAT_RANGE))))
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
            if (!OPEN_CHESTS)
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
                    GetInLOSRange(_target.Position, 1, (short)(interactRange - 1));

                    if (telekinesis != null)
                    {
                        _input.DoInputAtWorldPosition(telekinesis.Key, chest.Position);
                        telekinesis.LastUsage = Now;
                        System.Threading.Thread.Sleep(telekinesis.Cooldown);
                    }
                    else
                    {
                        _input.DoInputAtWorldPosition("{LMB}", chest.Position);
                        System.Threading.Thread.Sleep(1000);
                    }

                    _log.Info("Opened chest " + chest.UnitId + ".");
                }
            }
            else
            {
                _log.Info("Too dangerous to look for chests!");
            }
        }

        public void Update(GameData gameData)
        {
            if (gameData != null && gameData.PlayerUnit.IsValidPointer() && gameData.PlayerUnit.IsValidUnit())
            {
                _chests = gameData.Objects.Where(x => x.IsChest() &&
                                                (x.ObjectData.InteractType & ((byte)Chest.InteractFlags.Locked)) == ((byte)Chest.InteractFlags.None)); // only non-locked chests
                _monsters = gameData.Monsters;
                _playerPosition = gameData.PlayerPosition;

                if (_target.IsValidPointer())
                {
                    _target = _monsters.Where(x => x.UnitId == _target.UnitId).FirstOrDefault() ?? new UnitAny(IntPtr.Zero);
                    
                    if (!_target.IsValidPointer())
                    {
                        _log.Info("Killed!");

                        if (_areaToClear == null)
                        {
                            _fighting = false;
                        }
                    }
                }

                if (_fighting && !_combatWorker.IsBusy)
                {
                    _combatWorker.RunWorkerAsync();
                }
            }
        }

        public void Reset()
        {
            _fighting = false;
            _areaToClear = null;
            _reposition = true;
            _lastEscapeAttempt = 0;
            _blacklist = new List<uint>();
            _monsters = new HashSet<UnitAny>();
            _chests = new List<UnitAny>();
            _attackAttempts = 0;
            _targetLastHealth = int.MaxValue;
            _target = new UnitAny(IntPtr.Zero);
            _combatWorker.CancelAsync();
            OnlyBosses = false;
        }

        private void Fight(object sender, DoWorkEventArgs e)
        {
            if (_areaToClear != null && !_target.IsValidPointer())
            {
                var monstersInArea = _monsters.Where(x => (!OnlyBosses || IsBoss(x)) && !_blacklist.Contains(x.UnitId) &&
                        ((_reposition && Automaton.GetDistance((Point)_areaToClear, x.Position) <= DETECTION_RANGE) ||
                        (!_reposition && Automaton.GetDistance((Point)_areaToClear, x.Position) <= COMBAT_RANGE)));

                if (monstersInArea.Count() > 0)
                {
                    _fighting = true;
                    _target = GetNextVictim(monstersInArea);
                }
                else
                {
                    _log.Info("Killed them all!");
                    _fighting = false;
                    _areaToClear = null;
                    _reposition = true;
                }
            }

            if (_target.IsValidPointer())
            {
                var castLocation = new Point(_target.Position.X, _target.Position.Y);

                if (_target.Mode != 0 && (uint)_target.Mode != 12) // if not dying or dead
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
                        System.Threading.Thread.Sleep(200);
                        _input.DoInputAtWorldPosition(skillToUse.Key, castLocation);
                        skillToUse.LastUsage = Now;
                        System.Threading.Thread.Sleep(200);
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
                            System.Threading.Thread.Sleep(200);
                        }
                    }
                    else if (COMBAT_SKILLS.Any(x => x.IsRanged && !x.IsAoe && !x.IsStatic && !x.IsTelekinesis && !x.IsAura && (_target.Immunities == null || !_target.Immunities.Contains(x.DamageType))))
                    {
                        // ranged combat
                        CombatSkill skillToUse = COMBAT_SKILLS.Where(x => x.IsRanged && !x.IsAoe && !x.IsStatic && !x.IsTelekinesis && !x.IsAura && (_target.Immunities == null || !_target.Immunities.Contains(x.DamageType))).First();
                        
                        if (_reposition &&
                            Automaton.GetDistance(_target.Position, _playerPosition) < TOO_CLOSE_RANGE &&
                            Now - _lastEscapeAttempt > ESCAPE_COOLDOWN)
                        {
                            _log.Info("This is a bit personal, lets get away.");
                            System.Threading.Thread.Sleep(300);
                            GetInLOSRange(_target.Position, TOO_CLOSE_RANGE, (short)(skillToUse.MaxRange - 1));
                            _lastEscapeAttempt = Now;
                            System.Threading.Thread.Sleep(300);
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
                            _attackAttempts += 1;
                        }
                    }
                }

                if (_attackAttempts >= MAX_ATTACK_ATTEMPTS)
                {
                    _blacklist.Add(_target.UnitId);
                    _target = new UnitAny(IntPtr.Zero);
                    _attackAttempts = 0;
                }
            }
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
            _log.Info("Attack start");
            if (_reposition &&
                (Automaton.GetDistance(worldPosition, _playerPosition) > skill.MaxRange ||
                ((skill.IsAoe || skill.IsRanged) && !_pathing.HasLineOfSight(_playerPosition, worldPosition))))
            {
                _log.Info("Want to use " + skill.Name + ", lets get closer.");
                GetInLOSRange(worldPosition, TOO_CLOSE_RANGE, skill.MaxRange);
            }

            _input.DoInputAtWorldPosition(skill.Key, worldPosition);
            _log.Info("Attack end");
            skill.LastUsage = Now;
            System.Threading.Thread.Sleep(100);
        }

        private void GetInLOSRange(Point target, short minRange, short maxRange)
        {
            System.Threading.Thread.Sleep(300);
            _movement.GetInLOSRange(target, minRange, maxRange - 1, HAS_TELEPORT);
            System.Threading.Thread.Sleep(300);
        }

        private UnitAny GetNextVictim(IEnumerable<UnitAny> monsters)
        {
            _targetLastHealth = int.MaxValue;
            var mainSkill = COMBAT_SKILLS.Where(x => x.IsMainSkill).FirstOrDefault();
            var fallbackSkill = COMBAT_SKILLS.Where(x => !x.IsMainSkill && !x.IsAoe && !x.IsBuff && !x.IsStatic).FirstOrDefault();
            var victim = new UnitAny(IntPtr.Zero);

            var supers = monsters.Where(x => (x.MonsterData.MonsterType & Structs.MonsterTypeFlags.SuperUnique) == Structs.MonsterTypeFlags.SuperUnique ||
                                            (x.MonsterData.MonsterType & Structs.MonsterTypeFlags.Unique) == Structs.MonsterTypeFlags.Unique);

            // get closest super not immune to us with line of sight
            if (supers.Any(x => !x.Immunities.Contains(mainSkill.DamageType) && _pathing.HasLineOfSight(_playerPosition, x.Position)))
            {
                victim = supers.Where(x => !x.Immunities.Contains(mainSkill.DamageType) && _pathing.HasLineOfSight(_playerPosition, x.Position))
                                .OrderBy(x => Automaton.GetDistance(_playerPosition, x.Position)).First();
            }

            // get closest enemy not immune to us with line of sight
            if (!victim.IsValidPointer())
            { 
                victim = monsters.Where(x => !x.Immunities.Contains(mainSkill.DamageType) && _pathing.HasLineOfSight(_playerPosition, x.Position))
                                .OrderBy(x => Automaton.GetDistance(_playerPosition, x.Position)).FirstOrDefault() ?? new UnitAny(IntPtr.Zero);
            }

            // get closest enemy not immune to our fallback with line of sight
            if (!victim.IsValidPointer() && fallbackSkill != null)
            {
                victim = monsters.Where(x => !x.Immunities.Contains(fallbackSkill.DamageType) && _pathing.HasLineOfSight(_playerPosition, x.Position))
                                .OrderBy(x => Automaton.GetDistance(_playerPosition, x.Position)).FirstOrDefault() ?? new UnitAny(IntPtr.Zero);
            }

            // get closest enemy not immune to us
            if (!victim.IsValidPointer())
            {
                victim = monsters.Where(x => !x.Immunities.Contains(mainSkill.DamageType))
                                .OrderBy(x => Automaton.GetDistance(_playerPosition, x.Position)).FirstOrDefault() ?? new UnitAny(IntPtr.Zero);
            }

            // get closest enemy not immune to our fallback
            if (!victim.IsValidPointer() && fallbackSkill != null)
            {
                victim = monsters.Where(x => !x.Immunities.Contains(fallbackSkill.DamageType))
                                .OrderBy(x => Automaton.GetDistance(_playerPosition, x.Position)).FirstOrDefault() ?? new UnitAny(IntPtr.Zero);
            }

            return victim;
        }

        private bool IsBoss(UnitAny monster)
        {
            return (monster.MonsterData.MonsterType & Structs.MonsterTypeFlags.SuperUnique) == Structs.MonsterTypeFlags.SuperUnique ||
                   (monster.MonsterData.MonsterType & Structs.MonsterTypeFlags.Unique) == Structs.MonsterTypeFlags.Unique ||
                   (monster.MonsterData.MonsterType & Structs.MonsterTypeFlags.Champion) == Structs.MonsterTypeFlags.Champion ||
                   (monster.MonsterData.MonsterType & Structs.MonsterTypeFlags.Ghostly) == Structs.MonsterTypeFlags.Ghostly ||
                   (monster.MonsterData.MonsterType & Structs.MonsterTypeFlags.Minion) == Structs.MonsterTypeFlags.Minion ||
                   (monster.MonsterData.MonsterType & Structs.MonsterTypeFlags.Possessed) == Structs.MonsterTypeFlags.Possessed;
        }

        private long Now => DateTimeOffset.Now.ToUnixTimeMilliseconds();
    }
}
