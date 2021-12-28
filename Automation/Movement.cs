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
    class Movement
    {
        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();
        private static int _areaChangeSafetyLimit = 3;

        private BackgroundWorker _movementWorker;
        private Input _input;
        private GameData _gameData;
        private Pathing _pathing;

        private Area _currentArea = Area.None;
        private Area _possiblyNewArea = Area.None;
        private int _possiblyNewAreaCounter = 0;
        private bool _useTeleport = false;
        private bool _moving = false;
        private List<Point> _path;
        private Point? _targetLocation;

        public bool Busy => _moving;

        public Movement(Input input)
        {
            _input = input;

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
                    _log.Debug($"counting {_possiblyNewAreaCounter}/{_areaChangeSafetyLimit}");
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

        public void TeleportTo(Point worldPosition)
        {
            if (_moving || _movementWorker.IsBusy)
            {
                // emergency abort
                _moving = false;
                _movementWorker.CancelAsync();
            }

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

        public void Reset()
        {
            _currentArea = Area.None;
            _movementWorker.CancelAsync();
            _useTeleport = false;
            _moving = false;
            _path = new List<Point>();
            _targetLocation = null;
        }

        private void Move(object sender, DoWorkEventArgs e)
        {
            if (_gameData != null && _targetLocation != null && _pathing != null)
            {
                var success = _useTeleport ? Teleport(_path[0]) : Walk(_path[0]);

                if (success)
                {
                    _log.Debug($"Moved to {_path[0].X}/{_path[0].Y}");
                }
                else
                {
                    _log.Warn("Move went wrong, recalculating and retrying!");

                    _path = _pathing.GetPathToLocation(_gameData.MapSeed, _gameData.Difficulty, _useTeleport, _gameData.PlayerPosition, (Point)_targetLocation);

                    return;
                }

                _path.RemoveAt(0);

                if (_path.Count == 0)
                {
                    _log.Debug($"Done moving!");
                    _moving = false;
                    _targetLocation = null;
                    _useTeleport = false;
                }
            }
        }

        private bool Teleport(Point worldPoint)
        {
            _input.DoInputAtWorldPosition("w", worldPoint);

            for (var i = 0; i < 20; i++)
            {
                System.Threading.Thread.Sleep(50);

                if (IsNear(_gameData.PlayerPosition, worldPoint))
                    break;
            }

            System.Threading.Thread.Sleep(100);
            return IsNear(_gameData.PlayerPosition, worldPoint);
        }

        private bool Walk(Point worldPoint)
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

        private bool IsNear(Point p1, Point p2)
        {
            var range = 5;

            return Automaton.GetDistance(p1, p2) < range;
        }
    }
}
