// PowerUpDirector.cs
//
// Decides when power-ups drop and applies them. Ports from _zombiemode_powerups.gsc:
//   watch_for_drop() / powerup_drop() / get_valid_powerup() : via PowerUpDropper (see PowerUps.cs)
//   nuke_powerup()          : after 0.5 s every live zombie dies, nearest first, 0.1-0.7 s
//                             apart, with no kill points or drops; then +400 for every player
//   insta_kill_powerup()    : 30 s of one-hit kills (ZombieGameState.InstaKill)
//   double_points_powerup() : 30 s of zombie_point_scalar = 2 (ZombieGameState.PointMultiplier)
//   full_ammo_powerup()     : every gun of every standing player gets its spare ammo filled
//   start_carpenter_new()   : every window is fully boarded, then +200 for every player
//   start_fire_sale()       : 30 s of 10-point boxes at every location (MysteryBoxDirector)
//
// Only kills by players drop power-ups (zombie_death_points: attacker must be a player), and
// never outside the playable area. Here that means zombies still outside a window don't drop.

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ZombiesPort
{
    public class PowerUpDirector : MonoBehaviour
    {
        [SerializeField] RoundDirector rounds;
        [SerializeField] MysteryBoxDirector mysteryBox;
        [SerializeField] LastStandDirector lastStand;
        [Tooltip("Optional prefab with a PowerUpPickup. Without one, colored spheres are used.")]
        [SerializeField] PowerUpPickup pickupPrefab;
        [Tooltip("At most 4 drop per round, and each lasts 26.5 s, so 6 covers every case.")]
        [SerializeField] int poolSize = 6;
        [Tooltip("powerup_drop(): drop_point + (0, 0, 40). 40 units is about 1 m.")]
        [SerializeField] float dropHeight = 40f * MysteryBoxRules.UnitsToMeters;
        [SerializeField] List<PowerUpId> included = new List<PowerUpId>(PowerUpInfo.Kino);
        [Tooltip("Carpenter: the GSC waits for the carpenter sound to finish before paying.")]
        [SerializeField] float carpenterPayDelay = 2.5f;

        public PowerUpDropper Dropper { get; private set; }
        public TimedPowerUps Timed { get; } = new TimedPowerUps();

        public event Action<PowerUpId, ZombiePlayer> Grabbed;
        public event Action NukeFlash;

        readonly List<PowerUpPickup> _pool = new List<PowerUpPickup>();
        readonly List<ZombieAgent> _nukeTargets = new List<ZombieAgent>(24);
        Barricade[] _windows = new Barricade[0];
        readonly List<PlayerScore> _scores = new List<PlayerScore>();

        static readonly Color[] Colors =
        {
            new Color(1f, 0.55f, 0.1f),  // Nuke
            new Color(0.95f, 0.95f, 0.9f), // Insta-Kill (skull)
            new Color(1f, 0.85f, 0.1f),  // Double Points
            new Color(0.3f, 0.8f, 0.3f), // Max Ammo
            new Color(0.6f, 0.4f, 0.2f), // Carpenter
            new Color(0.9f, 0.15f, 0.15f), // Fire Sale
        };
        static readonly string[] Letters = { "N", "IK", "x2", "A", "C", "$" };

        ZombieGameState State => GameBootstrap.Instance.State;

        void Start()
        {
            var boot = GameBootstrap.Instance;
            if (boot == null) { Debug.LogError("PowerUpDirector: add a GameBootstrap to the scene.", this); return; }
            if (rounds == null) rounds = FindFirstObjectByType<RoundDirector>();
            if (mysteryBox == null) mysteryBox = FindFirstObjectByType<MysteryBoxDirector>();
            if (lastStand == null) lastStand = FindFirstObjectByType<LastStandDirector>();
            _windows = FindObjectsByType<Barricade>(FindObjectsSortMode.None);

            var state = boot.State;
            Dropper = new PowerUpDropper(state.Players.Count, state.Vars.StartingPoints, included);
            foreach (var s in state.Players) { _scores.Add(s); s.OnScoreChanged += OnScoreChanged; }

            if (rounds != null)
            {
                rounds.RoundStarted += OnRoundStarted;
                rounds.ZombieKilled += OnZombieKilled;
            }
            if (lastStand != null) lastStand.GameOver += OnGameOver;
            Timed.Started += OnTimedStarted;
            Timed.Ended += OnTimedEnded;

            for (int i = 0; i < poolSize; i++) _pool.Add(CreatePickup(i));
        }

        void OnDestroy()
        {
            foreach (var s in _scores) s.OnScoreChanged -= OnScoreChanged;
            if (rounds != null)
            {
                rounds.RoundStarted -= OnRoundStarted;
                rounds.ZombieKilled -= OnZombieKilled;
            }
            if (lastStand != null) lastStand.GameOver -= OnGameOver;
        }

        void Update() => Timed.Tick(Time.deltaTime);

        // ---------------- Dropping ----------------

        void OnRoundStarted(int round) => Dropper.StartRound();

        void OnScoreChanged(PlayerScore score, int delta)
        {
            if (delta <= 0) return;                      // Only earned points move score_total.
            int total = 0;
            foreach (var s in _scores) total += s.ScoreTotal;
            Dropper.OnTeamScoreTotal(total);
        }

        void OnZombieKilled(ZombieAgent zombie, ZombiePlayer killer)
        {
            if (killer == null || zombie == null || zombie.IsOutside) return; // player kills inside the map only
            if (State.Intermission) return;

            int broken = 0;
            foreach (var w in _windows) if (w != null && w.IsOpen) broken++;
            var conditions = new PowerUpConditions
            {
                FullyBrokenWindows = broken,
                FireSaleActive = Timed.IsActive(PowerUpId.FireSale),
                ChestMoves = mysteryBox != null ? mysteryBox.ChestMoves : 0,
            };
            var id = Dropper.TryDrop(conditions);
            if (id.HasValue) Spawn(id.Value, zombie.transform.position + Vector3.up * dropHeight);
        }

        /// <summary>specific_powerup_drop(): drop a chosen power-up (debug tools, scripted events).</summary>
        public void Spawn(PowerUpId id, Vector3 position)
        {
            foreach (var p in _pool)
            {
                if (p.InUse) continue;
                p.Show(id, position, Colors[(int)id], Letters[(int)id]);
                return;
            }
        }

        internal void Release(PowerUpPickup p) => p.gameObject.SetActive(false);

        // ---------------- Grabbing ----------------

        internal void OnGrabbed(PowerUpPickup pickup, ZombiePlayer player)
        {
            var id = pickup.Id;
            Vector3 at = pickup.transform.position;
            Release(pickup);

            switch (id)
            {
                case PowerUpId.Nuke: StartCoroutine(Nuke(at)); break;
                case PowerUpId.MaxAmmo: MaxAmmo(); break;
                case PowerUpId.Carpenter: StartCoroutine(Carpenter()); break;
                default: Timed.Activate(id); break;       // Insta-Kill, Double Points, Fire Sale
            }
            Grabbed?.Invoke(id, player);
        }

        void OnTimedStarted(PowerUpId id)
        {
            var state = State;
            switch (id)
            {
                case PowerUpId.InstaKill: state.InstaKill = true; break;
                case PowerUpId.DoublePoints: state.PointMultiplier = 2; break;
                case PowerUpId.FireSale: if (mysteryBox != null) mysteryBox.StartFireSale(); break;
            }
        }

        void OnTimedEnded(PowerUpId id)
        {
            var state = State;
            switch (id)
            {
                case PowerUpId.InstaKill: state.InstaKill = false; break;
                case PowerUpId.DoublePoints: state.PointMultiplier = 1; break;
                case PowerUpId.FireSale: if (mysteryBox != null) mysteryBox.EndFireSale(); break;
            }
        }

        IEnumerator Nuke(Vector3 at)
        {
            NukeFlash?.Invoke();
            yield return new WaitForSeconds(PowerUpInfo.NukeDelay);

            // Everyone alive right now, nearest first. Zombies that spawn during the sequence survive.
            _nukeTargets.Clear();
            if (rounds != null) _nukeTargets.AddRange(rounds.Alive);
            _nukeTargets.Sort((a, b) => (a.transform.position - at).sqrMagnitude.CompareTo((b.transform.position - at).sqrMagnitude));

            foreach (var z in _nukeTargets)
            {
                yield return new WaitForSeconds(UnityEngine.Random.Range(PowerUpInfo.NukeKillGapMin, PowerUpInfo.NukeKillGapMax));
                if (z != null && z.IsAlive) z.Kill(null, HitZone.Head); // dodamage without an attacker: no points, no drop
            }

            Pay(PowerUpInfo.NukePoints);
        }

        void MaxAmmo()
        {
            foreach (var p in GameBootstrap.Instance.Players)
            {
                if (p == null || p.IsDowned || p.IsSpectating) continue; // full_ammo_powerup() skips downed players
                var weapon = p.GetComponent<PlayerWeapon>();
                if (weapon == null) continue;
                foreach (var w in p.Weapons.Primaries) weapon.GetState(w).FillReserveToMax();
            }
        }

        IEnumerator Carpenter()
        {
            foreach (var w in _windows) if (w != null) w.RepairAll();
            yield return new WaitForSeconds(carpenterPayDelay);
            Pay(PowerUpInfo.CarpenterPoints);
        }

        void Pay(int raw)
        {
            var state = State;
            int points = state.PointsForPowerUp(raw);
            foreach (var p in GameBootstrap.Instance.Players)
                if (p != null && p.Score != null) p.Score.Add(points, true, state.Intermission);
        }

        void OnGameOver(int round)
        {
            Timed.Clear();
            StopAllCoroutines();
            foreach (var p in _pool) Release(p);
        }

        // ---------------- Testing (right-click the component) ----------------

        [ContextMenu("Debug: Drop Nuke")] void DebugNuke() => DebugDrop(PowerUpId.Nuke);
        [ContextMenu("Debug: Drop Insta-Kill")] void DebugInsta() => DebugDrop(PowerUpId.InstaKill);
        [ContextMenu("Debug: Drop Double Points")] void DebugDouble() => DebugDrop(PowerUpId.DoublePoints);
        [ContextMenu("Debug: Drop Max Ammo")] void DebugAmmo() => DebugDrop(PowerUpId.MaxAmmo);
        [ContextMenu("Debug: Drop Carpenter")] void DebugCarpenter() => DebugDrop(PowerUpId.Carpenter);
        [ContextMenu("Debug: Drop Fire Sale")] void DebugFireSale() => DebugDrop(PowerUpId.FireSale);

        /// <summary>Drops one 2 m in front of the first player, ignoring the drop rules.</summary>
        void DebugDrop(PowerUpId id)
        {
            var boot = GameBootstrap.Instance;
            if (!Application.isPlaying || boot == null || boot.Players.Count == 0) return;
            Transform p = boot.Players[0].transform;
            Spawn(id, p.position + p.forward * 2f + Vector3.up * dropHeight);
        }

        // ---------------- Pool ----------------

        PowerUpPickup CreatePickup(int i)
        {
            PowerUpPickup p;
            if (pickupPrefab != null)
            {
                p = Instantiate(pickupPrefab, transform);
            }
            else
            {
                var root = new GameObject("PowerUp");
                root.transform.SetParent(transform, false);
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.SetParent(root.transform, false);
                sphere.transform.localScale = Vector3.one * 0.45f;
                Destroy(sphere.GetComponent<Collider>()); // Visual only: never blocks bullets or players.

                var text = new GameObject("Letter");
                text.transform.SetParent(root.transform, false);
                text.transform.localPosition = new Vector3(0f, 0.45f, 0f);
                var tmp = text.AddComponent<TextMeshPro>();
                tmp.fontSize = 3f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.rectTransform.sizeDelta = new Vector2(1f, 0.5f);

                p = root.AddComponent<PowerUpPickup>();
                SetPrivate(p, "model", sphere.transform);
                SetPrivate(p, "label", tmp);
            }
            p.name = "PowerUp_" + i;
            p.Init(this);
            return p;
        }

        // Fills a private [SerializeField] on the generated placeholder (prefabs set these in the Inspector).
        static void SetPrivate(object target, string field, object value)
        {
            var f = target.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(target, value);
        }
    }
}
