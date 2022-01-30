using GameOverlay.Drawing;
using MapAssist.Helpers;
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
        private short MAX_RETRIES;
        private string GAMBLE_ITEM;
        private int GAMBLE_STOP_AT;
        private int GAME_CHANGE_SAFETY_LIMIT;
        private bool AUTO_START;

        private static List<RunProfile> RUN_PROFILES = new List<RunProfile>();

        private BackgroundWorker _worker;
        private BackgroundWorker _explorer;

        private BuffBoy _buffboy;
        private Chicken _chicken;
        private Combat _combat;
        private Input _input;
        private Inventory _inventory;
        private MapApi _mapApi;
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
        private IRunProfile _activeSpecialProfile;
        private bool _goBotGo = false;

        private List<Point> _exploreSpots = new List<Point>();
        private bool _exploring = false;

        public Orchestrator(
            BotConfiguration config,
            BuffBoy buffboy,
            Chicken chicken,
            Combat combat,
            Input input,
            Inventory inventory,
            Movement movement,
            MenuMan menuMan,
            Pathing pathing,
            PickIt pickIt,
            TownManager townManager)
        {
            MAX_RETRIES = (short)config.Settings.MaxRetries;
            GAMBLE_ITEM = config.Character.GambleItem;
            GAMBLE_STOP_AT = config.Character.GambleGoldStop;
            GAME_CHANGE_SAFETY_LIMIT = config.Settings.GameChangeVerificationAttempts;
            AUTO_START = config.Settings.Autostart;

            RUN_PROFILES.AddRange(config.RunProfiles);

            _buffboy = buffboy;
            _chicken = chicken;
            _combat = combat;
            _input = input;
            _inventory = inventory;
            _menuMan = menuMan;
            _movement = movement;
            _pathing = pathing;
            _pickit = pickIt;
            _townManager = townManager;

            _worker = new BackgroundWorker();
            _worker.DoWork += new DoWorkEventHandler(Orchestrate);
            _worker.WorkerSupportsCancellation = true;

            _explorer = new BackgroundWorker();
            _explorer.DoWork += new DoWorkEventHandler(DoExplorationStep);
            _explorer.WorkerSupportsCancellation = true;
        }

        public void Update(GameData gameData, List<PointOfInterest> pointsOfInterest, MapApi mapApi)
        {
            if (gameData != null && gameData.PlayerUnit.IsValidPointer && gameData.PlayerUnit.IsValidUnit)
            {
                _gameData = gameData;
                _currentArea = gameData.Area;
                _pointsOfInterest = pointsOfInterest;

                if (_gameData.MapSeed != _currentGameSeed && _gameData.MapSeed != _possiblyNewGameSeed)
                {
                    _log.Debug("Current game " + _currentGameSeed + ", possible new game " + _gameData.MapSeed);
                    _possiblyNewGameSeed = _gameData.MapSeed;
                }

                if (_possiblyNewGameSeed == _gameData.MapSeed)
                {
                    _log.Debug($"counting {_possiblyNewGameCounter + 1}/{GAME_CHANGE_SAFETY_LIMIT}");
                    _possiblyNewGameCounter += 1;
                }

                if (_possiblyNewGameCounter >= GAME_CHANGE_SAFETY_LIMIT)
                {
                    _log.Info("Entered new game " + _gameData.MapSeed + ", resetting everything.");

                    Reset();

                    _currentGameSeed = _gameData.MapSeed;

                    if (AUTO_START)
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

            if (mapApi != null)
            {
                _mapApi = mapApi;
            }

            if (_activeSpecialProfile != null)
            {
                _activeSpecialProfile.Update(gameData, pointsOfInterest);

                if (!_goBotGo)
                {
                    _activeSpecialProfile.Abort();
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
            _exploring = false;
            _exploreSpots = new List<Point>();
            _activeSpecialProfile = null;

            _buffboy.Reset();
            _chicken.Reset();
            _combat.Reset();
            _movement.Reset();
            _pathing.Reset();
            _pickit.Reset();
            _townManager.Reset();
        }

        private void Orchestrate(object sender, DoWorkEventArgs e)
        {
            if (_activeProfileIndex == RUN_PROFILES.Count() - 1)
            {
                _log.Error("Exhausted all profiles, done!");
                _goBotGo = false;
                _menuMan.ExitGame();
                return;
            }

            _activeProfileIndex = _activeProfileIndex + 1;

            RunProfile activeProfile = RUN_PROFILES[_activeProfileIndex];

            if (!_townManager.IsInTown && _currentArea != activeProfile.AreaPath[0].Area)
            {
                _log.Error("Every run needs to start in town, something is borked!");
                _goBotGo = false;
                _menuMan.ExitGame();
                return;
            }

            _log.Info("Let's do " + activeProfile.Name);

            if (_townManager.IsInTown)
            {
                RecoverCorpse();
                BeltAnyPotions();
                _combat.PrepareForTown();
                var stashed = DoChores();

                if (!stashed)
                {
                    // !! PANIKK
                    _log.Info("Stash is full or something went wrong, shutting down.");
                    _goBotGo = false;
                    _activeProfileIndex = RUN_PROFILES.Count() - 1;
                    return;
                }
            }

            if (activeProfile.MonsterFilter.Count > 0)
            {
                _combat.MonsterFilter = activeProfile.MonsterFilter;
            }

            foreach (RunArea runArea in activeProfile.AreaPath)
            {
                if (_goBotGo == false)
                {
                    _log.Info("Aborting run after Area change to " + runArea.Area);
                    return;
                }

                if (_currentArea == runArea.Area)
                    continue;

                if (_townManager.IsInTown && _menuMan.IsWaypointArea(runArea.Area))
                {
                    var isActChange = _menuMan.IsActChange(runArea.Area);

                    _townManager.OpenWaypointMenu();

                    do
                    {
                        System.Threading.Thread.Sleep(100);

                        if (_goBotGo == false)
                        {
                            _log.Info("Aborting run while waiting for Waypoint Menu.");
                            _townManager.Reset();
                            return;
                        }
                    }
                    while (_townManager.State != TownState.WP_MENU);

                    _menuMan.TakeWaypoint(runArea.Area);

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
                        _combat.PrepareForCombat(); // maybe select defense aura for movement?
                    }

                    _log.Info("looking for " + runArea.Area);

                    Point? interactPoint = null;

                    if (runArea.Area == Area.NihlathaksTemple)
                    {
                        MoveTo(new Point(5124, 5119));

                        var nihlaPortal = _gameData.Objects.Where(x => x.TxtFileNo == (uint)GameObject.PermanentTownPortal).FirstOrDefault() ?? new UnitObject(IntPtr.Zero);

                        if (nihlaPortal.IsValidPointer)
                        {
                            interactPoint = nihlaPortal.Position;
                        }
                    }
                    else
                    {
                        var count = 0;
                        PointOfInterest target = null;

                        while (target == null && count < MAX_RETRIES)
                        {
                            target = _pointsOfInterest.Where(x => x.NextArea == runArea.Area).FirstOrDefault();

                            if (target != null)
                            {
                                break;
                            }

                            System.Threading.Thread.Sleep(3000);
                            count += 1;
                        }

                        if (target == null)
                        {
                            _log.Error("Couldn't find PointOfInterest for " + runArea.Area.Name() + "! Help!");
                            _movement.TakePortalHome();
                            return;
                        }

                        interactPoint = target.Position;

                        try
                        {
                            _log.Info("Moving to " + ((Point)interactPoint).X + "/" + ((Point)interactPoint).Y + " from " + _gameData.PlayerPosition.X + "/" + _gameData.PlayerPosition.Y + " Distance " + Automaton.GetDistance((Point)interactPoint, _gameData.PlayerPosition));
                            MoveTo((Point)interactPoint);
                            _log.Info("Distance now: " + Automaton.GetDistance(_gameData.PlayerPosition, (Point)interactPoint));
                        }
                        catch (NoPathFoundException)
                        {
                            _movement.TakePortalHome();
                            return;
                        }
                    }

                    if (_goBotGo == false)
                    {
                        _log.Info("Aborting run before changing to next area from " + runArea.Area);
                        return;
                    }

                    var changedArea = false;

                    if (AreaExtensions.RequiresStitching(runArea.Area))
                    {
                        // means it's connected
                        var areaData = _mapApi.GetMapData(runArea.Area);

                        var behind = _movement.GetPointBehind(_gameData.PlayerPosition, (Point)interactPoint, 8);

                        if (!areaData.IncludesPoint(behind))
                        {
                            _log.Info("point " + behind.X + "/" + behind.Y + " wasn't good, switching");

                            behind = _movement.GetPointBehind((Point)interactPoint, _gameData.PlayerPosition, 8);
                        }

                        _log.Info(runArea.Area + " is connected, got point " + behind.X + "/" + behind.Y);

                        _movement.Teleport(behind);

                        System.Threading.Thread.Sleep(500);

                        changedArea = _gameData.Area == runArea.Area;
                    }
                    else
                    {
                        // otherwise it's a level exit
                        _log.Info(runArea.Area + " trying to click it");
                        if (runArea.Area == Area.NihlathaksTemple || runArea.Area == Area.Abaddon) // expand here later
                        {
                            var portal = _gameData.Objects.Where(x => x.TxtFileNo == (uint)GameObject.PermanentTownPortal).FirstOrDefault() ?? new UnitObject(IntPtr.Zero);
                            changedArea = _movement.ChangeArea(runArea.Area, portal);
                        }
                        else
                        {
                            changedArea = _movement.ChangeArea(runArea.Area, (Point)interactPoint);
                        }
                    }

                    if (_goBotGo == false)
                    {
                        _log.Info("Aborting run after trying to change area.");
                        return;
                    }

                    if (!changedArea)
                    {
                        if (_townManager.IsInTown)
                        {
                            _log.Info("Failed to change area from " + runArea.Area + ", quitting game.");
                            _goBotGo = false;
                            _menuMan.ExitGame();
                            return;
                        }
                        else
                        {
                            _log.Info("failed to change area from " + runArea.Area + ", returning to town.");
                            _movement.TakePortalHome();
                            return;
                        }
                    }

                    if (runArea.GroupSize > 0)
                    {
                        _combat.GroupSize = runArea.GroupSize;
                    }

                    if (runArea.CombatRange > -1)
                    {
                        _combat.SetCombatRange((short)runArea.CombatRange);
                    }

                    _combat.OpenChests = runArea.OpenChests;

                    if (activeProfile.Type == RunType.Explore && runArea.Kill != KillType.Nothing)
                    {
                        _log.Info("Gonna kill some things in " + runArea.Area);
                        _combat.HuntBosses = runArea.Kill == KillType.Bosses;
                        _exploreSpots = _pathing.GetExploratoryPath(_combat.HasTeleport, _gameData.PlayerPosition);
                        _exploring = true;

                        do
                        {
                            System.Threading.Thread.Sleep(500);

                            if (_goBotGo == false)
                            {
                                _log.Info("Aborting run while exploring " + activeProfile.Name);
                                _exploring = false;
                                return;
                            }
                        }
                        while (_exploring);

                        _exploreSpots = new List<Point>();
                    }
                }
            }

            if (!_townManager.IsInTown)
            {
                BuffMe();
                _combat.PrepareForCombat();
            }

            if (_goBotGo == false)
            {
                _log.Info("Aborting run after entering final area.");
                return;
            }

            if (activeProfile.KillSpot != null && activeProfile.KillSpot.X != 0 && activeProfile.KillSpot.Y != 0)
            {
                _log.Info("Moving to KillSpot " + activeProfile.KillSpot);
                MoveTo(activeProfile.KillSpot);
            }

            if (activeProfile.GoSuperchest && _pointsOfInterest.Any(x => x.Type == PoiType.SuperChest))
            {
                var location = _pointsOfInterest.Where(x => x.Type == PoiType.SuperChest).First().Position;

                _log.Info("Moving to Superchest " + location);
                MoveTo(location);
            }

            if (activeProfile.Target != null && activeProfile.Target.Length > 0 && _pointsOfInterest.Any(x => x.Label == activeProfile.Target))
            {
                var location = _pointsOfInterest.Where(x => x.Label == activeProfile.Target).First().Position;

                _log.Info("Moving to Point of Interest " + activeProfile.Target);
                MoveTo(location);
            }

            if (activeProfile.Type == RunType.KillTarget)
            {
                if (activeProfile.MonsterType != Npc.NpcNotApplicable)
                {
                    _log.Info("Gonna kill " + activeProfile.MonsterType);

                    _combat.Kill((uint) activeProfile.MonsterType, true, activeProfile.Reposition);

                    do
                    {
                        System.Threading.Thread.Sleep(100);

                        if (_goBotGo == false)
                        {
                            _log.Info("Aborting run while killing " + activeProfile.MonsterType);
                            _combat.Reset();
                            return;
                        }
                    }
                    while (_combat.Busy);
                }
                else if (!string.IsNullOrEmpty(activeProfile.MonsterName))
                {
                    _log.Info("Gonna kill " + activeProfile.MonsterName);

                    _combat.Kill(activeProfile.MonsterName, true, activeProfile.Reposition);

                    do
                    {
                        System.Threading.Thread.Sleep(100);

                        if (_goBotGo == false)
                        {
                            _log.Info("Aborting run while killing " + activeProfile.MonsterType);
                            _combat.Reset();
                            return;
                        }
                    }
                    while (_combat.Busy);
                }
            }
            else if (activeProfile.Type == RunType.ClearArea)
            {
                _combat.ClearArea(_gameData.PlayerPosition, activeProfile.Reposition);
            }
            else if (activeProfile.Type == RunType.Travincal)
            {
                _activeSpecialProfile = new Profiles.Travincal(_buffboy, _combat, _movement, _pickit);
                System.Threading.Thread.Sleep(500); // give update time to insert data
                _activeSpecialProfile.Run();

                do
                {
                    System.Threading.Thread.Sleep(100);
                }
                while (_activeSpecialProfile.IsBusy() && !_activeSpecialProfile.HasError());

                _activeSpecialProfile = null;
            }
            else if (activeProfile.Type == RunType.Cows)
            {
                _activeSpecialProfile = new Profiles.Cows(_buffboy, _combat, _input, _inventory, _menuMan, _movement, _pickit, _townManager);
                System.Threading.Thread.Sleep(500); // give update time to insert data
                _activeSpecialProfile.Run();

                do
                {
                    System.Threading.Thread.Sleep(100);
                }
                while (_activeSpecialProfile.IsBusy() && !_activeSpecialProfile.HasError());

                _activeSpecialProfile = null;

                if (_currentArea == Area.MooMooFarm)
                {
                    _combat.OpenChests = activeProfile.AreaPath[0].OpenChests;
                    _combat.HuntBosses = activeProfile.AreaPath[0].Kill == KillType.Bosses;
                    
                    if (activeProfile.AreaPath[0].GroupSize > 0)
                        _combat.GroupSize = activeProfile.AreaPath[0].GroupSize;

                    _exploreSpots = _pathing.GetExploratoryPath(_combat.HasTeleport, _gameData.PlayerPosition);
                    _exploring = true;

                    do
                    {
                        System.Threading.Thread.Sleep(500);

                        if (_goBotGo == false)
                        {
                            _log.Info("Aborting run while exploring " + activeProfile.Name);
                            _exploring = false;
                            return;
                        }
                    }
                    while (_exploring);

                    _exploreSpots = new List<Point>();
                }
            }

            do
            {
                System.Threading.Thread.Sleep(100);

                if (!_combat.IsSafe && !_combat.Busy)
                {
                    _combat.ClearArea(_gameData.PlayerPosition);
                }

                if (_goBotGo == false)
                {
                    _log.Info("Aborting run while doing a final sweep.");
                    _combat.Reset();
                    return;
                }
            }
            while (!_combat.IsSafe);

            System.Threading.Thread.Sleep(300);

            _combat.CheckChests();

            PickThings();

            if (_goBotGo == false)
            {
                _log.Info("Aborting run after final pickit.");
                return;
            }

            if (_activeProfileIndex == RUN_PROFILES.Count() - 1)
            {
                _log.Info("Finished run!");
                _goBotGo = false;
                _menuMan.ExitGame();
            }
            else
            {
                // this is for eldritch + shenk, don't take tp if already there
                if (RUN_PROFILES[_activeProfileIndex + 1].AreaPath[0].Area != _currentArea)
                {
                    _movement.TakePortalHome();
                }
            }

            _combat.Reset();
        }

        public void ExploreArea()
        {
            _exploreSpots = _pathing.GetExploratoryPath(_combat.HasTeleport, _gameData.PlayerPosition);
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
                if (_combat.IsSafe && _buffboy.HasWork)
                {
                    System.Threading.Thread.Sleep(500);
                    BuffMe();
                }

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

                    if (_goBotGo == false)
                    {
                        _log.Info("Aborting run while exploring.");
                        return;
                    }
                }
                while (!_combat.IsSafe);

                _combat.CheckChests();

                PickThings();
            }
        }

        private bool DoChores()
        {
            GoHeal();
            ReviveMerc();
            GoRepair();
            GoTrade();
            GoGamble();
            var result = GoStash();
            System.Threading.Thread.Sleep(500); // fixes potential race condition between oog reset after 300 ms
            return result;
        }

        private void PickThings()
        {
            while (_pickit.HasWork)
            {
                if (_goBotGo == false)
                {
                    _log.Info("Aborting run while picking things.");
                    return;
                }

                _pickit.Run();

                do
                {
                    System.Threading.Thread.Sleep(50);

                    if (_goBotGo == false)
                    {
                        _log.Info("Aborting run while picking things.");
                        return;
                    }
                }
                while (_pickit.Busy);

                if (_pickit.Full)
                {
                    var currentArea = _gameData.Area;

                    _movement.TakePortalHome();

                    DoChores();

                    _townManager.GoToPortalSpot();

                    do
                    {
                        System.Threading.Thread.Sleep(100);

                        if (_goBotGo == false)
                        {
                            _log.Info("Aborting run while going to town portal.");
                            return;
                        }
                    }
                    while (_townManager.State != TownState.PORTAL_SPOT);

                    var portal = _gameData.Objects.Where(x => x.TxtFileNo == (uint)GameObject.TownPortal &&
                                                              (Area)Enum.ToObject(typeof(Area), x.ObjectData.InteractType) == currentArea).FirstOrDefault() ?? new UnitObject(IntPtr.Zero);

                    if (portal.IsValidPointer)
                    {
                        if (Automaton.GetDistance(_gameData.PlayerPosition, portal.Position) > 10)
                        {
                            _movement.WalkTo(portal.Position);
                        }

                        var success = _movement.ChangeArea(currentArea, portal);

                        if (success)
                        {
                            _pickit.Reset();
                            System.Threading.Thread.Sleep(500);
                        }
                        else
                        {
                            _log.Error("Unable to return to world area, please help!");
                            _goBotGo = false;
                        }
                    }
                    else
                    {
                        _log.Error("Couldn't find town portal, help!");
                        _goBotGo = false;
                    }
                }
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
            if (_inventory.AnyItemsToIdentify || _inventory.AnyItemsToTrash || _inventory.TPScrolls < 5)
            {
                _townManager.OpenTradeMenu();

                do
                {
                    System.Threading.Thread.Sleep(100);
                }
                while (_townManager.State != TownState.TRADE_MENU);

                System.Threading.Thread.Sleep(500);

                _menuMan.SelectVendorTab(3); // sometimes tab is not set to misc

                foreach (var item in _inventory.ItemsToTrash)
                {
                    _log.Info($"Selling {item.ItemData.ItemQuality} {item.ItemBaseName}.");
                    _menuMan.SellItemAt(item.X, item.Y);
                }

                var npcInventory = _townManager.ActiveNPC.GetNpcInventory();

                foreach (var item in _inventory.ItemsToIdentify)
                {
                    var idsc = npcInventory.Where(x => x.TxtFileNo == 530).FirstOrDefault() ?? new UnitItem(IntPtr.Zero);

                    if (idsc.IsValidPointer)
                    {
                        var retries = 0;

                        do
                        {
                            _menuMan.VendorBuyOne(idsc.X, idsc.Y);
                            retries += 1;
                        }
                        while (!_inventory.IDScroll.IsValidPointer && retries <= MAX_RETRIES);

                        if (_inventory.IDScroll.IsValidPointer)
                        {
                            var itemX = item.X;
                            var itemY = item.Y;

                            _menuMan.RightClickInventoryItem(_inventory.IDScroll.X, _inventory.IDScroll.Y);
                            _menuMan.LeftClickInventoryItem(item.X, item.Y);

                            if (item.ItemMode == ItemMode.ONCURSOR)
                            {
                                _log.Info("Picked up " + item.ItemBaseName + ", oh dear!");

                                do
                                {
                                    _menuMan.LeftClickInventoryItem(itemX, itemY);
                                    System.Threading.Thread.Sleep(500);
                                }
                                while (item.ItemMode == ItemMode.ONCURSOR);
                            }

                            var identifiedItem = item.Update();

                            while (!identifiedItem.IsIdentified)
                            {
                                System.Threading.Thread.Sleep(100);
                                identifiedItem = identifiedItem.Update();
                            }

                            if (!Identification.IdentificationFilter.IsKeeper(identifiedItem))
                            {
                                _log.Info(item.ItemData.ItemQuality + " " + item.ItemBaseName + " didn't make the cut, selling!");
                                _menuMan.SellItemAt(item.X, item.Y);
                            }
                        }
                        else
                        {
                            _log.Error("Couldn't find Scroll of Identification in Inventory! Something is wrong!");
                            break;
                        }
                    }
                    else
                    {
                        _log.Error("Couldn't find Scroll of Identification at " + _townManager.ActiveNPC.UnitId);
                    }
                }

                if (_inventory.TPScrolls < 15)
                {
                    var tp = npcInventory.Where(x => x.TxtFileNo == 529).FirstOrDefault() ?? new UnitItem(IntPtr.Zero);

                    if (tp.IsValidPointer)
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

        public void GoGamble()
        {
            if (_inventory.NeedsGamble)
            {
                _log.Info("GAMBLE GAMBLE GAMBLE!");

                while (_inventory.Gold > GAMBLE_STOP_AT)
                {
                    _townManager.OpenGambleMenu();

                    do
                    {
                        System.Threading.Thread.Sleep(100);
                    }
                    while (_townManager.State != TownState.GAMBLE_MENU);

                    System.Threading.Thread.Sleep(1000);

                    var npcInventory = _townManager.ActiveNPC.GetNpcInventory();

                    var gambleItem = npcInventory.Where(x => x.ItemBaseName == GAMBLE_ITEM).FirstOrDefault() ?? new UnitItem(IntPtr.Zero);

                    if (gambleItem.IsValidPointer)
                    {
                        while (_inventory.Gold > GAMBLE_STOP_AT && _inventory.Freespace >= _inventory.GetItemTotalSize(gambleItem))
                        {
                            _log.Info($"Buying one more {GAMBLE_ITEM}, free {_inventory.Freespace}, gold {_inventory.Gold}");
                            _menuMan.VendorBuyOne(gambleItem.X, gambleItem.Y);
                        }
                    }
                    else
                    {
                        _log.Error("Couldn't find " + GAMBLE_ITEM + " in Gamble inventory, something is wrong!");
                        return;
                    }

                    _menuMan.CloseMenu();

                    _townManager.OpenGambleTradeMenu();

                    do
                    {
                        System.Threading.Thread.Sleep(100);
                    }
                    while (_townManager.State != TownState.TRADE_MENU);

                    foreach (var item in _inventory.ItemsToStash)
                    {
                        if (!Identification.IdentificationFilter.IsKeeper(item))
                        {
                            _log.Info(item.ItemData.ItemQuality + " " + item.ItemBaseName + " didn't make the cut, selling!");
                            _menuMan.SellItemAt(item.X, item.Y);
                        }
                    }

                    foreach (var item in _inventory.ItemsToTrash)
                    {
                        _log.Info("Selling trash " + item.ItemData.ItemQuality + " " + item.ItemBaseName + ".");
                        _menuMan.SellItemAt(item.X, item.Y);
                    }

                    _menuMan.CloseMenu();
                }
            }
        }

        private bool GoStash()
        {
            var success = true;

            if (_inventory.AnyItemsToStash)
            {
                _townManager.OpenStashMenu();

                do
                {
                    System.Threading.Thread.Sleep(100);
                }
                while (_townManager.State != TownState.STASH_MENU);

                foreach (var item in _inventory.ItemsToStash)
                {
                    _log.Info("Stashing " + item.ItemBaseName);
                    _menuMan.StashItemAt(item.X, item.Y);
                }

                _menuMan.DepositGold();

                if (_inventory.AnyItemsToStash)
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

        private void GoRepair()
        {
            if (_inventory.NeedsRepair)
            {
                _townManager.Repair();

                do
                {
                    System.Threading.Thread.Sleep(100);
                }
                while (_townManager.State != TownState.IDLE);
            }
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
            if (_gameData.Players.Any(x => x.Value.IsCorpse && x.Value.Name == _gameData.PlayerUnit.Name && x.Value.UnitId != _gameData.PlayerUnit.UnitId))
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
                        _goBotGo = false;
                        break;
                    }
                }
                while (_gameData.Players.Any(x => x.Value.IsCorpse && x.Value.Name == _gameData.PlayerUnit.Name));
            }
        }

        private void BeltAnyPotions()
        {
            if (_inventory.AnyItemsToBelt && !_inventory.IsBeltFull())
            {
                _log.Info($"Putting {_inventory.ItemsToBelt.Count()} potions into belt.");

                _menuMan.OpenInventory();

                foreach (var potion in _inventory.ItemsToBelt)
                {
                    _menuMan.PutItemIntoBelt(potion.X, potion.Y);
                }

                _menuMan.CloseMenu();
            }
        }

        private void BuffMe(bool force = false)
        {
            if (force || _buffboy.HasWork)
            {
                _buffboy.Run(force);

                do
                {
                    System.Threading.Thread.Sleep(200);
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
