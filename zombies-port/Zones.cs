// Zones.cs
//
// The zone graph, in plain C# (no engine types). Port of maps/_zombiemode_zone_manager.gsc:
//   add_adjacent_zone( a, b, flag ) : a and b connect when the flag is set (a door or debris
//                                     sets it when bought). Adding the same pair again with
//                                     another flag means EITHER flag connects them.
//   zone_flag_wait()                : setting a flag enables both zones and connects them
//   manage_zones() (every second)   : a zone is ACTIVE if a player is in it, or if it's
//                                     enabled and connected to an occupied zone. If nobody is in
//                                     any zone, the first initial zone counts as active.
//   create_spawner_list()           : zombies spawn only in zones that are enabled and active
//
// So zombies come from the room you're in and the opened rooms next to it, never from the far
// side of the map, and never from rooms nobody has opened.

using System;
using System.Collections.Generic;

namespace ZombiesPort
{
    public class ZoneGraph
    {
        class Adjacency
        {
            public string To;
            public readonly List<string> Flags = new List<string>();
            public bool AnyFlag;     // flags_do_or_check: true once the pair was added twice
            public bool Connected;
        }

        class Zone
        {
            public bool Enabled, Occupied, Active;
            public readonly Dictionary<string, Adjacency> Adjacent = new Dictionary<string, Adjacency>();
        }

        readonly Dictionary<string, Zone> _zones = new Dictionary<string, Zone>();
        readonly HashSet<string> _flags = new HashSet<string>();

        public event Action<string> ZoneEnabled;

        public IEnumerable<string> ZoneNames => _zones.Keys;

        /// <summary>zone_init(): register a zone (disabled until opened).</summary>
        public void AddZone(string name)
        {
            if (!string.IsNullOrEmpty(name) && !_zones.ContainsKey(name)) _zones[name] = new Zone();
        }

        /// <summary>enable_zone(): the zone is open (its spawners and windows can be used).</summary>
        public void Enable(string name)
        {
            AddZone(name);
            var z = _zones[name];
            if (z.Enabled) return;
            z.Enabled = true;
            ZoneEnabled?.Invoke(name);
        }

        /// <summary>add_adjacent_zone( a, b, flag, one_way ).</summary>
        public void AddAdjacentZone(string a, string b, string flag, bool oneWay = false)
        {
            AddZone(a);
            AddZone(b);
            MakeAdjacent(a, b, flag);
            if (!oneWay) MakeAdjacent(b, a, flag);
            if (_flags.Contains(flag)) Reevaluate(); // Flag already set before the link was added.
        }

        void MakeAdjacent(string from, string to, string flag)
        {
            var zone = _zones[from];
            if (!zone.Adjacent.TryGetValue(to, out var adj))
            {
                adj = new Adjacency { To = to };
                zone.Adjacent[to] = adj;
            }
            else
            {
                adj.AnyFlag = true; // make_zone_adjacent(): a second flag on the same pair is an OR
            }
            adj.Flags.Add(flag);
        }

        /// <summary>connect_zones(): connect and enable both straight away.</summary>
        public void ConnectZones(string a, string b, bool oneWay = false)
        {
            Enable(a);
            Enable(b);
            Connect(a, b);
            if (!oneWay) Connect(b, a);
        }

        void Connect(string from, string to)
        {
            var zone = _zones[from];
            if (!zone.Adjacent.TryGetValue(to, out var adj))
            {
                adj = new Adjacency { To = to };
                zone.Adjacent[to] = adj;
            }
            adj.Connected = true;
        }

        public bool IsFlagSet(string flag) => _flags.Contains(flag);

        /// <summary>flag_set() + zone_flag_wait(): a door or debris was bought (or the power came on).</summary>
        public void SetFlag(string flag)
        {
            if (string.IsNullOrEmpty(flag) || !_flags.Add(flag)) return;
            Reevaluate();
        }

        void Reevaluate()
        {
            foreach (var kv in _zones)
            {
                foreach (var adj in kv.Value.Adjacent.Values)
                {
                    if (adj.Connected) continue;
                    bool open;
                    if (adj.AnyFlag)
                    {
                        open = false;
                        foreach (var f in adj.Flags) if (_flags.Contains(f)) { open = true; break; }
                    }
                    else
                    {
                        open = true;
                        foreach (var f in adj.Flags) if (!_flags.Contains(f)) { open = false; break; }
                    }
                    if (!open) continue;
                    Enable(kv.Key);
                    adj.Connected = true;
                    Enable(adj.To);
                }
            }
        }

        /// <summary>
        /// The manage_zones() loop body. occupied(zone) says whether a (non-spectating) player is in
        /// that zone. Run it about once a second.
        /// </summary>
        public void UpdateActivity(Func<string, bool> occupied, string initialZone)
        {
            foreach (var z in _zones.Values) { z.Active = false; z.Occupied = false; }

            bool anyActive = false;
            foreach (var kv in _zones)
            {
                var zone = kv.Value;
                if (!zone.Enabled) continue;
                zone.Occupied = occupied != null && occupied(kv.Key);
                if (!zone.Occupied) continue;

                zone.Active = true;
                anyActive = true;
                foreach (var adj in zone.Adjacent.Values)
                    if (adj.Connected && _zones[adj.To].Enabled) _zones[adj.To].Active = true;
            }

            if (!anyActive && initialZone != null && _zones.TryGetValue(initialZone, out var start))
            {
                start.Active = true;
                start.Occupied = true;
            }
        }

        public bool IsEnabled(string zone) => zone != null && _zones.TryGetValue(zone, out var z) && z.Enabled;
        public bool IsActive(string zone) => zone != null && _zones.TryGetValue(zone, out var z) && z.Active;
        public bool IsOccupied(string zone) => zone != null && _zones.TryGetValue(zone, out var z) && z.Occupied;

        /// <summary>create_spawner_list(): may zombies spawn from this zone right now?</summary>
        public bool CanSpawnIn(string zone) => IsEnabled(zone) && IsActive(zone);
    }

    public enum DoorKind
    {
        Buyable,          // "default": pay to open
        Electric,         // "electric_door": opens by itself, free, when the power comes on
        ElectricBuyable,  // "electric_buyable_door": needs the power on, then pay
    }

    public static class DoorRules
    {
        public const int DefaultCost = 1000;      // door_init(): cost = 1000 unless zombie_cost is set
        public const float OpenSeconds = 1f;      // door_activate(): time = 1 by default

        /// <summary>
        /// door_buy() / debris_think(): the buyer pays from their own points (BO1's team point
        /// pool never held anything). Returns whether the door can be bought right now.
        /// </summary>
        public static bool CanBuy(DoorKind kind, bool powered, bool isOpen, int score, int cost)
        {
            if (isOpen || kind == DoorKind.Electric) return false;
            if (kind == DoorKind.ElectricBuyable && !powered) return false;
            return score >= cost;
        }

        /// <summary>A door's script_flag can name several flags separated by commas.</summary>
        public static string[] ParseFlags(string flags)
        {
            if (string.IsNullOrEmpty(flags)) return new string[0];
            var parts = flags.Split(',');
            var list = new List<string>(parts.Length);
            foreach (var p in parts)
            {
                var t = p.Trim();
                if (t.Length > 0) list.Add(t);
            }
            return list.ToArray();
        }
    }
}
