using GameOverlay.Drawing;
using MapAssist.Helpers;
using MapAssist.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapAssist.Automation
{
    class PickIt
    {
        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();

        private static double _pickRange = 3;
        private static int _retryLimit = 3;

        private BackgroundWorker _worker;
        private Input _input;
        private Movement _movement;

        private IEnumerable<UnitAny> _items;
        private Point _playerPosition;
        private bool _working = false;

        public bool Busy => _working;

        public PickIt(Movement movement, Input input)
        {
            _input = input;
            _movement = movement;

            _worker = new BackgroundWorker();
            _worker.DoWork += new DoWorkEventHandler(PickThings);
            _worker.WorkerSupportsCancellation = true;
        }

        public void Update(GameData gameData)
        {
            if (gameData != null && gameData.PlayerUnit.IsValidPointer() && gameData.PlayerUnit.IsValidUnit())
            {
                _items = gameData.Items;
                _playerPosition = gameData.PlayerPosition;

                if (_working && !_worker.IsBusy)
                {
                    _worker.RunWorkerAsync();
                }
            }
        }

        public void Run()
        {
            if (!_working)
            {
                _log.Info("Looking for treasure!");
                _working = true;
                _worker.RunWorkerAsync();
            }
            else
            {
                // emergency abort
                _working = false;
            }
        }

        private void PickThings(object sender, DoWorkEventArgs e)
        {
            var pickPotions = !Inventory.IsBeltFull();

            var itemsToPick = _items.Where(x => x.IsDropped() &&
                                                (LootFilter.Filter(x) ||
                                                (pickPotions && Items.ItemName(x.TxtFileNo) == "Full Rejuvenation Potion")))
                                    .OrderBy(x => Automaton.GetDistance(x.Position, _playerPosition));

            if (itemsToPick.Count() > 0)
            {
                _working = true;

                var item = itemsToPick.First();

                _log.Info($"Picking up {Items.ItemName(item.TxtFileNo)}.");

                var itemId = item.UnitId;

                if (Automaton.GetDistance(item.Position, _playerPosition) > _pickRange)
                {
                    _log.Info("Too far away, moving closer...");
                    _movement.TeleportTo(item.Position);

                    do
                    {
                        System.Threading.Thread.Sleep(100);
                    }
                    while (Automaton.GetDistance(item.Position, _playerPosition) > _pickRange && _movement.Busy);

                    System.Threading.Thread.Sleep(500);
                }

                for (var i = 0; i < _retryLimit; i++)
                {
                    _log.Info($"Clicking it {i + 1}/{_retryLimit}");
                    _input.DoInputAtWorldPosition("{LMB}", item.Position);
                    System.Threading.Thread.Sleep(1000);

                    var refreshedItem = _items.Where(x => x.UnitId == itemId).FirstOrDefault() ?? new UnitAny(IntPtr.Zero);

                    if (refreshedItem.IsValidPointer() && (ItemMode)refreshedItem.Mode == ItemMode.STORED)
                    {
                        _log.Info("Got it!");
                        break;
                    }
                }
            }
            else
            {
                _working = false;
            }
        }
    }
}
