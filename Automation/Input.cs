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
    class Input
    {
        IntPtr _mainWindowHandle;
        Point _playerPositionWorld;
        Point _playerPositionScreen;

        public Input() { }

        public void Update(GameData gameData, Rectangle window)
        {
            if (gameData != null && gameData.PlayerUnit.IsValidPointer() && gameData.PlayerUnit.IsValidUnit())
            {
                _mainWindowHandle = gameData.MainWindowHandle;
                _playerPositionWorld = gameData.PlayerUnit.Position;
                _playerPositionScreen = new Point(window.Width / 2, (int)(window.Height * 0.49));
            }
        }

        public void DoInputAtWorldPosition(string input, Point worldPosition)
        {
            Point screenPosition = Automaton.TranslateToScreenOffset(_playerPositionWorld, worldPosition, _playerPositionScreen);

            DoInputAtScreenPosition(input, screenPosition);
        }

        public void DoInputAtScreenPosition(string input, Point screenPosition)
        {
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

        private void MouseMove(Point p)
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
