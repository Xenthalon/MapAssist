using System;
using System.Collections.Generic;
using GameOverlay.Drawing;
using MapAssist.Helpers;
using MapAssist.Structs;
using static MapAssist.Types.Stats;

namespace MapAssist.Types
{
    public class UnitAny2
    {
        public IntPtr PtrUnit { get; private set; }
        public Structs.UnitAny Struct { get; private set; }
        public UnitType UnitType => Struct.UnitType;
        public uint UnitId => Struct.UnitId;
        public uint TxtFileNo => Struct.TxtFileNo;
        public Area Area { get; private set; }
        public Point Position => new Point(X, Y);
        public ushort X => IsMovable ? (ushort)Path.DynamicX : Path.StaticX;
        public ushort Y => IsMovable ? (ushort)Path.DynamicY : Path.StaticY;
        public StatListStruct StatsStruct { get; private set; }
        public Dictionary<Stat, Dictionary<ushort, int>> StatLayers { get; private set; }
        public Dictionary<Stat, int> Stats { get; private set; }
        protected uint[] StateFlags { get; set; }
        public DateTime FoundTime { get; set; } = DateTime.Now;
        public bool IsHovered { get; set; } = false;
        public bool IsCached { get; set; } = false;
        private Path Path { get; set; }

        private Inventory _inventory;

        public UnitAny2(IntPtr ptrUnit)
        {
            PtrUnit = ptrUnit;

            if (IsValidPointer)
            {
                using (var processContext = GameManager.GetProcessContext())
                {
                    Struct = processContext.Read<Structs.UnitAny>(PtrUnit);
                    Path = new Path(Struct.pPath);
                }
            }
        }

        public void CopyFrom(UnitAny2 other)
        {
            if (Struct.UnitId != other.Struct.UnitId) IsCached = false; // Reload all data since the unit id changed

            PtrUnit = other.PtrUnit;
            Struct = other.Struct;
            Path = other.Path;
        }

        protected bool Update()
        {
            if (IsValidPointer)
            {
                using (var processContext = GameManager.GetProcessContext())
                {
                    var newStruct = processContext.Read<Structs.UnitAny>(PtrUnit);

                    if (newStruct.UnitId == uint.MaxValue) return false;
                    else Struct = newStruct;

                    if (IsCached) return false;

                    if (IsValidUnit)
                    {
                        Area = Path.Room.RoomEx.Level.LevelId;

                        if (Struct.pStatsListEx != IntPtr.Zero)
                        {
                            var stats = new Dictionary<Stat, int>();
                            var statLayers = new Dictionary<Stat, Dictionary<ushort, int>>();

                            StatsStruct = processContext.Read<StatListStruct>(Struct.pStatsListEx);
                            StateFlags = StatsStruct.StateFlags;

                            var statValues = processContext.Read<StatValue>(StatsStruct.Stats.pFirstStat, Convert.ToInt32(StatsStruct.Stats.Size));
                            foreach (var stat in statValues)
                            {
                                if (statLayers.ContainsKey(stat.Stat))
                                {
                                    if (stat.Layer == 0) continue;
                                    if (!statLayers[stat.Stat].ContainsKey(stat.Layer))
                                    {
                                        statLayers[stat.Stat].Add(stat.Layer, stat.Value);
                                    }
                                }
                                else
                                {
                                    stats.Add(stat.Stat, stat.Value);
                                    statLayers.Add(stat.Stat, new Dictionary<ushort, int>() { { stat.Layer, stat.Value } });
                                }
                            }

                            Stats = stats;
                            StatLayers = statLayers;
                        }

                        if (GameMemory.cache.ContainsKey(UnitId)) IsCached = true;

                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsMovable => !(Struct.UnitType == UnitType.Object || Struct.UnitType == UnitType.Item);

        public bool IsValidPointer => PtrUnit != IntPtr.Zero;

        public bool IsValidUnit => Struct.pUnitData != IntPtr.Zero && Struct.pPath != IntPtr.Zero && Struct.UnitType <= UnitType.Tile;

        public bool IsPlayer => Struct.UnitType == UnitType.Player && Struct.pAct != IntPtr.Zero;

        public bool IsPlayerOwned => IsMerc && Stats.ContainsKey(Stat.Strength); // This is ugly, but seems to work.

        public bool IsMonster
        {
            get
            {
                if (Struct.UnitType != UnitType.Monster) return false;
                if (Struct.Mode == 0 || Struct.Mode == 12) return false;
                if (NPC.Dummies.ContainsKey(TxtFileNo)) { return false; }

                return true;
            }
        }
        // public bool IsMerc()
        // {
        //     if (_updated)
        //     {
        //         return _isMerc;
        //     }
        //     else
        //     {
        //         if (_unitAny.UnitType != UnitType.Monster) return false;
        //         if (_unitAny.Mode == 0 || _unitAny.Mode == 12) return false;
        //         if (NPC.Mercs.TryGetValue(_unitAny.TxtFileNo, out var _) &&
        //             Stats.TryGetValue(Stat.STAT_STRENGTH, out var _)) { // workaround, only real mercs have this
        //             _isMerc = true;
        //             return true;
        //         }

        //         return false;
        //     }
        // }
        public bool IsTownNpc
        {
            get
            {
                if (Struct.UnitType != UnitType.Monster) return false;
                if (Struct.Mode == 0 || Struct.Mode == 12) return false;
                if (NPC.NPCs.ContainsKey(TxtFileNo)) { return true; }

                return false;
            }
        }

        public List<UnitItem> GetNpcInventory()
        {
            var result = new List<UnitItem>();

            if (!IsValidPointer || !IsTownNpc)
                return result;

            using (var processContext = GameManager.GetProcessContext())
            {
                _inventory = processContext.Read<Inventory>(Struct.pInventory);

                var firstItem = new UnitItem(_inventory.pFirstItem);
                firstItem.Update();
                result.Add(firstItem);
                var nextItemPtr = firstItem.ItemData.pNextItem;

                while (nextItemPtr != IntPtr.Zero)
                {
                    var nextItem = new UnitItem(nextItemPtr);
                    nextItem.Update();
                    result.Add(nextItem);
                    nextItemPtr = nextItem.ItemData.pNextItem;
                }
            }

            return result;
        }

        public bool IsMerc => new List<Npc> { Npc.Rogue2, Npc.Guard, Npc.IronWolf, Npc.Act5Hireling2Hand }.Contains((Npc)TxtFileNo) &&
                                Stats.TryGetValue(Stat.Strength, out var _);

        public bool IsCorpse => Struct.isCorpse && UnitId != GameMemory.PlayerUnit.UnitId && Area != Area.None;
    }
}
