using GameOverlay.Drawing;
using MapAssist.Helpers;
using MapAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MapAssist.Automation
{
    public class Input
    {
        IntPtr _mainWindowHandle;
        Point _playerPositionWorld;
        Point _playerPositionScreen;
        Rectangle _window;

        public Input() { }

        public void Update(GameData gameData, Rectangle window)
        {
            if (gameData != null && gameData.PlayerUnit.IsValidPointer && gameData.PlayerUnit.IsValidUnit)
            {
                _mainWindowHandle = gameData.MainWindowHandle;
                _playerPositionWorld = gameData.PlayerUnit.Position;
                _playerPositionScreen = new Point(window.Width / 2, (int)(window.Height * 0.49));
                _window = window;
            }
            else
            {
                _mainWindowHandle = GameManager.MainWindowHandle;
                _window = window;
            }
        }

        public void DoInputOnUnit(string input, UnitAny unit)
        {
            var maxTries = 10;

            Point screenPosition = Automaton.TranslateToScreenOffset(_playerPositionWorld, unit.Position, _playerPositionScreen);

            var upfactor = 0.02;

            if (unit.TxtFileNo == (uint)GameObject.TownPortal || unit.TxtFileNo == (uint)GameObject.PermanentTownPortal)
            {
                upfactor = 0.07;
            }

            screenPosition.Y = (float)(screenPosition.Y - (_window.Height * upfactor));

            if (screenPosition.X < 20)
                screenPosition.X = 20;

            if (screenPosition.X > _window.Width * 0.98)
                screenPosition.X = (int)(_window.Width * 0.98);

            if (screenPosition.Y < 20)
                screenPosition.Y = 20;

            if (screenPosition.Y > _window.Height * 0.9)
                screenPosition.Y = (int)(_window.Height * 0.9);

            MouseMove(screenPosition);

            if (unit is UnitMonster)
                ((UnitMonster)unit).Update();
            else if (unit is UnitObject)
                ((UnitObject)unit).Update();
            else if (unit is UnitItem)
                ((UnitItem)unit).Update();

            var direction = 0;
            var tries = 0;

            while (!unit.IsHovered)
            {
                tries += 1;
                var point = new Point(screenPosition.X, screenPosition.Y);

                if (direction == 0)
                {
                    point.Y += (int)(_window.Height * (0.005 * tries));
                }
                else if (direction == 1)
                {
                    point.Y -= (int)(_window.Height * (0.005 * tries));
                }
                else if (direction == 2)
                {
                    point.X += (int)(_window.Width * (0.005 * tries));
                }
                else if (direction == 3)
                {
                    point.X -= (int)(_window.Width * (0.005 * tries));
                }

                MouseMove(point);
                System.Threading.Thread.Sleep(50);

                if (unit is UnitMonster)
                    ((UnitMonster)unit).Update();
                else if (unit is UnitObject)
                    ((UnitObject)unit).Update();
                else if (unit is UnitItem)
                    ((UnitItem)unit).Update();

                if (tries > maxTries)
                {
                    tries = 0;
                    direction += 1;
                }

                if (direction >= 4)
                {
                    break;
                }
            }

            if (unit.IsHovered)
            {
                DoInput(input);
            }
        }

        public void DoInputAtWorldPosition(string input, Point worldPosition, bool targetFloor = true)
        {
            Point screenPosition = Automaton.TranslateToScreenOffset(_playerPositionWorld, worldPosition, _playerPositionScreen);

            // move Y axis up ever so slightly if floor, otherwise more
            var upfactor = targetFloor ? 0.02 : 0.07;

            screenPosition.Y = (float)(screenPosition.Y - (_window.Height * upfactor));

            DoInputAtScreenPosition(input, screenPosition);
        }

        public void DoInputAtScreenPosition(string input, Point screenPosition)
        {
            if (screenPosition.X < 20)
                screenPosition.X = 20;

            if (screenPosition.X > _window.Width * 0.98)
                screenPosition.X = (int)(_window.Width * 0.98);

            if (screenPosition.Y < 20)
                screenPosition.Y = 20;

            if (screenPosition.Y > _window.Height * 0.9)
                screenPosition.Y = (int)(_window.Height * 0.9);

            MouseMove(screenPosition);
            System.Threading.Thread.Sleep(10);

            DoInput(input);
        }

        public void DoInput(string input)
        {
            if (input == "+{LMB}")
            {
                InputOperations.HoldLShift();
                MouseClick();
                InputOperations.ReleaseLShift();
            }
            else if (input == "^{LMB}")
            {
                InputOperations.HoldLCtrl();
                MouseClick();
                InputOperations.ReleaseLCtrl();
            }
            else if (input == "+{RMB}")
            {
                InputOperations.HoldLShift();
                MouseClick(false);
                InputOperations.ReleaseLShift();
            }
            else if (input == "{CTRL}")
            {
                InputOperations.HoldLCtrl();
                System.Threading.Thread.Sleep(50);
                InputOperations.ReleaseLCtrl();
            }
            else if (input == "{LMB}")
            {
                MouseClick();
            }
            else if (input == "{RMB}")
            {
                MouseClick(false);
            }
            else
            {
                SendKeys.SendWait(input);
            }
        }

        public void MouseMove(Point p)
        {
            var point = new InputOperations.MousePoint((int)p.X, (int)p.Y);
            InputOperations.ClientToScreen(_mainWindowHandle, ref point);
            InputOperations.SetCursorPosition(point.X, point.Y);
        }

        private void MouseClick(bool left = true)
        {
            if (left)
            {
                InputOperations.MouseEvent(InputOperations.MouseEventFlags.LeftDown);
                System.Threading.Thread.Sleep(50);
                InputOperations.MouseEvent(InputOperations.MouseEventFlags.LeftUp);
            }
            else
            {
                InputOperations.MouseEvent(InputOperations.MouseEventFlags.RightDown);
                System.Threading.Thread.Sleep(50);
                InputOperations.MouseEvent(InputOperations.MouseEventFlags.RightUp);
            }
        }
    }
}
