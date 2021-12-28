using GameOverlay.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapAssist.Automation
{
    class OOG
    {
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
            _input.DoInputAtScreenPosition("{LMB}", new Point((int)(_window.Width * 0.63), (int)(_window.Height * 0.065)));

            System.Threading.Thread.Sleep(1500);

            var gamename = RandomString(random.Next(10, 16));
            var password = RandomString(random.Next(5, 8));

            _input.DoInput(gamename + "{TAB}");

            System.Threading.Thread.Sleep(500);

            _input.DoInput(password);

            System.Threading.Thread.Sleep(500);

            _input.DoInput("{ENTER}");
        }

        private static string RandomString(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}
