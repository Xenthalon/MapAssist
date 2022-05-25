using MapAssist.Settings;
using MapAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MapAssist.Helpers
{
    public class GameDataReader
    {
        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();
        private static readonly int GAME_CHANGE_SAFETY_LIMIT = 5;
        private (uint, Difficulty, Area) _possiblyNew = (0, Difficulty.Normal, Area.None);
        private int _possiblyNewCounter = 0;
        private volatile GameData _gameData;
        private AreaData _areaData;
        private (uint, Difficulty) _gameSeed;
        private MapApi _mapApi;

        public (GameData, AreaData, MapApi, bool) Get()
        {
            GameData gameData = null;

            for (var i = 0; i < 50; i++)
            {
                try
                {
                    gameData = GameMemory.GetGameData();
                    break;
                }
                catch (Exception e)
                {
                    if (e.Message != "Level id out of bounds.")
                    {
                        throw;
                    }
                    else
                    {
                        System.Threading.Thread.Sleep(100);
                        _log.Debug($"Level id out of bounds {i}/50");
                    }
                }
            }

            var changed = false;

            if (gameData != null)
            {
                if ((gameData.MapSeed != _gameSeed.Item1 && _possiblyNew.Item1 != gameData.MapSeed) ||
                    (gameData.Difficulty != _gameSeed.Item2 && _possiblyNew.Item2 != gameData.Difficulty) ||
                    (gameData.Area != _areaData.Area && _possiblyNew.Item3 != gameData.Area))
                {
                    _possiblyNew = (gameData.MapSeed, gameData.Difficulty, gameData.Area);
                    _log.Debug("Currently in " + _gameSeed + ", " + (_areaData != null ? _areaData.Area : Area.None) + ", Possible new " + _possiblyNew);
                }

                if (gameData.MapSeed == _possiblyNew.Item1 && gameData.Difficulty == _possiblyNew.Item2 && gameData.Area == _possiblyNew.Item3)
                {
                    _log.Debug($"counting {_possiblyNewCounter + 1}/{GAME_CHANGE_SAFETY_LIMIT}");
                    _possiblyNewCounter += 1;
                }

                if (_gameData == null || _possiblyNewCounter >= GAME_CHANGE_SAFETY_LIMIT)
                {
                    if (gameData.HasGameChanged(_gameData))
                    {
                        _log.Info($"Game changed to {gameData.Difficulty} with {gameData.MapSeed} seed");
                        _mapApi = new MapApi(gameData);
                        _gameSeed = (gameData.MapSeed, gameData.Difficulty);
                    }

                    if (gameData.HasMapChanged(_gameData) && gameData.Area != Area.None)
                    {
                        _log.Info($"Area changed to {gameData.Area}");
                        _areaData = _mapApi.GetMapData(gameData.Area);

                        if (_areaData == null)
                        {
                            _log.Info($"Area data not loaded");
                        }
                        else if (_areaData.PointsOfInterest == null)
                        {
                            _areaData.PointsOfInterest = PointOfInterestHandler.Get(_mapApi, _areaData, gameData);
                            _log.Info($"Found {_areaData.PointsOfInterest.Count} points of interest");
                        }

                        changed = true;
                    }

                    _possiblyNew = (0, Difficulty.Normal, Area.None);
                    _possiblyNewCounter = 0;
                }

                if (gameData.Area == _areaData.Area)
                {
                    _gameData = gameData;
                }
            }
            else
            {
                _gameData = gameData;
            }

            ImportFromGameData();

            return (_gameData, _areaData, _mapApi, changed);
        }

        private void ImportFromGameData()
        {
            if (_gameData == null || _areaData == null) return;

            foreach (var gameObject in _gameData.Objects)
            {
                if (!_areaData.IncludesPoint(gameObject.Position)) continue;

                if (gameObject.IsShrine || gameObject.IsWell)
                {
                    var existingPoint = _areaData.PointsOfInterest.FirstOrDefault(x => x.Position == gameObject.Position);

                    if (existingPoint != null)
                    {
                        existingPoint.Label = Shrine.ShrineDisplayName(gameObject);
                    }
                    else
                    {
                        _areaData.PointsOfInterest.Add(new PointOfInterest()
                        {
                            Area = _areaData.Area,
                            Label = Shrine.ShrineDisplayName(gameObject),
                            Position = gameObject.Position,
                            RenderingSettings = MapAssistConfiguration.Loaded.MapConfiguration.Shrine,
                            Type = PoiType.Shrine
                        });
                    }
                }
            }
        }
    }
}
