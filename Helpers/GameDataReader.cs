using MapAssist.Types;
using System.Collections.Generic;

namespace MapAssist.Helpers
{
    public class GameDataReader
    {
        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();
        private Compositor _compositor;
        private volatile GameData _gameData;
        private List<PointOfInterest> _pointsOfInterest;
        private AreaData _areaData;
        private MapApi _mapApi;

        public (Compositor, GameData, AreaData, List<PointOfInterest>) Get()
        {
            var gameData = GameMemory.GetGameData();

            if (gameData != null)
            {
                if (gameData.HasGameChanged(_gameData))
                {
                    _log.Info($"Game changed to {gameData.Difficulty} with {gameData.MapSeed} seed");
                    _mapApi = new MapApi(gameData.Difficulty, gameData.MapSeed);
                }

                if (gameData.HasMapChanged(_gameData))
                {
                    Compositor compositor = null;

                    if (gameData.Area != Area.None)
                    {
                        _log.Info($"Area changed to {gameData.Area}");
                        _areaData = _mapApi.GetMapData(gameData.Area);

                        if (_areaData != null)
                        {
                            _pointsOfInterest = PointOfInterestHandler.Get(_mapApi, _areaData, gameData);
                            _log.Info($"Found {_pointsOfInterest.Count} points of interest");

                            compositor = new Compositor(_areaData, _pointsOfInterest);
                        }
                        else
                        {
                            _log.Info($"Area data not loaded");
                        }
                    }

                    _compositor = compositor;
                }
            }

            _gameData = gameData;

            return (_compositor, _gameData, _areaData, _pointsOfInterest);
        }
    }
}
