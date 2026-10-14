// PlayerWeapon.cs
//
// Shooting, aiming, reloading, swapping and the knife. Reads MobileInput; damage goes through
// ZombieAgent.TakeDamage(), so points come out exactly as in BO1 (10 per non-lethal hit, 60/100
// for body/head kills, 130 for knife kills).
//
// Hit detection is hitscan from the center of the screen, like CoD: one RaycastNonAlloc per
// shot into a preallocated buffer, so shooting allocates nothing. A bullet can pass through
// Penetration zombies and stops at the first wall.
//
// It follows WeaponInventory: when the Mystery Box hands over a gun, this equips it; a
// duplicate refills its ammo (GiveStartAmmo). Each gun keeps its own ammo when swapped.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombiesPort
{
    [RequireComponent(typeof(ZombiePlayer))]
    public class PlayerWeapon : MonoBehaviour
    {
        [SerializeField] MobileInput input;
        [SerializeField] Camera playerCamera;
        [Tooltip("Every gun the player can hold. Ids not listed fall back to the first entry's stats.")]
        [SerializeField] List<WeaponDefinition> catalog = new List<WeaponDefinition>();
        [SerializeField] LayerMask hitMask = ~0;
        [Tooltip("Hold Fire to keep shooting a semi-auto at its top rate. Recommended on touch.")]
        [SerializeField] bool holdToRepeatSemiAuto = true;
        [SerializeField] MeleeStats knife = MeleeStats.Knife();
        [SerializeField] float swapSeconds = 0.5f;
        [SerializeField] AudioSource audioSource;

        public WeaponState Current { get; private set; }
        public WeaponDefinition CurrentDefinition { get; private set; }
        public bool IsAiming { get; private set; }
        /// <summary>0 = hip, 1 = fully aimed. Drives FOV, spread and the crosshair.</summary>
        public float AimAmount { get; private set; }
        public bool IsBusy => Time.time < _busyUntil;

        public event Action<WeaponState> Fired;
        public event Action<WeaponState> DryFired;
        public event Action<WeaponState> ReloadStarted;
        public event Action<WeaponState> Equipped;
        public event Action<bool> Knifed;                         // true if it hit a zombie
        public event Action<ZombieAgent, HitZone> ZombieHit;

        ZombiePlayer _player;
        readonly Dictionary<string, WeaponState> _states = new Dictionary<string, WeaponState>();
        readonly Dictionary<string, GameObject> _viewModels = new Dictionary<string, GameObject>();
        readonly RaycastHit[] _hits = new RaycastHit[16];
        readonly Collider[] _overlaps = new Collider[16];
        readonly List<ZombieAgent> _hitThisShot = new List<ZombieAgent>(4);
        static readonly HitDistanceComparer ByDistance = new HitDistanceComparer();
        float _baseFov;
        float _busyUntil, _nextKnife;
        readonly HashSet<string> _warnedFallback = new HashSet<string>();

        void Awake()
        {
            _player = GetComponent<ZombiePlayer>();
            if (input == null) input = FindFirstObjectByType<MobileInput>();
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
            if (catalog.Count == 0 || catalog[0] == null) catalog.Insert(0, WeaponDefinition.CreateM1911());
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (playerCamera != null) _baseFov = playerCamera.fieldOfView;
        }

        void Start()
        {
            _player.Weapons.OnWeaponGiven += OnWeaponGiven;
            Equip(_player.Weapons.Current);
        }

        void OnDestroy()
        {
            if (_player != null && _player.Weapons != null) _player.Weapons.OnWeaponGiven -= OnWeaponGiven;
        }

        void Update()
        {
            float now = Time.time;

            // The inventory is the source of truth (the Mystery Box may have swapped the gun).
            if (Current == null || Current.Stats.Id != _player.Weapons.Current) Equip(_player.Weapons.Current);
            if (Current == null || input == null) return;

            Current.Tick(now);

            bool canAct = !_player.IsDowned && !_player.IsDrinkingPerk;

            // Aim: not while reloading, swapping or knifing (as in CoD).
            IsAiming = canAct && input.AimHeld && !Current.IsReloading && !IsBusy;
            float aimSpeed = Current.Stats.AdsSeconds > 0f ? Time.deltaTime / Current.Stats.AdsSeconds : 1f;
            AimAmount = Mathf.MoveTowards(AimAmount, IsAiming ? 1f : 0f, aimSpeed);
            if (playerCamera != null) playerCamera.fieldOfView = Mathf.Lerp(_baseFov, Current.Stats.AdsFov, AimAmount);

            if (!canAct) return;

            if (input.KnifePressed && now >= _nextKnife) { Knife(now); return; }
            if (input.SwapPressed && !IsBusy) { SwapToNext(now); return; }
            if (input.ReloadPressed && !IsBusy && Current.StartReload(now)) OnReloadStarted();

            if (IsBusy) return;
            switch (Current.TryFire(now, input.FireHeld, holdToRepeatSemiAuto))
            {
                case FireResult.Fired:
                    Shoot();
                    break;
                case FireResult.AutoReload:
                    OnReloadStarted();
                    break;
                case FireResult.DryFire:
                    Play(CurrentDefinition.dryFireSound);
                    DryFired?.Invoke(Current);
                    break;
            }
        }

        // ---------------- Shooting ----------------

        void Shoot()
        {
            var stats = Current.Stats;
            Play(CurrentDefinition.fireSound);
            Fired?.Invoke(Current);
            if (playerCamera == null) return;

            // Random spread inside a cone, tighter when aiming.
            float spread = Mathf.Lerp(stats.HipSpreadDegrees, stats.AdsSpreadDegrees, AimAmount);
            Vector2 jitter = UnityEngine.Random.insideUnitCircle * spread;
            Transform cam = playerCamera.transform;
            Vector3 dir = Quaternion.AngleAxis(jitter.x, cam.up) * (Quaternion.AngleAxis(jitter.y, cam.right) * cam.forward);

            int count = Physics.RaycastNonAlloc(cam.position, dir, _hits, stats.Range, hitMask, QueryTriggerInteraction.Ignore);
            if (count == 0) return;
            Array.Sort(_hits, 0, count, ByDistance);

            _hitThisShot.Clear();
            int remaining = Mathf.Max(1, stats.Penetration);
            for (int i = 0; i < count && remaining > 0; i++)
            {
                var col = _hits[i].collider;
                if (col.transform.IsChildOf(transform)) continue; // Our own collider.

                var zombie = col.GetComponentInParent<ZombieAgent>();
                if (zombie == null) break;                         // A wall: the bullet stops.
                if (!zombie.IsAlive || _hitThisShot.Contains(zombie)) continue;

                HitZone zone = ResolveZone(zombie, i, count);
                _hitThisShot.Add(zombie);
                zombie.TakeDamage(stats.DamageFor(zone), _player, zone);
                ZombieHit?.Invoke(zombie, zone);
                remaining--;
            }
        }

        /// <summary>
        /// Prefer a ZombieHitbox anywhere along this bullet's path through the zombie (a head
        /// sphere sits inside the body capsule, so the capsule is often hit first). Otherwise,
        /// use the height on the zombie's collider.
        /// </summary>
        HitZone ResolveZone(ZombieAgent zombie, int first, int count)
        {
            for (int j = first; j < count; j++)
            {
                var c = _hits[j].collider;
                if (c.GetComponentInParent<ZombieAgent>() != zombie) continue;
                var box = c.GetComponent<ZombieHitbox>();
                if (box != null) return box.zone;
            }
            Bounds b = _hits[first].collider.bounds;
            float t = Mathf.InverseLerp(b.min.y, b.max.y, _hits[first].point.y);
            return HitZoneUtil.FromHeight(t);
        }

        // ---------------- Knife ----------------

        /// <summary>Hits the closest live zombie within range and in front. Cancels a reload, as in CoD.</summary>
        void Knife(float now)
        {
            _nextKnife = now + knife.Cooldown;
            _busyUntil = now + knife.BusySeconds;
            Current.CancelReload();
            input.CancelAim();

            Transform eye = playerCamera != null ? playerCamera.transform : transform;
            Vector3 origin = eye.position;
            Vector3 forward = Vector3.ProjectOnPlane(eye.forward, Vector3.up).normalized;
            float minDot = Mathf.Cos(knife.ConeDegrees * Mathf.Deg2Rad);

            ZombieAgent best = null;
            float bestSqr = float.MaxValue;
            int n = Physics.OverlapSphereNonAlloc(origin, knife.Range, _overlaps, hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var z = _overlaps[i].GetComponentInParent<ZombieAgent>();
                if (z == null || !z.IsAlive) continue;
                Vector3 to = z.transform.position - origin;
                to.y = 0f;
                if (to.sqrMagnitude > 0.0001f && Vector3.Dot(forward, to.normalized) < minDot) continue;
                if (to.sqrMagnitude < bestSqr) { bestSqr = to.sqrMagnitude; best = z; }
            }

            if (best != null)
            {
                best.TakeDamage(knife.Damage, _player, HitZone.TorsoUpper, melee: true);
                ZombieHit?.Invoke(best, HitZone.TorsoUpper);
            }
            Knifed?.Invoke(best != null);
        }

        // ---------------- Weapons ----------------

        void SwapToNext(float now)
        {
            var primaries = _player.Weapons.Primaries;
            if (primaries.Count < 2) return;
            int i = 0;
            for (int k = 0; k < primaries.Count; k++) if (primaries[k] == _player.Weapons.Current) i = k;
            _player.Weapons.SwitchTo(primaries[(i + 1) % primaries.Count]);
            input.CancelAim();
            _busyUntil = now + swapSeconds;
            // Update() notices the inventory change and equips it.
        }

        void OnWeaponGiven(string id, bool refill)
        {
            var state = GetState(id);
            if (refill) state.RefillToStart(); // A duplicate from the box or wall refills ammo.
        }

        void Equip(string id)
        {
            if (string.IsNullOrEmpty(id)) { Current = null; return; }
            if (Current != null) Current.CancelReload();

            Current = GetState(id);
            CurrentDefinition = FindDefinition(id);

            foreach (var kv in _viewModels) kv.Value.SetActive(kv.Key == id);
            if (!_viewModels.ContainsKey(id) && CurrentDefinition.viewModelPrefab != null && playerCamera != null)
            {
                // Created once per gun and reused after that.
                var vm = Instantiate(CurrentDefinition.viewModelPrefab, playerCamera.transform, false);
                _viewModels[id] = vm;
            }
            Equipped?.Invoke(Current);
        }

        WeaponState GetState(string id)
        {
            if (!_states.TryGetValue(id, out var s))
            {
                var stats = FindDefinition(id).stats;
                if (stats.Id != id)
                {
                    // Fallback stats, but keep the real id and name so the HUD doesn't say "M1911" for a Ray Gun.
                    stats.Id = id;
                    stats.DisplayName = id;
                }
                s = new WeaponState(stats);
                _states[id] = s;
            }
            return s;
        }

        WeaponDefinition FindDefinition(string id)
        {
            foreach (var d in catalog)
                if (d != null && d.stats.Id == id) return d;
            if (_warnedFallback.Add(id))
                Debug.LogWarning("PlayerWeapon: no WeaponDefinition for \"" + id + "\", using " +
                                 catalog[0].stats.DisplayName + " stats. Create one via Create > Zombies > Weapon Definition.", this);
            return catalog[0];
        }

        void OnReloadStarted()
        {
            Play(CurrentDefinition.reloadSound);
            ReloadStarted?.Invoke(Current);
        }

        void Play(AudioClip clip)
        {
            if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
        }

        sealed class HitDistanceComparer : IComparer<RaycastHit>
        {
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
