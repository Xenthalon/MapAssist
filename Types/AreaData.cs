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
using MapAssist.Helpers;
using MapAssist.Settings;
using Roy_T.AStar.Graphs;
using Roy_T.AStar.Grids;
using Roy_T.AStar.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MapAssist.Types
{
    public class AdjacentLevel
    {
        public Area Area;
        public Point[] Exits;
        public bool IsPortal;
    }

    public class AreaData
    {
        public Area Area;
        public Point Origin;
        public Dictionary<Area, AdjacentLevel> AdjacentLevels;
        public int[][] CollisionGrid;
        public Rectangle ViewInputRect;
        public Rectangle ViewOutputRect;
        public Dictionary<Npc, Point[]> NPCs;
        public Dictionary<GameObject, Point[]> Objects;

        public void CalcViewAreas(float angleRadians)
        {
            var points = new List<Point>(); // All non-unwalkable points used for calculating the input and output view dimensions

            // Calculate borders
            var lookOffsets = new int[][] {
                            new int[] { -1, -1 },
                            new int[] { -1, 0 },
                            new int[] { -1, 1 },
                            new int[] { 0, -1 },
                            new int[] { 0, 1 },
                            new int[] { 1, -1 },
                            new int[] { 1, 0 },
                            new int[] { 1, 1 }
                        };

            for (var y = 0; y < CollisionGrid.Length; y++)
            {
                for (var x = 0; x < CollisionGrid[0].Length; x++)
                {
                    var type = CollisionGrid[y][x];
                    var isCurrentPixelWalkable = type == 0;
                    var isCurrentPixelUnwalkable = type == -1;

                    if (isCurrentPixelWalkable)
                    {
                        points.Add(new Point(x, y));
                        continue;
                    }

                    foreach (var offset in lookOffsets)
                    {
                        var dy = y + offset[0];
                        var dx = x + offset[1];

                        var offsetInBounds =
                            dy >= 0 && dy < CollisionGrid.Length &&
                            dx >= 0 && dx < CollisionGrid[0].Length;

                        if (offsetInBounds && CollisionGrid[dy][dx] == 0)
                        {
                            CollisionGrid[y][x] = 1; // Wall
                            points.Add(new Point(x, y));
                            break;
                        }
                    }
                }
            }

            if (MapAssistConfiguration.Loaded.RenderingConfiguration.OverlayMode)
            {
                ViewOutputRect = ViewInputRect = new Rectangle(0, 0, CollisionGrid[0].Length, CollisionGrid.Length);
            }
            else
            {
                ViewInputRect = points.ToArray().ToRectangle(1);
                ViewOutputRect = points.Select(point => point.Subtract(ViewInputRect.Left + ViewInputRect.Width / 2, ViewInputRect.Top + ViewInputRect.Height / 2).Rotate(angleRadians)).ToArray().ToRectangle(1);
            }
        }
    }

    class AreaLabel
    {
        public string Text;
        public int[] Level;
    }

    public static class AreaDataExtensions
    {
        private static readonly float DistanceBetweenCells = 5.0f;

        public static bool TryMapToPointInMap(this AreaData areaData, Point point, out Point relativePoint)
        {
            try
            {
                var relativePosition = new Point(point.X - areaData.Origin.X, point.Y - areaData.Origin.Y);
                ;
                var rows = areaData.CollisionGrid.GetLength(0);
                if (rows == 0)
                {
                    relativePoint = new Point();
                    return false;

                }
                var columns = areaData.CollisionGrid[0].GetLength(0);
                if (relativePosition.X < columns && relativePosition.Y < rows)
                {
                    relativePoint = relativePosition;
                    return true;
                }
            }
            catch (ArithmeticException)
            {

            }

            relativePoint = new Point();
            return false;
        }

        public static Point MapToPoint(this AreaData areaData, Position position)
        {
            var point = areaData.Origin;
            return new Point((ushort)(point.X + position.X / DistanceBetweenCells), (ushort)(point.Y + position.Y / DistanceBetweenCells));
        }

        public static Grid MapToGrid(this AreaData areaData)
        {
            var rows = areaData.CollisionGrid.GetLength(0);
            var columns = areaData.CollisionGrid[0].GetLength(0);
            var nodes = new Node[columns, rows];

            for (var i = 0; i < columns; i++)
            {
                for (var j = 0; j < rows; j++)
                {
                    nodes[i, j] = new Node(new Position(i * DistanceBetweenCells, j * DistanceBetweenCells));
                }
            }

            for (var i = 0; i < columns; i++)
            {
                for (var j = 0; j < rows; j++)
                {
                    if (!IsMovable(areaData.CollisionGrid[j][i]))
                    {
                        continue;
                    }

                    var fromNode = nodes[i, j];

                    var speed = GetVelocityWithAdjacency(areaData, j, i, rows, columns);
                    if (i + 1 < columns && IsMovable(areaData.CollisionGrid[j][i + 1]))
                    {
                        var toNode = nodes[i + 1, j];
                        var velocity = Velocity.FromMetersPerSecond(speed);
                        fromNode.Connect(toNode, velocity);
                        toNode.Connect(fromNode, velocity);
                    }

                    if (j + 1 < rows && IsMovable(areaData.CollisionGrid[j + 1][i]))
                    {
                        var toNode = nodes[i, j + 1];
                        var velocity = Velocity.FromMetersPerSecond(speed);
                        fromNode.Connect(toNode, velocity);
                        toNode.Connect(fromNode, velocity);
                    }
                }
            }

            var grid = Grid.CreateGridFrom2DArrayOfNodes(nodes);
            return grid;
        }

        public static bool IsMovable(int value)
        {
            return value == 0 || value == 16;
        }

        public static float GetVelocityWithAdjacency(this AreaData areaData, int i, int j, int columns, int rows)
        {
            var velocityCurrent = GetVelocityToPoint(areaData, i, j, columns, rows);
            var minAdjacents = GetVelocityToPoint(areaData, i - 3, j - 3, columns, rows);
            minAdjacents = Math.Min(minAdjacents, GetVelocityToPoint(areaData, i - 3, j + 3, columns, rows));
            minAdjacents = Math.Min(minAdjacents, GetVelocityToPoint(areaData, i + 3, j - 3, columns, rows));
            minAdjacents = Math.Min(minAdjacents, GetVelocityToPoint(areaData, i + 3, j + 3, columns, rows));
            return (float)(velocityCurrent * 0.3 + minAdjacents * 0.7);
        }

        public static float GetVelocityToPoint(this AreaData areaData, int i, int j, int columns, int rows)
        {
            if (i < 0 || j < 0 || i >= columns || j >= rows)
            {
                return DistanceBetweenCells;
            }

            if ((j == 0 || IsMovable(areaData.CollisionGrid[i][j - 1])) && (j + 1 >= rows || IsMovable(areaData.CollisionGrid[i][j + 1])) && (i == 0 || IsMovable(areaData.CollisionGrid[i - 1][j])) && (i + 1 >= columns || IsMovable(areaData.CollisionGrid[i + 1][j])))
            {
                return DistanceBetweenCells;
            }

            return DistanceBetweenCells / 10;
        }
    }
}
