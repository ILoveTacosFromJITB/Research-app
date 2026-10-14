// RoundDirector.cs
//
// Runs the round loop. Ports from maps/_zombiemode.gsc:
//   round_start()     : wait 2 s after load, then start round 1
//   round_think()     : the endless round loop
//   round_spawning()  : spawn one zombie every SpawnDelay while under 24 alive
//   round_wait()      : the round ends when nothing is alive and nothing is left to spawn
//   chalk_round_over(): the 10-second break between rounds
//   zombie_speed_up() : the last zombie sprints on rounds 4-9
//
// The GSC polled with wait() loops on several "threads". Here one coroutine runs the round,
// one runs spawning, and zombies report their own deaths, so nothing checks state every frame.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZombiesPort
{
    public class RoundDirector : MonoBehaviour
    {
        [SerializeField] ZombiePool pool;
        [Tooltip("Leave empty to use every ZombieSpawnPoint in the scene.")]
        [SerializeField] List<ZombieSpawnPoint> spawnPoints = new List<ZombieSpawnPoint>();
        [Tooltip("round_start(): wait( 2 ) before round 1.")]
        [SerializeField] float roundStartDelay = 2f;
        [Tooltip("Only spawn at points within this distance of a player (meters). 0 = any active point. " +
                 "With a ZoneManager in the scene you usually leave this at 0: zones decide.")]
        [SerializeField] float maxSpawnDistance = 0f;

        /// <summary>A new round began (number). The chalk HUD shows it.</summary>
        public event Action<int> RoundStarted;
        /// <summary>A round was cleared (number, length of the break in seconds).</summary>
        public event Action<int, float> RoundEnded;
        public event Action<ZombieAgent, ZombiePlayer> ZombieKilled;
        /// <summary>A zombie swung at a player. Hook the player-health / last-stand system here.</summary>
        public event Action<ZombieAgent, ZombiePlayer> PlayerAttacked;

        public ZombieGameState State => GameBootstrap.Instance.State;
        public int AliveCount => _alive.Count;
        public bool IsBetweenRounds { get; private set; } = true;
        public bool IsHalted { get; private set; }

        readonly List<ZombieAgent> _alive = new List<ZombieAgent>();
        readonly List<ZombieSpawnPoint> _candidates = new List<ZombieSpawnPoint>();
        static readonly WaitForSeconds Wait01 = new WaitForSeconds(0.1f);
        static readonly WaitForSeconds Wait05 = new WaitForSeconds(0.5f);
        ZombieSpawnPoint _lastSpawn;
        ZoneManager _zones;
        bool _speedUpArmed;
        bool _warnedNoSpawns;

        IEnumerator Start()
        {
            if (GameBootstrap.Instance == null)
            {
                Debug.LogError("RoundDirector: add a GameBootstrap to the scene.", this);
                yield break;
            }
            if (pool == null) pool = FindFirstObjectByType<ZombiePool>();
            _zones = FindFirstObjectByType<ZoneManager>();
            if (spawnPoints.Count == 0)
                spawnPoints.AddRange(FindObjectsByType<ZombieSpawnPoint>(FindObjectsSortMode.None));

            yield return pool.Prewarm();
            if (!pool.IsReady) yield break;

            yield return new WaitForSeconds(roundStartDelay + State.Vars.GameStartDelay);

            while (!State.Intermission)
                yield return RoundRoutine();
        }

        /// <summary>One pass of round_think().</summary>
        IEnumerator RoundRoutine()
        {
            var state = State;
            state.BeginRound();
            _speedUpArmed = false;
            IsBetweenRounds = false;
            RoundStarted?.Invoke(state.RoundNumber);

            Coroutine spawning = StartCoroutine(SpawnRoutine());

            // round_wait(): GSC waited 1 s, then checked once a second.
            yield return new WaitForSeconds(1f);
            while (!state.IsRoundOver(AliveCount))
            {
                if (state.Intermission) yield break;
                SpeedUpLastZombie(state);
                yield return Wait05;
            }

            StopCoroutine(spawning);
            IsBetweenRounds = true;
            float breakSeconds = state.Vars.BetweenRoundTime;
            RoundEnded?.Invoke(state.RoundNumber, breakSeconds);

            // chalk_round_over(): the tally blinks for the length of the break.
            yield return new WaitForSeconds(breakSeconds);
            state.EndRound();
        }

        /// <summary>round_spawning(): the spawn loop for one round.</summary>
        IEnumerator SpawnRoutine()
        {
            var state = State;
            while (true)
            {
                // Wait for a free slot (24 alive), a queued zombie, and a free pooled body
                // (a corpse still sinking holds its slot for a moment).
                while (AliveCount >= state.Vars.AiLimit || state.ZombieTotal <= 0 || pool.FreeCount == 0)
                    yield return Wait01;

                var point = PickSpawnPoint();
                if (point == null)
                {
                    if (!_warnedNoSpawns)
                    {
                        Debug.LogWarning("RoundDirector: no active spawn points. Activate a ZombieSpawnPoint " +
                                         "near the player (GSC asserted: \"No active spawners in the map\").", this);
                        _warnedNoSpawns = true;
                    }
                    yield return Wait05;
                    continue;
                }

                if (!state.TryTakeSpawnTicket()) continue;
                var z = pool.Take(point.transform.position, point.transform.rotation);
                if (z == null) { state.ReturnSpawnTicket(); continue; }

                z.Activate(this, state.ZombieHealth, state.RollSpeed(), point.Window);
                _alive.Add(z);

                yield return new WaitForSeconds(state.SpawnDelay);
            }
        }

        /// <summary>
        /// Random active spawn point. Like GSC, if it rolls the same point as last time it
        /// re-rolls once, which makes back-to-back spawns from one window less likely.
        /// </summary>
        ZombieSpawnPoint PickSpawnPoint()
        {
            _candidates.Clear();
            float maxSqr = maxSpawnDistance * maxSpawnDistance;
            foreach (var p in spawnPoints)
            {
                if (p == null || !p.IsActive) continue;
                if (_zones != null && !_zones.CanSpawnAt(p)) continue; // create_spawner_list(): enabled + active zones only
                if (maxSpawnDistance > 0f && !AnyPlayerWithin(p.transform.position, maxSqr)) continue;
                _candidates.Add(p);
            }
            if (_candidates.Count == 0) return null;

            var state = State;
            var pick = _candidates[state.RandomIndex(_candidates.Count)];
            if (pick == _lastSpawn) pick = _candidates[state.RandomIndex(_candidates.Count)];
            _lastSpawn = pick;
            return pick;
        }

        bool AnyPlayerWithin(Vector3 pos, float sqrDistance)
        {
            foreach (var p in GameBootstrap.Instance.Players)
                if (p != null && (p.transform.position - pos).sqrMagnitude <= sqrDistance) return true;
            return false;
        }

        /// <summary>
        /// zombie_speed_up(): once 4 or fewer are left to spawn and 3 or fewer are alive,
        /// the moment only one zombie is alive it switches to a sprint.
        /// </summary>
        void SpeedUpLastZombie(ZombieGameState state)
        {
            if (!state.ShouldSpeedUpLastZombie) return;
            if (!_speedUpArmed && state.ZombieTotal <= 4 && AliveCount <= 3) _speedUpArmed = true;
            if (_speedUpArmed && AliveCount == 1 && _alive[0].Speed != ZombieSpeed.Sprint)
                _alive[0].SetSpeed(ZombieSpeed.Sprint);
        }

        // ---------------- Called by ZombieAgent ----------------

        /// <summary>The nearest player who can be chased (not downed).</summary>
        public ZombiePlayer NearestPlayer(Vector3 from)
        {
            ZombiePlayer best = null;
            float bestSqr = float.MaxValue;
            foreach (var p in GameBootstrap.Instance.Players)
            {
                if (p == null || p.IsDowned || p.IsSpectating) continue; // self.ignoreme while downed
                float d = (p.transform.position - from).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = p; }
            }
            return best;
        }

        internal void NotifyKilled(ZombieAgent z, ZombiePlayer killer)
        {
            _alive.Remove(z);
            State.RecordKill();
            ZombieKilled?.Invoke(z, killer);
        }

        internal void NotifyTimedOut(ZombieAgent z)
        {
            _alive.Remove(z);
            State.ReturnSpawnTicket(); // put_timed_out_zombies_back_in_queue
        }

        internal void NotifyAttack(ZombieAgent z, ZombiePlayer target) => PlayerAttacked?.Invoke(z, target);

        /// <summary>
        /// Game over (end_game): stop the round loop and spawning, and freeze every zombie in place.
        /// </summary>
        public void Halt()
        {
            if (IsHalted) return;
            IsHalted = true;
            StopAllCoroutines();
            foreach (var z in _alive) z.Freeze();
        }

        // ---------------- Testing ----------------

        /// <summary>Kills every live zombie without awarding points. Right-click the component to use.</summary>
        [ContextMenu("Debug: Kill All Zombies")]
        public void DebugKillAll()
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
                _alive[i].Kill(null, HitZone.TorsoUpper);
        }
    }
}
