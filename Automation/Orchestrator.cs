using GameOverlay.Drawing;
using MapAssist.Settings;
using MapAssist.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapAssist.Automation
{
    class Orchestrator
    {
        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();
        
        private static List<RunProfile> _runProfiles = new List<RunProfile>();

        private BackgroundWorker _worker;

        private BuffBoy _buffboy;
        private Chicken _chicken;
        private Combat _combat;
        private Input _input;
        private MenuMan _menuMan;
        private Movement _movement;
        private PickIt _pickit;
        private TownManager _townManager;

        private Area _currentArea;
        private uint _currentGameSeed = 0;
        private List<PointOfInterest> _pointsOfInterest;
        private GameData _gameData;

        private int _activeProfileIndex = -1;
        private bool _goBotGo = false;

        public string PortalKey = "f";

        public Orchestrator(
            BuffBoy buffboy,
            Chicken chicken,
            Combat combat,
            Input input,
            Movement movement,
            MenuMan menuMan,
            PickIt pickIt,
            TownManager townManager)
        {
            _buffboy = buffboy;
            _chicken = chicken;
            _combat = combat;
            _input = input;
            _menuMan = menuMan;
            _movement = movement;
            _pickit = pickIt;
            _townManager = townManager;

            _worker = new BackgroundWorker();
            _worker.DoWork += new DoWorkEventHandler(Orchestrate);
            _worker.WorkerSupportsCancellation = true;

            _runProfiles.Add(new RunProfile { Name = "Mephisto", Type = RunType.KillTarget, AreaPath = new Area[] { Area.DuranceOfHateLevel2, Area.DuranceOfHateLevel3 }, KillSpot = new Point(17565, 8070), MonsterType = Npc.Mephisto });
            _runProfiles.Add(new RunProfile { Name = "Pindleskin", Type = RunType.ClearArea, AreaPath = new Area[] { Area.Harrogath, Area.NihlathaksTemple }, KillSpot = new Point(10058, 13234) });
        }

        public void Update(GameData gameData, List<PointOfInterest> pointsOfInterest)
        {
            if (gameData != null && gameData.PlayerUnit.IsValidPointer() && gameData.PlayerUnit.IsValidUnit())
            {
                _gameData = gameData;
                _currentArea = gameData.Area;
                _pointsOfInterest = pointsOfInterest;

                if (_gameData.MapSeed != _currentGameSeed)
                {
                    _log.Info("Entered new game " + _gameData.MapSeed + ", resetting everything.");

                    _goBotGo = false;
                    _activeProfileIndex = -1;
                    _worker.CancelAsync();
                    _worker = new BackgroundWorker();
                    _worker.DoWork += new DoWorkEventHandler(Orchestrate);
                    _worker.WorkerSupportsCancellation = true;

                    _buffboy.Reset();
                    _combat.Reset();
                    _movement.Reset();
                    _pickit.Reset();
                    _townManager.Reset();

                    _currentGameSeed = _gameData.MapSeed;

                    Task.Factory.StartNew(() =>
                    {
                        System.Threading.Thread.Sleep(5000);
                        _log.Info("Go run!");
                        Run();
                    });
                }

                if (_goBotGo && !_worker.IsBusy)
                {
                    _worker.RunWorkerAsync();
                }
            }
        }

        public void Run()
        {
            if (_worker.IsBusy)
            {
                _worker.CancelAsync();
                _goBotGo = false;
            }
            else
            {
                _worker.RunWorkerAsync();
                _log.Info("Go Bot Go!");
                _goBotGo = true;
            }
        }

        private void Orchestrate(object sender, DoWorkEventArgs e)
        {
            if (_activeProfileIndex == _runProfiles.Count() - 1)
            {
                _log.Error("Exhausted all profiles, done!");
                _goBotGo = false;
                _menuMan.ExitGame();
                System.Threading.Thread.Sleep(10000);
                return;
            }

            if (!_townManager.IsInTown)
            {
                _log.Error("Every run needs to start in town, something is borked!");
                return;
            }

            _activeProfileIndex = _activeProfileIndex + 1;

            RunProfile activeProfile = _runProfiles[_activeProfileIndex];

            _log.Info("Let's do " + activeProfile.Name);

            GoHeal();
            GoBuyStuff();
            var stashed = GoStash();

            if (!stashed)
            {
                // !! PANIKK
                _goBotGo = false;
                _activeProfileIndex = _runProfiles.Count() - 1;
                return;
            }

            foreach (Area area in activeProfile.AreaPath)
            {
                if (_currentArea == area)
                    continue;

                if (_townManager.IsInTown && _menuMan.IsWaypointArea(area))
                {
                    var isActChange = _menuMan.IsActChange(area);

                    _townManager.OpenWaypointMenu();

                    do
                    {
                        System.Threading.Thread.Sleep(100);
                    }
                    while (_townManager.State != TownState.WP_MENU);

                    _menuMan.TakeWaypoint(area);

                    // wait for load
                    if (isActChange)
                    {
                        System.Threading.Thread.Sleep(5000);
                    }
                    else
                    {
                        System.Threading.Thread.Sleep(1500);
                    }
                }
                else
                {
                    if (!_townManager.IsInTown)
                    {
                        BuffMe();
                    }

                    Point? interactPoint = null;

                    if (area == Area.NihlathaksTemple)
                    {
                        MoveTo(new Point(5124, 5119));

                        var nihlaPortal = _gameData.Objects.Where(x => x.TxtFileNo == (uint)GameObject.PermanentTownPortal).FirstOrDefault() ?? new UnitAny(IntPtr.Zero);

                        if (nihlaPortal.IsValidPointer())
                        {
                            interactPoint = nihlaPortal.Position;
                        }
                    }
                    else
                    {
                        var target = _pointsOfInterest.Where(x => x.Label == Utils.GetAreaLabel(area, _gameData.Difficulty)).FirstOrDefault();

                        if (target == null)
                        {
                            _log.Error("Couldn't find PointOfInterest for " + Utils.GetAreaLabel(area, _gameData.Difficulty) + "! Help!");
                            return;
                        }

                        interactPoint = target.Position;

                        MoveTo((Point)interactPoint);
                    }

                    ChangeArea(area, (Point)interactPoint);
                }
            }

            if (!_townManager.IsInTown)
            {
                BuffMe();
            }

            _log.Info("Moving to KillSpot " + activeProfile.KillSpot);
            MoveTo(activeProfile.KillSpot);

            if (activeProfile.Type == RunType.KillTarget)
            {
                if (activeProfile.MonsterType != Npc.NpcNotApplicable)
                {
                    _log.Info("Gonna kill " + activeProfile.MonsterType);

                    _combat.Kill((uint) activeProfile.MonsterType);

                    do
                    {
                        System.Threading.Thread.Sleep(100);
                    }
                    while (_combat.Busy);

                    _log.Info("We got that sucker, making sure things are safe...");

                    _combat.ClearArea(_gameData.PlayerPosition);
                }
            }
            else if (activeProfile.Type == RunType.ClearArea)
            {
                // not quite right, need pathing here
                _combat.ClearArea(_gameData.PlayerPosition);
            }

            do
            {
                System.Threading.Thread.Sleep(100);
            }
            while (!_combat.IsSafe);

            _pickit.Run();

            do
            {
                System.Threading.Thread.Sleep(100);
            }
            while (_pickit.Busy);

            TakePortalHome();
        }

        private void GoHeal()
        {
            // maybe also check for poison or curses
            if (_chicken.PlayerLifePercentage < 0.9)
            {
                _townManager.Heal();

                do
                {
                    System.Threading.Thread.Sleep(100);
                }
                while (_townManager.State != TownState.IDLE);
            }
        }

        private void GoBuyStuff()
        {
            if (Inventory.TPScrolls < 5)
            {
                _townManager.OpenTradeMenu();

                do
                {
                    System.Threading.Thread.Sleep(100);
                }
                while (_townManager.State != TownState.TRADE_MENU);

                System.Threading.Thread.Sleep(500);

                var npcInventory = _townManager.ActiveNPC.GetNpcInventory();

                var tp = npcInventory.Where(x => x.TxtFileNo == 529).FirstOrDefault() ?? new UnitAny(IntPtr.Zero);

                if (tp.IsValidPointer())
                {
                    _menuMan.VendorBuyMax(tp.X, tp.Y);
                }
                else
                {
                    _log.Error("Couldn't find Scroll of Town Portals at " + _townManager.ActiveNPC.UnitId);
                }

                _menuMan.CloseMenu();
            }
        }

        private bool GoStash()
        {
            var success = true;

            if (Inventory.AnyItemsToStash)
            {
                _townManager.OpenStashMenu();

                do
                {
                    System.Threading.Thread.Sleep(100);
                }
                while (_townManager.State != TownState.STASH_MENU);

                foreach (var item in Inventory.ItemsToStash)
                {
                    _log.Info("Stashing " + Items.ItemName(item.TxtFileNo));
                    _menuMan.StashItemAt(item.X, item.Y);
                }

                if (Inventory.AnyItemsToStash)
                {
                    _log.Error("Something is really wrong, stop everything!");
                    success = false;
                }
                else
                {
                    _menuMan.CloseMenu();
                }
            }

            return success;
        }

        private bool TakePortalHome()
        {
            var retryLimit = 3;
            var retryCount = 0;

            var success = false;

            _log.Info("Taking portal home!");

            var portal = new UnitAny(IntPtr.Zero);

            do
            {
                _input.DoInput(PortalKey);
                System.Threading.Thread.Sleep(2000);
                portal = _gameData.Objects.Where(x => x.TxtFileNo == (uint)GameObject.TownPortal).FirstOrDefault() ?? new UnitAny(IntPtr.Zero);
                retryCount += 1;
            }
            while (!portal.IsValidPointer() && retryCount <= retryLimit);

            if (portal.IsValidPointer())
            {
                var destinationArea = (Area)Enum.ToObject(typeof(Area), portal.ObjectData.InteractType);

                success = ChangeArea(destinationArea, portal.Position);
            }
            else
            {
                _log.Error("Couldn't find portal, help!");
            }

            return success;
        }

        private bool ChangeArea(Area destination, Point interactionPoint)
        {
            var success = true;

            var isActChange = _menuMan.IsActChange(destination);

            _input.DoInputAtWorldPosition("{LMB}", interactionPoint);

            var retryLimit = 3;
            var loopLimit = 30;
            var loops = 0;
            var retrys = 0;

            do
            {
                System.Threading.Thread.Sleep(100);

                loops += 1;

                if (loops >= loopLimit)
                {
                    retrys += 1;
                    loops = 0;

                    _input.DoInputAtWorldPosition("{LMB}", interactionPoint);

                    if (retrys >= retryLimit)
                    {
                        _log.Error("Unable to interact with " + interactionPoint + ", help!");
                        success = false;
                        break;
                    }
                }
            }
            while (_currentArea != destination);

            // wait for load
            if (isActChange)
            {
                System.Threading.Thread.Sleep(4000);
            }
            else
            {
                System.Threading.Thread.Sleep(1200);
            }

            return success;
        }

        private void BuffMe()
        {
            if (_buffboy.HasWork)
            {
                _buffboy.Run();

                do
                {
                    System.Threading.Thread.Sleep(100);
                }
                while (_buffboy.Busy);
            }
        }

        private void MoveTo(Point point)
        {
            if (_townManager.IsInTown)
            {
                _movement.WalkTo(point);
            }
            else
            {
                _movement.TeleportTo(point);
            }

            do
            {
                System.Threading.Thread.Sleep(100);
            }
            while (_movement.Busy);
        }
    }
}
