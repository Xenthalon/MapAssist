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

        private BackgroundWorker _movementWorker;
        private Input _input;
        private GameData _gameData;
        private Pathing _pathing;

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

                if (_moving && !_movementWorker.IsBusy)
                {
                    _movementWorker.RunWorkerAsync();
                }
            }
        }

        public void MoveTo(Point worldPosition)
        {
            if (_moving || _movementWorker.IsBusy)
            {
                // emergency abort
                _moving = false;
                _movementWorker.CancelAsync();
            }

            _log.Debug($"Moving to {worldPosition}");

            _targetLocation = worldPosition;
            _path = _pathing.GetPathToLocation(_gameData.MapSeed, _gameData.Difficulty, true, _gameData.PlayerPosition, worldPosition);
            _moving = true;

            _movementWorker.RunWorkerAsync();
        }

        private void Move(object sender, DoWorkEventArgs e)
        {
            if (_gameData != null && _targetLocation != null && _pathing != null)
            {
                var teleportSuccess = TeleportTo(_path[0]);
                System.Threading.Thread.Sleep(100);

                if (teleportSuccess)
                {
                    _log.Debug($"Teleported to {_path[0].X}/{_path[0].Y}");
                }
                else
                {
                    _log.Warn("Teleport went wrong, recalculating and retrying!");

                    _path = _pathing.GetPathToLocation(_gameData.MapSeed, _gameData.Difficulty, true, _gameData.PlayerPosition, (Point)_targetLocation);

                    return;
                }

                _path.RemoveAt(0);

                if (_path.Count == 0)
                {
                    _log.Debug($"Done teleporting!");
                    _moving = false;
                    _targetLocation = null;
                }
            }
        }

        private bool TeleportTo(Point worldPoint)
        {
            _input.DoInputAtWorldPosition("w", worldPoint);

            for (var i = 0; i < 20; i++)
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
