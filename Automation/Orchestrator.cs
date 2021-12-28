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
        private List<PointOfInterest> _pointsOfInterest;
        private GameData _gameData;

        private int _activeProfileIndex = -1;

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

            _runProfiles.Add(new RunProfile { Name = "Mephisto", Type = RunType.ClearArea, AreaPath = new Area[] { Area.DuranceOfHateLevel2, Area.DuranceOfHateLevel3 }, KillSpot = new Point(17565, 8070) });
        }

        public void Update(GameData gameData, List<PointOfInterest> pointsOfInterest)
        {
            if (gameData != null && gameData.PlayerUnit.IsValidPointer() && gameData.PlayerUnit.IsValidUnit())
            {
                _gameData = gameData;

                _currentArea = gameData.Area;

                _pointsOfInterest = pointsOfInterest;
            }
        }

        public void Run()
        {
            if (_worker.IsBusy)
            {
                _worker.CancelAsync();
            }
            else
            {
                _worker.RunWorkerAsync();
            }
        }

        private void Orchestrate(object sender, DoWorkEventArgs e)
        {
            if (_activeProfileIndex == _runProfiles.Count() - 1)
            {
                _log.Error("Exhausted all profiles, done!");
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

            if (_chicken.PlayerLifePercentage < 0.9)
            {
                _townManager.Heal();

                do
                {
                    System.Threading.Thread.Sleep(100);
                }
                while (_townManager.State != TownState.IDLE);
            }

            foreach (Area area in activeProfile.AreaPath)
            {
                if (_townManager.IsInTown)
                {
                    _townManager.OpenWaypointMenu();

                    do
                    {
                        System.Threading.Thread.Sleep(100);
                    }
                    while (_townManager.State != TownState.WP_MENU);

                    _menuMan.TakeWaypoint(area);
                }
                else
                {
                    BuffMe();   

                    var target = _pointsOfInterest.Where(x => x.Label == Utils.GetAreaLabel(area, _gameData.Difficulty)).FirstOrDefault();

                    if (target == null)
                    {
                        _log.Error("Couldn't find PointOfInterest for " + Utils.GetAreaLabel(area, _gameData.Difficulty) + "! Help!");
                        return;
                    }

                    MoveTo(target.Position);

                    _input.DoInputAtWorldPosition("{LMB}", target.Position);
                }

                do
                {
                    System.Threading.Thread.Sleep(100);
                }
                while (_currentArea != area);

                // wait for load
                System.Threading.Thread.Sleep(3000);
            }

            BuffMe();

            MoveTo(activeProfile.KillSpot);

            // switch here by profile type later
            _combat.ClearArea(_gameData.PlayerPosition);

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
            while (!_pickit.Busy);

            // cast portal, go back to town
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
            _movement.TeleportTo(point);

            do
            {
                System.Threading.Thread.Sleep(100);
            }
            while (_movement.Busy);
        }
    }
}
