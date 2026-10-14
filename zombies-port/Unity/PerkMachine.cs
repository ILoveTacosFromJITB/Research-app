// PerkMachine.cs
//
// A Perk-a-Cola machine, bought through the interact button. Port of vending_trigger_think()
// and check_player_has_perk() from _zombiemode_perks.gsc. The button shows:
//   - "Power Required"           until the power switch is on (solo Quick Revive needs none)
//   - "Juggernog  2500"          when buyable; the cost turns red if the player can't afford it
//   - nothing                    if the player already has it, is downed, drinking or reviving
// A tap pays, plays the jingle and starts the drink (PlayerPerks).
//
// Solo Quick Revive (solo_revive_buy_trigger_move): unavailable while its self-revive is unused,
// and after the third purchase is used up, the machine disappears for the rest of the game.
//
// Setup: put this on the machine with a trigger collider about 2 m around the front. Assign
// the visible model so it can be hidden when solo Quick Revive is gone.

using UnityEngine;

namespace ZombiesPort
{
    public class PerkMachine : MonoBehaviour, IInteractable
    {
        [SerializeField] PerkId perk = PerkId.Juggernog;
        [Tooltip("Off = always powered (useful for testing).")]
        [SerializeField] bool requirePower = true;
        [Tooltip("Hidden when the solo Quick Revive machine is used up.")]
        [SerializeField] GameObject machineModel;
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip jingle;
        [SerializeField] AudioClip deny;

        public PerkId Perk => perk;
        public Vector3 InteractPosition => transform.position;

        PowerSwitch _power;
        bool _gone;
        float _nextGoneCheck;

        bool Powered => !requirePower || _power == null || _power.IsOn;
        static bool Solo => GameBootstrap.Instance == null || GameBootstrap.Instance.Players.Count <= 1;

        void Start()
        {
            _power = FindFirstObjectByType<PowerSwitch>();
            if (requirePower && _power == null)
                Debug.Log("PerkMachine (" + PerkInfo.DisplayName(perk) + "): no PowerSwitch in the scene, so the power counts as on.", this);
        }

        void Update()
        {
            // The solo Quick Revive machine leaves after its third self-revive has been used.
            if (_gone || perk != PerkId.QuickRevive || !Solo || Time.time < _nextGoneCheck) return;
            _nextGoneCheck = Time.time + 0.5f;
            var boot = GameBootstrap.Instance;
            if (boot == null || !boot.State.SoloQuickRevive.Exhausted) return;
            foreach (var p in boot.Players)
            {
                var h = p != null ? p.GetComponent<PlayerHealth>() : null;
                if (h != null && h.Vitals.Lives > 0) return; // Still holding the last life.
            }
            _gone = true;
            if (machineModel != null) machineModel.SetActive(false);
        }

        public InteractPrompt GetPrompt(ZombiePlayer player)
        {
            if (_gone || player == null || player.IsDowned || player.IsSpectating) return InteractPrompt.Hidden;
            var perks = player.GetComponent<PlayerPerks>();
            if (perks == null || perks.Perks.Has(perk)) return InteractPrompt.Hidden;

            if (PerkInfo.NeedsPower(perk, Solo) && !Powered)
                return new InteractPrompt { Visible = true, Label = "Power Required", Affordable = false };

            var result = perks.CanBuy(perk, Powered);
            if (result == PerkBuyResult.Busy || result == PerkBuyResult.Unavailable) return InteractPrompt.Hidden;

            int cost = PerkInfo.Cost(perk, Solo);
            return new InteractPrompt
            {
                Visible = true,
                Label = PerkInfo.DisplayName(perk),
                Cost = cost,
                Affordable = result == PerkBuyResult.Ok,
            };
        }

        public InteractResult Interact(ZombiePlayer player)
        {
            var perks = player != null ? player.GetComponent<PlayerPerks>() : null;
            if (_gone || perks == null) return InteractResult.Unavailable;

            switch (perks.TryBuy(perk, Powered))
            {
                case PerkBuyResult.Ok:
                    Play(jingle);
                    return InteractResult.Done;
                case PerkBuyResult.NotEnoughPoints:
                    Play(deny);
                    return InteractResult.NotEnoughPoints;
                case PerkBuyResult.PerkLimit:
                case PerkBuyResult.NoPower:
                case PerkBuyResult.AlreadyOwned:
                    Play(deny);
                    return InteractResult.Denied;
                default:
                    return InteractResult.Unavailable;
            }
        }

        void Play(AudioClip clip)
        {
            if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
        }
    }
}
