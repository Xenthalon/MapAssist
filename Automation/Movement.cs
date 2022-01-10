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
    public class Movement
    {
        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();
        private static readonly int _areaChangeSafetyLimit = 3;
        private static readonly int _moveMaxTries = 3;
        private static readonly int _abortLimit = 3;
        private static readonly string TELEPORT_KEY = "w";

        private BackgroundWorker _movementWorker;
        private Input _input;
        private GameData _gameData;
        private MenuMan _menuMan;
        private Pathing _pathing;

        private Area _currentArea = Area.None;
        private Area _possiblyNewArea = Area.None;
        private int _possiblyNewAreaCounter = 0;
        private int _moveTries = 0;
        private int _failuresUntilAbort = 0;
        private bool _useTeleport = false;
        private bool _moving = false;
        private List<Point> _path;
        private Point? _targetLocation;

        public bool Busy => _moving;

        public Movement(Input input, MenuMan menuMan)
        {
            _input = input;
            _menuMan = menuMan;

            _movementWorker = new BackgroundWorker();
            _movementWorker.DoWork += new DoWorkEventHandler(Move);
            _movementWorker.WorkerSupportsCancellation = true;
        }

        public void Update(GameData gameData, Pathing pathing)
        {
            if (gameData != null && gameData.PlayerUnit.IsValidPointer() && gameData.PlayerUnit.IsValidUnit())
            {
                _gameData = gameData;
                _pathing = pathing;

                if (_gameData.Area != _currentArea && _possiblyNewArea != _gameData.Area)
                {
                    _log.Debug("Currently in " + _currentArea + ", Possible new area " + _gameData.Area);
                    _possiblyNewArea = _gameData.Area;
                }

                if (_possiblyNewArea == _gameData.Area)
                {
                    _log.Debug($"counting {_possiblyNewAreaCounter + 1}/{_areaChangeSafetyLimit}");
                    _possiblyNewAreaCounter += 1;
                }

                if (_possiblyNewAreaCounter >= _areaChangeSafetyLimit)
                {
                    _log.Debug("Area changed to " + _gameData.Area + ", resetting Movement.");
                    _currentArea = _gameData.Area;
                    _movementWorker.CancelAsync();
                    _moving = false;
                    _path = new List<Point>();
                    _targetLocation = null;
                    _possiblyNewArea = Area.None;
                    _possiblyNewAreaCounter = 0;
                }

                if (_moving && !_movementWorker.IsBusy)
                {
                    _movementWorker.RunWorkerAsync();
                }
            }
        }

        public void GetInLOSRange(Point target, double minRange, double maxRange, bool hasTeleport = false)
        {
            if (target == null || (target.X == 0 && target.Y == 0))
            {
                return;
            }

            var circleSpots = new List<Point>();

            for (var range = minRange; range <= maxRange; range++)
            {
                circleSpots.AddRange(GetCirclePoints(target, range));
            }

            var nicestSpot = circleSpots.Where(x => _pathing.HasLineOfSight(target, x) && _pathing.IsWalkable(x))
                                        .OrderByDescending(x => Automaton.GetDistance(x, target))
                                        .Take(36)
                                        // this should get us the point with enemies the furthest away from it
                                        .OrderByDescending(x => _gameData.Monsters.Count() > 0 ? Automaton.GetDistance(x, _gameData.Monsters.OrderBy(m => Automaton.GetDistance(m.Position, x)).First().Position) : 0)
                                        // .OrderBy(x => Automaton.GetDistance(_gameData.PlayerPosition, x))
                                        .FirstOrDefault();

            if (nicestSpot == null || (nicestSpot.X == 0 && nicestSpot.Y == 0))
            {
                _log.Info("Couldn't find cool location, getting personal.");
                nicestSpot = target;
            }

            try
            {
                if (hasTeleport)
                {
                    TeleportTo(nicestSpot);
                }
                else
                {
                    WalkTo(nicestSpot);
                }
            }
            catch (NoPathFoundException) { }

            do
            {
                System.Threading.Thread.Sleep(100);
            }
            while (Busy);
        }

        // adapted from https://stackoverflow.com/questions/5300938/calculating-the-position-of-points-in-a-circle
        public List<Point> GetCirclePoints(Point target, double range)
        {
            var pointAmount = 36.0; // tripple of clock, should be okay?
            var points = new List<Point>();

            var slice = 2 * Math.PI / pointAmount;

            for (var i = 0; i < pointAmount; i++)
            {
                var angle = slice * i;
                var newX = (int)target.X + (range * Math.Cos(angle));
                var newY = (int)target.Y + (range * Math.Sin(angle));

                points.Add(new Point((float)newX, (float)newY));
            }

            return points;
        }

        public void TeleportTo(Point worldPosition)
        {
            if (_moving || _movementWorker.IsBusy)
            {
                // emergency abort
                _moving = false;
                _movementWorker.CancelAsync();
            }
            else
            {
                _log.Debug($"Teleporting to {worldPosition}");
                _useTeleport = true;
                _targetLocation = worldPosition;
                _path = _pathing.GetPathToLocation(_gameData.MapSeed, _gameData.Difficulty, true, _gameData.PlayerPosition, worldPosition);

                if (_path.Count() > 0)
                {
                    _moving = true;

                    _movementWorker.RunWorkerAsync();
                }
                else
                {
                    _log.Error("Unable to find path to " + worldPosition);
                    Reset();
                    throw new NoPathFoundException();
                }
            }
        }

        public void WalkTo(Point worldPosition)
        {
            if (_moving || _movementWorker.IsBusy)
            {
                // emergency abort
                _moving = false;
                _movementWorker.CancelAsync();
            }
            else
            {
                _log.Debug($"Walking to {worldPosition}");
                _useTeleport = false;
                _targetLocation = worldPosition;
                _path = _pathing.GetPathToLocation(_gameData.MapSeed, _gameData.Difficulty, false, _gameData.PlayerPosition, worldPosition);

                if (_path.Count() > 0)
                {
                    _moving = true;

                    _movementWorker.RunWorkerAsync();
                }
                else
                {
                    _log.Error("Unable to find path to " + worldPosition);
                }
            }
        }

        public void Reset()
        {
            _currentArea = Area.None;
            _movementWorker.CancelAsync();
            _useTeleport = false;
            _moving = false;
            _path = new List<Point>();
            _targetLocation = null;
            _moveTries = 0;
            _failuresUntilAbort = 0;
        }

        private void Move(object sender, DoWorkEventArgs e)
        {
            if (_gameData != null && _targetLocation != null && _pathing != null)
            {
                var success = _useTeleport ? Teleport(_path[0]) : Walk(_path[0]);

                if (success)
                {
                    _log.Debug($"Moved to {_path[0].X}/{_path[0].Y}");
                    _moveTries = 0;
                }
                else
                {
                    _log.Warn("Move went wrong, recalculating and retrying!");
                    _moveTries += 1;

                    if (_failuresUntilAbort > _abortLimit)
                    {
                        _log.Error("Reached abort limit, exiting game!");
                        Reset();
                        _menuMan.ExitGame();
                        return;
                    }

                    if (_moveTries >= _moveMaxTries)
                    {
                        _failuresUntilAbort += 1;

                        Point retryPoint = GetRecoveryPoint(_failuresUntilAbort);

                        _log.Warn($"Seems we are stuck, trying to recover from {retryPoint.X}/{retryPoint.Y}.");

                        if (_useTeleport)
                            Teleport(retryPoint);
                        else
                            Walk(retryPoint);

                        _moveTries = 0;
                    }

                    _path = _pathing.GetPathToLocation(_gameData.MapSeed, _gameData.Difficulty, _useTeleport, _gameData.PlayerPosition, (Point)_targetLocation);

                    return;
                }

                _path.RemoveAt(0);

                if (_path.Count == 0)
                {
                    _log.Debug($"Done moving!");
                    _moving = false;
                    _moveTries = 0;
                    _failuresUntilAbort = 0;
                    _targetLocation = null;
                    _useTeleport = false;
                }
            }
        }

        /// <summary>
        /// Immediate teleport to spot, no path finding. Should be on screen.
        /// </summary>
        /// <param name="worldPoint"></param>
        /// <returns></returns>
        public bool Teleport(Point worldPoint)
        {
            _input.DoInputAtWorldPosition(TELEPORT_KEY, worldPoint);

            for (var i = 0; i < 20; i++)
            {
                System.Threading.Thread.Sleep(50);

                if (IsNear(_gameData.PlayerPosition, worldPoint))
                    break;
            }

            System.Threading.Thread.Sleep(100);
            return IsNear(_gameData.PlayerPosition, worldPoint);
        }

        /// <summary>
        /// Immediate walking to spot, no path finding. Should be on screen.
        /// </summary>
        /// <param name="worldPoint"></param>
        /// <returns></returns>
        public bool Walk(Point worldPoint)
        {
            _input.DoInputAtWorldPosition("{LMB}", worldPoint);

            for (var i = 0; i < 50; i++)
            {
                System.Threading.Thread.Sleep(50);

                if (IsNear(_gameData.PlayerPosition, worldPoint))
                    break;
            }

            return IsNear(_gameData.PlayerPosition, worldPoint);
        }

        private Point GetRecoveryPoint(int tryNumber)
        {
            var recoveryLocation = new Point(_gameData.PlayerPosition.X, _gameData.PlayerPosition.Y);

            if (tryNumber == 1)
            {
                recoveryLocation.X = recoveryLocation.X - 10;
            }
            else if (tryNumber == 2)
            {
                recoveryLocation.Y = recoveryLocation.Y - 10;
            }
            else if (tryNumber == 3)
            {
                recoveryLocation.X = recoveryLocation.X + 10;
            }

            return recoveryLocation;
        }

        private List<Point> GetPointsOnLineBetween(Point start, Point through)
        {
            // from https://stackoverflow.com/questions/21249739/how-to-calculate-the-points-between-two-given-points-and-given-distance

            var diff_X = through.X - start.X;
            var diff_Y = through.Y - start.Y;
            var pointNum = 5;

            var interval_X = diff_X / (pointNum + 1);
            var interval_Y = diff_Y / (pointNum + 1);

            var pointList = new List<Point>();

            for (var i = 1; i <= pointNum; i++)
            {
                pointList.Add(new Point(start.X + (interval_X * i), start.Y + (interval_Y * i)));
            }

            return pointList;
        }

        private bool IsNear(Point p1, Point p2)
        {
            var range = 5;

            return Automaton.GetDistance(p1, p2) < range;
        }
    }
}
