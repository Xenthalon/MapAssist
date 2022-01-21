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
using System.Linq;

namespace MapAssist.Helpers
{
    public static class GameMemory
    {
        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();
        private static Dictionary<int, uint> _lastMapSeed = new Dictionary<int, uint>();
        private static int _currentProcessId;

        public static Dictionary<int, UnitAny> PlayerUnits = new Dictionary<int, UnitAny>();
        public static Dictionary<int, Dictionary<uint, UnitAny>> Corpses = new Dictionary<int, Dictionary<uint, UnitAny>>();

        private static bool _firstMemoryRead = true;

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
                var lastHoverData = processContext.Read<Structs.HoverData>(GameManager.LastHoverDataOffset);
                var lastNpcInteracted = (Npc)processContext.Read<ushort>(GameManager.InteractedNpcOffset);

                if (!menuData.InGame && Corpses.ContainsKey(_currentProcessId))
                {
                    Corpses[_currentProcessId].Clear();
                }

                var playerUnit = GameManager.PlayerUnit;

                if (!PlayerUnits.ContainsKey(_currentProcessId))
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
                    if (!_lastMapSeed.ContainsKey(_currentProcessId))
                    {
                        _lastMapSeed.Add(_currentProcessId, 0);
                    }
                    if (mapSeed != _lastMapSeed[_currentProcessId])
                    {
                        _lastMapSeed[_currentProcessId] = mapSeed;
                        //dispose leftover timers in this process if we started a new game
                        if (Items.ItemLogTimers.ContainsKey(_currentProcessId))
                        {
                            foreach (var timer in Items.ItemLogTimers[_currentProcessId])
                            {
                                if (timer != null)
                                {
                                    timer.Dispose();
                                }
                            }
                        }

                        if (!Items.ItemUnitHashesSeen.ContainsKey(_currentProcessId))
                        {
                            Items.ItemUnitHashesSeen.Add(_currentProcessId, new HashSet<string>());
                            Items.ItemUnitIdsSeen.Add(_currentProcessId, new HashSet<uint>());
                            Items.ItemUnitIdsToSkip.Add(_currentProcessId, new HashSet<uint>());
                            Items.ItemVendors.Add(_currentProcessId, new Dictionary<uint, Npc>());
                            Items.ItemLog.Add(_currentProcessId, new List<UnitAny>());
                        }
                        else
                        {
                            Items.ItemUnitHashesSeen[_currentProcessId].Clear();
                            Items.ItemUnitIdsSeen[_currentProcessId].Clear();
                            Items.ItemUnitIdsToSkip[_currentProcessId].Clear();
                            Items.ItemVendors[_currentProcessId].Clear();
                            Items.ItemLog[_currentProcessId].Clear();
                        }

                        if (!Corpses.ContainsKey(_currentProcessId))
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
                        var itemList = new HashSet<UnitAny>();
                        var objectList = new HashSet<UnitAny>();
                        var playerList = new Dictionary<uint, UnitAny>();
                        GetUnits(rosterData, ref monsterList, ref mercList, ref itemList, ref playerList, ref objectList);

                        if (lastHoverData.IsHovered)
                        {
                            var units = monsterList.Concat(mercList).Concat(itemList).Concat(playerList.Values).Concat(objectList).Where(x => x.UnitId == lastHoverData.UnitId).ToArray();
                            if (units.Length > 0) units[0].IsHovered = true;
                        }

                        foreach (var item in itemList)
                        {
                            if (!item.IsInStore()) continue;

                            if (Items.ItemVendors[_currentProcessId].TryGetValue(item.UnitId, out var vendor))
                            {
                                item.VendorOwner = vendor;
                            }
                            else
                            {
                                item.VendorOwner = !_firstMemoryRead ? lastNpcInteracted : Npc.Unknown; // This prevents marking the VendorOwner for all store items when restarting MapAssist in the middle of the game
                                Items.ItemVendors[_currentProcessId].Add(item.UnitId, item.VendorOwner);
                            }
                        }

                        foreach (var item in itemList.Where(item => item.IsPlayerHolding()))
                        {
                            if (!Items.ItemUnitIdsToSkip[_currentProcessId].Contains(item.UnitId))
                            {
                                Items.ItemUnitIdsToSkip[_currentProcessId].Add(item.UnitId);
                            }
                        }

                        _firstMemoryRead = false;

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
                            Items = itemList,
                            Objects = objectList,
                            Players = playerList,
                            Session = session,
                            Roster = rosterData,
                            PlayerUnit = playerUnit,
                            MenuOpen = menuData,
                            MenuPanelOpen = menuOpen,
                            LastNpcInteracted = lastNpcInteracted,
                            ProcessId = _currentProcessId
                        };
                    }
                }
            }

            GameManager.ResetPlayerUnit();
            return null;
        }

        private static void GetUnits(Roster rosterData, ref HashSet<UnitAny> monsterList, ref HashSet<UnitAny> mercList, ref HashSet<UnitAny> itemList, ref Dictionary<uint, UnitAny> playerList, ref HashSet<UnitAny> objectList)
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
                                else if (!monsterList.Contains(unitAny) && unitAny.IsMerc())
                                {
                                    mercList.Add(unitAny);
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
                                if (!playerList.ContainsKey(unitAny.UnitId) && unitAny.IsPlayer())
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

        private static void GetUnits(HashSet<Room> rooms, ref List<UnitAny> monsterList, ref List<UnitAny> itemList)
        {
            foreach (var room in rooms)
            {
                var unitAny = room.UnitFirst;
                while (unitAny.IsValidUnit())
                {
                    switch (unitAny.UnitType)
                    {
                        case UnitType.Monster:
                            if (!monsterList.Contains(unitAny) && unitAny.IsMonster())
                            {
                                monsterList.Add(unitAny);
                            }

                            break;

                        case UnitType.Item:
                            if (!itemList.Contains(unitAny))
                            {
                                itemList.Add(unitAny);
                            }
                            break;
                    }

                    unitAny = unitAny.RoomNext;
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
