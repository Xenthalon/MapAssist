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
using MapAssist.Helpers;
using MapAssist.Interfaces;
using MapAssist.Settings;
using MapAssist.Structs;
using System;
using System.Collections.Generic;
using System.Text;

namespace MapAssist.Types
{
    public class UnitAny : IUpdatable<UnitAny>
    {
        private readonly IntPtr _pUnit;
        private Structs.UnitAny _unitAny;
        private Act _act;
        private Path _path;
        private Inventory _inventory;
        private MonsterData _monsterData;
        private MonStats _monsterStats;
        private ItemData _itemData;
        private ObjectData _objectData;
        private ObjectTxt _objectTxt;
        private Dictionary<Stat, int> _statList;
        private Dictionary<Stat, Dictionary<ushort, int>> _itemStatList;
        private List<Resist> _immunities;
        private uint[] _stateFlags;
        private List<State> _stateList;
        private string _name;
        private bool _isMonster;
        private bool _updated;
        private Skills _skills;
        private bool _isPlayerUnit;
        private Roster _rosterData;
        private bool _hostileToPlayer;
        private bool _inPlayerParty;
        private PlayerClass _playerClass;
        private Area _initialArea;
        public Npc VendorOwner { get; set; } = Npc.Invalid;
        public bool IsHovered { get; set; } = false;

        public UnitAny(IntPtr pUnit)
        {
            _pUnit = pUnit;
            Update();
        }

        public UnitAny(IntPtr pUnit, Roster rosterData)
        {
            _pUnit = pUnit;
            Update(rosterData);
        }

        public UnitAny Update()
        {
            return Update(null);
        }

        public UnitAny Update(Roster rosterData = null)
        {
            if (IsValidPointer())
            {
                using (var processContext = GameManager.GetProcessContext())
                {
                    _unitAny = processContext.Read<Structs.UnitAny>(_pUnit);
                    if (IsValidUnit())
                    {
                        _path = new Path(_unitAny.pPath);
                        if (_unitAny.pStatsListEx != IntPtr.Zero)
                        {
                            var statListStruct = processContext.Read<StatListStruct>(_unitAny.pStatsListEx);
                            var statList = new Dictionary<Stat, int>();
                            var statValues = processContext.Read<StatValue>(statListStruct.Stats.FirstStatPtr, Convert.ToInt32(statListStruct.Stats.Size));
                            foreach (var stat in statValues)
                            {
                                //ensure we dont add duplicates
                                if (!statList.TryGetValue(stat.Stat, out var _))
                                {
                                    statList.Add(stat.Stat, stat.Value);
                                }
                            }

                            _statList = statList;
                            _immunities = GetImmunities();
                            _stateFlags = statListStruct.StateFlags;
                        }
                        switch (_unitAny.UnitType)
                        {
                            case UnitType.Player:
                                if (IsPlayer())
                                {
                                    _name = Encoding.ASCII.GetString(processContext.Read<byte>(_unitAny.pUnitData, 16))
                                        .TrimEnd((char)0);
                                    _inventory = processContext.Read<Inventory>(_unitAny.pInventory);
                                    _act = new Act(_unitAny.pAct);
                                    _rosterData = rosterData;
                                    _playerClass = _unitAny.playerClass;
                                    _initialArea = Path.Room.RoomEx.Level.LevelId;
                                    if (IsPlayerUnit())
                                    {
                                        _skills = new Skills(_unitAny.pSkills);
                                        _stateList = GetStateList();
                                    }
                                    else
                                    {
                                        if (GameManager.PlayerFound && _rosterData != null)
                                        {
                                            if (PartyId == ushort.MaxValue)
                                            {
                                                _hostileToPlayer = IsHostileTo(GameManager.PlayerUnit);
                                                _inPlayerParty = false;
                                            }
                                            else
                                            {
                                                if (PartyId != GameManager.PlayerUnit.PartyId)
                                                {
                                                    _hostileToPlayer = IsHostileTo(GameManager.PlayerUnit);
                                                    _inPlayerParty = false;
                                                }
                                                else
                                                {
                                                    _inPlayerParty = true;
                                                }
                                            }
                                        }
                                    }
                                }
                                break;

                            case UnitType.Monster:
                                if (IsMonster())
                                {
                                    _monsterData = processContext.Read<MonsterData>(_unitAny.pUnitData);
                                    _monsterStats = processContext.Read<MonStats>(_monsterData.pMonStats);
                                }
                                break;

                            case UnitType.Item:
                                if (MapAssistConfiguration.Loaded.ItemLog.Enabled)
                                {
                                    _itemData = processContext.Read<ItemData>(_unitAny.pUnitData);

                                    if (_unitAny.pStatsListEx != IntPtr.Zero)
                                    {
                                        var itemStatList = new Dictionary<Stat, Dictionary<ushort, int>>();

                                        var statListStruct = processContext.Read<StatListStruct>(_unitAny.pStatsListEx);
                                        var statValues = processContext.Read<StatValue>(statListStruct.Stats.FirstStatPtr, Convert.ToInt32(statListStruct.Stats.Size));
                                        foreach (var stat in statValues)
                                        {
                                            if (itemStatList.ContainsKey(stat.Stat))
                                            {
                                                if (stat.Layer == 0) continue;
                                                if (!itemStatList[stat.Stat].ContainsKey(stat.Layer))
                                                {
                                                    itemStatList[stat.Stat].Add(stat.Layer, stat.Value);
                                                }
                                            }
                                            else
                                            {
                                                itemStatList.Add(stat.Stat, new Dictionary<ushort, int>() { { stat.Layer, stat.Value } });
                                            }
                                        }

                                        _itemStatList = itemStatList;
                                    }

                                    if ((IsDropped() && !IsIdentified()) || IsInStore())
                                    {
                                        Items.LogItem(this, processContext.ProcessId);
                                    }
                                }
                                break;

                            case UnitType.Object:
                                _objectData = processContext.Read<ObjectData>(_unitAny.pUnitData);
                                if (_objectData.pObjectTxt != IntPtr.Zero)
                                {
                                    _objectTxt = processContext.Read<ObjectTxt>(_objectData.pObjectTxt);
                                }
                                break;
                        }
                        _updated = true;
                    }
                    else
                    {
                        return default(UnitAny);
                    }
                }
            }

            return this;
        }

        public string Name => _name;
        public UnitType UnitType => _unitAny.UnitType;
        public uint TxtFileNo => _unitAny.TxtFileNo;
        public uint UnitId => _unitAny.UnitId;
        public ItemMode Mode => (ItemMode)_unitAny.Mode;
        public IntPtr UnitDataPtr => _unitAny.pUnitData;
        public Dictionary<Stat, int> Stats => _statList;
        public Dictionary<Stat, Dictionary<ushort, int>> ItemStats => _itemStatList;
        public MonsterData MonsterData => _monsterData;
        public MonStats MonsterStats => _monsterStats;
        public ItemData ItemData => _itemData;
        public ObjectData ObjectData => _objectData;
        public ObjectTxt ObjectTxt => _objectTxt;
        public Act Act => _act;
        public Path Path => _path;
        public IntPtr StatsListExPtr => _unitAny.pStatsListEx;
        public Inventory Inventory => _inventory;
        public ushort X => IsMovable() ? _path.DynamicX : _path.StaticX;
        public ushort Y => IsMovable() ? _path.DynamicY : _path.StaticY;
        public Point Position => new Point(X, Y);

        public UnitAny ListNext(Roster rosterData) => new UnitAny(_unitAny.pListNext, rosterData);

        public UnitAny RoomNext => new UnitAny(_unitAny.pRoomNext);
        public List<Resist> Immunities => _immunities;
        public uint[] StateFlags => _stateFlags;
        public List<State> StateList => _stateList;
        public Skills Skills => _skills;
        public ushort PartyId => GetPartyId();
        public bool HostileToPlayer => _hostileToPlayer;
        public bool InPlayerParty => _inPlayerParty;
        public bool IsCorpse => _unitAny.isCorpse;
        public PlayerClass PlayerClass => _playerClass;
        public Area InitialArea => _initialArea;

        public bool IsMovable()
        {
            return !(UnitType == UnitType.Object || UnitType == UnitType.Item);
        }

        public bool IsValidPointer()
        {
            return _pUnit != IntPtr.Zero;
        }

        public bool IsValidUnit()
        {
            return _unitAny.pUnitData != IntPtr.Zero && _unitAny.pPath != IntPtr.Zero && _unitAny.UnitType <= UnitType.Tile;
        }

        public bool IsPlayer()
        {
            return UnitType == UnitType.Player && _unitAny.pAct != IntPtr.Zero;
        }

        public bool IsMerc()
        {
            return new List<Npc> { Npc.Rogue2, Npc.Guard, Npc.IronWolf, Npc.Act5Hireling2Hand }.Contains((Npc)TxtFileNo);
        }

        public bool IsPlayerOwned()
        {
            return IsMerc() && Stats.ContainsKey(Stat.Strength); // This is ugly, but seems to work.
        }

        public bool IsPlayerUnit()
        {
            if (_isPlayerUnit)
            {
                return true;
            }
            else
            {
                using (var processContext = GameManager.GetProcessContext())
                {
                    var processId = processContext.ProcessId;
                    if (GameMemory.PlayerUnits.TryGetValue(processId, out var playerUnit))
                    {
                        if (!Equals(playerUnit, default(UnitAny)))
                        {
                            if (playerUnit.UnitId != UnitId)
                            {
                                return false;
                            }
                            else
                            {
                                if (!Equals(playerUnit, default(UnitAny)))
                                {
                                    _isPlayerUnit = true;
                                    return true;
                                }
                            }
                        }
                    }
                    if (IsPlayer() && _unitAny.pInventory != IntPtr.Zero)
                    {
                        var playerInfoPtr = processContext.Read<PlayerInfo>(GameManager.ExpansionCheckOffset);
                        var playerInfo = processContext.Read<PlayerInfoStrc>(playerInfoPtr.pPlayerInfo);
                        var expansionCharacter = playerInfo.Expansion;
                        var userBaseOffset = 0x30;
                        var checkUser1 = 1;
                        if (expansionCharacter)
                        {
                            userBaseOffset = 0x70;
                            checkUser1 = 0;
                        }

                        var userBaseCheck = processContext.Read<int>(IntPtr.Add(_unitAny.pInventory, userBaseOffset));
                        if (userBaseCheck != checkUser1)
                        {
                            _isPlayerUnit = true;
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public bool IsPortal()
        {
            var castedType = (GameObject)TxtFileNo;
            var name = Enum.GetName(typeof(GameObject), TxtFileNo);
            return ((!string.IsNullOrWhiteSpace(name) && name.Contains("Portal") &&
                     castedType != GameObject.WaypointPortal) || castedType == GameObject.HellGate);
        }

        public bool IsWaypoint()
        {
            var castedType = (GameObject)TxtFileNo;
            return castedType.IsWaypoint();
        }

        public bool IsShrine()
        {
            return UnitType == UnitType.Object && _objectData.pShrineTxt != IntPtr.Zero &&
                _objectData.InteractType <= (byte)ShrineType.Poison;
        }

        public bool IsWell()
        {
            return UnitType == UnitType.Object && _objectData.pObjectTxt != IntPtr.Zero &&
                _objectTxt.ObjectType == "Well";
        }

        public bool IsChest()
        {
            return UnitType == UnitType.Object && _objectData.pObjectTxt != IntPtr.Zero && _unitAny.Mode == 0 &&
                Chest.NormalChests.Contains((GameObject)_objectTxt.Id);
        }

        public bool IsMonster()
        {
            if (_updated)
            {
                return _isMonster;
            }
            else
            {
                if (_unitAny.UnitType != UnitType.Monster) return false;
                if (_unitAny.Mode == 0 || _unitAny.Mode == 12) return false;
                if (NPC.Dummies.TryGetValue(_unitAny.TxtFileNo, out var _)) { return false; }

                _isMonster = true;
                return true;
            }
        }

        public bool IsIdentified()
        {
            return ItemData.ItemQuality >= ItemQuality.MAGIC && (ItemData.ItemFlags & ItemFlags.IFLAG_IDENTIFIED) == ItemFlags.IFLAG_IDENTIFIED;
        }

        public bool IsDropped()
        {
            return ItemModeMapped() == Types.ItemModeMapped.Ground;
        }

        public bool IsInStore()
        {
            return ItemModeMapped() == Types.ItemModeMapped.Vendor;
        }

        public bool IsPlayerHolding()
        {
            switch (ItemModeMapped())
            {
                case Types.ItemModeMapped.Belt:
                case Types.ItemModeMapped.Inventory:
                case Types.ItemModeMapped.Cube:
                case Types.ItemModeMapped.Stash:
                case Types.ItemModeMapped.Player:
                case Types.ItemModeMapped.Mercenary:
                    return true;
            }
            return false;
        }

        public ItemModeMapped ItemModeMapped()
        {
            switch (Mode)
            {
                case ItemMode.INBELT: return Types.ItemModeMapped.Belt;
                case ItemMode.DROPPING: return Types.ItemModeMapped.Ground;
                case ItemMode.ONGROUND: return Types.ItemModeMapped.Ground;
                case ItemMode.SOCKETED: return Types.ItemModeMapped.Socket;
                case ItemMode.EQUIP:
                    if (ItemData.dwOwnerID != uint.MaxValue) return Types.ItemModeMapped.Player;
                    else return Types.ItemModeMapped.Mercenary;
            }

            if (ItemData.dwOwnerID == uint.MaxValue && (ItemData.ItemFlags & ItemFlags.IFLAG_INSTORE) == ItemFlags.IFLAG_INSTORE) return Types.ItemModeMapped.Vendor;
            if (ItemData.dwOwnerID != uint.MaxValue && ItemData.InvPage == InvPage.EQUIP) return Types.ItemModeMapped.Trade; // Other player's trade window

            switch (ItemData.InvPage)
            {
                case InvPage.INVENTORY: return Types.ItemModeMapped.Inventory;
                case InvPage.TRADE: return Types.ItemModeMapped.Trade;
                case InvPage.CUBE: return Types.ItemModeMapped.Cube;
                case InvPage.STASH: return Types.ItemModeMapped.Stash;
            }

            return Types.ItemModeMapped.Unknown; // Items that appeared in the trade window before will appear here
        }

        public string ItemHash()
        {
            return Items.ItemName(TxtFileNo) + "/" + Position.X + "/" + Position.Y;
        }

        private List<Resist> GetImmunities()
        {
            _statList.TryGetValue(Stat.DamageReduced, out var resistanceDamage);
            _statList.TryGetValue(Stat.MagicResist, out var resistanceMagic);
            _statList.TryGetValue(Stat.FireResist, out var resistanceFire);
            _statList.TryGetValue(Stat.LightningResist, out var resistanceLightning);
            _statList.TryGetValue(Stat.ColdResist, out var resistanceCold);
            _statList.TryGetValue(Stat.PoisonResist, out var resistancePoison);

            var resists = new List<int>
            {
                resistanceDamage,
                resistanceMagic,
                resistanceFire,
                resistanceLightning,
                resistanceCold,
                resistancePoison
            };
            var immunities = new List<Resist>();

            for (var i = 0; i < 6; i++)
            {
                if (resists[i] >= 100)
                {
                    immunities.Add((Resist)i);
                }
            }

            return immunities;
        }

        public bool GetState(State state)
        {
            return (StateFlags[(int)state >> 5] & StateMasks.gdwBitMasks[(int)state & 31]) > 0;
        }

        private List<State> GetStateList()
        {
            var stateList = new List<State>();
            for (var i = 0; i <= States.StateCount; i++)
            {
                if (GetState((State)i))
                {
                    stateList.Add((State)i);
                }
            }
            return stateList;
        }

        public bool IsHostileTo(UnitAny otherUnit)
        {
            if (UnitType != UnitType.Player || otherUnit.UnitType != UnitType.Player)
            {
                return false;
            }
            var otherUnitId = otherUnit.UnitId;
            if (otherUnitId == UnitId)
            {
                return false;
            }
            if (_rosterData != null)
            {
                using (var processContext = GameManager.GetProcessContext())
                {
                    if (_rosterData.EntriesByUnitId.TryGetValue(UnitId, out var rosterEntry))
                    {
                        var hostileInfo = rosterEntry.HostileInfo;
                        while (hostileInfo.NextHostileInfo != IntPtr.Zero)
                        {
                            if (hostileInfo.UnitId == otherUnitId)
                            {
                                return hostileInfo.HostileFlag > 0;
                            }
                            hostileInfo = processContext.Read<HostileInfo>(hostileInfo.NextHostileInfo);
                        }
                        if (hostileInfo.UnitId == otherUnitId)
                        {
                            return hostileInfo.HostileFlag > 0;
                        }
                    }
                }
            }
            return false;
        }

        private ushort GetPartyId()
        {
            if (_rosterData != null)
            {
                if (_rosterData.EntriesByUnitId.TryGetValue(UnitId, out var rosterEntry))
                {
                    return rosterEntry.PartyID;
                }
            }
            return ushort.MaxValue; //maxvalue = not in party
        }

        public double DistanceTo(Point position)
        {
            return Math.Sqrt((Math.Pow(position.X - Position.X, 2) + Math.Pow(position.Y - Position.Y, 2)));
        }

        public float GetHealthPercentage()
        {
            if (_statList.TryGetValue(Stat.Life, out var health) &&
                _statList.TryGetValue(Stat.MaxLife, out var maxHp) && maxHp > 0)
            {
                return (float)health / maxHp;
            }
            return 0.0f;
        }

        public override bool Equals(object obj) => obj is UnitAny other && Equals(other);

        public bool Equals(UnitAny unit) => !(unit is null) && UnitId == unit.UnitId;

        public override int GetHashCode() => UnitId.GetHashCode();

        public static bool operator ==(UnitAny unit1, UnitAny unit2) => (unit1 is null && unit2 is null) || (!(unit1 is null) && unit1.Equals(unit2));

        public static bool operator !=(UnitAny unit1, UnitAny unit2) => !(unit1 == unit2);

        public UnitAny Clone()
        {
            var unitAny = new UnitAny(_pUnit);
            return unitAny;
        }
    }
}
