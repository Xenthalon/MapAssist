using GameOverlay.Drawing;
using MapAssist.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace MapAssist.Automation.Profiles
{
    class Travincal : IRunProfile
    {
        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();

        private bool _abort = false;

        private BuffBoy _buffBoy;
        private Combat _combat;
        private Movement _movement;
        private PickIt _pickit;

        private BackgroundWorker _worker;

        private Area _currentArea;
        private List<PointOfInterest> _pointsOfInterests;
        private Point _playerPosition;

        private bool _busy = false;
        private bool _error = false;

        public Travincal(BuffBoy buffBoy, Combat combat, Movement movement, PickIt pickit)
        {
            _buffBoy = buffBoy;
            _combat = combat;
            _movement = movement;
            _pickit = pickit;

            _worker = new BackgroundWorker();
            _worker.DoWork += new DoWorkEventHandler(Work);
            _worker.WorkerSupportsCancellation = true;
        }

        public void Run()
        {
            if (_currentArea != Area.Travincal)
            {
                _log.Error("Must be in Travincal to run this profile!");
                throw new Exception("Not in Travincal!");
            }

            _busy = true;
            _worker.RunWorkerAsync();
        }

        public void Update(GameData gameData, List<PointOfInterest> pointsOfInterest)
        {
            if (_pointsOfInterests == null || _pointsOfInterests.Count != pointsOfInterest.Count)
            {
                _pointsOfInterests = pointsOfInterest;
            }

            if (gameData != null && gameData.PlayerUnit.IsValidPointer && gameData.PlayerUnit.IsValidUnit)
            {
                _currentArea = gameData.Area;
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
                _combat.MonsterFilter = new List<Npc> { Npc.CouncilMember, Npc.CouncilMember2, Npc.CouncilMember3 };
                _combat.DefendAgainst(Resist.FIRE);

                var location = _pointsOfInterests.Where(x => x.Label.StartsWith("Durance of Hate Level 1")).First().Position;

                var entrance = new Point(location.X + 3, location.Y + 29);
                _log.Info("Moving to Entrance");
                MoveTo(entrance);

                if (!_abort)
                {
                    _combat.ClearArea(entrance);

                    do
                    {
                        System.Threading.Thread.Sleep(100);
                    }
                    while (!_combat.IsSafe && !_abort);
                }

                if (!_abort)
                {
                    var step1 = new Point(entrance.X - 20, entrance.Y);
                    _log.Info("Moving to Entrance left");
                    MoveTo(step1);

                    _combat.ClearArea(step1);

                    do
                    {
                        System.Threading.Thread.Sleep(100);
                    }
                    while (!_combat.IsSafe && !_abort);
                }

                if (!_abort)
                {
                    var step2 = new Point(location.X - 12, location.Y + 8);
                    _log.Info("Going inside.");
                    MoveTo(step2);

                    _combat.ClearArea(step2);

                    do
                    {
                        System.Threading.Thread.Sleep(100);
                    }
                    while (!_combat.IsSafe && !_abort);
                }
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
    }
}
