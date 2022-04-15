using GameOverlay.Drawing;
using MapAssist.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace MapAssist.Automation.Profiles
{
    class Baal : IRunProfile
    {
        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();

        private bool _abort = false;

        private BuffBoy _buffBoy;
        private Combat _combat;
        private Input _input;
        private Movement _movement;
        private PickIt _pickit;
        private Orchestrator _orchestrator;

        private BackgroundWorker _worker;

        private Area _currentArea;
        private AreaData _areaData;
        private List<PointOfInterest> _pointsOfInterests;
        private UnitObject[] _objects;
        private UnitMonster[] _monsters;
        private Point _playerPosition;

        private bool _busy = false;
        private bool _error = false;

        public Baal(BuffBoy buffBoy, Combat combat, Movement movement, Input input, PickIt pickit, Orchestrator orchestrator)
        {
            _buffBoy = buffBoy;
            _combat = combat;
            _input = input;
            _movement = movement;
            _pickit = pickit;
            _orchestrator = orchestrator;

            _worker = new BackgroundWorker();
            _worker.DoWork += new DoWorkEventHandler(Work);
            _worker.WorkerSupportsCancellation = true;
        }

        public void Run()
        {
            if (_currentArea != Area.ThroneOfDestruction)
            {
                _log.Error("Must be in Throne Of Destruction to run this profile!");
                throw new Exception("Not in Throne Of Destruction!");
            }

            _busy = true;
            _worker.RunWorkerAsync();
        }

        public void Update(GameData gameData, AreaData areaData, List<PointOfInterest> pointsOfInterest)
        {
            if (_pointsOfInterests == null || _pointsOfInterests.Count != pointsOfInterest.Count)
            {
                _pointsOfInterests = pointsOfInterest;
            }

            if (gameData != null && gameData.PlayerUnit.IsValidPointer && gameData.PlayerUnit.IsValidUnit)
            {
                _currentArea = gameData.Area;
                _playerPosition = gameData.PlayerPosition;
                _objects = gameData.Objects;
                _monsters = gameData.Monsters;
            }

            if (areaData != null)
            {
                _areaData = areaData;
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
                _log.Info("Moving to throne entrance.");

                var entrance = new Point(15095, 5070);

                MoveTo(entrance);

                _combat.PrepareForCombat();

                _log.Info("Clearing throne room.");

                var lowerRight = new Point(entrance.X + 20, entrance.Y);
                var lowerLeft = new Point(entrance.X - 20, entrance.Y);
                var middle = new Point(entrance.X, entrance.Y - 30);
                var middleRight = new Point(lowerRight.X, lowerRight.Y - 30);
                var middleLeft = new Point(lowerLeft.X, lowerLeft.Y - 30);
                var upperRight = new Point(middleRight.X, middleRight.Y - 30);
                var upperLeft = new Point(middleLeft.X, middleLeft.Y - 30);

                ClearArea(entrance);

                MoveTo(lowerRight);

                ClearArea(lowerRight);

                MoveTo(middleRight);

                ClearArea(middleRight);

                MoveTo(upperRight);

                ClearArea(upperRight);

                MoveTo(middle);

                ClearArea(middle);

                MoveTo(upperLeft);

                ClearArea(upperLeft);

                MoveTo(middleLeft);

                ClearArea(middleLeft);

                MoveTo(lowerLeft);

                ClearArea(lowerLeft);

                Loot();

                MoveTo(middle);

                _combat.RemoveDebuffs();
                _combat.HealUp();
                Buff();
                _combat.PrepareForCombat();

                KillBoss("Colenzo the Annihilator");
                MoveTo(middle);
                ClearArea(middle);
                Loot();
                _combat.RemoveDebuffs();
                _combat.HealUp();
                Buff();
                _combat.PrepareForCombat();

                KillBoss("Achmel the Cursed", true);
                MoveTo(middle);
                ClearArea(middle);
                Loot();
                _combat.RemoveDebuffs();
                _combat.HealUp();
                Buff();
                _combat.PrepareForCombat();

                KillBoss("Bartuc the Bloody", true);
                MoveTo(middle);
                ClearArea(middle);
                Loot();
                _combat.RemoveDebuffs();
                _combat.HealUp();
                Buff();
                _combat.PrepareForCombat();

                KillBoss("Ventar the Unholy", true);
                MoveTo(middle);
                ClearArea(middle);
                Loot();
                _combat.RemoveDebuffs();
                _combat.HealUp();
                Buff(true);
                _combat.PrepareForCombat();

                KillBoss("Lister the Tormentor", true);
                MoveTo(middle);
                ClearArea(middle);
                Loot();
                _combat.RemoveDebuffs();
                _combat.HealUp();
                Buff();
                _combat.PrepareForCombat();

                var baalPortal = _objects.Where(x => x.GameObject == GameObject.BaalsPortal).FirstOrDefault() ?? new UnitObject(new IntPtr());

                if (baalPortal.IsValidPointer)
                {
                    MoveTo(baalPortal.Position);

                    System.Threading.Thread.Sleep(15000);
                    Buff(true);
                    _combat.PrepareForCombat();

                    _movement.ChangeArea(Area.TheWorldstoneChamber, baalPortal);

                    Point[] baalLocation;

                    _areaData.NPCs.TryGetValue(Npc.BaalCrab, out baalLocation);

                    MoveTo(baalLocation[0]);

                    ClearArea(baalLocation[0]);
                    Loot();
                }
            }
            catch (Exception exception)
            {
                _log.Error(exception);
                _error = true;
            }

            _busy = false;
        }

        private void KillBoss(string name, bool doDistancePrecast = false)
        {
            do
            {
                System.Threading.Thread.Sleep(100);
            }
            while (!_monsters.Any(x => (x.MonsterData.MonsterType & Structs.MonsterTypeFlags.SuperUnique) == Structs.MonsterTypeFlags.SuperUnique));

            _log.Info("There's old " + name + ", let's get him!");

            if (doDistancePrecast)
            {
                var target = _monsters.Where(x => (x.MonsterData.MonsterType & Structs.MonsterTypeFlags.SuperUnique) == Structs.MonsterTypeFlags.SuperUnique).First();

                _movement.GetInLOSRange(target.Position, 10, 20, _combat.HasTeleport);
                _combat.PrecastMainSkillForAt(4000, target.Position);
            }

            while (_monsters.Any(x => (x.MonsterData.MonsterType & Structs.MonsterTypeFlags.SuperUnique) == Structs.MonsterTypeFlags.SuperUnique))
            {
                _combat.Kill(name, true);

                do
                {
                    System.Threading.Thread.Sleep(100);
                }
                while (_combat.Busy && !_abort);
            }
        }

        private void ClearArea(Point position)
        {
            if (!_combat.IsSafe)
            {
                _combat.ClearArea(position);

                do
                {
                    System.Threading.Thread.Sleep(100);
                }
                while (_combat.Busy);
            }
        }

        private void Loot()
        {
            _orchestrator.PickThings();
        }

        private void Buff(bool force = false)
        {
            if (_buffBoy.HasWork)
            {
                _buffBoy.Run(force);

                do
                {
                    System.Threading.Thread.Sleep(100);
                }
                while (_buffBoy.Busy);
            }
        }

        private void MoveTo(Point position)
        {
            do
            {
                System.Threading.Thread.Sleep(100);
            }
            while (_movement.Busy && !_abort);

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
            while (_movement.Busy && !_abort);
        }

        private bool ActivateSeal(UnitObject seal, Point location)
        {
            var success = false;
            var maxRetries = 3;
            var retries = 0;

            while (retries <= maxRetries && !success)
            {
                if (Automaton.GetDistance(seal.Position, _playerPosition) > 9)
                    MoveTo(location);

                var activated = TryActivateSeal(seal);
                seal.IsCached = false;
                seal = seal.Update();

                if (seal.Struct.Mode != 0)
                {
                    success = true;
                    break;
                }

                retries += 1;
                _movement.GetInLOSRange(seal.Position, 7, 9, _combat.HasTeleport); // move away from seal
            }

            return success;
        }

        private bool TryActivateSeal(UnitObject seal)
        {
            var maxRetries = 3;
            var retries = 0;

            while (seal.Struct.Mode == 0 && retries <= maxRetries)
            {
                _input.DoInputOnUnit("{LMB}", seal);

                System.Threading.Thread.Sleep(500);

                seal.IsCached = false;
                seal = seal.Update();
                retries += 1;
            }

            return retries <= maxRetries;
        }

        private Point GetPoint(AreaData areaData, GameObject target)
        {
            var results = new Point[0];
            var offset = new Point(5, 5);

            var success = areaData.Objects.TryGetValue(target, out results);

            if (success && target == GameObject.DiabloSeal3 && results[0].X == 7773 && results[0].Y == 5155)
            {
                // stand to lower left of seal 3, because it's buggy as hell
                offset = new Point(-3, 4);
            }

            return success ? new Point(results[0].X + offset.X, results[0].Y + offset.Y) : new Point();
        }

        private UnitObject FindObject(UnitObject[] objects, GameObject target)
        {
            return objects.Where(x => x.GameObject == target).FirstOrDefault() ?? new UnitObject(IntPtr.Zero);
        }
    }
}
