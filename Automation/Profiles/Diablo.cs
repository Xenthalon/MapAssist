using GameOverlay.Drawing;
using MapAssist.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace MapAssist.Automation.Profiles
{
    class Diablo : IRunProfile
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

        public Diablo(BuffBoy buffBoy, Combat combat, Movement movement, Input input, PickIt pickit, Orchestrator orchestrator)
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
            if (_currentArea != Area.ChaosSanctuary)
            {
                _log.Error("Must be in ChaosSanctuary to run this profile!");
                throw new Exception("Not in Chaos Sanctuary!");
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
                var diabloSpawn = GetPoint(_areaData, GameObject.DiabloStartPoint);
                var seal1Pos = GetPoint(_areaData, GameObject.DiabloSeal1);
                var seal2Pos = GetPoint(_areaData, GameObject.DiabloSeal2);
                var seal3Pos = GetPoint(_areaData, GameObject.DiabloSeal3);
                var seal4Pos = GetPoint(_areaData, GameObject.DiabloSeal4);
                var seal5Pos = GetPoint(_areaData, GameObject.DiabloSeal5);

                _combat.DefendAgainst(Resist.Fire);

                _log.Info("Moving to center");
                MoveTo(diabloSpawn);

                _log.Info("Going left");
                MoveTo(seal4Pos);

                var seal4 = FindObject(_objects, GameObject.DiabloSeal4);

                if (!seal4.IsValidPointer)
                {
                    _log.Error("Couldn't find seal 4, aborting");
                    _busy = false;
                    _error = true;
                    return;
                }

                _combat.PrepareForCombat();
                ClearArea(seal4.Position);
                MoveTo(seal4Pos);

                var activated = ActivateSeal(seal4, seal4Pos);

                if (!activated)
                {
                    _log.Error("Couldn't activate seal 4, aborting");
                    _busy = false;
                    _error = true;
                    return;
                }

                MoveTo(seal5Pos);

                var seal5 = FindObject(_objects, GameObject.DiabloSeal5);

                if (!seal5.IsValidPointer)
                {
                    _log.Error("Couldn't find seal 5, aborting");
                    _busy = false;
                    _error = true;
                    return;
                }

                ClearArea(seal5.Position);
                _combat.RemoveDebuffs();
                _combat.HealUp();
                Buff();
                _combat.PrepareForCombat();

                MoveTo(seal5Pos);
                activated = ActivateSeal(seal5, seal5Pos);

                if (!activated)
                {
                    _log.Error("Couldn't activate seal 5, aborting");
                    _busy = false;
                    _error = true;
                    return;
                }

                KillBoss("Grand Vizier of Chaos");

                if (_abort)
                {
                    _busy = false;
                    return;
                }

                Loot();

                _combat.DefendAgainst(Resist.Fire);
                _log.Info("Going up");
                MoveTo(seal3Pos);

                var seal3 = FindObject(_objects, GameObject.DiabloSeal3);

                if (!seal3.IsValidPointer)
                {
                    _log.Error("Couldn't find seal 3, aborting");
                    _busy = false;
                    _error = true;
                    return;
                }

                _combat.PrepareForCombat();
                ClearArea(seal3.Position);
                Buff();

                MoveTo(seal3Pos);
                activated = ActivateSeal(seal3, seal3Pos);

                if (!activated)
                {
                    _log.Error("Couldn't activate seal 3, aborting");
                    _busy = false;
                    _error = true;
                    return;
                }

                if (seal3Pos.X == 7770 && seal3Pos.Y == 5159)
                {
                    MoveTo(new Point(seal3Pos.X, seal3Pos.Y + 40));
                }
                else if (seal3Pos.X == 7820 && seal3Pos.Y == 5160)
                {
                    MoveTo(new Point(seal3Pos.X - 27, seal3Pos.Y - 8));
                }

                KillBoss("Lord De Seis", true);  // condition for precast... class based? add it to diablo profile config?

                if (_abort)
                {
                    _busy = false;
                    return;
                }

                Loot();

                _combat.RemoveDebuffs();
                _combat.HealUp();
                _combat.DefendAgainst(Resist.Fire);

                MoveTo(diabloSpawn);

                _log.Info("Going right");
                MoveTo(seal2Pos);

                var seal2 = FindObject(_objects, GameObject.DiabloSeal2);

                if (!seal2.IsValidPointer)
                {
                    _log.Error("Couldn't find seal 2, aborting");
                    _error = true;
                    _busy = false;
                    return;
                }

                _combat.PrepareForCombat();
                ClearArea(seal2.Position);
                MoveTo(seal2Pos);
                activated = ActivateSeal(seal2, seal2Pos);

                if (!activated)
                {
                    _log.Error("Couldn't activate seal 2, aborting");
                    _error = true;
                    _busy = false;
                    return;
                }

                MoveTo(seal1Pos);

                var seal1 = FindObject(_objects, GameObject.DiabloSeal1);

                if (!seal1.IsValidPointer)
                {
                    _log.Error("Couldn't find seal 1, aborting");
                    _error = true;
                    _busy = false;
                    return;
                }

                ClearArea(seal1.Position);
                _combat.RemoveDebuffs();
                _combat.HealUp();
                Buff();
                _combat.DefendAgainst(Resist.Fire);
                MoveTo(seal1Pos);
                activated = ActivateSeal(seal1, seal1Pos);

                if (!activated)
                {
                    _log.Error("Couldn't activate seal 1, aborting");
                    _error = true;
                    _busy = false;
                    return;
                }

                if (seal1Pos.X == 7920 && seal1Pos.Y == 5320)
                {
                    MoveTo(new Point(seal1Pos.X + 12, seal1Pos.Y - 18));
                }
                else if (seal1Pos.X == 7898 && seal1Pos.Y == 5318)
                {
                    MoveTo(new Point(seal1Pos.X + 3, seal1Pos.Y - 22));
                }

                KillBoss("Infector of Souls", true);

                if (_abort)
                {
                    _busy = false;
                    return;
                }

                Loot();

                _combat.RemoveDebuffs();
                _combat.HealUp();
                Buff();

                _log.Info("Ripping old D-Bag a new one!");
                MoveTo(diabloSpawn);
                
                if (_buffBoy.HasWork)
                {
                    _buffBoy.Run();

                    do
                    {
                        System.Threading.Thread.Sleep(100);
                    }
                    while (_buffBoy.Busy && !_abort);
                }

                _combat.PrepareForCombat();

                do
                {
                    System.Threading.Thread.Sleep(100);
                }
                while (!_monsters.Any(x => x.TxtFileNo == (uint)Npc.Diablo) && !_abort);

                _combat.Kill((uint)Npc.Diablo, true);

                do
                {
                    System.Threading.Thread.Sleep(100);
                }
                while (_combat.Busy && !_abort && _monsters.Any(x => x.TxtFileNo == (uint)Npc.Diablo));
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

        private void Buff()
        {
            if (_buffBoy.HasWork)
            {
                _buffBoy.Run();

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
