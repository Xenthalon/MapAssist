using GameOverlay.Drawing;
using MapAssist.Helpers;
using System;
using System.Linq;

namespace MapAssist.Automation
{
    class OOG
    {
        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();

        private int LONG_SLEEP;
        private int WINDOW_X;
        private int WINDOW_Y;
        private int WINDOW_WIDTH;
        private int WINDOW_HEIGHT;

        private static Random random = new Random();

        private Input _input;
        private Rectangle _window;

        public OOG(BotConfiguration config, Input input)
        {
            WINDOW_X = config.Settings.WindowX;
            WINDOW_Y = config.Settings.WindowY;
            WINDOW_WIDTH = config.Settings.WindowWidth;
            WINDOW_HEIGHT = config.Settings.WindowHeight;
            LONG_SLEEP = config.Settings.LongSleep;

            _input = input;
        }

        public void Update(Rectangle window)
        {
            _window = window;
        }

        public void CreateGame()
        {
            _input.DoInputAtScreenPosition("{LMB}", new Point((int)(_window.Width * 0.66), (int)(_window.Height * 0.065)));

            System.Threading.Thread.Sleep(3 * LONG_SLEEP);

            _input.DoInputAtScreenPosition("{LMB}", new Point((int)(_window.Width * 0.8), (int)(_window.Height * 0.17)));

            var gamename = RandomString(random.Next(12, 16));
            var password = RandomString(random.Next(5, 8));

            _input.DoInput("^{a}");
            System.Threading.Thread.Sleep(LONG_SLEEP);
            _input.DoInput("{BACKSPACE}" + gamename + "{TAB}");
            System.Threading.Thread.Sleep(LONG_SLEEP);

            _input.DoInput("^{a}");
            System.Threading.Thread.Sleep(LONG_SLEEP);
            _input.DoInput(password);
            System.Threading.Thread.Sleep(LONG_SLEEP);

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
            System.Threading.Thread.Sleep(LONG_SLEEP);
        }

        private static string RandomString(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}
