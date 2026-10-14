// DogRoundDirector.cs
//
// Hellhound rounds (maps/_zombiemode_ai_dogs.gsc). RoundDirector hands a round over to this when
// it's a dog round; the round number, chalk counter and break work as usual around it.
//
// A dog round:
//   1. Announcement and a 7 s pause (play_dog_round)
//   2. Dogs spawn one at a time: a lightning strike, then 1.5 s later the dog appears, facing
//      the player it's hunting (dog_spawn_fx). Each dog goes after the player the fewest dogs
//      are already after. At most 2 dogs alive per standing player.
//   3. When the last dog dies it drops a Max Ammo, and the round ends 8 s later.
//
// Dog spots: ZombieSpawnPoints with "Dogs Only" ticked, in active zones, 400-1000 units
// (10-25 m) from the target. With no dog spots in the scene, a random reachable NavMesh point
// 10-25 m from the target is used instead, so a test scene works without placing any.
//
// The dogs are ZombieAgents (Is Dog ticked) from their own small pool, so kill points, Insta-Kill,
// the Nuke and the stuck-zombie failsafe all apply to them as in BO1.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ZombiesPort
{
    public class DogRoundDirector : MonoBehaviour
    {
        [Tooltip("A ZombiePool whose prefab has Is Dog ticked. 8 covers four players (2 alive each).")]
        [SerializeField] ZombiePool dogPool;
        [SerializeField] PowerUpDirector powerUps;
        [Tooltip("Optional lightning strike shown at the spawn spot for 1.5 s. Without one, a glowing pillar is used.")]
        [SerializeField] GameObject spawnBoltPrefab;
        [SerializeField] float boltHeight = 6f;

        public DogRoundTracker Tracker { get; private set; }
        public bool IsDogRoundActive { get; private set; }

        /// <summary>A dog round is starting (round number). The HUD shows the announcement.</summary>
        public event Action<int> DogRoundStarting;
        public event Action<int> DogRoundEnded;
        /// <summary>A dog is about to appear here in 1.5 s (for lightning VFX and sound).</summary>
        public event Action<Vector3> DogIncoming;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        readonly List<ZombieSpawnPoint> _dogSpots = new List<ZombieSpawnPoint>();
        readonly List<ZombieSpawnPoint> _candidates = new List<ZombieSpawnPoint>();
        readonly List<float> _distances = new List<float>();
        readonly List<GameObject> _bolts = new List<GameObject>();
        readonly System.Random _rng = new System.Random();
        NavMeshPath _path;
        ZombieSpawnPoint _lastSpot;
        int _pendingSpawns;
        Vector3 _lastDogPosition;
        bool _hasLastDog;

        void Awake()
        {
            Tracker = new DogRoundTracker();
            _path = new NavMeshPath();
        }

        IEnumerator Start()
        {
            if (powerUps == null) powerUps = FindFirstObjectByType<PowerUpDirector>();
            foreach (var p in FindObjectsByType<ZombieSpawnPoint>(FindObjectsSortMode.None))
                if (p.DogsOnly) _dogSpots.Add(p);

            for (int i = 0; i < 4; i++) _bolts.Add(CreateBolt(i));

            if (dogPool == null)
            {
                Debug.LogWarning("DogRoundDirector: no dog pool assigned, so there will be no dog rounds.", this);
                yield break;
            }
            if (!dogPool.HoldsDogs)
                Debug.LogWarning("DogRoundDirector: the dog pool's prefab doesn't have Is Dog ticked.", this);
            yield return dogPool.Prewarm();
        }

        public bool IsDogRound(int round) => dogPool != null && dogPool.IsReady && Tracker.IsDogRound(round);

        /// <summary>dog_round_spawning() + round_wait()'s dog branch + dog_round_aftermath().</summary>
        internal IEnumerator RunDogRound(RoundDirector rounds, ZombieGameState state)
        {
            int round = state.RoundNumber;
            Tracker.OnDogRoundStarted(round);
            IsDogRoundActive = true;
            _hasLastDog = false;
            rounds.ZombieKilled += OnKilled;
            DogRoundStarting?.Invoke(round);

            var players = GameBootstrap.Instance.Players;
            yield return new WaitForSeconds(DogRules.StartDelay);

            int dogRound = Tracker.DogRoundCount;
            int max = DogRules.Count(dogRound, players.Count);
            int health = DogRules.Health(dogRound);
            state.StartDogQueue(max);

            var huntedBy = new int[players.Count];
            var standing = new bool[players.Count];
            var holdOff = new WaitForSeconds(2f); // "while( get_enemy_count() >= num_player_valid * 2 ) wait( 2 )"
            int spawned = 0;

            while (spawned < max)
            {
                if (state.Intermission) { Finish(rounds); yield break; }

                int up = 0;
                for (int i = 0; i < players.Count; i++)
                {
                    var p = players[i];
                    standing[i] = p != null && !p.IsDowned && !p.IsSpectating;
                    if (standing[i]) up++;
                }
                if (rounds.AliveCount + _pendingSpawns >= DogRules.MaxAlive(up) || dogPool.FreeCount <= _pendingSpawns)
                {
                    yield return holdOff;
                    continue;
                }

                int fav = DogRules.PickFavoriteEnemy(huntedBy, standing);
                if (fav < 0) { yield return holdOff; continue; }
                var target = players[fav];

                Vector3 position;
                if (!PickSpawnPosition(target, rounds.Zones, out position))
                {
                    yield return holdOff;
                    continue;
                }

                state.TryTakeSpawnTicket();
                spawned++;
                StartCoroutine(SpawnDog(rounds, state, position, target, health));
                yield return new WaitForSeconds(DogRules.SpawnWait(dogRound, spawned, max));
            }

            // round_wait(): wait until every dog (including ones still arriving) is dead.
            while (_pendingSpawns > 0 || rounds.AliveCount > 0)
            {
                if (state.Intermission) { Finish(rounds); yield break; }
                yield return null;
            }

            // dog_round_aftermath(): "last_dog_down" drops a Max Ammo where it died.
            if (_hasLastDog && powerUps != null) powerUps.SpawnAtGround(PowerUpId.MaxAmmo, _lastDogPosition);
            yield return new WaitForSeconds(DogRules.AftermathSeconds);

            Finish(rounds);
            Tracker.OnDogRoundEnded();
            DogRoundEnded?.Invoke(round);
        }

        void Finish(RoundDirector rounds)
        {
            rounds.ZombieKilled -= OnKilled;
            IsDogRoundActive = false;
        }

        void OnKilled(ZombieAgent z, ZombiePlayer killer)
        {
            if (z == null || !z.IsDog) return;
            _lastDogPosition = z.transform.position;
            _hasLastDog = true;
        }

        /// <summary>dog_spawn_fx(): lightning, 1.5 s, then the dog appears facing its target.</summary>
        IEnumerator SpawnDog(RoundDirector rounds, ZombieGameState state, Vector3 position, ZombiePlayer target, int health)
        {
            _pendingSpawns++;
            DogIncoming?.Invoke(position);
            var bolt = ShowBolt(position);
            yield return new WaitForSeconds(DogRules.SpawnFxSeconds);
            if (bolt != null) bolt.SetActive(false);

            ZombieAgent dog = null;
            if (!state.Intermission)
            {
                Vector3 look = target != null ? target.transform.position - position : Vector3.forward;
                look.y = 0f;
                var rotation = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity;
                dog = dogPool.Take(position, rotation);
            }

            if (dog == null)
            {
                state.ReturnSpawnTicket();
            }
            else
            {
                dog.Activate(rounds, health, ZombieSpeed.Sprint, null, target); // zombie_move_speed = "sprint"
                rounds.AdoptSpawned(dog);
            }
            _pendingSpawns--;
        }

        /// <summary>
        /// dog_spawn_factory_logic(): a dog spot in an active zone, 400-1000 units from the target,
        /// not the one used last. Falls back to a reachable NavMesh point 10-25 m away.
        /// </summary>
        bool PickSpawnPosition(ZombiePlayer target, ZoneManager zones, out Vector3 position)
        {
            position = Vector3.zero;
            if (target == null) return false;
            Vector3 from = target.transform.position;

            _candidates.Clear();
            _distances.Clear();
            foreach (var p in _dogSpots)
            {
                if (p == null || !p.IsActive) continue;
                if (zones != null && !zones.CanSpawnAt(p)) continue;
                _candidates.Add(p);
                _distances.Add(Vector3.Distance(p.transform.position, from) / MysteryBoxRules.UnitsToMeters);
            }

            if (_candidates.Count > 0)
            {
                int i = DogRules.PickSpawn(_distances, _candidates.IndexOf(_lastSpot), _rng);
                _lastSpot = _candidates[i];
                position = _lastSpot.transform.position;
                return true;
            }

            // No dog spots: somewhere 400-1000 units away that the dog can actually run from.
            float min = DogRules.SpawnMinUnits * MysteryBoxRules.UnitsToMeters;
            float max = DogRules.SpawnMaxUnits * MysteryBoxRules.UnitsToMeters;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
                float dist = Mathf.Lerp(min, max, (float)_rng.NextDouble());
                Vector3 guess = from + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * dist;
                if (!NavMesh.SamplePosition(guess, out var hit, 3f, NavMesh.AllAreas)) continue;
                if (!NavMesh.CalculatePath(hit.position, from, NavMesh.AllAreas, _path) ||
                    _path.status != NavMeshPathStatus.PathComplete) continue;
                position = hit.position;
                return true;
            }
            return false;
        }

        // ---------------- Testing (right-click the component) ----------------

        /// <summary>Makes the next round a dog round (pair with RoundDirector's "Debug: Kill All Zombies").</summary>
        [ContextMenu("Debug: Next Round Is a Dog Round")]
        void DebugNextRoundIsDogRound()
        {
            var boot = GameBootstrap.Instance;
            if (!Application.isPlaying || boot == null) return;
            var rounds = FindFirstObjectByType<RoundDirector>();
            int round = boot.State.RoundNumber + (rounds != null && rounds.IsBetweenRounds ? 0 : 1);
            Tracker.ScheduleNext(round);
            Debug.Log("Round " + round + " will be a dog round.", this);
        }

        // ---------------- Lightning placeholder ----------------

        GameObject ShowBolt(Vector3 position)
        {
            foreach (var b in _bolts)
            {
                if (b.activeSelf) continue;
                b.transform.position = position + Vector3.up * (boltHeight * 0.5f);
                b.SetActive(true);
                return b;
            }
            return null;
        }

        GameObject CreateBolt(int i)
        {
            GameObject bolt;
            if (spawnBoltPrefab != null)
            {
                bolt = Instantiate(spawnBoltPrefab, transform);
            }
            else
            {
                bolt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                bolt.transform.SetParent(transform, false);
                bolt.transform.localScale = new Vector3(0.12f, boltHeight * 0.5f, 0.12f);
                Destroy(bolt.GetComponent<Collider>());
                var block = new MaterialPropertyBlock();
                var color = new Color(0.6f, 0.85f, 1f);
                block.SetColor(ColorId, color);
                block.SetColor(BaseColorId, color);
                bolt.GetComponent<Renderer>().SetPropertyBlock(block);
            }
            bolt.name = "DogSpawnBolt_" + i;
            bolt.SetActive(false);
            return bolt;
        }
    }
}
