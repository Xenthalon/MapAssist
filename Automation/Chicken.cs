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

        private BackgroundWorker _chickenWorker;
        private Combat _combat;
        private Input _input;
        private MenuMan _menuman;
        private Movement _movement;

        private bool _mercIsDead = true;
        private bool _amDead = false;

        public int potionDrinkWaitInterval = 500;
        public double potionLifePercentage = 35.0;
        public double mercPotionLifePercentage = 15.0;
        public string[] potionKeys = { "1", "2", "3", "4" };

        public int playerHealth = -1;
        public int playerHealthMax = -1;
        public int mercHealth = -1;

        public double PlayerLifePercentage => playerHealth / (double)playerHealthMax;
        public double MercLifePercentage => mercHealth / (double)32768.0;
        public bool MercIsDead => _mercIsDead;

        public Chicken(Combat combat, Input input, MenuMan menuman, Movement movement)
        {
            _combat = combat;
            _input = input;
            _menuman = menuman;
            _movement = movement;

            _chickenWorker = new BackgroundWorker();
            _chickenWorker.DoWork += new DoWorkEventHandler(Watch);
            _chickenWorker.WorkerSupportsCancellation = true;
        }

        public void Update(GameData gameData)
        {
            if (gameData != null && gameData.PlayerUnit.IsValidPointer() && gameData.PlayerUnit.IsValidUnit() && gameData.PlayerUnit.Stats != null)
            {
                var health = -1;

                if (gameData.PlayerUnit.Stats.ContainsKey(Stat.STAT_HITPOINTS))
                {
                    gameData.PlayerUnit.Stats.TryGetValue(Stat.STAT_HITPOINTS, out health);
                    playerHealth = ConvertHexHealthToInt(health);
                }

                if (gameData.PlayerUnit.Stats.ContainsKey(Stat.STAT_MAXHP))
                {
                    gameData.PlayerUnit.Stats.TryGetValue(Stat.STAT_MAXHP, out health);
                    playerHealthMax = ConvertHexHealthToInt(health);
                }

                _amDead = playerHealth == -1 && gameData.PlayerUnit.IsCorpse && gameData.PlayerUnit.Mode == 17;

                UnitAny merc = gameData.Mercs.Where(x => x.IsMerc()).FirstOrDefault() ?? new UnitAny(IntPtr.Zero);

                if (merc.IsValidPointer() && merc.IsValidUnit() &&
                    merc.Stats.ContainsKey(Stat.STAT_HITPOINTS))
                {
                    merc.Stats.TryGetValue(Stat.STAT_HITPOINTS, out mercHealth);
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

            var nextPotionSlot = -1;

            if (PlayerLifePercentage < potionLifePercentage / 100)
            {
                _log.Info($"Life at {PlayerLifePercentage * 100}%, eating a potion from slot {nextPotionSlot}.");

                nextPotionSlot = Inventory.GetNextPotionSlotToUse();

                if (nextPotionSlot == -1)
                {
                    _log.Info("No more potions, panic!");
                    return;
                }

                _input.DoInput(potionKeys[nextPotionSlot]);
                System.Threading.Thread.Sleep(potionDrinkWaitInterval);
            }

            if (_mercIsDead || mercHealth == -1)
            {
                return;
            }

            if (MercLifePercentage < mercPotionLifePercentage / 100)
            {
                _log.Info($"Merc life at {MercLifePercentage * 100}%, giving a potion from slot {nextPotionSlot}.");

                nextPotionSlot = Inventory.GetNextPotionSlotToUse();

                if (nextPotionSlot == -1)
                {
                    _log.Info("No more potions, panic!");
                    return;
                }

                _input.DoInput("+" + potionKeys[nextPotionSlot]);
                System.Threading.Thread.Sleep(potionDrinkWaitInterval);
            }
        }

        public static int ConvertHexHealthToInt(int hexHealth)
        {
            var hexValue = hexHealth.ToString("X");
            hexValue = hexValue.Substring(0, hexValue.Length - 2);
            return int.Parse(hexValue, System.Globalization.NumberStyles.HexNumber);
        }
    }
}
