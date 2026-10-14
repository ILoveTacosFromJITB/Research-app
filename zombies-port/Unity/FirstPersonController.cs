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

        public bool IsSprinting { get; private set; }

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _player = GetComponent<ZombiePlayer>();
            if (input == null) input = FindFirstObjectByType<MobileInput>();
            if (weapon == null) weapon = GetComponent<PlayerWeapon>();
            if (cameraPivot == null && Camera.main != null) cameraPivot = Camera.main.transform;
        }

        void Update()
        {
            if (input == null) return;
            if (_player.IsDowned) { IsSprinting = false; return; } // Last stand will take over here.

            Look();
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
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
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
            IsSprinting = input.Sprint && !aiming && !firing && !_player.IsDrinkingPerk;

            float speed = walkSpeed;
            if (weapon != null && weapon.Current != null) speed *= weapon.Current.Stats.MoveSpeedScale;
            if (IsSprinting) speed *= sprintMultiplier;
            else if (aiming) speed *= aimMoveMultiplier;

            Vector3 planar = (transform.right * stick.x + transform.forward * stick.y) * speed;

            if (_cc.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f; // Keep grounded on slopes.
            _verticalVelocity += gravity * Time.deltaTime;

            _cc.Move((planar + Vector3.up * _verticalVelocity) * Time.deltaTime);
        }
    }
}
