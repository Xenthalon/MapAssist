using MapAssist.Helpers;
using MapAssist.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MapAssist.Automation
{
    class Chicken
    {
        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();

        private int MAX_GAME_LENGTH;
        public int POTION_DRINK_WAIT_INTERVAL;
        public double POTION_LIFE_PERCENTAGE;
        public double MERC_POTION_LIFE_PERCENTAGE;
        public string[] POTION_KEYS;

        private BackgroundWorker _chickenWorker;
        private Combat _combat;
        private Input _input;
        private Inventory _inventory;
        private MenuMan _menuman;
        private Movement _movement;

        private bool _mercIsDead = true;
        private bool _amDead = false;
        private long _gamestart = 0;

        public int playerHealth = -1;
        public int playerHealthMax = -1;
        public int mercHealth = -1;

        public double PlayerLifePercentage => playerHealth / (double)playerHealthMax;
        public double MercLifePercentage => mercHealth / (double)32768.0;
        public bool MercIsDead => _mercIsDead;

        public Chicken(BotConfiguration config, Combat combat, Input input, Inventory inventory, MenuMan menuman, Movement movement)
        {
            MAX_GAME_LENGTH = config.Settings.MaxGameLength;
            POTION_DRINK_WAIT_INTERVAL = config.Character.PotionWaitInterval;
            POTION_LIFE_PERCENTAGE = config.Character.PotionLifePercent;
            MERC_POTION_LIFE_PERCENTAGE = config.Character.PotionMercLifePercent;
            POTION_KEYS = config.Character.KeysPotion;

            _combat = combat;
            _input = input;
            _inventory = inventory;
            _menuman = menuman;
            _movement = movement;

            _chickenWorker = new BackgroundWorker();
            _chickenWorker.DoWork += new DoWorkEventHandler(Watch);
            _chickenWorker.WorkerSupportsCancellation = true;
        }

        public void Update(GameData gameData)
        {
            if (gameData != null && gameData.PlayerUnit.IsValidPointer && gameData.PlayerUnit.IsValidUnit && gameData.PlayerUnit.Stats != null)
            {
                if (_gamestart == 0)
                {
                    _gamestart = Now;
                }

                var health = -1;

                if (gameData.PlayerUnit.Stats.ContainsKey(Stat.Life))
                {
                    gameData.PlayerUnit.Stats.TryGetValue(Stat.Life, out health);
                    playerHealth = ConvertHexHealthToInt(health);
                }
                else
                {
                    _amDead = true;
                    playerHealth = 0;
                }

                if (gameData.PlayerUnit.Stats.ContainsKey(Stat.MaxLife))
                {
                    gameData.PlayerUnit.Stats.TryGetValue(Stat.MaxLife, out health);
                    playerHealthMax = ConvertHexHealthToInt(health);
                }

                UnitMonster merc = gameData.Mercs.Where(x => x.IsMerc && x.IsPlayerOwned).FirstOrDefault() ?? new UnitMonster(IntPtr.Zero);

                if (merc.IsValidPointer && merc.IsValidUnit &&
                    merc.Stats.ContainsKey(Stat.Life))
                {
                    merc.Stats.TryGetValue(Stat.Life, out mercHealth);
                    _mercIsDead = false;
                }
                else
                {
                    _mercIsDead = true;
                }
            }

            if (!_chickenWorker.IsBusy)
            {
                _chickenWorker.RunWorkerAsync();
            }
        }

        public void Reset()
        {
            _mercIsDead = true;
            _amDead = false;
            _gamestart = 0;

            playerHealth = -1;
            playerHealthMax = -1;
            mercHealth = -1;
        }

        private void Watch(object sender, DoWorkEventArgs e)
        {
            if (_amDead)
            {
                _log.Info("Died! Leaving game.");
                _movement.Reset();
                _combat.Reset();
                _input.DoInput("{ESC}");

                System.Threading.Thread.Sleep(8000); // wait for town to load

                _menuman.ExitGame();
                return;
            }

            if (playerHealth <= 0 || playerHealthMax == -1)
            {
                return;
            }

            if (_gamestart > 0 && Now - _gamestart > MAX_GAME_LENGTH)
            {
                _log.Error($"Tripped MAX_GAME_LENGTH timer of {MAX_GAME_LENGTH / (1000 * 60)} minutes! Emergency abort!");
                _menuman.ExitGame();
                return;
            }

            var nextPotionSlot = -1;

            if (PlayerLifePercentage < POTION_LIFE_PERCENTAGE / 100)
            {
                _log.Info($"Life at {PlayerLifePercentage * 100}%, eating a potion from slot {nextPotionSlot}.");

                nextPotionSlot = _inventory.GetNextPotionSlotToUse();

                if (nextPotionSlot == -1)
                {
                    _log.Info("No more potions, panic!");
                    return;
                }

                _input.DoInput(POTION_KEYS[nextPotionSlot]);
                System.Threading.Thread.Sleep(POTION_DRINK_WAIT_INTERVAL);
            }

            if (_mercIsDead || mercHealth == -1)
            {
                return;
            }

            if (MercLifePercentage < MERC_POTION_LIFE_PERCENTAGE / 100)
            {
                _log.Info($"Merc life at {MercLifePercentage * 100}%, giving a potion from slot {nextPotionSlot}.");

                nextPotionSlot = _inventory.GetNextPotionSlotToUse();

                if (nextPotionSlot == -1)
                {
                    _log.Info("No more potions, panic!");
                    return;
                }

                _input.DoInput("+" + POTION_KEYS[nextPotionSlot]);
                System.Threading.Thread.Sleep(POTION_DRINK_WAIT_INTERVAL);
            }
        }

        public static int ConvertHexHealthToInt(int hexHealth)
        {
            var hexValue = hexHealth.ToString("X");
            hexValue = hexValue.Substring(0, hexValue.Length - 2);
            return int.Parse(hexValue, System.Globalization.NumberStyles.HexNumber);
        }

        private long Now => DateTimeOffset.Now.ToUnixTimeMilliseconds();
    }
}
