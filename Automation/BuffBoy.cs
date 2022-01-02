using MapAssist.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapAssist.Automation
{
    class BuffBoy
    {
        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();

        private BackgroundWorker _worker;
        private Input _input;
        List<State> _playerStates;

        private bool _useCta = true;
        private string _switchKey = "x";
        private bool _buffing = false;
        private bool _force = false;
        private List<CombatSkill> _availableBuffs = new List<CombatSkill>();

        public bool Busy => _buffing;
        public bool HasWork => _availableBuffs.Any(x => !_playerStates.Contains(x.BuffState));

        public BuffBoy(Input input)
        {
            _input = input;

            _worker = new BackgroundWorker();
            _worker.DoWork += new DoWorkEventHandler(BuffMe);
            _worker.WorkerSupportsCancellation = true;

            _availableBuffs.Add(new CombatSkill { Name = "Frozen Armor", Cooldown = 500, Key = "q", IsBuff = true, BuffState = State.STATE_FROZENARMOR });
            _availableBuffs.Add(new CombatSkill { Name = "Battle Orders", Cooldown = 500, Key = "d", IsBuff = true, BuffState = State.STATE_BATTLEORDERS });
            _availableBuffs.Add(new CombatSkill { Name = "Battle Command", Cooldown = 500, Key = "r", IsBuff = true, BuffState = State.STATE_BATTLECOMMAND });
        }

        public void Update(GameData gameData)
        {
            if (gameData != null && gameData.PlayerUnit.IsValidPointer() && gameData.PlayerUnit.IsValidUnit())
            {
                _playerStates = gameData.PlayerUnit.StateList;
            }
        }

        public void Run(bool force = false)
        {
            _log.Info($"BuffBoy called with force {force}, currently {(_worker.IsBusy ? "busy" : "not busy")}");

            if (!_worker.IsBusy)
            {
                _force = force;
                _worker.RunWorkerAsync();
            }
        }

        public void Reset()
        {
            _buffing = false;
            _force = false;
            _worker.CancelAsync();
        }

        private void BuffMe(object sender, DoWorkEventArgs e)
        {
            var missingBuffs = _availableBuffs.Where(x => !_playerStates.Contains(x.BuffState));

            if (_force || missingBuffs.Count() > 0)
            {
                _buffing = true;
                if (_force)
                    _log.Info("Forced re-buff.");
                else
                    _log.Info("Missing " + string.Join(",", missingBuffs.Select(x => x.Name)) + ", recasting all buffs.");

                IEnumerable<CombatSkill> remainingBuffs = _availableBuffs;

                if (_useCta)
                {
                    _input.DoInput(_switchKey);
                    System.Threading.Thread.Sleep(500);

                    var battleCommand = _availableBuffs.Where(x => x.BuffState == State.STATE_BATTLECOMMAND).First();

                    CastBuff(battleCommand);
                    CastBuff(battleCommand);

                    var battleOrders = _availableBuffs.Where(x => x.BuffState == State.STATE_BATTLEORDERS).First();

                    CastBuff(battleOrders);

                    _input.DoInput(_switchKey);
                    System.Threading.Thread.Sleep(500);

                    remainingBuffs = _availableBuffs.Where(x => x.BuffState != State.STATE_BATTLECOMMAND && x.BuffState != State.STATE_BATTLEORDERS);
                }

                foreach (CombatSkill buff in remainingBuffs)
                {
                    CastBuff(buff);
                }

                _buffing = false;
            }
        }

        private void CastBuff(CombatSkill buff)
        {
            _log.Info("Casting " + buff.Name);
            _input.DoInput(buff.Key);
            System.Threading.Thread.Sleep(buff.Cooldown);
        }
    }
}
