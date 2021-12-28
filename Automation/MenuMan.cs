using GameOverlay.Drawing;
using MapAssist.Structs;
using MapAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapAssist.Automation
{
    class MenuMan
    {
        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();

        private static readonly HashSet<Waypoint> _waypoints = new HashSet<Waypoint>();

        Input _input;
        private MenuData _menus;
        private Rectangle _window;

        public MenuMan(Input input)
        {
            InitializeWaypoints();

            _input = input;
        }

        public void Update(GameData gameData, Rectangle window)
        {
            _menus = gameData.MenuOpen;
            _window = window;
        }

        public void TakeWaypoint(string name)
        {
            if (!_menus.Waypoint)
            {
                _log.Error("Waypoint menu not open!");
                return;
            }

            if (!_waypoints.Any(x => x.Name == name))
            {
                _log.Error("Invalid waypoint name!");
                return;
            }

            Waypoint target = _waypoints.Where(x => x.Name == name).First();

            _input.DoInputAtScreenPosition("{LMB}", new Point((int)((_window.Width * 0.1) + ((target.Act - 1) * _window.Width * 0.04)), (int)(_window.Height * 0.20)));
            _input.DoInputAtScreenPosition("{LMB}", new Point((int)(_window.Width * 0.1), (int)((_window.Height * 0.25) + (target.Index * _window.Height * 0.056))));
        }

        public void ClickRepair()
        {
            if (!_menus.NpcShop)
            {
                _log.Error("Shop menu not open!");
                return;
            }

            _input.DoInputAtScreenPosition("{LMB}", new Point((int)(_window.Width * 0.265), (int)(_window.Height * 0.71)));
        }

        public void CloseMenu()
        {
            if (_menus.Inventory || _menus.NpcShop || _menus.Stash || _menus.Waypoint)
            {
                _input.DoInput("{ESC}");
            }
        }

        // this might be needed later if I have to move to static pixel offsets
        private void wp_debugging()
        {
            for (var i = 0; i < 5; i++)
            {
                _input.MouseMove(new Point((int)((_window.Width * 0.1) + (i * _window.Width * 0.04)), (int)(_window.Height * 0.20)));

                System.Threading.Thread.Sleep(500);
            }

            for (var i = 0; i < 9; i++)
            {
                System.Threading.Thread.Sleep(500);

                _input.MouseMove(new Point(300, (int)((_window.Height * 0.25) + (i * _window.Height * 0.056))));
            }
        }

        private void InitializeWaypoints()
        {
            _waypoints.Add(new Waypoint { Name = "Rogue Encampment", Act = 1, Index = 0 });
            _waypoints.Add(new Waypoint { Name = "Cold Plains", Act = 1, Index = 1 });
            _waypoints.Add(new Waypoint { Name = "Stony Field", Act = 1, Index = 2 });
            _waypoints.Add(new Waypoint { Name = "Dark Wood", Act = 1, Index = 3 });
            _waypoints.Add(new Waypoint { Name = "Black Marsh", Act = 1, Index = 4 });
            _waypoints.Add(new Waypoint { Name = "Outer Cloister", Act = 1, Index = 5 });
            _waypoints.Add(new Waypoint { Name = "Jail Level 1", Act = 1, Index = 6 });
            _waypoints.Add(new Waypoint { Name = "Inner Cloister", Act = 1, Index = 7 });
            _waypoints.Add(new Waypoint { Name = "Catacombs Level 2", Act = 1, Index = 8 });
            _waypoints.Add(new Waypoint { Name = "Lut Gholein", Act = 2, Index = 0 });
            _waypoints.Add(new Waypoint { Name = "Sewers Level 2", Act = 2, Index = 1 });
            _waypoints.Add(new Waypoint { Name = "Dry Hills", Act = 2, Index = 2 });
            _waypoints.Add(new Waypoint { Name = "Halls of the Dead Level 2", Act = 2, Index = 3 });
            _waypoints.Add(new Waypoint { Name = "Far Oasis", Act = 2, Index = 4 });
            _waypoints.Add(new Waypoint { Name = "Lost City", Act = 2, Index = 5 });
            _waypoints.Add(new Waypoint { Name = "Palace Cellar Level 1", Act = 2, Index = 6 });
            _waypoints.Add(new Waypoint { Name = "Arcane Sanctuary", Act = 2, Index = 7 });
            _waypoints.Add(new Waypoint { Name = "Canyon of the Magi", Act = 2, Index = 8 });
            _waypoints.Add(new Waypoint { Name = "Kurast Docks", Act = 3, Index = 0 });
            _waypoints.Add(new Waypoint { Name = "Spider Forest", Act = 3, Index = 1 });
            _waypoints.Add(new Waypoint { Name = "Great Marsh", Act = 3, Index = 2 });
            _waypoints.Add(new Waypoint { Name = "Flayer Jungle", Act = 3, Index = 3 });
            _waypoints.Add(new Waypoint { Name = "Lower Kurast", Act = 3, Index = 4 });
            _waypoints.Add(new Waypoint { Name = "Kurast Bazar", Act = 3, Index = 5 });
            _waypoints.Add(new Waypoint { Name = "Upper Kurast", Act = 3, Index = 6 });
            _waypoints.Add(new Waypoint { Name = "Travincal", Act = 3, Index = 7 });
            _waypoints.Add(new Waypoint { Name = "Durance of Hate Level 2", Act = 3, Index = 8 });
            _waypoints.Add(new Waypoint { Name = "The Pandemonium Fortress", Act = 4, Index = 0 });
            _waypoints.Add(new Waypoint { Name = "City of the Damned", Act = 4, Index = 1 });
            _waypoints.Add(new Waypoint { Name = "River of Flame", Act = 4, Index = 2 });
            _waypoints.Add(new Waypoint { Name = "Harrogath", Act = 5, Index = 0 });
            _waypoints.Add(new Waypoint { Name = "Frigid Highlands", Act = 5, Index = 1 });
            _waypoints.Add(new Waypoint { Name = "Arreat Plateau", Act = 5, Index = 2 });
            _waypoints.Add(new Waypoint { Name = "Crystalline Passage", Act = 5, Index = 3 });
            _waypoints.Add(new Waypoint { Name = "Glacial Trail", Act = 5, Index = 4 });
            _waypoints.Add(new Waypoint { Name = "Halls of Pain", Act = 5, Index = 5 });
            _waypoints.Add(new Waypoint { Name = "Frozen Tundra", Act = 5, Index = 6 });
            _waypoints.Add(new Waypoint { Name = "The Ancients' Way", Act = 5, Index = 7 });
            _waypoints.Add(new Waypoint { Name = "Worldstone Keep Level 2", Act = 5, Index = 8 });
        }
    }
}
