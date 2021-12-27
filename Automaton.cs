using GameOverlay.Drawing;
using MapAssist.Automation;
using MapAssist.Helpers;
using MapAssist.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MapAssist
{
    class Automaton
    {
        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();

        private GameData _currentGameData;
        private List<PointOfInterest> _pointsOfInterests;
        private bool _useChicken = true;
        private Input _input;
        private Chicken _chicken;
        private Combat _combat;
        private Movement _movement;
        private PickIt _pickit;

        public Automaton()
        {
            _input = new Input();
            _chicken = new Chicken(_input);
            _movement = new Movement(_input);
            _combat = new Combat(_input);
            _pickit = new PickIt(_movement, _input);
        }

        public void Update(GameData gameData, List<PointOfInterest> pointsOfInterest, Pathing pathing, Rectangle windowRect)
        {
            _currentGameData = gameData;
            _pointsOfInterests = pointsOfInterest;

            Inventory.Update(_currentGameData.PlayerUnit.UnitId, _currentGameData.Items);
            _input.Update(gameData, windowRect);
            _combat.Update(gameData);
            _movement.Update(gameData, pathing);
            _pickit.Update(gameData);

            if (_useChicken == true)
            {
                _chicken.Update(gameData);
            }
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
                        _log.Info($"{i} {unitAny.UnitId}: {unitAny.UnitType} {unitAny.Name} {unitAny.Position}");

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
        }

        public void Fight()
        {
            _combat.ClearArea(_currentGameData.PlayerPosition);
            
            if (!_combat.Busy)
            {
                _pickit.Run();
            }
        }

        public void StartAutoTele()
        {
            _log.Debug($"Teleporting to {_pointsOfInterests[0].Label}");
            _movement.MoveTo(_pointsOfInterests[0].Position);
        }

        public static Point TranslateToScreenOffset(Point playerPositionWorld, Point targetPositionWorld, Point playerPositionScreen)
        {
            var magic = 51.192;
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
