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
        private Pathing _pathing;
        private BackgroundWorker _teleportWorker;
        private bool _teleporting = false;
        private bool _useChicken = true;
        private Input _input;
        private Chicken _chicken;
        private Combat _combat;
        private List<Point> _teleportPath;

        public Automaton()
        {
            _input = new Input();
            _chicken = new Chicken();
            _combat = new Combat(_input);
        }

        public void Update(GameData gameData, List<PointOfInterest> pointsOfInterest, Pathing pathing, Rectangle windowRect)
        {
            _currentGameData = gameData;
            _pointsOfInterests = pointsOfInterest;
            _pathing = pathing;

            Inventory.Update(_currentGameData.PlayerUnit.UnitId, _currentGameData.Items);
            _input.Update(gameData, windowRect);
            _combat.Update(gameData);

            if (_useChicken == true)
            {
                _chicken.Update(gameData);
            }

            if (_teleporting && _teleportWorker != null && !_teleportWorker.IsBusy)
            {
                _teleportWorker.RunWorkerAsync();
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
        }

        public void StartAutoTele()
        {
            if (_teleportWorker != null && _teleportWorker.IsBusy)
            {
                _teleportWorker.CancelAsync();
                _teleportWorker.Dispose();
                _teleporting = false;
            }

            _log.Debug($"Teleporting to {_pointsOfInterests[0].Label}");

            _teleportPath = _pathing.GetPathToLocation(_currentGameData.MapSeed, _currentGameData.Difficulty, true, _currentGameData.PlayerPosition, _pointsOfInterests[0].Position);

            _teleporting = true;

            _teleportWorker = new BackgroundWorker();
            _teleportWorker.DoWork += new DoWorkEventHandler(autoTele);
            _teleportWorker.WorkerSupportsCancellation = true;
            _teleportWorker.RunWorkerAsync();
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

        private void autoTele(object sender, DoWorkEventArgs e)
        {
            if (_currentGameData != null && _pointsOfInterests != null && _pointsOfInterests.Count > 0 && _pathing != null)
            {
                var teleportSuccess = TeleportTo(_teleportPath[0]);
                System.Threading.Thread.Sleep(100);

                if (teleportSuccess)
                {
                    _log.Debug($"Teleported to {_teleportPath[0].X}/{_teleportPath[0].Y}");
                }
                else
                {
                    _log.Warn("Teleport went wrong, recalculating and retrying!");

                    _teleportPath = _pathing.GetPathToLocation(_currentGameData.MapSeed, _currentGameData.Difficulty, true, _currentGameData.PlayerPosition, _pointsOfInterests[0].Position);

                    return;
                }

                if (_teleportPath.Count == 1)
                {
                    _input.DoInputAtWorldPosition("{LMB}", _teleportPath[0]);
                    _log.Debug("Took exit.");
                }

                _teleportPath.RemoveAt(0);

                if (_teleportPath.Count == 0)
                {
                    _log.Debug($"Done teleporting!");
                    _teleporting = false;
                }
            }
        }

        private bool TeleportTo(Point worldPoint)
        {
            _input.DoInputAtWorldPosition("w", worldPoint);

            for (var i = 0; i < 20; i++)
            {
                System.Threading.Thread.Sleep(50);

                if (IsNear(_currentGameData.PlayerPosition, worldPoint))
                    break;
            }

            return IsNear(_currentGameData.PlayerPosition, worldPoint);
        }

        private bool IsNear(Point p1, Point p2)
        {
            var range = 5;

            return GetDistance(p1, p2) < range;
        }

        public static double GetDistance(Point p1, Point p2)
        {
            return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
        }
    }
}
