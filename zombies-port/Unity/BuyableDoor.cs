// BuyableDoor.cs
//
// A door or debris pile that players pay to open. Port of door_init(), door_think(), door_buy(),
// door_opened(), door_activate() and debris_think() from _zombiemode_blockers.gsc:
//   - "Open Door  1000" / "Clear Debris  1250" on the interact button; the buyer pays
//   - Electric doors open by themselves, free, when the power comes on; electric buyable
//     doors show "Power Required" until then
//   - Opening sets the door's flag(s) on the ZoneManager, which opens the zones behind it
//   - Each piece stops blocking at once, then slides or swings open over 1 s; debris pieces
//     disappear when done. Any NavMeshObstacle on a piece is switched off so zombies can path through.
//
// Setup: trigger colliders on both sides of the doorway (children are fine), this component on
// the parent, and the door or debris pieces listed under Pieces with how each one moves.
// For the NavMesh: bake with the doorway open, and give each piece a NavMeshObstacle with Carve
// on, so the closed door blocks zombies until it's bought.

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace ZombiesPort
{
    public class BuyableDoor : MonoBehaviour, IInteractable
    {
        [Serializable]
        public class Piece
        {
            public Transform transform;
            [Tooltip("script_vector: how far it slides, in its parent's local space (meters).")]
            public Vector3 moveBy = new Vector3(0f, 3f, 0f);
            [Tooltip("script_angles: how far it swings (degrees).")]
            public Vector3 rotateBy;
        }

        [SerializeField] DoorKind kind = DoorKind.Buyable;
        [SerializeField] int cost = DoorRules.DefaultCost;
        [Tooltip("Debris says \"Clear Debris\" and its pieces vanish after moving.")]
        [SerializeField] bool isDebris;
        [Tooltip("script_flag: the flag(s) this sets, comma separated (e.g. vip_to_dining).")]
        [SerializeField] string flags = "door_room2";
        [SerializeField] Piece[] pieces = new Piece[0];
        [SerializeField] float openSeconds = DoorRules.OpenSeconds;
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip openSound;
        [SerializeField] AudioClip denySound;

        public bool IsOpen { get; private set; }
        public int Cost => cost;
        public Vector3 InteractPosition => transform.position;
        public event Action<BuyableDoor> Opened;

        ZoneManager _zones;
        PowerSwitch _power;

        bool Powered => _power == null || _power.IsOn;

        void Start()
        {
            _zones = FindFirstObjectByType<ZoneManager>();
            _power = FindFirstObjectByType<PowerSwitch>();
            if (kind == DoorKind.Electric)
            {
                if (_power == null) Open();                  // No switch in the scene: treat as powered.
                else _power.PoweredOn += OnPowerOn;
            }
        }

        void OnDestroy()
        {
            if (_power != null) _power.PoweredOn -= OnPowerOn;
        }

        void OnPowerOn() => Open();                          // electric_door: flag_wait( "power_on" )

        static bool CanUse(ZombiePlayer p) =>
            p != null && !p.IsDowned && !p.IsSpectating && !p.IsReviving && p.Score != null; // is_player_valid, not in a revive

        public InteractPrompt GetPrompt(ZombiePlayer player)
        {
            if (IsOpen || (kind == DoorKind.Electric && Powered) || !CanUse(player)) return InteractPrompt.Hidden;
            if (kind != DoorKind.Buyable && !Powered)
                return new InteractPrompt { Visible = true, Label = "Power Required", Affordable = false }; // &"ZOMBIE_NEED_POWER"

            return new InteractPrompt
            {
                Visible = true,
                Label = isDebris ? "Clear Debris" : "Open Door",
                Cost = cost,
                Affordable = player.Score.Score >= cost,
            };
        }

        public InteractResult Interact(ZombiePlayer player)
        {
            if (IsOpen || !CanUse(player)) return InteractResult.Unavailable;
            if (kind != DoorKind.Buyable && !Powered) return InteractResult.Denied;
            if (kind == DoorKind.Electric) return InteractResult.Unavailable;

            var state = GameBootstrap.Instance != null ? GameBootstrap.Instance.State : null;
            bool intermission = state != null && state.Intermission;
            if (!DoorRules.CanBuy(kind, Powered, IsOpen, player.Score.Score, cost) ||
                !player.Score.TrySpend(cost, intermission))
            {
                Play(denySound);                             // "no_purchase", "door_deny"
                return InteractResult.NotEnoughPoints;
            }
            Open();
            return InteractResult.Done;
        }

        /// <summary>door_opened(): set the flags, then open every piece.</summary>
        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;

            foreach (var f in DoorRules.ParseFlags(flags))
            {
                if (_zones != null) _zones.SetFlag(f);
                else Debug.LogWarning("BuyableDoor: no ZoneManager in the scene, so flag \"" + f + "\" opens no zones.", this);
            }

            Play(openSound);
            foreach (var p in pieces)
                if (p != null && p.transform != null) StartCoroutine(OpenPiece(p));

            // The interaction triggers aren't needed any more.
            foreach (var c in GetComponentsInChildren<Collider>()) if (c.isTrigger) c.enabled = false;
            Opened?.Invoke(this);
        }

        /// <summary>door_activate(): NotSolid() and ConnectPaths() at once, then the move.</summary>
        IEnumerator OpenPiece(Piece p)
        {
            Transform t = p.transform;
            foreach (var c in t.GetComponentsInChildren<Collider>()) c.enabled = false;
            foreach (var o in t.GetComponentsInChildren<NavMeshObstacle>()) o.enabled = false;

            Vector3 fromPos = t.localPosition, toPos = fromPos + p.moveBy;
            Quaternion fromRot = t.localRotation, toRot = fromRot * Quaternion.Euler(p.rotateBy);
            // MoveTo( ..., time, time * 0.25, time * 0.25 ): ease in and out.
            for (float e = 0f; e < openSeconds; e += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, e / openSeconds);
                t.localPosition = Vector3.Lerp(fromPos, toPos, k);
                t.localRotation = Quaternion.Slerp(fromRot, toRot, k);
                yield return null;
            }
            t.localPosition = toPos;
            t.localRotation = toRot;
            if (isDebris) t.gameObject.SetActive(false);     // debris_move(): junk deleted at the end
        }

        void Play(AudioClip clip)
        {
            if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
        }
    }
}
