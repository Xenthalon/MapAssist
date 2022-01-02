using GameOverlay.Drawing;
using MapAssist.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapAssist.Automation
{
    class OOG
    {
        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();

        private static readonly int WINDOW_X = 0;
        private static readonly int WINDOW_Y = 0;
        private static readonly int WINDOW_WIDTH = 1920;
        private static readonly int WINDOW_HEIGHT = 1080;

        private static Random random = new Random();

        private Input _input;
        private Rectangle _window;

        public OOG(Input input)
        {
            _input = input;
        }

        public void Update(Rectangle window)
        {
            _window = window;
        }

        public void CreateGame()
        {
            _input.DoInputAtScreenPosition("{LMB}", new Point((int)(_window.Width * 0.66), (int)(_window.Height * 0.065)));

            System.Threading.Thread.Sleep(1500);

            var gamename = RandomString(random.Next(10, 16));
            var password = RandomString(random.Next(5, 8));

            _input.DoInput(gamename + "{TAB}");

            System.Threading.Thread.Sleep(500);

            _input.DoInput(password);

            System.Threading.Thread.Sleep(500);

            _input.DoInput("{ENTER}");
        }

        public bool NeedsResize(Rectangle window)
        {
            return !(window.Left == WINDOW_X && window.Top == WINDOW_Y && window.Width == WINDOW_WIDTH && window.Height == WINDOW_HEIGHT);
        }

        public void ResizeWindow(IntPtr windowHandle)
        {
            _log.Info("Resizing window!");
            const int SWP_SHOWWINDOW = 0x0040;

            WindowsExternal.SetWindowPos(windowHandle, IntPtr.Zero, WINDOW_X, WINDOW_Y, WINDOW_WIDTH, WINDOW_HEIGHT, SWP_SHOWWINDOW);
            System.Threading.Thread.Sleep(500);
        }

        private static string RandomString(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}
