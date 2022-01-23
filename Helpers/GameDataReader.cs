using MapAssist.Types;
using System.Collections.Generic;

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
        private List<PointOfInterest> _pointsOfInterest;
        private MapApi _mapApi;

        public (GameData, AreaData, List<PointOfInterest>, bool) Get()
        {
            var gameData = GameMemory.GetGameData();
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
                        _mapApi = new MapApi(gameData.Difficulty, gameData.MapSeed);
                        _gameSeed = (gameData.MapSeed, gameData.Difficulty);
                    }

                    if (gameData.HasMapChanged(_gameData) && gameData.Area != Area.None)
                    {
                        _log.Info($"Area changed to {gameData.Area}");
                        _areaData = _mapApi.GetMapData(gameData.Area);

                        if (_areaData != null)
                        {
                            _pointsOfInterest = PointOfInterestHandler.Get(_mapApi, _areaData, gameData);
                            _log.Info($"Found {_pointsOfInterest.Count} points of interest");
                        }
                        else
                        {
                            _log.Info($"Area data not loaded");
                        }

                        changed = true;
                    }

                    _possiblyNew = (0, Difficulty.Normal, Area.None);
                    _possiblyNewCounter = 0;
                    _gameData = gameData;
                }
            }

            return (gameData, _areaData, _pointsOfInterest, changed);
        }
    }
}
