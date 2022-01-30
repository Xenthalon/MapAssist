using GameOverlay.Drawing;
using MapAssist.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace MapAssist.Automation.Profiles
{
    class Cows : IRunProfile
    {
        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();

        private bool _abort = false;

        private BuffBoy _buffBoy;
        private Combat _combat;
        private MenuMan _menuMan;
        private Movement _movement;
        private Input _input;
        private Inventory _inventory;
        private PickIt _pickit;
        private TownManager _townManager;

        private BackgroundWorker _worker;

        private Area _currentArea;
        private GameData _gameData;
        private List<PointOfInterest> _pointsOfInterests;
        private Point _playerPosition;

        private bool _busy = false;
        private bool _error = false;

        public Cows(BuffBoy buffBoy, Combat combat, Input input, Inventory inventory, MenuMan menuMan, Movement movement, PickIt pickit, TownManager townManager)
        {
            _buffBoy = buffBoy;
            _combat = combat;
            _input = input;
            _inventory = inventory;
            _menuMan = menuMan;
            _movement = movement;
            _pickit = pickit;
            _townManager = townManager;

            _worker = new BackgroundWorker();
            _worker.DoWork += new DoWorkEventHandler(Work);
            _worker.WorkerSupportsCancellation = true;
        }

        public void Run()
        {
            if (_currentArea != Area.StonyField)
            {
                _log.Error("Must be in StonyField to run this profile!");
                throw new Exception("Not in StonyField!");
            }

            _busy = true;
            _worker.RunWorkerAsync();
        }

        public void Update(GameData gameData, AreaData areaData, List<PointOfInterest> pointsOfInterest)
        {
            if (_pointsOfInterests == null || _pointsOfInterests.Count != pointsOfInterest.Count)
            {
                _pointsOfInterests = pointsOfInterest;
            }

            if (gameData != null && gameData.PlayerUnit.IsValidPointer && gameData.PlayerUnit.IsValidUnit)
            {
                _currentArea = gameData.Area;
                _gameData = gameData;
                _playerPosition = gameData.PlayerPosition;
            }
        }

        public void Abort()
        {
            _abort = true;
        }

        public bool HasError()
        {
            return _error;
        }

        public bool IsBusy()
        {
            return _busy;
        }

        private void Work(object sender, DoWorkEventArgs e)
        {
            try
            {
                var stoneCircleLocation = _pointsOfInterests.Where(x => x.Label.StartsWith("Tristram")).First().Position;

                _log.Info("Moving to Tristram Portal");
                MoveTo(stoneCircleLocation);

                _combat.SetCombatRange(10);
                ClearArea(stoneCircleLocation);
                _combat.Reset();
                PickThings();

                MoveTo(stoneCircleLocation);

                var tristPortal = _gameData.Objects.Where(x => x.TxtFileNo == (uint)GameObject.PermanentTownPortal).FirstOrDefault() ?? new UnitObject(IntPtr.Zero);

                if (!tristPortal.IsValidPointer)
                {
                    _log.Error("Couldn't find portal to Tristram, abort!");
                    _busy = false;
                    return;
                }

                _movement.ChangeArea(Area.Tristram, tristPortal);

                var wirtLocation = _pointsOfInterests.Where(x => x.Label.StartsWith("Wirt's Leg")).First().Position;
                MoveTo(wirtLocation);
                _combat.SetCombatRange(10);
                ClearArea(wirtLocation);
                _combat.Reset();

                var wirtCorpse = _gameData.Objects.Where(x => x.TxtFileNo == (uint)GameObject.WirtCorpse).FirstOrDefault() ?? new UnitObject(IntPtr.Zero);

                if (!wirtCorpse.IsValidPointer)
                {
                    _log.Error("Couldn't find Wirt's corpse, abort!");
                    _busy = false;
                    return;
                }

                if (Automaton.GetDistance(wirtCorpse.Position, _playerPosition) > 5)
                    MoveTo(wirtCorpse.Position);

                do
                {
                    _input.DoInputAtWorldPosition("{LMB}", wirtCorpse.Position);
                    System.Threading.Thread.Sleep(100);
                    wirtCorpse.Update();
                }
                while (wirtCorpse.Struct.Mode == 0);

                do
                {
                    System.Threading.Thread.Sleep(100);
                }
                while (!_gameData.AllItems.Any(x => x.ItemBaseName == "Wirt's Leg"));

                var leg = _gameData.AllItems.Where(x => x.ItemBaseName == "Wirt's Leg").First();

                do
                {
                    _input.DoInputAtWorldPosition("{LMB}", leg.Position);
                    System.Threading.Thread.Sleep(500);
                    leg.Update();
                }
                while (leg.ItemModeMapped != ItemModeMapped.Inventory);

                _movement.TakePortalHome();

                _townManager.OpenTradeMenu();

                do
                {
                    System.Threading.Thread.Sleep(100);
                }
                while (_townManager.State != TownState.TRADE_MENU);

                var npcInventory = _townManager.ActiveNPC.GetNpcInventory();

                var tpTome = npcInventory.Where(x => x.ItemBaseName == "Tome of Town Portal").FirstOrDefault() ?? new UnitItem(IntPtr.Zero);

                if (!tpTome.IsValidPointer)
                {
                    _log.Error("Couldn't find Tome of Town Portal, aborting!");
                    _busy = false;
                    return;
                }

                _menuMan.VendorBuyOne(tpTome.X, tpTome.Y);

                _menuMan.CloseMenu();
                _townManager.OpenStashMenu();

                do
                {
                    System.Threading.Thread.Sleep(100);
                }
                while (_townManager.State != TownState.STASH_MENU);

                var cube = _gameData.AllItems.Where(x => x.ItemBaseName == "Horadric Cube").FirstOrDefault() ?? new UnitItem(IntPtr.Zero);

                if (!cube.IsValidPointer)
                {
                    _log.Error("Couldn't find Horadric Cube, aborting!");
                    _busy = false;
                    return;
                }

                _menuMan.ClickStashItem(cube.X, cube.Y, true);

                var legInventory = _inventory.ItemsToTrash.Where(x => x.ItemBaseName == "Wirt's Leg").First();
                var tpTomeInventory = _inventory.ItemsToTrash.Where(x => x.ItemBaseName == "Tome of Town Portal").First();

                _menuMan.StashItemAt(legInventory.X, legInventory.Y);
                System.Threading.Thread.Sleep(300);
                _menuMan.StashItemAt(tpTomeInventory.X, tpTomeInventory.Y);
                System.Threading.Thread.Sleep(300);

                _menuMan.ClickTransmute();
                System.Threading.Thread.Sleep(500);
                _menuMan.CloseMenu();

                System.Threading.Thread.Sleep(1500);

                var cowPortal = _gameData.Objects.Where(x => x.TxtFileNo == (uint)GameObject.PermanentTownPortal).FirstOrDefault() ?? new UnitObject(IntPtr.Zero);
                if (!cowPortal.IsValidPointer)
                {
                    _log.Error("Couldn't open cow portal, aborting!");
                    _busy = false;
                    return;
                }

                _movement.ChangeArea(Area.MooMooFarm, cowPortal);
            }
            catch (Exception exception)
            {
                _log.Error(exception);
                _error = true;
            }

            _busy = false;
        }

        private void MoveTo(Point position)
        {
            if (_combat.HasTeleport)
            {
                _movement.TeleportTo(position);
            }
            else
            {
                _movement.WalkTo(position);
            }

            do
            {
                System.Threading.Thread.Sleep(100);
            }
            while (_movement.Busy);
        }

        private void ClearArea(Point position, bool reposition = true)
        {
            _combat.ClearArea(position, reposition);

            do
            {
                System.Threading.Thread.Sleep(100);
            }
            while (!_combat.IsSafe && !_abort);
        }

        private void PickThings()
        {
            while (_pickit.HasWork)
            {
                if (_abort)
                {
                    _log.Info("Aborting run while picking things.");
                    return;
                }

                _pickit.Run();

                do
                {
                    System.Threading.Thread.Sleep(100);

                    if (_abort)
                    {
                        _log.Info("Aborting run while picking things.");
                        return;
                    }
                }
                while (_pickit.Busy);
            }
        }
    }
}
