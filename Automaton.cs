using GameOverlay.Drawing;
using MapAssist.API;
using MapAssist.Automation;
using MapAssist.Helpers;
using MapAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MapAssist
{
    public class Automaton
    {
        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();

        private GameData _currentGameData;
        private List<PointOfInterest> _pointsOfInterests;

        public Movement Movement => _movement;
        public Pathing Pathing => _pathing;

        private BuffBoy _buffBoy;
        private Chicken _chicken;
        private Combat _combat;
        private Input _input;
        private Inventory _inventory;
        private MenuMan _menuMan;
        private Movement _movement;
        private Orchestrator _orchestrator;
        private Pathing _pathing;
        private PickIt _pickit;
        private TownManager _townManager;

        public Automaton(BotConfiguration config)
        {
            _inventory = new Inventory(config);
            _input = new Input();
            _pathing = new Pathing(config);
            _buffBoy = new BuffBoy(config, _input);
            _menuMan = new MenuMan(config, _input);
            _movement = new Movement(config, _input, _menuMan, _pathing);
            _combat = new Combat(config, _input, _movement, _pathing);
            _chicken = new Chicken(config, _combat, _input, _inventory, _menuMan, _movement);
            _pickit = new PickIt(config, _input, _inventory, _movement);
            _townManager = new TownManager(config, _input, _menuMan, _movement);

            _orchestrator = new Orchestrator(config, _buffBoy, _chicken, _combat, _input, _inventory, _movement, _menuMan, _pathing, _pickit, _townManager);
        }

        public void Update(GameData gameData, List<PointOfInterest> pointsOfInterest, AreaData areaData, MapApi mapApi, Rectangle windowRect)
        {
            _currentGameData = gameData;
            _pointsOfInterests = pointsOfInterest;

            _inventory.Update(gameData);
            _input.Update(gameData, windowRect);
            _pathing.Update(gameData, areaData);
            _buffBoy.Update(gameData);
            _chicken.Update(gameData);
            _combat.Update(gameData);
            _menuMan.Update(gameData, windowRect);
            _movement.Update(gameData);
            _pickit.Update(gameData);
            _townManager.Update(gameData, areaData);
            _orchestrator.Update(gameData, pointsOfInterest, mapApi);
        }

        public void Reset()
        {
            _orchestrator.Reset();
        }

        public BotStateModel GetState()
        {
            return new BotStateModel() {
                InGame = _currentGameData != null ? _currentGameData.MenuOpen.InGame : false,
                CharacterName = _currentGameData != null ? _currentGameData.PlayerName : "",
                MapSeed = _currentGameData != null ? _currentGameData.MapSeed : 0
            };
        }

        public void dumpGameData()
        {
            _log.Info("Units:");
            var rosterData = new Roster(GameManager.RosterDataOffset);

            for (var i = 0; i < 12; i++)
            {
                var unitHashTable = GameManager.UnitHashTable(128 * 8 * i);
                var unitType = (UnitType)i;
                foreach (var pUnitAny in unitHashTable.UnitTable)
                {
                    var unitAny = new UnitAny(pUnitAny);
                    while (unitAny.IsValidUnit())
                    {
                        _log.Info($"{i} {unitAny.UnitId}: {unitAny.UnitType} {unitAny.TxtFileNo} {unitAny.Name} {unitAny.Position}");

                        unitAny = unitAny.ListNext(rosterData);
                    }
                }
            }

            _log.Info("Belt items:");
            foreach (UnitAny item in _currentGameData.Items.Where(x => x.ItemData.dwOwnerID == _currentGameData.PlayerUnit.UnitId && x.ItemData.InvPage == InvPage.NULL && x.ItemData.BodyLoc == BodyLoc.NONE).OrderBy(x => x.X % 4))
            {
                // belt is nodePos 2/2 ... or InvPage NULL, BodyLoc NULL
                _log.Info($"{Items.ItemName(item.TxtFileNo)} {(item.X % 4) + 1}/{item.X / 4}");
            }

            _log.Info("Inventory items:");
            foreach (UnitAny item in _currentGameData.Items.Where(x => x.ItemData.dwOwnerID == _currentGameData.PlayerUnit.UnitId && x.ItemData.InvPage == InvPage.INVENTORY))
            {
                _log.Info($"{Items.ItemName(item.TxtFileNo)} at {item.X}/{item.Y}");
            }

            _log.Info("Other items:");
            foreach (UnitAny item in _currentGameData.Items.Where(x => x.ItemData.dwOwnerID != _currentGameData.PlayerUnit.UnitId))
            {
                _log.Info($"{Items.ItemName(item.TxtFileNo)} at {item.X}/{item.Y} Owner {item.ItemData.dwOwnerID}");
            }
        }

        public void GoBotGo()
        {
            _orchestrator.Run();
        }

        public void MouseMoveTest()
        {
            _menuMan.ClickRepair();
        }

        public void Fight()
        {
            if (!_combat.IsSafe)
            {
                _log.Info("Not safe, fighting!");
                _combat.ClearArea(_currentGameData.PlayerPosition);
            }
            else
            {
                if (_buffBoy.HasWork && !_buffBoy.Busy)
                {
                    _log.Info("No buffs, buffing!");
                    _buffBoy.Run();

                    do
                    {
                        System.Threading.Thread.Sleep(200);
                    }
                    while (_buffBoy.Busy);
                }

                _pickit.Run();
            }
        }

        public void DoTownStuff()
        {
            _townManager.OpenWaypointMenu();
        }

        public void DoExploreStuff()
        {
            _orchestrator.ExploreArea();
        }

        public void StartAutoTele()
        {
            _log.Debug($"Teleporting to {_pointsOfInterests[0].Label}");
            _movement.TeleportTo(_pointsOfInterests[0].Position);
        }

        public static Point TranslateToScreenOffset(Point playerPositionWorld, Point targetPositionWorld, Point playerPositionScreen)
        {
            // var magic = 51.192; // old value from my widescreen gaming
            var magic = 39; // value for 1920x1080, maybe also works for other 16:9 ratios?
            var beta = 45f * Math.PI / 180d;

            var relativePosition = new Point(targetPositionWorld.X - playerPositionWorld.X, targetPositionWorld.Y - playerPositionWorld.Y);
            var s0 = (Math.Cos(beta) * relativePosition.X) - (Math.Sin(beta) * relativePosition.Y);
            var s1 = (Math.Sin(beta) * relativePosition.X) + (Math.Cos(beta) * relativePosition.Y);

            // calculate vector to screen coordinate vector
            // I suppose this is my approximation of a screen matrix
            // var diffX = (s0 * 1.2) * 42.66;
            // var diffY = (s1 * 0.7) * 37.16;
            var diffX = s0 * magic;
            var diffY = s1 * magic * 0.5;
            // maybe try around dividing screen width and height through 28,085

            return new Point((int)(playerPositionScreen.X + diffX), (int)(playerPositionScreen.Y + diffY));
        }

        public static double GetDistance(Point p1, Point p2)
        {
            return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
        }
    }
}
