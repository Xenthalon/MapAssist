/**
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
using MapAssist.Types;
using System.Collections.Generic;
using Roy_T.AStar.Paths;
using Roy_T.AStar.Primitives;
using System.Linq;
using System;
using Roy_T.AStar.Grids;

namespace MapAssist.Automation
{
    public class Pathing
    {
        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();
        // add any tight indoors areas here to make line of sight stricter
        private static List<Area> _indoorAreas = new List<Area> {
            Area.AncientTunnels,
            Area.TowerCellarLevel1,
            Area.TowerCellarLevel2,
            Area.TowerCellarLevel3,
            Area.TowerCellarLevel4,
            Area.TowerCellarLevel5,
            Area.PitLevel1,
            Area.PitLevel2,
            Area.CatacombsLevel1,
            Area.CatacombsLevel2,
            Area.CatacombsLevel3,
            Area.CatacombsLevel4
        };

        // cache for calculated paths. each cache entry is only valid for a specified amount of time
        private Dictionary<(uint, Difficulty, Area, Point, Point), (List<Point>, long)> PathCache = new Dictionary<(uint, Difficulty, Area, Point, Point), (List<Point>, long)>();

        private readonly Grid Grid;

        private readonly AreaData _areaData;

        // Stuff for stinkyness exploration
        private static readonly Random Randomizer = new Random();
        private static readonly short StinkSampleSize = 50;
        private static readonly short StinkRange = 30;
        private static readonly short StinkMaximum = 500;
        private static readonly double MaxStinkSaturation = 0.98;

        // Stuff for teleport pathing
        private static readonly short RangeInvalid = 10000;
        private static readonly short TpRange = 20;
        private static readonly short BlockRange = 2;
        private short[,] m_distanceMatrix;
        private int m_rows;
        private int m_columns;

        public Pathing(AreaData areaData)
        {
            _areaData = areaData;
            Grid = _areaData.MapToGrid();
        }

        public List<Point> GetExploratoryPath(bool teleport, Point fromLocation)
        {
            var gridLocation = new Point((int)(fromLocation.X) - _areaData.Origin.X, (int)(fromLocation.Y) - _areaData.Origin.Y);

            var samples = new List<(List<Point> points, double coverage)>();

            for (var i = 0; i < StinkSampleSize; i++)
            {
                samples.Add(GetStinkyPath(gridLocation, teleport));
            }

            var path = new List<Point>(); ;
            var bestScore = 0.0;

            foreach (var sample in samples)
            {
                var score = sample.coverage / sample.points.Count();
                _log.Debug($"{Math.Round(sample.coverage, 5)} in {sample.points.Count()}: {score}");

                if (score > bestScore)
                {
                    path = sample.points;
                    bestScore = score;
                }
            }

            _log.Debug($"{path.Count()} chosen with best score {bestScore}");

            var result = new List<Point>();

            foreach (var point in path)
            {
                result.Add(new Point(point.X + _areaData.Origin.X, point.Y + _areaData.Origin.Y));
            }

            return result;
        }

        public bool HasLineOfSight(Point point1, Point point2)
        {
            var maxY = _areaData.CollisionGrid.GetLength(0);
            var maxX = _areaData.CollisionGrid[0].GetLength(0);

            var sight = true;

            var gridLocation1 = new Point((int)(point1.X) - _areaData.Origin.X, (int)(point1.Y) - _areaData.Origin.Y);
            var gridLocation2 = new Point((int)(point2.X) - _areaData.Origin.X, (int)(point2.Y) - _areaData.Origin.Y);

            double vectorX = gridLocation2.X - gridLocation1.X;
            double vectorY = gridLocation2.Y - gridLocation1.Y;

            if (Math.Abs(vectorX) > Math.Abs(vectorY))
            {
                var ySteps = vectorY / vectorX;

                if (vectorX >= 0)
                {
                    for (var i = 0; i < vectorX; i++)
                    {
                        for (var j = -1; j <= 1; j++)
                        {
                            var x = (int)gridLocation1.X + i;
                            var y = (int)Math.Round(gridLocation1.Y + (i * ySteps), 0) + j;

                            if (x >= 0 && y >= 0 && x < maxX && y < maxY)
                            {
                                var collisionValue = _areaData.CollisionGrid[y][x];

                                if (collisionValue == -1 || (IsIndoors() && collisionValue == 1)) // -1 are non-mapped areas, 1 are not walkable or walls, but can often be attacked through, 0 normal
                                {
                                    sight = false;
                                    break;
                                }
                            }
                        }
                    }
                }
                else
                {
                    for (var i = 0; i > vectorX; i--)
                    {
                        for (var j = -1; j <= 1; j++)
                        {
                            var x = (int)gridLocation1.X + i;
                            var y = (int)Math.Round(gridLocation1.Y + (i * ySteps), 0) + j;

                            if (x >= 0 && y >= 0 && x < maxX && y < maxY)
                            {
                                var collisionValue = _areaData.CollisionGrid[y][x];

                                if (collisionValue == -1 || (IsIndoors() && collisionValue == 1)) // -1 are non-mapped areas, 1 are not walkable or walls, but can often be attacked through, 0 normal
                                {
                                    sight = false;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                var xSteps = vectorX / vectorY;

                if (vectorY >= 0)
                {
                    for (var i = 0; i < vectorY; i++)
                    {
                        for (var j = -1; j <= 1; j++)
                        {
                            var x = (int)Math.Round(gridLocation1.X + (i * xSteps), 0) + j;
                            var y = (int)gridLocation1.Y + i;

                            if (x >= 0 && y >= 0 && x < maxX && y < maxY)
                            {
                                var collisionValue = _areaData.CollisionGrid[y][x];

                                if (collisionValue == -1 || (IsIndoors() && collisionValue == 1)) // -1 are non-mapped areas, 1 are not walkable or walls, but can often be attacked through, 0 normal
                                {
                                    sight = false;
                                    break;
                                }
                            }
                        }
                    }
                }
                else
                {
                    for (var i = 0; i > vectorY; i--)
                    {
                        for (var j = -1; j <= 1; j++)
                        {
                            var x = (int)Math.Round(gridLocation1.X + (i * xSteps), 0) + j;
                            var y = (int)gridLocation1.Y + i;

                            if (x >= 0 && y >= 0 && x < maxX && y < maxY)
                            {
                                var collisionValue = _areaData.CollisionGrid[y][x];

                                if (collisionValue == -1 || (IsIndoors() && collisionValue == 1)) // -1 are non-mapped areas, 1 are not walkable or walls, but can often be attacked through, 0 normal
                                {
                                    sight = false;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            return sight;
        }

        // debug functions
        public (Point, int)[] GetGridPointsBetween(Point point1, Point point2)
        {
            var maxY = _areaData.CollisionGrid.GetLength(0);
            var maxX = _areaData.CollisionGrid[0].GetLength(0);

            var gridPoints = new List<(Point, int)>();

            var gridLocation1 = new Point((int)(point1.X) - _areaData.Origin.X, (int)(point1.Y) - _areaData.Origin.Y);
            var gridLocation2 = new Point((int)(point2.X) - _areaData.Origin.X, (int)(point2.Y) - _areaData.Origin.Y);

            double vectorX = gridLocation2.X - gridLocation1.X;
            double vectorY = gridLocation2.Y - gridLocation1.Y;

            if (Math.Abs(vectorX) > Math.Abs(vectorY))
            {
                var ySteps = vectorY / vectorX;

                if (vectorX >= 0)
                {
                    for (var i = 0; i < vectorX; i++)
                    {
                        for (var j = -1; j <= 1; j++)
                        {
                            var x = (int)gridLocation1.X + i;
                            var y = (int)Math.Round(gridLocation1.Y + (i * ySteps), 0) + j;

                            if (x >= 0 && y >= 0 && x < maxX && y < maxY)
                            {
                                var collisionValue = _areaData.CollisionGrid[y][x];

                                gridPoints.Add((new Point(x + _areaData.Origin.X, y + _areaData.Origin.Y), collisionValue));
                            }
                        }
                    }
                }
                else
                {
                    for (var i = 0; i > vectorX; i--)
                    {
                        for (var j = -1; j <= 1; j++)
                        {
                            var x = (int)gridLocation1.X + i;
                            var y = (int)Math.Round(gridLocation1.Y + (i * ySteps), 0) + j;

                            if (x >= 0 && y >= 0 && x < maxX && y < maxY)
                            {
                                var collisionValue = _areaData.CollisionGrid[y][x];

                                gridPoints.Add((new Point(x + _areaData.Origin.X, y + _areaData.Origin.Y), collisionValue));
                            }
                        }
                    }
                }
            }
            else
            {
                var xSteps = vectorX / vectorY;

                if (vectorY >= 0)
                {
                    for (var i = 0; i < vectorY; i++)
                    {
                        for (var j = -1; j <= 1; j++)
                        {
                            var x = (int)Math.Round(gridLocation1.X + (i * xSteps), 0) + j;
                            var y = (int)gridLocation1.Y + i;

                            if (x >= 0 && y >= 0 && x < maxX && y < maxY)
                            {
                                var collisionValue = _areaData.CollisionGrid[y][x];

                                gridPoints.Add((new Point(x + _areaData.Origin.X, y + _areaData.Origin.Y), collisionValue));
                            }
                        }
                    }
                }
                else
                {
                    for (var i = 0; i > vectorY; i--)
                    {
                        for (var j = -1; j <= 1; j++)
                        {
                            var x = (int)Math.Round(gridLocation1.X + (i * xSteps), 0) + j;
                            var y = (int)gridLocation1.Y + i;

                            if (x >= 0 && y >= 0 && x < maxX && y < maxY)
                            {
                                var collisionValue = _areaData.CollisionGrid[y][x];

                                gridPoints.Add((new Point(x + _areaData.Origin.X, y + _areaData.Origin.Y), collisionValue));
                            }
                        }
                    }
                }
            }

            return gridPoints.ToArray();
        }

        public bool IsIndoors()
        {
            return _indoorAreas.Contains(_areaData.Area);
        }

        public bool IsWalkable(Point worldPoint)
        {
            var gridLocation = new Point((int)(worldPoint.X) - _areaData.Origin.X, (int)(worldPoint.Y) - _areaData.Origin.Y);

            var maxY = _areaData.CollisionGrid.GetLength(0);
            var maxX = _areaData.CollisionGrid[0].GetLength(0);

            if (gridLocation.X < 0 || gridLocation.Y < 0 || gridLocation.X >= maxX || gridLocation.Y >= maxY)
                return false;

            var value = _areaData.CollisionGrid[(int)gridLocation.Y][(int)gridLocation.X];

            return value == 0 || value == 16; // IsMovable implementation
        }

        public List<Point> GetPathToLocation(uint mapId, Difficulty difficulty, bool teleport, Point fromLocation, Point toLocation)
        {
            // check if we have a valid cache entry for this path. if thats the case we are done and can return the cache entry
            (uint mapId, Difficulty difficulty, Area area, Point fromLocation, Point toLocation) pathCacheKey = (mapId, difficulty, _areaData.Area, fromLocation, toLocation);

            if (PathCache.ContainsKey(pathCacheKey) && ((DateTimeOffset.Now.ToUnixTimeMilliseconds() - PathCache[pathCacheKey].Item2) < 5000))
            {
                return PathCache[pathCacheKey].Item1;
            }

            var result = new List<Point>();

            // cancel if the provided points dont map into the collisionMap of the AreaData
            if (!_areaData.TryMapToPointInMap(fromLocation, out var fromPosition) || !_areaData.TryMapToPointInMap(toLocation, out var toPosition))
            {
                return result;
            }

            if (teleport)
            {
                var pathFound = false;
                var teleportPath = GetTeleportPath(fromPosition, toPosition, out pathFound);
                if (pathFound)
                {
                    /* the TeleportPather returns the Points on the path withoug the Origin offset. 
                     * The Compositor expects this offset so we have to add it before we can return the result */
                    result = teleportPath.Select(p => new Point(p.X += _areaData.Origin.X, p.Y += _areaData.Origin.Y)).Skip(1).ToList();

                    // remove the last teleport location if its too close
                    if (result.Count() > 1 && Automaton.GetDistance(result.ElementAt(result.Count() - 2), result.ElementAt(result.Count() - 1)) < 5)
                    {
                        result.RemoveAt(result.Count() - 1);
                    }
                }
            }
            else
            {
                var pathFinder = new PathFinder();
                var fromGridPosition = new GridPosition((int)fromPosition.X, (int)fromPosition.Y);
                var toGridPosition = new GridPosition((int)toPosition.X, (int)toPosition.Y);

                var path = pathFinder.FindPath(fromGridPosition, toGridPosition, Grid);
                var endPosition = path.Edges.LastOrDefault()?.End.Position;
                if (endPosition.HasValue && _areaData.MapToPoint(endPosition.Value) == toLocation)
                {
                    result = path.Edges.Where((p, i) => i % 3 == 0 || i == path.Edges.Count - 1).Select(e => _areaData.MapToPoint(e.End.Position)).ToList();
                }
            }

            // add the calculated path to the cache
            PathCache[pathCacheKey] = (result, DateTimeOffset.Now.ToUnixTimeMilliseconds());

            return result;
        }

        private (List<Point> points, double coverage) GetStinkyPath(Point fromLocation, bool teleport)
        {
            var path = new List<Point>();

            var cleanArea = _areaData.MapOfStinkyness();
            var farts = 0;
            var coverage = 0.0;

            var currentLocation = fromLocation;

            do
            {
                StinkUpThePlace(ref cleanArea, (int)currentLocation.X, (int)currentLocation.Y);

                currentLocation = FindClosestCleanSpot(cleanArea, (int)currentLocation.X, (int)currentLocation.Y, teleport);

                path.Add(currentLocation);

                farts += 1;
                coverage = GetStinkCoverage(cleanArea);
            }
            while (farts < StinkMaximum && coverage < MaxStinkSaturation);

            return (path, Math.Round(coverage * 100, 2));
        }

        private Point FindClosestCleanSpot(Navigation.StinkySpot[,] area, int placeX, int placeY, bool teleport = false)
        {
            var range = teleport ? TpRange : 5;

            var currentSmell = double.MaxValue;
            var bestLocations = new List<Point>();

            for (var j = range * -1; j < range; j++)
            {
                for (var i = range * -1; i < range; i++)
                {
                    if (placeX + i < 0 || placeY + j < 0 ||
                        placeX + i >= area.GetUpperBound(0) || placeY + j >= area.GetUpperBound(1) ||
                        !area[placeX + i, placeY + j].IsWalkable ||
                        CalculateDistance(placeX, placeY, placeX + i, placeY + j) > range)
                    {
                        continue;
                    }

                    if (Math.Round(area[placeX + i, placeY + j].Stinkyness, 1) < Math.Round(currentSmell, 1))
                    {
                        currentSmell = area[placeX + i, placeY + j].Stinkyness;
                        bestLocations = new List<Point>();
                        bestLocations.Add(new Point(placeX + i, placeY + j));
                    }
                    else if (Math.Round(area[placeX + i, placeY + j].Stinkyness, 1) == Math.Round(currentSmell, 1))
                    {
                        bestLocations.Add(new Point(placeX + i, placeY + j));
                    }
                }
            }

            var bestLocation = bestLocations[0];

            if (bestLocations.Count() > 1)
            {
                bestLocation = bestLocations[Randomizer.Next(0, bestLocations.Count())];
            }

            return bestLocation;
        }

        private void StinkUpThePlace(ref Navigation.StinkySpot[,] cleanArea, int placeX, int placeY)
        {
            for (var j = StinkRange * -1; j < StinkRange; j++)
            {
                for (var i = StinkRange * -1; i < StinkRange; i++)
                {
                    if (placeX + i < 0 || placeY + j < 0 ||
                        placeX + i >= cleanArea.GetUpperBound(0) || placeY + j >= cleanArea.GetUpperBound(1) ||
                        !cleanArea[placeX + i, placeY + j].IsWalkable)
                    {
                        continue;
                    }

                    var distance = CalculateDistance(placeX, placeY, placeX + i, placeY + j);

                    var stinkyness = distance == 0 ? StinkRange * 25 : StinkRange / distance;
                    // _log.Info($"{i}/{j}: {stinkyness}");

                    cleanArea[placeX + i, placeY + j].Stinkyness += stinkyness;
                }
            }
        }

        private double GetStinkCoverage(Navigation.StinkySpot[,] area)
        {
            var total = 0;
            var stinky = 0;

            foreach (var e in area)
            {
                if (e.IsWalkable)
                {
                    total += 1;

                    if (e.Stinkyness > 0)
                    {
                        stinky += 1;
                    }
                }
            }

            return (double)stinky / (double)total;
        }

        private List<Point> GetTeleportPath(Point fromLocation, Point toLocation, out bool pathFound)
        {
            MakeDistanceTable(toLocation);
            var path = new List<Point>
            {
                fromLocation
            };
            var idxPath = 1;

            var bestMove = new BestMove
            {
                Move = fromLocation,
                Result = PathingResult.DestinationNotReachedYet
            };

            var move = GetBestMove(bestMove.Move, toLocation, BlockRange);
            while (move.Result != PathingResult.Failed && idxPath < 100)
            {
                // Reached?
                if (move.Result == PathingResult.Reached)
                {
                    AddToListAtIndex(path, toLocation, idxPath);
                    idxPath++;
                    pathFound = true;
                    return path.GetRange(0, idxPath);
                }

                // Perform a redundancy check
                var nRedundancy = GetRedundancy(path, idxPath, move.Move);
                if (nRedundancy == -1)
                {
                    // no redundancy
                    AddToListAtIndex(path, move.Move, idxPath);
                    idxPath++;
                }
                else
                {
                    // redundancy found, discard all redundant steps
                    idxPath = nRedundancy + 1;
                    AddToListAtIndex(path, move.Move, idxPath);
                }

                move = GetBestMove(move.Move, toLocation, BlockRange);
            }

            pathFound = false;
            return null;
        }

        private void MakeDistanceTable(Point toLocation)
        {
            m_rows = _areaData.CollisionGrid.GetLength(0);
            m_columns = _areaData.CollisionGrid[0].GetLength(0);
            m_distanceMatrix = new short[m_columns, m_rows];
            for (var i = 0; i < m_columns; i++)
            {
                for (var k = 0; k < m_rows; k++)
                {
                    m_distanceMatrix[i, k] = (short)_areaData.CollisionGrid[k][i];
                }
            }

            for (var x = 0; x < m_columns; x++)
            {
                for (var y = 0; y < m_rows; y++)
                {
                    if ((m_distanceMatrix[x, y] % 2) == 0)
                        m_distanceMatrix[x, y] = (short)CalculateDistance(x, y, toLocation.X, toLocation.Y);
                    else
                        m_distanceMatrix[x, y] = RangeInvalid;
                }
            }

            m_distanceMatrix[(int)toLocation.X, (int)toLocation.Y] = 1;
        }


        private void AddToListAtIndex(List<Point> list, Point point, int index)
        {
            if (index < list.Count)
            {
                list[index] = point;
                return;
            }
            else if (index == list.Count)
            {
                list.Add(point);
                return;
            }

            throw new InvalidOperationException();
        }

        private BestMove GetBestMove(Point position, Point toLocation, int blockRange)
        {
            if (CalculateDistance(toLocation, position) <= TpRange)
            {
                return new BestMove
                {
                    Result = PathingResult.Reached,
                    Move = toLocation
                };
            }

            if (!IsValidIndex((int)position.X, (int)position.Y))
            {
                return new BestMove
                {
                    Result = PathingResult.Failed,
                    Move = new Point(0, 0)
                };
            }

            Block(position, blockRange);

            var best = new Point(0, 0);
            int value = RangeInvalid;

            for (var x = position.X - TpRange; x <= position.X + TpRange; x++)
            {
                for (var y = position.Y - TpRange; y <= position.Y + TpRange; y++)
                {
                    if (!IsValidIndex((int)x, (int)y))
                        continue;

                    var p = new Point((ushort)x, (ushort)y);

                    if (m_distanceMatrix[(int)p.X, (int)p.Y] < value && CalculateDistance(p, position) <= TpRange)
                    {
                        value = m_distanceMatrix[(int)p.X, (int)p.Y];
                        best = p;
                    }
                }
            }

            if (value >= RangeInvalid || best == null)
            {
                return new BestMove
                {
                    Result = PathingResult.Failed,
                    Move = new Point(0, 0)
                };
            }

            Block(best, blockRange);
            return new BestMove
            {
                Result = PathingResult.DestinationNotReachedYet,
                Move = best
            };
        }


        private void Block(Point position, int nRange)
        {
            nRange = Math.Max(nRange, 1);

            for (var i = position.X - nRange; i < position.X + nRange; i++)
            {
                for (var j = position.Y - nRange; j < position.Y + nRange; j++)
                {
                    if (IsValidIndex((int)i, (int)j))
                        m_distanceMatrix[(int)i, (int)j] = RangeInvalid;
                }
            }
        }

        private int GetRedundancy(List<Point> currentPath, int idxPath, Point position)
        {
            // step redundancy check
            for (var i = 1; i < idxPath; i++)
            {
                if (CalculateDistance(currentPath[i].X, currentPath[i].Y, position.X, position.Y) <= TpRange / 2.0)
                    return i;
            }

            return -1;
        }

        private bool IsValidIndex(int x, int y)
        {
            return x >= 0 && x < m_columns && y >= 0 && y < m_rows;
        }

        private static double CalculateDistance(float x1, float y1, float x2, float y2)
        {
            return Math.Sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2));
        }

        private static double CalculateDistance(Point point1, Point point2)
        {
            return CalculateDistance(point1.X, point1.Y, point2.X, point2.Y);
        }

        private struct BestMove
        {
            public PathingResult Result { get; set; }

            public Point Move { get; set; }
        }

    }

    enum PathingResult
    {
        Failed = 0,     // Failed, error occurred or no available path
        DestinationNotReachedYet,      // Path OK, destination not reached yet
        Reached // Path OK, destination reached(Path finding completed successfully)
    };

}
