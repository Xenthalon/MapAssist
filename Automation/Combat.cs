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

        private static readonly short combatRange = 20;
        private static readonly double staticRange = 8;
        private static readonly double tooCloseRange = 10;

        private BackgroundWorker _combatWorker;
        private bool _fighting = false;
        private Input _input;
        private Movement _movement;
        private List<CombatSkill> _skills = new List<CombatSkill>();
        private Point _playerPosition;
        private HashSet<UnitAny> _monsters;
        private UnitAny _target;
        private Point? _areaToClear;

        public bool IsSafe => !_monsters.Any(x => Automaton.GetDistance(_playerPosition, x.Position) <= combatRange);
        public bool Busy => _fighting;

        public Combat(Input input, Movement movement)
        {
            _input = input;
            _movement = movement;

            _combatWorker = new BackgroundWorker();
            _combatWorker.DoWork += new DoWorkEventHandler(Fight);
            _combatWorker.WorkerSupportsCancellation = true;

            _target = new UnitAny(IntPtr.Zero);

            _skills.Add(new CombatSkill { Name = "Glacial Spike", DamageType = Resist.COLD, Cooldown = 500, Key = "+{LMB}", IsRanged = true, IsMainSkill = true });
            _skills.Add(new CombatSkill { Name = "Blizzard", DamageType = Resist.COLD, Cooldown = 1800, Key = "{RMB}", IsRanged = true, IsAoe = true });
            // _skills.Add(new CombatSkill { Name = "Fireball", DamageType = Resist.FIRE, Cooldown = 350, Key = "a", IsRanged = true });
            _skills.Add(new CombatSkill { Name = "Static", DamageType = Resist.LIGHTNING, Cooldown = 350, Key = "s", IsRanged = true, IsStatic = true });
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

        public void ClearArea(Point location)
        {
            if (!_fighting && !_combatWorker.IsBusy &&
                _monsters.Any(x => Automaton.GetDistance(location, x.Position) <= combatRange))
            {
                _areaToClear = location;
                _fighting = true;
                _combatWorker.RunWorkerAsync();
            }
            else if (_fighting)
            {
                // emergency abort
                _fighting = false;
            }
        }

        public void Update(GameData gameData)
        {
            if (gameData != null && gameData.PlayerUnit.IsValidPointer() && gameData.PlayerUnit.IsValidUnit())
            {
                _monsters = gameData.Monsters;
                _playerPosition = gameData.PlayerPosition;

                if (_target.IsValidPointer())
                {
                    _target = _monsters.Where(x => x.UnitId == _target.UnitId).FirstOrDefault() ?? new UnitAny(IntPtr.Zero);
                    
                    if (!_target.IsValidPointer())
                    {
                        _log.Info("Killed that sucker!");
                        _fighting = false;
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
            _target = new UnitAny(IntPtr.Zero);
            _combatWorker.CancelAsync();
        }

        private void Fight(object sender, DoWorkEventArgs e)
        {
            if (_target.IsValidPointer())
            {
                Point castLocation = _target.Position;

                if (_target.Mode != 0 && _target.Mode != 12) // if not dying or dead
                {
                    var targetLife = 0;

                    _target.Stats.TryGetValue(Stat.STAT_HITPOINTS, out targetLife);

                    var targetLifePercentage = targetLife / 32768.0;

                    if (_skills.Any(x => x.IsAoe && Now - x.LastUsage > x.Cooldown))
                    {
                        CombatSkill skillToUse = _skills.Where(x => x.IsAoe && Now - x.LastUsage > x.Cooldown).First();
                        _input.DoInputAtWorldPosition(skillToUse.Key, castLocation);
                        skillToUse.LastUsage = Now;
                        System.Threading.Thread.Sleep(200);
                    }

                    if (targetLifePercentage > 0.6 && _skills.Any(x => x.IsStatic))
                    {
                        if (Automaton.GetDistance(_target.Position, _playerPosition) >= staticRange)
                        {
                            _log.Info("Want to use Static, lets get closer.");
                            System.Threading.Thread.Sleep(300);
                            _movement.GetInRangeOf(_target.Position, staticRange);
                            System.Threading.Thread.Sleep(300);
                        }

                        CombatSkill staticSkill = _skills.Where(x => x.IsStatic && Now - x.LastUsage > x.Cooldown).FirstOrDefault();

                        // means static is still on cooldown
                        if (staticSkill != null)
                        {
                            _input.DoInput(staticSkill.Key);
                            staticSkill.LastUsage = Now;
                            System.Threading.Thread.Sleep(200);
                        }
                    }
                    else if (_skills.Any(x => x.IsRanged && x.IsMainSkill && Now - x.LastUsage > x.Cooldown))
                    {
                        if (Automaton.GetDistance(_target.Position, _playerPosition) <= tooCloseRange)
                        {
                            _log.Info("This is a bit personal, lets get away.");
                            System.Threading.Thread.Sleep(300);
                            _movement.GetOutOfRangeOf(_target.Position, tooCloseRange);
                            System.Threading.Thread.Sleep(300);
                        }

                        CombatSkill skillToUse = _skills.Where(x => x.IsRanged && x.IsMainSkill && Now - x.LastUsage > x.Cooldown).First();
                        _input.DoInputAtWorldPosition(skillToUse.Key, castLocation);
                        skillToUse.LastUsage = Now;
                        System.Threading.Thread.Sleep(200);
                    }
                }
            }
            else if (_areaToClear != null)
            {
                var monstersInArea = _monsters.Where(x => Automaton.GetDistance((Point)_areaToClear, x.Position) <= combatRange);

                if (monstersInArea.Count() > 0)
                {
                    _fighting = true;

                    if (_skills.Any(x => x.IsAoe && Now - x.LastUsage > x.Cooldown))
                    {
                        CombatSkill skillToUse = _skills.Where(x => x.IsAoe && Now - x.LastUsage > x.Cooldown).First();
                        Point castLocation = GetHighValuePosition(monstersInArea);
                        _input.DoInputAtWorldPosition(skillToUse.Key, castLocation);
                        skillToUse.LastUsage = Now;
                        System.Threading.Thread.Sleep(200);
                    }

                    if (_skills.Any(x => x.IsRanged && x.IsMainSkill && Now - x.LastUsage > x.Cooldown))
                    {
                        CombatSkill skillToUse = _skills.Where(x => x.IsRanged && Now - x.LastUsage > x.Cooldown).First();
                        Point castLocation = monstersInArea.OrderBy(x => Automaton.GetDistance(_playerPosition, x.Position)).First().Position;
                        _input.DoInputAtWorldPosition(skillToUse.Key, castLocation);
                        skillToUse.LastUsage = Now;
                        System.Threading.Thread.Sleep(200);
                    }
                }
                else
                {
                    _log.Info("Killed them all!");
                    _fighting = false;
                    _areaToClear = null;
                }
            }
        }

        private Point GetHighValuePosition(IEnumerable<UnitAny> monsters)
        {
            var supers = monsters.Where(x => x.MonsterData.MonsterType > 0);

            if (supers.Count() > 0)
            {
                var closest = supers.OrderBy(x => Automaton.GetDistance(_playerPosition, x.Position)).First();
                return closest.Position;
            }

            // calculate cluster centers here later

            return monsters.OrderBy(x => Automaton.GetDistance(_playerPosition, x.Position)).First().Position;
        }

        private long Now => DateTimeOffset.Now.ToUnixTimeMilliseconds();
    }
}
