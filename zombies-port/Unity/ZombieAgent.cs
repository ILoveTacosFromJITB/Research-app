// ZombieAgent.cs
//
// One pooled zombie. Ports the parts of _zombiemode_spawner.gsc and _zombiemode.gsc that the
// round system depends on:
//   zombie_spawn_init / set_zombie_run_cycle : health and walk/run/sprint on spawn
//   find_flesh (simplified)                  : chase the nearest player on the NavMesh
//   zombie_damage / zombie_death_event       : 10 points per non-lethal hit, kill points on death
//   round_spawn_failsafe                     : stuck or fallen zombies are recycled
//   zombie_goto_entrance / tear_into_building: walk to a window, pull boards one at a time
//                                              (reaching through to swipe at nearby players),
//                                              then climb in once every board is gone
//
// A zombie spawned at a ZombieSpawnPoint linked to a Barricade goes through the window phases
// first; one spawned at an unlinked point (inside, or a "riser") chases straight away.
//
// Not ported yet: crawlers and gibbing. The attack is raised as an event that LastStandDirector
// turns into player damage.
//
// Prefab setup: NavMeshAgent + capsule Collider on the root, optional Animator with a float
// "Speed" (0 walk, 1 run, 2 sprint) and triggers "Attack", "Die", "Tear" and "Climb". Missing
// parameters are skipped, so a plain capsule works for testing. Bake a NavMesh (AI Navigation package).

using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace ZombiesPort
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class ZombieAgent : MonoBehaviour
    {
        [Header("Type")]
        [Tooltip("Hellhound: spawned by DogRoundDirector, always sprints, hunts one player, never drops power-ups.")]
        [SerializeField] bool isDog;
        [Tooltip("Damage per hit on a player. Zombies 50 (player_damage_override default), dogs 40.")]
        [SerializeField] int meleeDamage = 50;

        [Header("Speeds (meters per second)")]
        [SerializeField] float walkSpeed = 1.0f;
        [SerializeField] float runSpeed = 2.6f;
        [SerializeField] float sprintSpeed = 4.2f;

        [Header("Behaviour")]
        [Tooltip("How often the path is recalculated. Spread out per zombie so they don't all repath on one frame.")]
        [SerializeField] float repathInterval = 0.35f;
        [SerializeField] float attackRange = 0.9f;
        [SerializeField] float attackInterval = 1.0f;
        [Tooltip("How long the body stays before going back to the pool.")]
        [SerializeField] float corpseSeconds = 1.5f;

        [SerializeField] Animator animator;

        public bool IsDog => isDog;
        public int MeleeDamage => meleeDamage;
        public int Health { get; private set; }
        public bool IsAlive { get; private set; }
        public ZombieSpeed Speed { get; private set; }
        /// <summary>Game over: stands still and stops attacking.</summary>
        public bool IsFrozen { get; private set; }

        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int AttackHash = Animator.StringToHash("Attack");
        static readonly int DieHash = Animator.StringToHash("Die");
        static readonly int TearHash = Animator.StringToHash("Tear");
        static readonly int ClimbHash = Animator.StringToHash("Climb");

        enum Phase { Chasing, ToWindow, AtWindow, Climbing }

        ZombiePool _pool;
        RoundDirector _director;
        NavMeshAgent _nav;
        Collider[] _colliders;
        WaitForSeconds _corpseWait;
        bool _hasSpeed, _hasAttack, _hasDie, _hasTear, _hasClimb;

        ZombiePlayer _target;
        ZombiePlayer _favorite;   // ai.favoriteenemy: the player a dog was sent after
        Vector3 _lastDestination;
        float _nextRepath, _nextAttack, _nextFailsafe;
        Vector3 _failsafeOrigin;

        // Window phases
        Phase _phase;
        Barricade _window;
        bool _hasSpot;
        int _board = -1;
        float _phaseTimer, _nextReachIn;
        Vector3 _climbFrom;

        /// <summary>True until the zombie has climbed in (or if it never had a window).</summary>
        public bool IsOutside => _phase != Phase.Chasing;

        /// <summary>Called once by the pool at load time: cache everything so spawning is cheap.</summary>
        internal void OnCreated(ZombiePool pool)
        {
            _pool = pool;
            _nav = GetComponent<NavMeshAgent>();
            _nav.enabled = false;
            _colliders = GetComponentsInChildren<Collider>(true);
            _corpseWait = new WaitForSeconds(corpseSeconds);
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null)
            {
                // Don't animate zombies the camera can't see: a big CPU saving with 24 on screen.
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                foreach (var p in animator.parameters)
                {
                    if (p.nameHash == SpeedHash) _hasSpeed = true;
                    else if (p.nameHash == AttackHash) _hasAttack = true;
                    else if (p.nameHash == DieHash) _hasDie = true;
                    else if (p.nameHash == TearHash) _hasTear = true;
                    else if (p.nameHash == ClimbHash) _hasClimb = true;
                }
            }
        }

        /// <summary>
        /// Reset everything a previous life may have changed. With a window, head for it first;
        /// otherwise start chasing.
        /// </summary>
        internal void Activate(RoundDirector director, int health, ZombieSpeed speed, Barricade window = null,
                               ZombiePlayer favoriteEnemy = null)
        {
            _favorite = favoriteEnemy;
            _director = director;
            Health = health;
            IsAlive = true;
            IsFrozen = false;

            foreach (var c in _colliders) c.enabled = true;
            if (animator != null) animator.Rebind();

            _nav.enabled = true;
            _nav.Warp(transform.position); // Required: moving a NavMeshAgent's transform directly doesn't move the agent.
            _nav.isStopped = false;
            SetSpeed(speed);

            float now = Time.time;
            _nextRepath = now + Random.Range(0f, repathInterval); // Staggered start.
            _nextAttack = now;
            _target = null;
            _lastDestination = new Vector3(float.MaxValue, 0f, 0f);
            _failsafeOrigin = transform.position;
            _nextFailsafe = now + director.State.Vars.FailsafeIntervalSeconds;

            _window = window;
            _phase = window != null ? Phase.ToWindow : Phase.Chasing;
            _hasSpot = false;
            _board = -1;
            _nextReachIn = now;
        }

        public void SetSpeed(ZombieSpeed speed)
        {
            Speed = speed;
            _nav.speed = speed == ZombieSpeed.Walk ? walkSpeed : speed == ZombieSpeed.Run ? runSpeed : sprintSpeed;
            if (_hasSpeed) animator.SetFloat(SpeedHash, (float)speed);
        }

        public void Freeze()
        {
            IsFrozen = true;
            if (_nav.enabled) _nav.isStopped = true;
        }

        void Update()
        {
            if (!IsAlive || IsFrozen) return;
            float now = Time.time;

            switch (_phase)
            {
                case Phase.ToWindow: ToWindow(now); break;
                case Phase.AtWindow: AtWindow(now); break;
                case Phase.Climbing: Climb(now); break;
                default: Chase(now); break;
            }

            // round_spawn_failsafe() skips zombies busy at a window (lastchunk_destroy_time).
            if (_phase == Phase.AtWindow || _phase == Phase.Climbing)
            {
                _nextFailsafe = now + _director.State.Vars.FailsafeIntervalSeconds;
                _failsafeOrigin = transform.position;
            }
            else if (now >= _nextFailsafe)
            {
                Failsafe(now);
            }
        }

        // ---------------- Window phases ----------------

        /// <summary>zombie_goto_entrance(): walk to a free attack spot (or wait behind them).</summary>
        void ToWindow(float now)
        {
            if (!_hasSpot)
            {
                if (_window.TryClaimSpot(this, out var spot))
                {
                    _hasSpot = true;
                    _nav.isStopped = false;
                    _nav.SetDestination(spot);
                }
                else if (now >= _nextRepath)
                {
                    _nextRepath = now + 0.5f; // All three spots taken: queue up and keep asking.
                    _nav.SetDestination(_window.WaitPoint);
                }
                return;
            }

            if (_nav.pathPending || _nav.remainingDistance > 0.3f) return;
            _nav.isStopped = true;
            _phase = Phase.AtWindow;
        }

        /// <summary>tear_into_building(): pull the nearest free board; reach through now and then.</summary>
        void AtWindow(float now)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, _window.FacingInside, 360f * Time.deltaTime);

            if (_window.IsOpen && _board < 0) { StartClimb(now); return; }

            if (_board < 0)
            {
                _board = _window.ClaimBoard(transform.position);
                if (_board >= 0)
                {
                    _phaseTimer = now + _window.TearSeconds;
                    if (_hasTear) animator.SetTrigger(TearHash);
                }
                else if (now >= _nextReachIn)
                {
                    // Every board left is taken by another zombie: swipe through the gaps instead.
                    _nextReachIn = now + attackInterval;
                    TryReachIn();
                }
                return;
            }

            if (now < _phaseTimer) return;
            _window.TearBoard(_board);
            _board = -1;
            TryReachIn();
        }

        /// <summary>
        /// should_attack_player_thru_boards(): 50% of the time, swipe at every standing player
        /// within 90 units (about 2.3 m). Damage goes through LastStandDirector like any other hit.
        /// </summary>
        void TryReachIn()
        {
            if (Random.Range(0, 100) >= BarricadeState.ReachInChancePercent) return;
            var boot = GameBootstrap.Instance;
            if (boot == null) return;
            float reach = BarricadeState.TearReachUnits * MysteryBoxRules.UnitsToMeters;
            bool swung = false;
            foreach (var p in boot.Players)
            {
                if (p == null || p.IsDowned || p.IsSpectating) continue;
                Vector3 d = p.transform.position - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > reach * reach) continue;
                if (!swung && _hasAttack) animator.SetTrigger(AttackHash);
                swung = true;
                _director.NotifyAttack(this, p);
            }
        }

        void StartClimb(float now)
        {
            _window.ReleaseSpot(this);
            _phase = Phase.Climbing;
            _phaseTimer = now;
            _climbFrom = transform.position;
            _nav.enabled = false; // Scripted move through the window; the NavMesh doesn't link through it.
            if (_hasClimb) animator.SetTrigger(ClimbHash);
        }

        void Climb(float now)
        {
            float t = (now - _phaseTimer) / Mathf.Max(0.01f, _window.ClimbSeconds);
            Vector3 to = _window.InsidePoint;
            if (t < 1f)
            {
                Vector3 p = Vector3.Lerp(_climbFrom, to, t);
                p.y += Mathf.Sin(t * Mathf.PI) * 0.9f; // Up over the sill and down inside.
                transform.position = p;
                return;
            }

            transform.position = to;
            _nav.enabled = true;
            _nav.Warp(to);
            _nav.isStopped = false;
            _phase = Phase.Chasing;
            _window = null;
            _nextRepath = now;
            _lastDestination = new Vector3(float.MaxValue, 0f, 0f);
        }

        /// <summary>Gives back the attack spot and any half-pulled board (on death or recycling).</summary>
        void LeaveWindow()
        {
            if (_window == null) return;
            if (_board >= 0) _window.ReleaseBoard(_board);
            _window.ReleaseSpot(this);
            _board = -1;
            _window = null;
        }

        // ---------------- Chasing ----------------

        void Chase(float now)
        {
            if (now >= _nextRepath)
            {
                _nextRepath = now + repathInterval;
                Repath();
            }

            if (_target != null && now >= _nextAttack &&
                (_target.transform.position - transform.position).sqrMagnitude <= attackRange * attackRange)
            {
                _nextAttack = now + attackInterval;
                if (_hasAttack) animator.SetTrigger(AttackHash);
                _director.NotifyAttack(this, _target);
            }
        }

        void Repath()
        {
            // Dogs stay on their assigned player while that player is up; everyone else takes the nearest.
            _target = _favorite != null && !_favorite.IsDowned && !_favorite.IsSpectating
                ? _favorite
                : _director.NearestPlayer(transform.position);
            if (_target == null) { _nav.isStopped = true; return; }

            _nav.isStopped = false;
            Vector3 dest = _target.transform.position;
            // Skip SetDestination unless the player moved half a meter: pathfinding is the
            // most expensive thing a zombie does.
            if ((dest - _lastDestination).sqrMagnitude > 0.25f)
            {
                _lastDestination = dest;
                _nav.SetDestination(dest);
            }
        }

        /// <summary>
        /// round_spawn_failsafe(): every 30 s, a zombie that has moved less than 24 units
        /// (about 0.6 m) or fallen below the world is removed and put back in the queue.
        /// </summary>
        void Failsafe(float now)
        {
            var vars = _director.State.Vars;
            _nextFailsafe = now + vars.FailsafeIntervalSeconds;
            if (!vars.UseFailsafe) return;

            Vector3 pos = transform.position;
            float minMove = vars.FailsafeMinMoveUnits * MysteryBoxRules.UnitsToMeters;
            bool fell = pos.y < vars.BelowWorldUnits * MysteryBoxRules.UnitsToMeters;
            bool stuck = (pos - _failsafeOrigin).sqrMagnitude < minMove * minMove;
            // Standing next to a player swinging isn't stuck.
            bool attacking = _target != null &&
                (_target.transform.position - pos).sqrMagnitude <= attackRange * attackRange * 4f;

            if (fell || (stuck && !attacking))
            {
                IsAlive = false;
                LeaveWindow();
                _director.NotifyTimedOut(this);
                Despawn();
                return;
            }
            _failsafeOrigin = pos;
        }

        /// <summary>
        /// Weapons call this. A hit that leaves the zombie standing pays 10 points
        /// (zombie_damage); the killing hit pays the kill points instead (zombie_death_event).
        /// Pass attacker = null for kills that shouldn't pay, such as traps or debug.
        /// </summary>
        public void TakeDamage(int amount, ZombiePlayer attacker, HitZone zone, bool melee = false)
        {
            if (!IsAlive) return;

            // check_for_instakill(): during Insta-Kill any player hit kills (normal kill points).
            if (attacker != null && _director.State.InstaKill)
            {
                Kill(attacker, zone, melee);
                return;
            }

            Health -= amount;
            if (Health <= 0)
            {
                Kill(attacker, zone, melee);
                return;
            }

            var state = _director.State;
            if (attacker != null && attacker.Score != null)
                attacker.Score.Add(state.PointsForHit(), true, state.Intermission);
        }

        public void Kill(ZombiePlayer attacker, HitZone zone, bool melee = false)
        {
            if (!IsAlive) return;
            IsAlive = false;

            var state = _director.State;
            if (attacker != null && attacker.Score != null)
                attacker.Score.Add(state.PointsForKill(zone, melee), true, state.Intermission);

            LeaveWindow();
            _nav.enabled = false;
            foreach (var c in _colliders) c.enabled = false; // Corpses don't block bullets or players.
            if (_hasDie) animator.SetTrigger(DieHash);

            _director.NotifyKilled(this, attacker);
            StartCoroutine(CorpseRoutine());
        }

        IEnumerator CorpseRoutine()
        {
            yield return _corpseWait;
            Despawn();
        }

        void Despawn()
        {
            _nav.enabled = false;
            _pool.Release(this);
        }
    }
}
