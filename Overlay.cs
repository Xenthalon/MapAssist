﻿/**
 *   Copyright (C) 2021 okaygo
 *
 *   https://github.com/misterokaygo/MapAssist/
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <https://www.gnu.org/licenses/>.
 **/

using GameOverlay.Drawing;
using GameOverlay.Windows;
using MapAssist.Helpers;
using MapAssist.Settings;
using MapAssist.Types;
using System;
using System.Windows.Forms;
using Graphics = GameOverlay.Drawing.Graphics;

namespace MapAssist
{
    public class Overlay : IDisposable
    {
        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();

        private readonly GraphicsWindow _window;
        private GameDataReader _gameDataReader;
        private GameData _gameData;
        private Compositor _compositor;
        private AreaData _areaData;
        private List<PointOfInterest> _pointsOfInterests;
        private Pathing _pathing;
        private BackgroundWorker _teleportWorker;
        private bool _teleporting = false;
        private List<System.Drawing.Point> _teleportPath;
        private bool _show = true;
        private static readonly object _lock = new object();

        public Overlay()
        {
            _gameDataReader = new GameDataReader();

            GameOverlay.TimerService.EnableHighPrecisionTimers();

            var gfx = new Graphics() { MeasureFPS = true };
            gfx.PerPrimitiveAntiAliasing = true;
            gfx.TextAntiAliasing = true;

            _window = new GraphicsWindow(0, 0, 1, 1, gfx) { FPS = 60, IsVisible = true };

            _window.DrawGraphics += _window_DrawGraphics;
            _window.DestroyGraphics += _window_DestroyGraphics;
        }

        private void _window_DrawGraphics(object sender, DrawGraphicsEventArgs e)
        {
            lock (_lock)
            {
                if (disposed) return;

                var gfx = e.Graphics;

                if (_teleporting && _teleportWorker != null && !_teleportWorker.IsBusy)
                {
                    _teleportWorker.RunWorkerAsync();
                }

                try
                {
                    (_compositor, _gameData) = _gameDataReader.Get();

                    gfx.ClearScene();

                    if (_compositor != null && InGame() && _compositor != null && _gameData != null)
                    {
                        UpdateLocation();

                        var errorLoadingAreaData = _compositor._areaData == null;

                        var overlayHidden = !_show ||
                            errorLoadingAreaData ||
                            (MapAssistConfiguration.Loaded.RenderingConfiguration.ToggleViaInGameMap && !_gameData.MenuOpen.Map) ||
                            (MapAssistConfiguration.Loaded.RenderingConfiguration.ToggleViaInGamePanels && _gameData.MenuPanelOpen > 0) ||
                            (MapAssistConfiguration.Loaded.RenderingConfiguration.ToggleViaInGamePanels && _gameData.MenuOpen.EscMenu) ||
                            Array.Exists(MapAssistConfiguration.Loaded.HiddenAreas, area => area == _gameData.Area) ||
                            _gameData.Area == Area.None ||
                            gfx.Width == 1 ||
                            gfx.Height == 1;

                        var size = MapAssistConfiguration.Loaded.RenderingConfiguration.Size;

                        var drawBounds = new Rectangle(0, 0, gfx.Width, gfx.Height * 0.8f);
                        switch (MapAssistConfiguration.Loaded.RenderingConfiguration.Position)
                        {
                            case MapPosition.TopLeft:
                                drawBounds = new Rectangle(PlayerIconWidth() + 40, PlayerIconWidth() + 100, 0, PlayerIconWidth() + 100 + size);
                                break;
                            case MapPosition.TopRight:
                                drawBounds = new Rectangle(0, 100, gfx.Width, 100 + size);
                                break;
                        }

                        _compositor.Init(gfx, _gameData, drawBounds);

                        if (!overlayHidden)
                        {
                            _compositor.DrawGamemap(gfx);
                            _compositor.DrawOverlay(gfx);
                            _compositor.DrawBuffs(gfx);
                        }

                        _compositor.DrawGameInfo(gfx, new Point(PlayerIconWidth() + 50, PlayerIconWidth() + 50), e, errorLoadingAreaData);
                        _compositor.DrawESP(gfx, _currentGameData, WindowSize(), _pathing);
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(ex);
                }
            }
        }

        public void Run()
        {
            _window.Create();
            _window.Join();
        }

        private bool InGame()
        {
            return _gameData != null && _gameData.MainWindowHandle != IntPtr.Zero;
        }

        public void dumpUnitData()
        {
            GameMemory.DumpUnits();
        }

        public void StartAutoTele()
        {
            if (_teleportWorker != null && _teleportWorker.IsBusy)
            {
                _teleportWorker.CancelAsync();
                _teleportWorker.Dispose();
                _teleporting = false;
            }

            _log.Debug($"Teleporting to {_pointsOfInterests[0].Label}");

            _teleportPath = _pathing.GetPathToLocation(_currentGameData.MapSeed, _currentGameData.Difficulty, true, _currentGameData.PlayerPosition, _pointsOfInterests[0].Position);

            _teleporting = true;

            _teleportWorker = new BackgroundWorker();
            _teleportWorker.DoWork += new DoWorkEventHandler(autoTele);
            _teleportWorker.WorkerSupportsCancellation = true;
            _teleportWorker.RunWorkerAsync();
        }

        public void autoTele(object sender, DoWorkEventArgs e)
        {
            if (_currentGameData != null && _pointsOfInterests != null && _pointsOfInterests.Count > 0 && _pathing != null)
            {
                Size windowSize = WindowSize();
                var playerPositionScreen = new Point(windowSize.Width / 2, (int)(windowSize.Height * 0.49));

                var nextMousePos = _compositor.translateToScreenOffset(_currentGameData.PlayerPosition, _teleportPath[0], playerPositionScreen);

                var point = new InputOperations.MousePoint((int)nextMousePos.X, (int)nextMousePos.Y);
                InputOperations.ClientToScreen(_currentGameData.MainWindowHandle, ref point);
                InputOperations.SetCursorPosition(point.X, point.Y);
                SendKeys.SendWait("{F3}");

                _log.Debug($"Teleported to {nextMousePos.X}/{nextMousePos.Y}");

                _teleportPath.RemoveAt(0);

                if (_teleportPath.Count > 0)
                {
                    System.Threading.Thread.Sleep(500);
                }
                else
                {
                    _log.Debug($"Done teleporting!");
                    _teleporting = false;
                }
            }
        }

        public void KeyPressHandler(object sender, KeyPressEventArgs args)
        {
            if (InGame())
            {
                if (args.KeyChar == 'l')
                {
                    dumpUnitData();
                }

                if (args.KeyChar == 'v')
                {
                    StartAutoTele();
                }

                if (args.KeyChar == MapAssistConfiguration.Loaded.HotkeyConfiguration.ToggleKey)
                {
                    _show = !_show;
                }

                if (args.KeyChar == MapAssistConfiguration.Loaded.HotkeyConfiguration.ZoomInKey)
                {
                    if (MapAssistConfiguration.Loaded.RenderingConfiguration.ZoomLevel > 0.25f)
                    {
                        MapAssistConfiguration.Loaded.RenderingConfiguration.ZoomLevel -= 0.25f;
                        MapAssistConfiguration.Loaded.RenderingConfiguration.Size +=
                          (int)(MapAssistConfiguration.Loaded.RenderingConfiguration.InitialSize * 0.05f);
                    }
                }

                if (args.KeyChar == MapAssistConfiguration.Loaded.HotkeyConfiguration.ZoomOutKey)
                {
                    if (MapAssistConfiguration.Loaded.RenderingConfiguration.ZoomLevel < 4f)
                    {
                        MapAssistConfiguration.Loaded.RenderingConfiguration.ZoomLevel += 0.25f;
                        MapAssistConfiguration.Loaded.RenderingConfiguration.Size -=
                          (int)(MapAssistConfiguration.Loaded.RenderingConfiguration.InitialSize * 0.05f);
                    }
                }

                if (args.KeyChar == MapAssistConfiguration.Loaded.HotkeyConfiguration.GameInfoKey)
                {
                    MapAssistConfiguration.Loaded.GameInfo.Enabled = !MapAssistConfiguration.Loaded.GameInfo.Enabled;
                }
            }
        }

        /// <summary>
        /// Resize overlay to currently active screen
        /// </summary>
        private void UpdateLocation()
        {
            var rect = WindowRect();
            var ultraWideMargin = UltraWideMargin();

            _window.Resize((int)(rect.Left + ultraWideMargin), (int)rect.Top, (int)(rect.Right - rect.Left - ultraWideMargin * 2), (int)(rect.Bottom - rect.Top));
            _window.PlaceAbove(_gameData.MainWindowHandle);
        }

        private Rectangle WindowRect()
        {
            WindowBounds rect;
            WindowHelper.GetWindowClientBounds(_gameData.MainWindowHandle, out rect);

            return new Rectangle(rect.Left, rect.Top, rect.Right, rect.Bottom);
        }

        private float UltraWideMargin()
        {
            var rect = WindowRect();
            return (float)Math.Max(Math.Round(((rect.Width + 2) - (rect.Height + 4) * 2.1f) / 2f), 0);
        }

        private float PlayerIconWidth()
        {
            var rect = WindowRect();
            return rect.Height / 20f;
        }

        ~Overlay()
        {
            Dispose(false);
        }

        private void _window_DestroyGraphics(object sender, DestroyGraphicsEventArgs e)
        {
            if (_compositor != null) _compositor.Dispose();
            _compositor = null;
        }

        private bool disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            lock (_lock)
            {
                if (!disposed)
                {
                    disposed = true; // This first to let GraphicsWindow.DrawGraphics know to return instantly
                    _window.Dispose(); // This second to dispose of GraphicsWindow
                    if (_compositor != null) _compositor.Dispose(); // This last so it's disposed after GraphicsWindow stops using it
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
