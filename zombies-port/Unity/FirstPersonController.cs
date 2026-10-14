// FirstPersonController.cs
//
// Moves and turns the player from MobileInput. Uses a CharacterController (not a Rigidbody):
// cheaper on mobile, no physics jitter, and it still triggers the Mystery Box's trigger collider.
//
// Speeds are in meters per second. BO1 moved players at roughly 190 units/s (about 4.8 m/s);
// the defaults are close to that and meant to be tuned by feel.
//
// Aim assist: BO1 on console slowed the aim when the crosshair crossed a zombie ("aim
// friction"). Touch aiming needs it even more, so it's built in: one sphere cast per frame.

using System;
using UnityEngine;

namespace ZombiesPort
{
    [RequireComponent(typeof(CharacterController), typeof(ZombiePlayer))]
    public class FirstPersonController : MonoBehaviour
    {
        [SerializeField] MobileInput input;
        [Tooltip("Child at eye height that holds the camera. Pitch (looking up and down) rotates this.")]
        [SerializeField] Transform cameraPivot;
        [SerializeField] PlayerWeapon weapon;

        [Header("Movement (m/s)")]
        [SerializeField] float walkSpeed = 4.6f;
        [SerializeField] float sprintMultiplier = 1.45f;
        [SerializeField] float aimMoveMultiplier = 0.6f;
        [SerializeField] float gravity = -20f;

        [Header("Last stand")]
        [Tooltip("Downed players can crawl slowly while shooting their pistol.")]
        [SerializeField] float downedCrawlSpeed = 0.8f;
        [Tooltip("Camera height while downed (meters above the feet).")]
        [SerializeField] float downedEyeHeight = 0.55f;
        [SerializeField] float eyeHeightChangeSpeed = 3f;

        [Header("Look")]
        [SerializeField] float maxPitch = 85f;
        [Tooltip("Slower turning while aiming down sights, for precision.")]
        [SerializeField] float aimLookMultiplier = 0.55f;

        [Header("Aim assist")]
        [SerializeField] bool aimFriction = true;
        [SerializeField] float frictionMultiplier = 0.5f;
        [SerializeField] float frictionRange = 30f;
        [SerializeField] float frictionRadius = 0.35f;
        [SerializeField] LayerMask frictionMask = ~0;

        CharacterController _cc;
        ZombiePlayer _player;
        float _pitch;
        float _verticalVelocity;
        float _standingEyeHeight;
        bool _airborne;
        float _fallPeak;
        PlayerPerks _perks;
        RoundDirector _rounds;
        bool _wasAiming;
        ZombieAgent _snapTarget;
        float _snapLeft;

        public bool IsSprinting { get; private set; }
        /// <summary>Sprint time left. Stamin-Up doubles it (PlayerPerks).</summary>
        public SprintStamina Stamina { get; } = new SprintStamina();
        /// <summary>Touched the ground after falling this many meters (PhD Flopper listens).</summary>
        public event Action<float> Landed;

        [Header("Deadshot")]
        [Tooltip("Only zombies this close are snapped to when aiming with Deadshot.")]
        [SerializeField] float deadshotRange = 30f;
        [SerializeField] float deadshotSnapSeconds = 0.1f;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _player = GetComponent<ZombiePlayer>();
            if (input == null) input = FindFirstObjectByType<MobileInput>();
            if (weapon == null) weapon = GetComponent<PlayerWeapon>();
            _perks = GetComponent<PlayerPerks>();
            if (cameraPivot == null && Camera.main != null) cameraPivot = Camera.main.transform;
            if (cameraPivot != null) _standingEyeHeight = cameraPivot.localPosition.y;
        }

        void Update()
        {
            if (input == null) return;

            UpdateEyeHeight();
            Look();
            if (_player.IsSpectating) { IsSprinting = false; return; } // Bled out: look around only.
            Move();
        }

        void Look()
        {
            Vector2 delta = input.ConsumeLook();
            float scale = 1f;
            if (weapon != null && weapon.AimAmount > 0f) scale *= Mathf.Lerp(1f, aimLookMultiplier, weapon.AimAmount);
            if (aimFriction && CrosshairOnZombie()) scale *= frictionMultiplier;

            transform.Rotate(0f, delta.x * scale, 0f, Space.Self);
            _pitch = Mathf.Clamp(_pitch - delta.y * scale, -maxPitch, maxPitch);
            DeadshotSnap();
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        /// <summary>
        /// Deadshot Daiquiri: starting to aim down sights pulls the view onto the head of the zombie
        /// nearest the crosshair (within 12 degrees and 30 m, in plain sight), over 0.1 s.
        /// </summary>
        void DeadshotSnap()
        {
            bool aiming = weapon != null && weapon.IsAiming;
            bool started = aiming && !_wasAiming;
            _wasAiming = aiming;
            if (!aiming || cameraPivot == null || _perks == null || !_perks.HasDeadshot) { _snapLeft = 0f; return; }

            if (started)
            {
                _snapTarget = FindDeadshotTarget();
                _snapLeft = _snapTarget != null ? deadshotSnapSeconds : 0f;
            }
            if (_snapLeft <= 0f) return;
            if (_snapTarget == null || !_snapTarget.IsAlive) { _snapLeft = 0f; return; }

            Vector3 dir = _snapTarget.HeadPosition - cameraPivot.position;
            float targetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float targetPitch = Mathf.Clamp(-Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg, -maxPitch, maxPitch);
            float t = Mathf.Clamp01(Time.deltaTime / _snapLeft);
            Vector3 e = transform.eulerAngles;
            e.y = Mathf.LerpAngle(e.y, targetYaw, t);
            transform.eulerAngles = e;
            _pitch = Mathf.Lerp(_pitch, targetPitch, t);
            _snapLeft -= Time.deltaTime;
        }

        ZombieAgent FindDeadshotTarget()
        {
            if (_rounds == null) _rounds = FindFirstObjectByType<RoundDirector>();
            if (_rounds == null) return null;
            Vector3 eye = cameraPivot.position;
            Vector3 forward = cameraPivot.forward;
            ZombieAgent best = null;
            float bestAngle = PerkEffects.DeadshotSnapDegrees;
            var alive = _rounds.Alive;
            for (int i = 0; i < alive.Count; i++)
            {
                var z = alive[i];
                if (z == null || !z.IsAlive) continue;
                Vector3 head = z.HeadPosition;
                Vector3 to = head - eye;
                if (to.sqrMagnitude > deadshotRange * deadshotRange) continue;
                float angle = Vector3.Angle(forward, to);
                if (angle > bestAngle) continue;
                // In plain sight: the first thing between the eye and the head is this zombie (or nothing).
                if (Physics.Linecast(eye, head, out var hit, frictionMask, QueryTriggerInteraction.Ignore) &&
                    !hit.collider.transform.IsChildOf(transform) && hit.collider.GetComponentInParent<ZombieAgent>() != z)
                    continue;
                best = z;
                bestAngle = angle;
            }
            return best;
        }

        /// <summary>Drops the camera toward the floor while downed, and back up when revived.</summary>
        void UpdateEyeHeight()
        {
            if (cameraPivot == null) return;
            float target = _player.IsDowned ? downedEyeHeight : _standingEyeHeight;
            Vector3 p = cameraPivot.localPosition;
            if (Mathf.Approximately(p.y, target)) return;
            p.y = Mathf.MoveTowards(p.y, target, eyeHeightChangeSpeed * Time.deltaTime);
            cameraPivot.localPosition = p;
        }

        bool CrosshairOnZombie()
        {
            if (cameraPivot == null) return false;
            if (!Physics.SphereCast(cameraPivot.position, frictionRadius, cameraPivot.forward, out var hit,
                    frictionRange, frictionMask, QueryTriggerInteraction.Ignore))
                return false;
            var z = hit.collider.GetComponentInParent<ZombieAgent>();
            return z != null && z.IsAlive;
        }

        void Move()
        {
            Vector2 stick = input.Move;
            bool aiming = weapon != null && weapon.IsAiming;
            bool firing = input.FireHeld;
            bool downed = _player.IsDowned;
            bool wantsSprint = input.Sprint && stick.sqrMagnitude > 0.01f && !aiming && !firing && !downed &&
                               !_player.IsDrinkingPerk && !_player.IsReviving;
            IsSprinting = Stamina.Tick(Time.deltaTime, wantsSprint); // Runs out; Stamin-Up doubles it.

            float speed = walkSpeed;
            if (weapon != null && weapon.Current != null) speed *= weapon.Current.Stats.MoveSpeedScale;
            if (downed) speed = downedCrawlSpeed;
            else if (_player.IsReviving) speed = 0f;            // Reviving roots you in place, as in BO1.
            else if (IsSprinting) speed *= sprintMultiplier;
            else if (aiming) speed *= aimMoveMultiplier;

            Vector3 planar = (transform.right * stick.x + transform.forward * stick.y) * speed;

            if (_cc.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f; // Keep grounded on slopes.
            _verticalVelocity += gravity * Time.deltaTime;

            _cc.Move((planar + Vector3.up * _verticalVelocity) * Time.deltaTime);
            TrackFall();
        }

        /// <summary>Measures each drop from its highest point and reports the landing.</summary>
        void TrackFall()
        {
            float y = transform.position.y;
            if (!_cc.isGrounded)
            {
                if (!_airborne) { _airborne = true; _fallPeak = y; }
                else if (y > _fallPeak) _fallPeak = y;
                return;
            }
            if (!_airborne) return;
            _airborne = false;
            float fall = _fallPeak - y;
            if (fall > 0.1f) Landed?.Invoke(fall);
        }
    }
}
