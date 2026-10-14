// PlayerHealth.cs
//
// Health, regeneration and last stand for one player, driving the plain-C# PlayerVitals.
//
// Going down: the player drops to the floor and can crawl and shoot their best pistol. Zombies
// stop chasing them. They bleed out after 45 s unless a teammate revives them.
//
// Reviving (_laststand.gsc revive_trigger_think / can_revive): a downed player is an
// IInteractable, so a teammate standing within about 1 m, facing them, with a clear line of sight,
// sees "Hold to Revive" on their interact button. Holding it for 3 s (1.5 s with Quick Revive)
// gets them up. Letting go resets the progress. The reviver can't move or shoot meanwhile.
//
// This works with any number of players in the scene. Until networking exists, the only way to
// see a revive in action is two local players or a test bot; in solo, a down ends the game
// unless the player has a self-revive (startingSoloLives, standing in for Solo Quick Revive).

using System;
using UnityEngine;

namespace ZombiesPort
{
    [RequireComponent(typeof(ZombiePlayer))]
    public class PlayerHealth : MonoBehaviour, IInteractable
    {
        [SerializeField] VitalsConfig config = new VitalsConfig();
        [Tooltip("Solo self-revives at the start. 0 matches BO1 without Quick Revive. Set 1 to test last stand solo.")]
        [SerializeField] int startingSoloLives = 0;
        [SerializeField] LayerMask lineOfSightMask = ~0;

        public PlayerVitals Vitals { get; private set; }
        public ZombiePlayer Player { get; private set; }
        public PlayerInteractor Interactor { get; private set; }
        /// <summary>The downed teammate this player is currently reviving (for their HUD bar).</summary>
        public PlayerHealth RevivingTarget { get; private set; }

        public event Action<PlayerHealth> Damaged;
        /// <summary>Raised as the player goes down, before the last-stand pistol comes out (perks are lost here).</summary>
        public event Action<PlayerHealth> GoingDown;
        public event Action<PlayerHealth> WentDown;
        public event Action<PlayerHealth> Revived;
        public event Action<PlayerHealth> BledOut;
        public event Action<PlayerHealth> Respawned;

        public Vector3 InteractPosition => transform.position;

        LastStandDirector _director;
        PlayerWeapon _weapon;
        CharacterController _cc;
        SphereCollider _reviveTrigger;
        PlayerHealth _reviver;

        void Awake()
        {
            Player = GetComponent<ZombiePlayer>();
            Interactor = GetComponent<PlayerInteractor>();
            _weapon = GetComponent<PlayerWeapon>();
            _cc = GetComponent<CharacterController>();
            Vitals = new PlayerVitals(config) { Lives = startingSoloLives };

            // revive_trigger_spawn(): a trigger around the downed player that teammates walk into.
            var go = new GameObject("ReviveTrigger");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 0.5f;
            _reviveTrigger = go.AddComponent<SphereCollider>();
            _reviveTrigger.isTrigger = true;
            _reviveTrigger.radius = config.ReviveRadiusUnits * MysteryBoxRules.UnitsToMeters + 0.3f; // + reviver's own radius
            _reviveTrigger.enabled = false;
        }

        internal void Init(LastStandDirector director) => _director = director;

        void Update()
        {
            switch (Vitals.Tick(Time.time, Time.deltaTime))
            {
                case VitalsEvent.Revived: FinishRevive(null); break;   // solo self-revive
                case VitalsEvent.BledOut: _director?.OnBledOut(this); break;
            }

            if (Vitals.State == LifeState.Downed && !Vitals.SelfReviving) UpdateTeamRevive();
        }

        // ---------------- Damage ----------------

        /// <summary>A zombie swung at this player (RoundDirector.PlayerAttacked).</summary>
        public void TakeZombieHit(ZombieAgent zombie)
        {
            if (_director != null && _director.IsGameOver) return;
            int damage = zombie != null ? zombie.MeleeDamage : config.ZombieHitDamage; // eAttacker.meleeDamage
            switch (Vitals.TakeHit(damage, zombie, Time.time))
            {
                case HitResult.Hurt: Damaged?.Invoke(this); break;
                case HitResult.Lethal: _director?.HandleLethalHit(this); break;
            }
        }

        // ---------------- State changes (called by LastStandDirector) ----------------

        internal void GoDown(int pointsLost, bool selfRevive, bool solo)
        {
            Vitals.GoDown(pointsLost, selfRevive);
            Player.IsDowned = true;
            StopReviving();
            _reviveTrigger.enabled = !selfRevive;
            GoingDown?.Invoke(this);   // Before the pistol swap, so losing Mule Kick takes the 3rd gun, not the pistol.
            if (_weapon != null) _weapon.EnterLastStand(solo);
            WentDown?.Invoke(this);
        }

        void FinishRevive(PlayerHealth reviver)
        {
            Player.IsDowned = false;
            _reviveTrigger.enabled = false;
            ClearReviver();
            if (_weapon != null) _weapon.ExitLastStand();
            if (reviver != null) _director?.AwardRevive(reviver, this);
            Revived?.Invoke(this);
        }

        internal void Spectate()
        {
            Player.IsDowned = false;
            Player.IsSpectating = true;
            _reviveTrigger.enabled = false;
            ClearReviver();
            if (_cc != null) _cc.enabled = false; // No collision or movement while waiting.
            BledOut?.Invoke(this);
        }

        internal void Respawn(Vector3 position, Quaternion rotation)
        {
            Vitals.Respawn();
            Player.IsSpectating = false;
            transform.SetPositionAndRotation(position, rotation);
            if (_cc != null) _cc.enabled = true;
            if (_weapon != null) _weapon.ResetLoadout(); // Bleeding out loses all weapons.
            Respawned?.Invoke(this);
        }

        // ---------------- Reviving ----------------

        void UpdateTeamRevive()
        {
            // Keep the current reviver while they're still valid; otherwise look for one.
            if (_reviver != null && !IsActivelyReviving(_reviver)) ClearReviver();
            if (_reviver == null && _director != null)
            {
                foreach (var p in _director.Players)
                    if (IsActivelyReviving(p)) { _reviver = p; break; }
            }

            if (_reviver != null)
            {
                _reviver.Player.IsReviving = true;
                _reviver.RevivingTarget = this;
            }

            bool done = Vitals.UpdateRevive(_reviver != null, _reviver != null && _reviver.Player.HasQuickRevive, Time.deltaTime);
            if (done) FinishRevive(_reviver);
        }

        /// <summary>is_reviving(): can_revive() and the interact button held, aimed at this player.</summary>
        bool IsActivelyReviving(PlayerHealth r) =>
            CanBeRevivedBy(r) && r.Interactor != null && r.Interactor.IsHolding &&
            ReferenceEquals(r.Interactor.Current, this);

        /// <summary>
        /// can_revive(): the reviver is standing (not downed or spectating), within reach, facing
        /// this player (dot > 0.9), with nothing solid in between.
        /// </summary>
        public bool CanBeRevivedBy(PlayerHealth r)
        {
            if (r == null || r == this || r.Vitals.State != LifeState.Alive) return false;
            if (Vitals.State != LifeState.Downed || Vitals.SelfReviving) return false;

            Vector3 to = transform.position - r.transform.position;
            to.y = 0f;
            float reach = _reviveTrigger.radius;
            if (to.sqrMagnitude > reach * reach) return false;

            Vector3 fwd = r.transform.forward;
            fwd.y = 0f;
            if (to.sqrMagnitude > 0.0001f && Vector3.Dot(fwd.normalized, to.normalized) <= config.ReviveFacingDot)
                return false;

            // SightTracePassed( reviver.origin + 50, revivee.origin + 30 ), in meters.
            Vector3 from = r.transform.position + Vector3.up * (50f * MysteryBoxRules.UnitsToMeters);
            Vector3 target = transform.position + Vector3.up * (30f * MysteryBoxRules.UnitsToMeters);
            if (Physics.Linecast(from, target, out var hit, lineOfSightMask, QueryTriggerInteraction.Ignore))
            {
                Transform t = hit.collider.transform;
                if (!t.IsChildOf(transform) && !t.IsChildOf(r.transform)) return false;
            }
            return true;
        }

        void StopReviving()
        {
            if (RevivingTarget != null) RevivingTarget.ClearReviverIf(this);
            Player.IsReviving = false;
            RevivingTarget = null;
        }

        void ClearReviverIf(PlayerHealth r)
        {
            if (_reviver == r) ClearReviver();
        }

        void ClearReviver()
        {
            if (_reviver != null)
            {
                _reviver.Player.IsReviving = false;
                _reviver.RevivingTarget = null;
            }
            _reviver = null;
        }

        // ---------------- IInteractable (what the reviver's button shows) ----------------

        public InteractPrompt GetPrompt(ZombiePlayer player)
        {
            var reviver = player != null ? player.GetComponent<PlayerHealth>() : null;
            if (!CanBeRevivedBy(reviver)) return InteractPrompt.Hidden;
            return new InteractPrompt
            {
                Visible = true,
                Label = "Hold to Revive", // &"GAME_BUTTON_TO_REVIVE_PLAYER"
                Affordable = true,
                Progress = _reviver == reviver ? Vitals.ReviveProgress : 0f,
            };
        }

        // Reviving is driven by holding the button (PlayerInteractor.IsHolding), not the tap.
        public InteractResult Interact(ZombiePlayer player) =>
            CanBeRevivedBy(player != null ? player.GetComponent<PlayerHealth>() : null)
                ? InteractResult.Done : InteractResult.Unavailable;
    }
}
