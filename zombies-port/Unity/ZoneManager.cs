// ZoneManager.cs
//
// The scene's zone graph (maps/_zombiemode_zone_manager.gsc). Set it up like a BO1 map script:
//   Initial Zones       : manage_zones( init_zones ). Open from the start (Kino: foyer, foyer2).
//   Adjacencies         : add_adjacent_zone( a, b, flag ). The door or debris that sets the flag
//                         connects the two zones.
//   Flags Set At Start  : flags the map sets itself (Kino sets "always_on").
// The power switch sets "power_on", so links using it open with the power (Kino's theater).
//
// RoundDirector asks CanSpawnAt() before using a spawn point: zombies come only from opened zones
// that are occupied or connected to an occupied zone. Spawn points with no zone name aren't
// restricted.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombiesPort
{
    public class ZoneManager : MonoBehaviour
    {
        [Serializable]
        public class Adjacency
        {
            public string zoneA = "start_zone";
            public string zoneB = "room2_zone";
            public string flag = "door_room2";
            public bool oneWay;
        }

        [SerializeField] List<string> initialZones = new List<string> { "start_zone" };
        [SerializeField] List<Adjacency> adjacencies = new List<Adjacency>();
        [SerializeField] List<string> flagsSetAtStart = new List<string>();
        [Tooltip("manage_zones() re-checks occupancy every second.")]
        [SerializeField] float updateInterval = 1f;

        public ZoneGraph Graph { get; private set; }
        public event Action<string> FlagSet;

        ZoneVolume[] _volumes = new ZoneVolume[0];
        PowerSwitch _power;
        float _nextUpdate;

        void Awake()
        {
            Graph = new ZoneGraph();
            foreach (var z in initialZones) Graph.Enable(z);
            foreach (var a in adjacencies)
                if (a != null) Graph.AddAdjacentZone(a.zoneA, a.zoneB, a.flag, a.oneWay);
            foreach (var f in flagsSetAtStart) Graph.SetFlag(f);
        }

        void Start()
        {
            _volumes = FindObjectsByType<ZoneVolume>(FindObjectsSortMode.None);
            foreach (var v in _volumes) Graph.AddZone(v.ZoneName);

            _power = FindFirstObjectByType<PowerSwitch>();
            if (_power != null) _power.PoweredOn += OnPowerOn;
            Refresh();
        }

        void OnDestroy()
        {
            if (_power != null) _power.PoweredOn -= OnPowerOn;
        }

        void OnPowerOn() => SetFlag("power_on");

        void Update()
        {
            if (Time.time < _nextUpdate) return;
            _nextUpdate = Time.time + updateInterval;
            Refresh();
        }

        /// <summary>A door, debris pile or the power switch sets a flag.</summary>
        public void SetFlag(string flag)
        {
            if (Graph.IsFlagSet(flag)) return;
            Graph.SetFlag(flag);
            FlagSet?.Invoke(flag);
            Refresh(); // New rooms can spawn right away, not a second later.
        }

        void Refresh() => Graph.UpdateActivity(IsOccupied, initialZones.Count > 0 ? initialZones[0] : null);

        /// <summary>player_in_zone(): any non-spectating player inside one of the zone's boxes.</summary>
        bool IsOccupied(string zone)
        {
            var boot = GameBootstrap.Instance;
            if (boot == null) return false;
            foreach (var v in _volumes)
            {
                if (v == null || v.ZoneName != zone) continue;
                foreach (var p in boot.Players)
                    if (p != null && !p.IsSpectating && v.Contains(p.transform.position)) return true;
            }
            return false;
        }

        /// <summary>May zombies spawn from this spawn point right now?</summary>
        public bool CanSpawnAt(ZombieSpawnPoint point) =>
            point == null || string.IsNullOrEmpty(point.Zone) || Graph.CanSpawnIn(point.Zone);

        /// <summary>The zone a world position is in (first match), for debugging and gameplay hooks.</summary>
        public string ZoneAt(Vector3 position)
        {
            foreach (var v in _volumes)
                if (v != null && v.Contains(position)) return v.ZoneName;
            return null;
        }
    }
}
