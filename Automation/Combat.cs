using GameOverlay.Drawing;
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

        private static readonly short DETECTION_RANGE = 30;
        private static readonly short COMBAT_RANGE = 20;
        private static readonly short TOO_CLOSE_RANGE = 7;
        private static readonly short MAX_ATTACK_ATTEMPTS = 10;
        private static readonly int ESCAPE_COOLDOWN = 3000;
        private static readonly bool OPEN_CHESTS = true;
        private static readonly bool HAS_TELEPORT = true;
        private static readonly short CHEST_RANGE = 20;

        private BackgroundWorker _combatWorker;
        private bool _fighting = false;
        private Input _input;
        private Movement _movement;
        private Pathing _pathing;
        private List<CombatSkill> _skills = new List<CombatSkill>();
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

        public bool IsSafe => !Busy && !_combatWorker.IsBusy && !_monsters.Any(x => !_blacklist.Contains(x.UnitId) &&
                Automaton.GetDistance(_playerPosition, x.Position) <= COMBAT_RANGE);
        public bool Busy => _fighting;

        public Combat(Input input, Movement movement, Pathing pathing)
        {
            _input = input;
            _movement = movement;
            _pathing = pathing;

            _combatWorker = new BackgroundWorker();
            _combatWorker.DoWork += new DoWorkEventHandler(Fight);
            _combatWorker.WorkerSupportsCancellation = true;

            _target = new UnitAny(IntPtr.Zero);

            _skills.Add(new CombatSkill { Name = "Glacial Spike", DamageType = Resist.COLD, MaxRange = 20, Cooldown = 250, Key = "+{LMB}", IsRanged = true, IsMainSkill = true });
            _skills.Add(new CombatSkill { Name = "Blizzard", DamageType = Resist.COLD, MaxRange = 20, Cooldown = 1900, Key = "{RMB}", IsRanged = true, IsAoe = true });
            _skills.Add(new CombatSkill { Name = "Fireball", DamageType = Resist.FIRE, MaxRange = 20, Cooldown = 250, Key = "a", IsRanged = true });
            _skills.Add(new CombatSkill { Name = "Static", DamageType = Resist.LIGHTNING, MaxRange = 8, Cooldown = 350, Key = "s", IsRanged = true, IsStatic = true });
            _skills.Add(new CombatSkill { Name = "Telekinesis", MaxRange = 22, Cooldown = 300, Key = "e", IsRanged = true, IsTelekinesis = true });
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
                _monsters.Any(x => !_blacklist.Contains(x.UnitId) &&
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

                var telekinesis = _skills.Where(x => x.IsTelekinesis).FirstOrDefault();

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
            _attackAttempts = 0;
            _targetLastHealth = int.MaxValue;
            _target = new UnitAny(IntPtr.Zero);
            _combatWorker.CancelAsync();
        }

        private void Fight(object sender, DoWorkEventArgs e)
        {
            if (_areaToClear != null && !_target.IsValidPointer())
            {
                var monstersInArea = _monsters.Where(x => !_blacklist.Contains(x.UnitId) &&
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

                if (_target.Mode != 0 && _target.Mode != 12) // if not dying or dead
                {
                    var targetLife = 0;

                    _target.Stats.TryGetValue(Stat.STAT_HITPOINTS, out targetLife);

                    if (targetLife < _targetLastHealth)
                    {
                        _targetLastHealth = targetLife;
                        _attackAttempts = 0;
                    }

                    var targetLifePercentage = targetLife / 32768.0;

                    if (_skills.Any(x => x.IsAoe && Now - x.LastUsage > x.Cooldown))
                    {
                        CombatSkill skillToUse = _skills.Where(x => x.IsAoe && Now - x.LastUsage > x.Cooldown).First();
                        System.Threading.Thread.Sleep(200);
                        _input.DoInputAtWorldPosition(skillToUse.Key, castLocation);
                        skillToUse.LastUsage = Now;
                        System.Threading.Thread.Sleep(200);
                    }

                    if ((_target.TxtFileNo == (uint)Npc.Mephisto || _target.TxtFileNo == (uint)Npc.Diablo || _target.TxtFileNo == (uint)Npc.BaalThrone) &&
                        targetLifePercentage > 0.6 &&
                        _skills.Any(x => x.IsStatic))
                    {
                        CombatSkill staticSkill = _skills.Where(x => x.IsStatic && Now - x.LastUsage > x.Cooldown).FirstOrDefault();

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
                    else if (_skills.Any(x => x.IsRanged && !x.IsAoe && !x.IsStatic && !x.IsTelekinesis && (_target.Immunities == null || !_target.Immunities.Contains(x.DamageType))))
                    {
                        CombatSkill skillToUse = _skills.Where(x => x.IsRanged && !x.IsAoe && !x.IsStatic && !x.IsTelekinesis && (_target.Immunities == null || !_target.Immunities.Contains(x.DamageType))).First();
                        
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
                            AttackWith(skillToUse, castLocation);
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

        private void AttackWith(CombatSkill skill, Point worldPosition)
        {
            if (_reposition &&
                (Automaton.GetDistance(worldPosition, _playerPosition) > skill.MaxRange ||
                !_pathing.HasLineOfSight(_playerPosition, worldPosition)))
            {
                _log.Info("Want to use " + skill.Name + ", lets get closer.");
                GetInLOSRange(worldPosition, TOO_CLOSE_RANGE, skill.MaxRange);
            }

            _input.DoInputAtWorldPosition(skill.Key, worldPosition);
            skill.LastUsage = Now;
            System.Threading.Thread.Sleep(200);
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
            var mainSkill = _skills.Where(x => x.IsMainSkill).FirstOrDefault();
            var fallbackSkill = _skills.Where(x => !x.IsMainSkill && !x.IsAoe && !x.IsBuff && !x.IsStatic).FirstOrDefault();
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

        private long Now => DateTimeOffset.Now.ToUnixTimeMilliseconds();
    }
}
