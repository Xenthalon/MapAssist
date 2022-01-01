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
        private static readonly int _gameChangeSafetyLimit = 150;
        private static readonly bool _autostart = true;

        private BackgroundWorker _worker;
        private BackgroundWorker _explorer;

        private BuffBoy _buffboy;
        private Chicken _chicken;
        private Combat _combat;
        private Input _input;
        private MenuMan _menuMan;
        private Movement _movement;
        private Pathing _pathing;
        private PickIt _pickit;
        private TownManager _townManager;

        private Area _currentArea;
        private uint _currentGameSeed = 0;
        private uint _possiblyNewGameSeed = 0;
        private int _possiblyNewGameCounter = 0;
        private List<PointOfInterest> _pointsOfInterest;
        private GameData _gameData;

        private int _activeProfileIndex = -1;
        private bool _goBotGo = false;

        private List<Point> _exploreSpots = new List<Point>();
        private bool _exploring = false;

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

            _explorer = new BackgroundWorker();
            _explorer.DoWork += new DoWorkEventHandler(DoExplorationStep);
            _explorer.WorkerSupportsCancellation = true;

            _runProfiles.Add(new RunProfile { Name = "Mephisto", Type = RunType.KillTarget, AreaPath = new Area[] { Area.DuranceOfHateLevel2, Area.DuranceOfHateLevel3 }, KillSpot = new Point(17565, 8070), MonsterType = Npc.Mephisto });
            _runProfiles.Add(new RunProfile { Name = "Ancient Tunnels", Type = RunType.Explore, AreaPath = new Area[] { Area.LostCity, Area.AncientTunnels } });
            _runProfiles.Add(new RunProfile { Name = "Pindleskin", Type = RunType.ClearArea, AreaPath = new Area[] { Area.Harrogath, Area.NihlathaksTemple }, KillSpot = new Point(10058, 13234), Reposition = false });
        }

        public void Update(GameData gameData, List<PointOfInterest> pointsOfInterest, Pathing pathing)
        {
            if (gameData != null && gameData.PlayerUnit.IsValidPointer() && gameData.PlayerUnit.IsValidUnit())
            {
                _gameData = gameData;
                _currentArea = gameData.Area;
                _pointsOfInterest = pointsOfInterest;
                _pathing = pathing;

                if (_gameData.MapSeed != _currentGameSeed && _gameData.MapSeed != _possiblyNewGameSeed)
                {
                    _log.Debug("Current game " + _currentGameSeed + ", possible new game " + _gameData.MapSeed);
                    _possiblyNewGameSeed = _gameData.MapSeed;
                }

                if (_possiblyNewGameSeed == _gameData.MapSeed)
                {
                    _log.Debug($"counting {_possiblyNewGameCounter + 1}/{_gameChangeSafetyLimit}");
                    _possiblyNewGameCounter += 1;
                }

                if (_possiblyNewGameCounter >= _gameChangeSafetyLimit)
                {
                    _log.Info("Entered new game " + _gameData.MapSeed + ", resetting everything.");

                    Reset();

                    _currentGameSeed = _gameData.MapSeed;

                    if (_autostart)
                    {
                        Task.Factory.StartNew(() =>
                        {
                            System.Threading.Thread.Sleep(5000);
                            _log.Info("Go run!");
                            Run();
                        });
                    }
                }

                if (_goBotGo && !_worker.IsBusy)
                {
                    _worker.RunWorkerAsync();
                }

                if (_goBotGo && _exploring && !_explorer.IsBusy)
                {
                    _explorer.RunWorkerAsync();
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

        public void Reset()
        {
            _goBotGo = false;
            _activeProfileIndex = -1;
            _worker.CancelAsync();
            _worker = new BackgroundWorker();
            _worker.DoWork += new DoWorkEventHandler(Orchestrate);
            _worker.WorkerSupportsCancellation = true;
            _possiblyNewGameSeed = 0;
            _possiblyNewGameCounter = 0;

            _buffboy.Reset();
            _chicken.Reset();
            _combat.Reset();
            _movement.Reset();
            _pickit.Reset();
            _townManager.Reset();
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
                _menuMan.ExitGame();
                System.Threading.Thread.Sleep(10000);
                return;
            }

            _activeProfileIndex = _activeProfileIndex + 1;

            RunProfile activeProfile = _runProfiles[_activeProfileIndex];

            _log.Info("Let's do " + activeProfile.Name);

            RecoverCorpse();
            BeltAnyPotions();
            GoHeal();
            GoTrade();
            ReviveMerc();
            var stashed = GoStash();

            if (!stashed)
            {
                // !! PANIKK
                _log.Info("Stash is full or something went wrong, shutting down.");
                _goBotGo = false;
                _activeProfileIndex = _runProfiles.Count() - 1;
                return;
            }

            foreach (Area area in activeProfile.AreaPath)
            {
                if (_goBotGo == false)
                {
                    _log.Info("received kill signal, aborting");
                    return;
                }

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

                        try
                        {
                            MoveTo((Point)interactPoint);
                        }
                        catch (NoPathFoundException)
                        {
                            TakePortalHome();
                            return;
                        }
                    }

                    if (_goBotGo == false)
                    {
                        _log.Info("received kill signal, aborting");
                        return;
                    }

                    // go back to town here if area not changed?
                    ChangeArea(area, (Point)interactPoint);
                }
            }

            if (!_townManager.IsInTown)
            {
                BuffMe();
            }

            if (_goBotGo == false)
            {
                _log.Info("received kill signal, aborting");
                return;
            }

            if (activeProfile.KillSpot != null && activeProfile.KillSpot.X != 0 && activeProfile.KillSpot.Y != 0)
            {
                _log.Info("Moving to KillSpot " + activeProfile.KillSpot);
                MoveTo(activeProfile.KillSpot);
            }

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
                _combat.ClearArea(_gameData.PlayerPosition, activeProfile.Reposition);
            }
            else if (activeProfile.Type == RunType.Explore)
            {
                _exploreSpots = _pathing.GetExploratoryPath(true, _gameData.PlayerPosition);

                _exploring = true;

                do
                {
                    System.Threading.Thread.Sleep(1000);
                }
                while (_exploring);
            }

            do
            {
                System.Threading.Thread.Sleep(100);

                if (!_combat.IsSafe && !_combat.Busy)
                {
                    _combat.ClearArea(_gameData.PlayerPosition);
                }
            }
            while (!_combat.IsSafe);

            System.Threading.Thread.Sleep(300);

            _pickit.Run();

            do
            {
                System.Threading.Thread.Sleep(100);
            }
            while (_pickit.Busy);

            if (_goBotGo == false)
            {
                _log.Info("received kill signal, aborting");
                return;
            }

            if (_activeProfileIndex == _runProfiles.Count() - 1)
            {
                _log.Info("Finished run!");
                _goBotGo = false;
                _menuMan.ExitGame();
                System.Threading.Thread.Sleep(10000);
            }
            else
            {
                TakePortalHome();
            }
        }

        public void ExploreArea()
        {
            _exploreSpots = _pathing.GetExploratoryPath(true, _gameData.PlayerPosition);

            _exploring = true;
        }

        private void DoExplorationStep(object sender, DoWorkEventArgs e)
        {
            if (_exploreSpots.Count() <= 0)
            {
                _log.Info("Exploration concluded!");
                _exploring = false;
            }
            else
            {
                var nextSpot = _exploreSpots[0];

                MoveTo(nextSpot);

                _exploreSpots.RemoveAt(0);

                do
                {
                    System.Threading.Thread.Sleep(100);

                    if (!_combat.IsSafe && !_combat.Busy)
                    {
                        _combat.ClearArea(_gameData.PlayerPosition);
                    }
                }
                while (!_combat.IsSafe);

                System.Threading.Thread.Sleep(100);

                _pickit.Run();

                do
                {
                    System.Threading.Thread.Sleep(100);
                }
                while (_pickit.Busy);
            }
        }

        private void GoHeal()
        {
            // maybe also check for poison or curses
            if (_chicken.PlayerLifePercentage < 0.9 || _chicken.MercLifePercentage < 0.7)
            {
                _townManager.Heal();

                do
                {
                    System.Threading.Thread.Sleep(100);
                }
                while (_townManager.State != TownState.IDLE);
            }
        }

        private void GoTrade()
        {
            if (Inventory.AnyItemsToTrash || Inventory.TPScrolls < 5)
            {
                _townManager.OpenTradeMenu();

                do
                {
                    System.Threading.Thread.Sleep(100);
                }
                while (_townManager.State != TownState.TRADE_MENU);

                System.Threading.Thread.Sleep(500);

                foreach (var item in Inventory.ItemsToTrash)
                {
                    _log.Info("Selling " + Items.ItemName(item.TxtFileNo));
                    _menuMan.SellItemAt(item.X, item.Y);
                }

                if (Inventory.TPScrolls < 15)
                {
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

                // fix for weird path finding issues in Act 2
                if (_gameData.Area == Area.LutGholein)
                {
                    _movement.Walk(new Point(5113, 5092));
                }
            }

            return success;
        }

        private bool ReviveMerc()
        {
            var maxRetries = 3;
            var retryCount = 0;

            while (_chicken.MercIsDead && retryCount < maxRetries)
            {
                _log.Info("Reviving merc!");
                _townManager.ReviveMerc();

                do
                {
                    System.Threading.Thread.Sleep(100);
                }
                while (_townManager.State != TownState.IDLE);

                retryCount += 1;
            }

            return !_chicken.MercIsDead;
        }

        private void RecoverCorpse()
        {
            if (_gameData.Players.Any(x => x.Value.IsCorpse && x.Value.Name == _gameData.PlayerUnit.Name && x.Value.Mode == 17 && x.Value.UnitId != _gameData.PlayerUnit.UnitId))
            {
                _log.Info("Awww shucks, seems we died, grabbing corpse.");

                var corpse = _gameData.Players.Where(x => x.Value.IsCorpse && x.Value.Name == _gameData.PlayerUnit.Name).First().Value;

                if (Automaton.GetDistance(corpse.Position, _gameData.PlayerUnit.Position) > 5)
                {
                    MoveTo(corpse.Position);
                }

                var maxRetries = 3;
                var retryCount = 0;

                do
                {
                    _input.DoInputAtWorldPosition("{LMB}", corpse.Position);
                    System.Threading.Thread.Sleep(1000);
                    retryCount += 1;

                    if (retryCount >= maxRetries)
                    {
                        _log.Error("Couldn't pick up corpse, exiting game.");
                        _menuMan.ExitGame();
                        break;
                    }
                }
                while (_gameData.Players.Any(x => x.Value.IsCorpse && x.Value.Name == _gameData.PlayerUnit.Name));
            }
        }

        private void BeltAnyPotions()
        {
            if (Inventory.AnyItemsToBelt && !Inventory.IsBeltFull())
            {
                _log.Info($"Putting {Inventory.ItemsToBelt.Count()} potions into belt.");

                _menuMan.OpenInventory();

                foreach (var potion in Inventory.ItemsToBelt)
                {
                    _menuMan.PutItemIntoBelt(potion.X, potion.Y);
                }

                _menuMan.CloseMenu();
            }
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
                System.Threading.Thread.Sleep(1500); // town loads take longer than other area changes
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
                System.Threading.Thread.Sleep(1000);
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
