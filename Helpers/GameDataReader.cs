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
        private Pathing _pathing;
        private MapApi _mapApi;

        public (Compositor, GameData, Pathing, List<PointOfInterest>) Get()
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
                        var areaData = _mapApi.GetMapData(gameData.Area);

                        if (areaData != null)
                        {
                            _pointsOfInterest = PointOfInterestHandler.Get(_mapApi, areaData, gameData);
                            _log.Info($"Found {_pointsOfInterest.Count} points of interest");

                            compositor = new Compositor(areaData, _pointsOfInterest);
                            _pathing = new Pathing(areaData);
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

            return (_compositor, _gameData, _pathing, _pointsOfInterest);
        }
    }
}
