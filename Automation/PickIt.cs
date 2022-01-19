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

        private static readonly int MAX_RETRY_COUNT = 3;

        private static double _pickRange = 5;

        private BackgroundWorker _worker;
        private Input _input;
        private Movement _movement;

        private IEnumerable<UnitAny> _items;
        private Point _playerPosition;
        private bool _working = false;
        private bool _full = false;

        public bool Busy => _working;
        public bool Full => _full;
        public bool HasWork => _items.Any(x => x.IsDropped() &&
                                            (LootFilter.Filter(x).Item1 ||
                                            (!Inventory.IsBeltFull() && (
                                                Items.ItemName(x.TxtFileNo) == "Full Rejuvenation Potion" ||
                                                Items.ItemName(x.TxtFileNo) == "Rejuvenation Potion"
                                            ))));

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

        public void Reset()
        {
            _working = false;
            _full = false;
            _worker.CancelAsync();
        }

        private void PickThings(object sender, DoWorkEventArgs e)
        {
            var pickPotions = !Inventory.IsBeltFull();

            var itemsToPick = _items.Where(x => x.IsDropped() &&
                                                (LootFilter.Filter(x).Item1 ||
                                                (pickPotions && (
                                                    Items.ItemName(x.TxtFileNo) == "Full Rejuvenation Potion" ||
                                                    Items.ItemName(x.TxtFileNo) == "Rejuvenation Potion"
                                                ))))
                                    .OrderBy(x => Automaton.GetDistance(x.Position, _playerPosition));

            if (itemsToPick.Count() > 0)
            {
                _working = true;

                var item = itemsToPick.First();

                _log.Info($"Picking up {Items.ItemName(item.TxtFileNo)}.");

                var itemId = item.UnitId;
                var picked = false;

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

                for (var i = 0; i < MAX_RETRY_COUNT; i++)
                {
                    _log.Info($"Clicking it {i + 1}/{MAX_RETRY_COUNT}");
                    _input.DoInputAtWorldPosition("{LMB}", item.Position);
                    System.Threading.Thread.Sleep(1000);

                    var refreshedItem = _items.Where(x => x.UnitId == itemId).FirstOrDefault() ?? new UnitAny(IntPtr.Zero);

                    if (refreshedItem.IsValidPointer() && ((ItemMode)refreshedItem.Mode == ItemMode.STORED || (ItemMode)refreshedItem.Mode == ItemMode.INBELT))
                    {
                        _log.Info("Got it!");
                        picked = true;
                        break;
                    }
                }

                if (!picked)
                {
                    _log.Info("Seems we are full, please help.");
                    _full = true;
                    _working = false;
                }
            }
            else
            {
                _working = false;
            }
        }
    }
}
