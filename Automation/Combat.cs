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

        private BackgroundWorker _combatWorker;
        private bool _fighting = false;
        private Input _input;
        private List<CombatSkill> _skills = new List<CombatSkill>();
        private Point _playerPosition;
        private HashSet<UnitAny> _monsters;
        private UnitAny _target;
        private Point? _areaToClear;

        public bool IsSafe => !_monsters.Any(x => Automaton.GetDistance(_playerPosition, x.Position) <= combatRange);
        public bool Busy => _fighting;

        public Combat(Input input)
        {
            _input = input;

            _combatWorker = new BackgroundWorker();
            _combatWorker.DoWork += new DoWorkEventHandler(Fight);
            _combatWorker.WorkerSupportsCancellation = true;

            _target = new UnitAny(IntPtr.Zero);

            _skills.Add(new CombatSkill { Name = "Glacial Spike", Cooldown = 300, Key = "+{LMB}", LastUsage = 0, IsRanged = true, IsAoe = false });
            _skills.Add(new CombatSkill { Name = "Blizzard", Cooldown = 1800, Key = "{RMB}", LastUsage = 0, IsRanged = true, IsAoe = true });
        }

        public void Kill(UnitAny monster)
        {

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

                if (_fighting && !_combatWorker.IsBusy)
                {
                    _combatWorker.RunWorkerAsync();
                }
            }
        }

        private void Fight(object sender, DoWorkEventArgs e)
        {
            if (_target.IsValidPointer())
            {
                // fight specific target
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
                    }

                    if (_skills.Any(x => x.IsRanged && Now - x.LastUsage > x.Cooldown))
                    {
                        CombatSkill skillToUse = _skills.Where(x => x.IsRanged && Now - x.LastUsage > x.Cooldown).First();
                        Point castLocation = monstersInArea.OrderBy(x => Automaton.GetDistance(_playerPosition, x.Position)).First().Position;
                        _input.DoInputAtWorldPosition(skillToUse.Key, castLocation);
                        skillToUse.LastUsage = Now;
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
