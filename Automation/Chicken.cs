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
        private Input _input;

        public int potionDrinkWaitInterval = 500;
        public double potionLifePercentage = 25.0;
        public double mercPotionLifePercentage = 15.0;
        public string[] potionKeys = { "1", "2", "3", "4" };

        public int playerHealth = -1;
        public int playerHealthMax = -1;
        public int mercHealth = -1;

        public Chicken(Input input)
        {
            _input = input;

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

                UnitAny merc = gameData.Mercs.Where(x => x.IsMerc()).FirstOrDefault() ?? new UnitAny(IntPtr.Zero);

                if (merc.IsValidPointer() && merc.IsValidUnit() &&
                    merc.Stats.ContainsKey(Stat.STAT_HITPOINTS))
                {
                    merc.Stats.TryGetValue(Stat.STAT_HITPOINTS, out mercHealth);
                }
            }

            if (!_chickenWorker.IsBusy)
            {
                _chickenWorker.RunWorkerAsync();
            }
        }

        private void Watch(object sender, DoWorkEventArgs e)
        {
            if (playerHealth <= 0 || playerHealthMax == -1)
            {
                return;
            }

            var nextPotionSlot = Inventory.GetNextPotionSlotToUse();

            if (nextPotionSlot == -1)
            {
                _log.Info("No more potions, panic!");
                return;
            }

            var currentLifePercentage = playerHealth / (double)playerHealthMax;

            if (currentLifePercentage < potionLifePercentage / 100)
            {
                _log.Info($"Life at {currentLifePercentage * 100}%, eating a potion from slot {nextPotionSlot}.");
                _input.DoInput(potionKeys[nextPotionSlot]);
                System.Threading.Thread.Sleep(potionDrinkWaitInterval);
            }

            nextPotionSlot = Inventory.GetNextPotionSlotToUse();

            if (nextPotionSlot == -1)
            {
                _log.Info("No more potions, panic!");
                return;
            }

            if (mercHealth == -1)
            {
                return;
            }

            var mercCurrentLifePercentage = mercHealth / 32768.0;

            if (mercCurrentLifePercentage < mercPotionLifePercentage / 100)
            {
                _log.Info($"Merc Life at {mercCurrentLifePercentage * 100}%, eating a potion from slot {nextPotionSlot}.");
                _input.DoInput("+" + potionKeys[nextPotionSlot]);
                System.Threading.Thread.Sleep(potionDrinkWaitInterval);
            }
        }

        private static int ConvertHexHealthToInt(int hexHealth)
        {
            var hexValue = hexHealth.ToString("X");
            hexValue = hexValue.Substring(0, hexValue.Length - 2);
            return int.Parse(hexValue, System.Globalization.NumberStyles.HexNumber);
        }
    }
}
