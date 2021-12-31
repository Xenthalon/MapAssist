using GameOverlay.Drawing;
using MapAssist.Structs;
using MapAssist.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapAssist.Automation
{
    enum TownState
    {
        IDLE,
        MOVING,
        WP_MENU,
        TRADE_MENU,
        GAMBLE_MENU,
        STASH_MENU
    }

    enum TownTask
    {
        HEAL,
        REPAIR,
        REVIVE,
        OPEN_TRADE_MENU,
        OPEN_GAMBLE_MENU,
        OPEN_WAYPOINT_MENU,
        OPEN_STASH_MENU,
        NONE
    }

    class TownManager
    {
        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();

        private static HashSet<NPC> _npcs = new HashSet<NPC>();
        private static int _retryLoopCount = 50;
        private static int _retrys = 5;

        private Input _input;
        private MenuMan _menuMan;
        private Movement _movement;
        private TownState _state = TownState.IDLE;
        private TownTask _task = TownTask.NONE;
        private BackgroundWorker _worker;

        private int _act;
        private Types.UnitAny _activeNpc;
        private Area _area;
        private MenuData _menus;
        private Point _playerPosition;
        private HashSet<Types.UnitAny> _closeNpcs;
        private HashSet<Types.UnitAny> _closeObjects;

        public bool IsInTown => _area == Area.RogueEncampment || _area == Area.LutGholein || _area == Area.KurastDocks || _area == Area.ThePandemoniumFortress || _area == Area.Harrogath;
        public TownState State => _state;
        public Types.UnitAny ActiveNPC => _activeNpc;

        public TownManager(Input input, MenuMan menuMan, Movement movement)
        {
            _input = input;
            _menuMan = menuMan;
            _movement = movement;

            InitializeNpcs();

            _worker = new BackgroundWorker();
            _worker.DoWork += new DoWorkEventHandler(Run);
            _worker.WorkerSupportsCancellation = true;
        }

        // revive merc
        public void ReviveMerc()
        {
            CancelWork();
            _task = TownTask.REVIVE;
            _worker.RunWorkerAsync();
        }

        // open wp menu
        public void OpenWaypointMenu()
        {
            CancelWork();
            _task = TownTask.OPEN_WAYPOINT_MENU;
            _worker.RunWorkerAsync();
        }

        // heal
        public void Heal()
        {
            CancelWork();
            _task = TownTask.HEAL;
            _worker.RunWorkerAsync();
        }

        // open stash
        public void OpenStashMenu()
        {
            CancelWork();
            _task = TownTask.OPEN_STASH_MENU;
            _worker.RunWorkerAsync();
        }

        // repair
        public void Repair()
        {
            CancelWork();
            _task = TownTask.REPAIR;
            _worker.RunWorkerAsync();
        }

        // open trade menu for idsc, tpsc, sales
        public void OpenTradeMenu()
        {
            CancelWork();
            _task = TownTask.OPEN_TRADE_MENU;
            _worker.RunWorkerAsync();
        }

        // open gamble menu
        public void OpenGambleMenu()
        {
            CancelWork();
            _task = TownTask.OPEN_GAMBLE_MENU;
            _worker.RunWorkerAsync();
        }

        public void Update(GameData gameData)
        {
            if (gameData != null && gameData.PlayerUnit.IsValidPointer() && gameData.PlayerUnit.IsValidUnit())
            {
                _act = (int)gameData.PlayerUnit.Act.ActId + 1;
                _area = gameData.Area;
                _menus = gameData.MenuOpen;
                _playerPosition = gameData.PlayerPosition;
                _closeNpcs = gameData.NPCs;
                _closeObjects = gameData.Objects;
            }
        }

        public void Reset()
        {
            CancelWork();
            _task = TownTask.NONE;
            _state = TownState.IDLE;
        }

        private void Run(object sender, DoWorkEventArgs e)
        {
            NPC target = null;

            switch (_task)
            {
                case TownTask.HEAL:
                    target = _npcs.Where(x => x.Act == _act && x.CanHeal).First();
                    break;
                case TownTask.REPAIR:
                    target = _npcs.Where(x => x.Act == _act && x.CanRepair).First();
                    break;
                case TownTask.REVIVE:
                    target = _npcs.Where(x => x.Act == _act && x.CanRevive).First();
                    break;
                case TownTask.OPEN_TRADE_MENU:
                    target = _npcs.Where(x => x.Act == _act && x.HasScrolls).First();
                    break;
                case TownTask.OPEN_GAMBLE_MENU:
                    target = _npcs.Where(x => x.Act == _act && x.CanGamble).First();
                    break;
                case TownTask.OPEN_STASH_MENU:
                    target = _npcs.Where(x => x.Act == _act && x.IsStash).First();
                    break;
                case TownTask.OPEN_WAYPOINT_MENU:
                    target = _npcs.Where(x => x.Act == _act && x.IsWaypoint).First();
                    break;
            }

            _state = TownState.MOVING;

            _movement.WalkTo(target.Position);

            do
            {
                System.Threading.Thread.Sleep(100);
            }
            while (_movement.Busy);

            var clickTarget = FindClickTarget(target.TxtFileId);

            if (!clickTarget.IsValidPointer())
            {
                _log.Error("Couldn't find " + target.Name + ", stuck, please help!");
                return;
            }

            _input.DoInputAtWorldPosition("{LMB}", clickTarget.Position);
            var loopCount = 0;
            var retrys = 0;

            do
            {
                System.Threading.Thread.Sleep(100);
                loopCount++;

                if (loopCount >= _retryLoopCount)
                {
                    clickTarget = FindClickTarget(target.TxtFileId);

                    if (!clickTarget.IsValidPointer())
                    {
                        _log.Error("Couldn't find " + target.Name + ", stuck, please help!");
                        return;
                    }

                    _input.DoInputAtWorldPosition("{LMB}", clickTarget.Position);
                    retrys++;
                    loopCount = 0;
                }

                if (retrys >= _retrys)
                {
                    _log.Error("Something went wrong interacting with " + target.Name);
                    _task = TownTask.NONE;
                    _state = TownState.IDLE;
                    break;
                }
            }
            while (!_menus.NpcInteract && !_menus.Waypoint && !_menus.Stash);

            switch (_task)
            {
                case TownTask.HEAL:
                    _state = TownState.IDLE;
                    break;
                case TownTask.REPAIR:
                    _input.MouseMove(new Point(100, 100));
                    System.Threading.Thread.Sleep(100);
                    _input.DoInput("{DOWN 2}{ENTER}");
                    System.Threading.Thread.Sleep(100);
                    _menuMan.ClickRepair();
                    System.Threading.Thread.Sleep(100);
                    _menuMan.CloseMenu();
                    System.Threading.Thread.Sleep(100);
                    _state = TownState.IDLE;
                    break;
                case TownTask.REVIVE:
                    _input.MouseMove(new Point(100, 100));
                    System.Threading.Thread.Sleep(100);
                    _input.DoInput("{DOWN 2}{ENTER}");
                    System.Threading.Thread.Sleep(500);
                    _state = TownState.IDLE;
                    break;
                case TownTask.OPEN_TRADE_MENU:
                    var downTimes1 = target.Name == "Jamella" ? 1 : 2;

                    _input.MouseMove(new Point(100, 100));
                    System.Threading.Thread.Sleep(100);
                    _input.DoInput("{DOWN " + downTimes1 + "}{ENTER}");

                    System.Threading.Thread.Sleep(500);

                    _activeNpc = clickTarget;
                    _state = TownState.TRADE_MENU;
                    break;
                case TownTask.OPEN_GAMBLE_MENU:
                    var downTimes2 = target.Name == "Jamella" ? 2 : 3;

                    _input.MouseMove(new Point(100, 100));
                    System.Threading.Thread.Sleep(100);
                    _input.DoInput("{DOWN " + downTimes2 + "}{ENTER}");

                    System.Threading.Thread.Sleep(500);

                    _activeNpc = clickTarget;
                    _state = TownState.GAMBLE_MENU;
                    break;
                case TownTask.OPEN_STASH_MENU:
                    _state = TownState.STASH_MENU;
                    break;
                case TownTask.OPEN_WAYPOINT_MENU:
                    _state = TownState.WP_MENU;
                    break;
            }

            _task = TownTask.NONE;
        }

        private Types.UnitAny FindClickTarget(int txtFileId)
        {
            var clickTarget = new Types.UnitAny(IntPtr.Zero);

            if (_task == TownTask.OPEN_WAYPOINT_MENU || _task == TownTask.OPEN_STASH_MENU)
            {
                clickTarget = _closeObjects.Where(x => x.TxtFileNo == txtFileId).FirstOrDefault() ?? new Types.UnitAny(IntPtr.Zero);
            }
            else
            {
                clickTarget = _closeNpcs.Where(x => x.TxtFileNo == txtFileId).FirstOrDefault() ?? new Types.UnitAny(IntPtr.Zero);
            }

            return clickTarget;
        }

        private void CancelWork()
        {
            if (_worker.IsBusy)
            {
                _state = TownState.IDLE;
                _worker.CancelAsync();
            }

            _activeNpc = new Types.UnitAny(IntPtr.Zero);
        }

        private void InitializeNpcs()
        {
            _npcs.Add(new NPC { Name = "Akara", Act = 1, TxtFileId = 148, Position = new Point(4370, 5413), CanHeal = true, HasScrolls = true, HasPotions = true });
            _npcs.Add(new NPC { Name = "Kashya", Act = 1, TxtFileId = 150, Position = new Point(4335, 5434), CanRevive = true });
            _npcs.Add(new NPC { Name = "Charsi", Act = 1, TxtFileId = 154, Position = new Point(4278, 5421), CanRepair = true });
            _npcs.Add(new NPC { Name = "Gheed", Act = 1, TxtFileId = 147, Position = new Point(4284, 5473), CanGamble = true });
            _npcs.Add(new NPC { Name = "Stash", Act = 1, TxtFileId = 267, Position = new Point(4311, 5429), IsStash = true });
            _npcs.Add(new NPC { Name = "Waypoint", Act = 1, TxtFileId = 119, Position = new Point(4326, 5423), IsWaypoint = true });
            _npcs.Add(new NPC { Name = "Portals", Act = 1, Position = new Point(4335, 5460), IsPortalSpot = true });
            _npcs.Add(new NPC { Name = "Drognan", Act = 2, TxtFileId = 177, Position = new Point(5094, 5036), HasScrolls = true, HasPotions = true });
            _npcs.Add(new NPC { Name = "Elzix", Act = 2, TxtFileId = 199, Position = new Point(5036, 5096), CanGamble = true });
            _npcs.Add(new NPC { Name = "Fara", Act = 2, TxtFileId = 178, Position = new Point(5123, 5078), CanHeal = true, CanRepair = true });
            _npcs.Add(new NPC { Name = "Greiz", Act = 2, TxtFileId = 198, Position = new Point(5040, 5052), CanRevive = true });
            _npcs.Add(new NPC { Name = "Stash", Act = 2, TxtFileId = 267, Position = new Point(5123, 5078), IsStash = true });
            _npcs.Add(new NPC { Name = "Waypoint", Act = 2, TxtFileId = 156, Position = new Point(5071, 5086), IsWaypoint = true });
            _npcs.Add(new NPC { Name = "Portals", Act = 2, Position = new Point(5174, 5056), IsPortalSpot = true });
            _npcs.Add(new NPC { Name = "Asheara", Act = 3, TxtFileId = 252, Position = new Point(5042, 5093), CanRevive = true });
            _npcs.Add(new NPC { Name = "Alkor", Act = 3, TxtFileId = 254, Position = new Point(5084, 5014), CanGamble = true });
            _npcs.Add(new NPC { Name = "Hrlati", Act = 3, TxtFileId = 253, Position = new Point(5222, 5043), CanRepair = true });
            _npcs.Add(new NPC { Name = "Ormus", Act = 3, TxtFileId = 255, Position = new Point(5132, 5093), CanHeal = true, HasScrolls = true, HasPotions = true });
            _npcs.Add(new NPC { Name = "Stash", Act = 3, TxtFileId = 267, Position = new Point(5144, 5059), IsStash = true });
            _npcs.Add(new NPC { Name = "Waypoint", Act = 3, TxtFileId = 237, Position = new Point(5161, 5051), IsWaypoint = true });
            _npcs.Add(new NPC { Name = "Portals", Act = 3, Position = new Point(5156, 5058), IsPortalSpot = true });
            _npcs.Add(new NPC { Name = "Halbu", Act = 4, TxtFileId = 257, Position = new Point(5087, 5029), CanRepair = true });
            _npcs.Add(new NPC { Name = "Tyrael", Act = 4, TxtFileId = 367, Position = new Point(5021, 5022), CanRevive = true });
            _npcs.Add(new NPC { Name = "Jamella", Act = 4, TxtFileId = 405, Position = new Point(5089, 5059), CanHeal = true, CanGamble = true, HasPotions = true, HasScrolls = true });
            _npcs.Add(new NPC { Name = "Stash", Act = 4, TxtFileId = 267, Position = new Point(5022, 5037), IsStash = true });
            _npcs.Add(new NPC { Name = "Portals", Act = 4, Position = new Point(5046, 5034), IsPortalSpot = true });
            _npcs.Add(new NPC { Name = "Waypoint", Act = 4, TxtFileId = 398, Position = new Point(5046, 5021), IsWaypoint = true });
            _npcs.Add(new NPC { Name = "Larzuk", Act = 5, TxtFileId = 511, Position = new Point(5137, 5046), CanRepair = true });
            _npcs.Add(new NPC { Name = "Malah", Act = 5, TxtFileId = 513, Position = new Point(5076, 5027), CanHeal = true, HasPotions = true, HasScrolls = true });
            _npcs.Add(new NPC { Name = "Qual-Kehk", Act = 5, TxtFileId = 515, Position = new Point(5068, 5081), CanRevive = true });
            _npcs.Add(new NPC { Name = "Anya", Act = 5, TxtFileId = 512, Position = new Point(5108, 5119), CanGamble = true });
            _npcs.Add(new NPC { Name = "Portals", Act = 5, Position = new Point(5096, 5022), IsPortalSpot = true });
            _npcs.Add(new NPC { Name = "Stash", Act = 5, TxtFileId = 267, Position = new Point(5122, 5059), IsStash = true });
            _npcs.Add(new NPC { Name = "Waypoint", Act = 5, TxtFileId = 429, Position = new Point(5119, 5062), IsWaypoint = true });
        }
    }
}
