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

using MapAssist.Settings;
using MapAssist.Types;
using System;
using System.Collections.Generic;

namespace MapAssist.Helpers
{
    public static class GameMemory
    {
        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();
        private static Dictionary<int, uint> _lastMapSeed = new Dictionary<int, uint>();
        private static int _currentProcessId;

        public static Dictionary<int, UnitAny> PlayerUnits = new Dictionary<int, UnitAny>();
        public static Dictionary<int, Dictionary<uint, UnitAny>> Corpses = new Dictionary<int, Dictionary<uint, UnitAny>>();

        public static GameData GetGameData()
        {
            if (!MapAssistConfiguration.Loaded.RenderingConfiguration.StickToLastGameWindow && !GameManager.IsGameInForeground)
            {
                return null;
            }

            var processContext = GameManager.GetProcessContext();

            if (processContext == null)
            {
                return null;
            }

            using (processContext)
            {
                _currentProcessId = processContext.ProcessId;

                var menuOpen = processContext.Read<byte>(GameManager.MenuOpenOffset);
                var menuData = processContext.Read<Structs.MenuData>(GameManager.MenuDataOffset);

                if (!menuData.InGame && Corpses.TryGetValue(_currentProcessId, out var _))
                {
                    Corpses[_currentProcessId].Clear();
                }

                var playerUnit = GameManager.PlayerUnit;

                if (!PlayerUnits.TryGetValue(_currentProcessId, out var _))
                {
                    PlayerUnits.Add(_currentProcessId, playerUnit);
                }
                else
                {
                    PlayerUnits[_currentProcessId] = playerUnit;
                }

                if (!Equals(playerUnit, default(UnitAny)))
                {
                    var mapSeed = playerUnit.Act.MapSeed;

                    if (mapSeed <= 0 || mapSeed > 0xFFFFFFFF)
                    {
                        throw new Exception("Map seed is out of bounds.");
                    }
                    if (!_lastMapSeed.TryGetValue(_currentProcessId, out var _))
                    {
                        _lastMapSeed.Add(_currentProcessId, 0);
                    }
                    if (mapSeed != _lastMapSeed[_currentProcessId])
                    {
                        _lastMapSeed[_currentProcessId] = mapSeed;
                        //dispose leftover timers in this process if we started a new game
                        if (Items.ItemLogTimers.TryGetValue(_currentProcessId, out var _))
                        {
                            foreach (var timer in Items.ItemLogTimers[_currentProcessId])
                            {
                                timer.Dispose();
                            }
                        }

                        if (!Items.ItemUnitHashesSeen.TryGetValue(_currentProcessId, out var _))
                        {
                            Items.ItemUnitHashesSeen.Add(_currentProcessId, new HashSet<string>());
                            Items.ItemUnitIdsSeen.Add(_currentProcessId, new HashSet<uint>());
                            Items.ItemLog.Add(_currentProcessId, new List<UnitAny>());
                        }
                        else
                        {
                            Items.ItemUnitHashesSeen[_currentProcessId].Clear();
                            Items.ItemUnitIdsSeen[_currentProcessId].Clear();
                            Items.ItemLog[_currentProcessId].Clear();
                        }

                        if (!Corpses.TryGetValue(_currentProcessId, out var _))
                        {
                            Corpses.Add(_currentProcessId, new Dictionary<uint, UnitAny>());
                        }
                        else
                        {
                            Corpses[_currentProcessId].Clear();
                        }
                    }

                    var session = new Session(GameManager.GameIPOffset);

                    var actId = playerUnit.Act.ActId;
                    var gameDifficulty = playerUnit.Act.ActMisc.GameDifficulty;

                    if (!gameDifficulty.IsValid())
                    {
                        throw new Exception("Game difficulty out of bounds.");
                    }

                    var levelId = playerUnit.Path.Room.RoomEx.Level.LevelId;

                    if (!levelId.IsValid())
                    {
                        throw new Exception("Level id out of bounds.");
                    }

                    Items.CurrentItemLog = Items.ItemLog[_currentProcessId];

                    var rosterData = new Roster(GameManager.RosterDataOffset);

                    playerUnit = playerUnit.Update(rosterData);
                    if (!Equals(playerUnit, default(UnitAny)))
                    {
                        var monsterList = new HashSet<UnitAny>();
                        var mercList = new HashSet<UnitAny>();
                        var npcList = new HashSet<UnitAny>();
                        var itemList = new HashSet<UnitAny>();
                        var objectList = new HashSet<UnitAny>();
                        var playerList = new Dictionary<uint, UnitAny>();
                        GetUnits(rosterData, ref monsterList, ref itemList, ref playerList, ref objectList, ref mercList, ref npcList);

                        return new GameData
                        {
                            PlayerPosition = playerUnit.Position,
                            MapSeed = mapSeed,
                            Area = levelId,
                            Difficulty = gameDifficulty,
                            MainWindowHandle = GameManager.MainWindowHandle,
                            PlayerName = playerUnit.Name,
                            Monsters = monsterList,
                            Mercs = mercList,
                            NPCs = npcList,
                            Items = itemList,
                            Objects = objectList,
                            Players = playerList,
                            Session = session,
                            Roster = rosterData,
                            PlayerUnit = playerUnit,
                            MenuOpen = menuData,
                            MenuPanelOpen = menuOpen,
                            ProcessId = _currentProcessId
                        };
                    }
                }
            }

            GameManager.ResetPlayerUnit();
            return null;
        }

        private static void GetUnits(Roster rosterData, ref HashSet<UnitAny> monsterList, ref HashSet<UnitAny> itemList, ref Dictionary<uint, UnitAny> playerList, ref HashSet<UnitAny> objectList, ref HashSet<UnitAny> mercList, ref HashSet<UnitAny> npcList)
        {
            for (var i = 0; i <= 4; i++)
            {
                var unitType = (UnitType)i;
                var unitHashTable = new Structs.UnitHashTable();
                if (unitType == UnitType.Missile)
                {
                    //missiles are contained in a different table
                    unitHashTable = GameManager.UnitHashTable(128 * 8 * (i + 6));
                }
                else
                {
                    unitHashTable = GameManager.UnitHashTable(128 * 8 * i);
                }
                foreach (var pUnitAny in unitHashTable.UnitTable)
                {
                    var unitAny = new UnitAny(pUnitAny, rosterData);
                    while (unitAny.IsValidUnit())
                    {
                        switch (unitType)
                        {
                            case UnitType.Monster:
                                if (!monsterList.Contains(unitAny) && unitAny.IsMonster())
                                {
                                    monsterList.Add(unitAny);
                                }

                                if (!mercList.Contains(unitAny) && unitAny.IsMerc())
                                {
                                    mercList.Add(unitAny);
                                }

                                if (!npcList.Contains(unitAny) && unitAny.IsTownNpc())
                                {
                                    npcList.Add(unitAny);
                                }

                                break;

                            case UnitType.Item:
                                if (!itemList.Contains(unitAny))
                                {
                                    itemList.Add(unitAny);
                                }
                                break;

                            case UnitType.Object:
                                if (!objectList.Contains(unitAny))
                                {
                                    objectList.Add(unitAny);
                                }
                                break;

                            case UnitType.Player:
                                if (!playerList.TryGetValue(unitAny.UnitId, out var _) && unitAny.IsPlayer())
                                {
                                    playerList.Add(unitAny.UnitId, unitAny);
                                }
                                break;
                        }
                        unitAny = unitAny.ListNext(rosterData);
                    }
                }
            }
        }

        private static HashSet<Room> GetRooms(Room startingRoom, ref HashSet<Room> roomsList)
        {
            var roomsNear = startingRoom.RoomsNear;
            foreach (var roomNear in roomsNear)
            {
                if (!roomsList.Contains(roomNear))
                {
                    roomsList.Add(roomNear);
                    GetRooms(roomNear, ref roomsList);
                }
            }

            if (!roomsList.Contains(startingRoom.RoomNextFast))
            {
                roomsList.Add(startingRoom.RoomNextFast);
                GetRooms(startingRoom.RoomNextFast, ref roomsList);
            }

            return roomsList;
        }
    }
}
