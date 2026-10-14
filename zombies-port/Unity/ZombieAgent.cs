// ZombieAgent.cs
//
// One pooled zombie. Ports the parts of _zombiemode_spawner.gsc and _zombiemode.gsc that the
// round system depends on:
//   zombie_spawn_init / set_zombie_run_cycle : health and walk/run/sprint on spawn
//   find_flesh (simplified)                  : chase the nearest player on the NavMesh
//   zombie_damage / zombie_death_event       : 10 points per non-lethal hit, kill points on death
//   round_spawn_failsafe                     : stuck or fallen zombies are recycled
//
// Not ported yet: tearing down window barriers and climbing in, crawlers, gibbing, and
// dealing damage to the player (the attack is raised as an event for the player-health system).
//
// Prefab setup: NavMeshAgent + capsule Collider on the root, optional Animator with a float
// "Speed" (0 walk, 1 run, 2 sprint) and triggers "Attack" and "Die". Missing parameters are
// skipped, so a plain capsule works for testing. Bake a NavMesh (AI Navigation package).

using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace ZombiesPort
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class ZombieAgent : MonoBehaviour
    {
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

        public int Health { get; private set; }
        public bool IsAlive { get; private set; }
        public ZombieSpeed Speed { get; private set; }
        /// <summary>Game over: stands still and stops attacking.</summary>
        public bool IsFrozen { get; private set; }

        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int AttackHash = Animator.StringToHash("Attack");
        static readonly int DieHash = Animator.StringToHash("Die");

        ZombiePool _pool;
        RoundDirector _director;
        NavMeshAgent _nav;
        Collider[] _colliders;
        WaitForSeconds _corpseWait;
        bool _hasSpeed, _hasAttack, _hasDie;

        ZombiePlayer _target;
        Vector3 _lastDestination;
        float _nextRepath, _nextAttack, _nextFailsafe;
        Vector3 _failsafeOrigin;

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
                }
            }
        }

        /// <summary>Reset everything a previous life may have changed, then start chasing.</summary>
        internal void Activate(RoundDirector director, int health, ZombieSpeed speed)
        {
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

            if (now >= _nextFailsafe) Failsafe(now);
        }

        void Repath()
        {
            _target = _director.NearestPlayer(transform.position);
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
