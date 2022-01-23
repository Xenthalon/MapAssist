using MapAssist.Types;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace MapAssist.Automation
{
    class BuffBoy
    {
        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();

        private int MAX_RETRIES;
        private bool USE_CTA;
        private string SWITCH_KEY;

        private BackgroundWorker _worker;
        private Input _input;
        List<State> _playerStates;
        Skills _skills;

        private bool _buffing = false;
        private bool _force = false;
        private List<CombatSkill> BUFF_SKILLS = new List<CombatSkill>();

        public bool Busy => _buffing;
        public bool HasWork => BUFF_SKILLS.Any(x => !_playerStates.Contains(x.BuffState));

        public BuffBoy(BotConfiguration config, Input input)
        {
            MAX_RETRIES = config.Settings.MaxRetries;
            USE_CTA = config.Character.UseCta;
            SWITCH_KEY = config.Character.KeyWeaponSwitch;
            BUFF_SKILLS.AddRange(config.Character.BuffSkills);

            _input = input;

            _worker = new BackgroundWorker();
            _worker.DoWork += new DoWorkEventHandler(BuffMe);
            _worker.WorkerSupportsCancellation = true;
        }

        public void Update(GameData gameData)
        {
            if (gameData != null && gameData.PlayerUnit.IsValidPointer() && gameData.PlayerUnit.IsValidUnit())
            {
                _skills = gameData.PlayerUnit.Skills;
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
            var missingBuffs = BUFF_SKILLS.Where(x => !_playerStates.Contains(x.BuffState));

            if (_force || missingBuffs.Count() > 0)
            {
                _buffing = true;
                if (_force)
                    _log.Info("Forced re-buff.");
                else
                    _log.Info("Missing " + string.Join(",", missingBuffs.Select(x => x.Name)) + ", recasting all buffs.");

                IEnumerable<CombatSkill> remainingBuffs = BUFF_SKILLS;

                if (USE_CTA)
                {
                    var retries = 0;

                    while (!_skills.AllSkills.Any(x => x.Skill == Skill.BattleOrders) && retries < MAX_RETRIES)
                    {
                        _input.DoInput(SWITCH_KEY);
                        System.Threading.Thread.Sleep(500);

                        retries += 1;
                    }

                    if (_skills.AllSkills.Any(x => x.Skill == Skill.BattleCommand))
                    {
                        var battleCommand = BUFF_SKILLS.Where(x => x.BuffState == State.STATE_BATTLECOMMAND).First();

                        CastBuff(battleCommand);
                        CastBuff(battleCommand);
                    }

                    if (_skills.AllSkills.Any(x => x.Skill == Skill.BattleOrders))
                    {
                        var battleOrders = BUFF_SKILLS.Where(x => x.BuffState == State.STATE_BATTLEORDERS).First();

                        CastBuff(battleOrders);
                    }

                    retries = 0;

                    while (_skills.AllSkills.Any(x => x.Skill == Skill.BattleOrders) && retries < MAX_RETRIES)
                    {
                        _input.DoInput(SWITCH_KEY);
                        System.Threading.Thread.Sleep(500);

                        retries += 1;
                    }

                    remainingBuffs = BUFF_SKILLS.Where(x => x.BuffState != State.STATE_BATTLECOMMAND && x.BuffState != State.STATE_BATTLEORDERS);
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
            _log.Debug("Casting " + buff.Name);
            _input.DoInput(buff.Key);
            System.Threading.Thread.Sleep(buff.Cooldown);
        }
    }
}
