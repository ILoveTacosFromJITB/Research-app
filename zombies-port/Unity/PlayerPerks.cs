// PlayerPerks.cs
//
// The perks one player holds, buying (with the drink), and what each perk does.
// Ports from _zombiemode_perks.gsc:
//   vending_trigger_think() : pay, then drink; the perk is only granted when the bottle is
//                             finished, and going down mid-drink loses it (the points are gone too)
//   perk_give_bottle_begin(): no aiming, sprinting, knifing or shooting while drinking
//   give_perk()             : Juggernog sets max health to 160. Quick Revive halves revive time;
//                             in solo it also grants one self-revive (self.lives = 1)
//   perk_think()            : going down loses every perk (Juggernog's health goes back to 100)

using System;
using System.Collections;
using UnityEngine;

namespace ZombiesPort
{
    [RequireComponent(typeof(ZombiePlayer))]
    public class PlayerPerks : MonoBehaviour
    {
        public PerkSet Perks { get; } = new PerkSet();
        public ZombiePlayer Player { get; private set; }
        public bool IsDrinking => Player.IsDrinkingPerk;

        /// <summary>Raised when the bottle comes out (for the drinking view model and sound).</summary>
        public event Action<PerkId> StartedDrinking;

        PlayerHealth _health;
        PlayerWeapon _weapon;
        Coroutine _drink;

        static ZombieGameState State => GameBootstrap.Instance != null ? GameBootstrap.Instance.State : null;
        static bool Solo => GameBootstrap.Instance == null || GameBootstrap.Instance.Players.Count <= 1;

        void Awake()
        {
            Player = GetComponent<ZombiePlayer>();
            _health = GetComponent<PlayerHealth>();
            _weapon = GetComponent<PlayerWeapon>();
            Perks.Gained += ApplyPerk;
            Perks.Lost += RemovePerk;
        }

        void OnEnable()
        {
            if (_health != null) _health.WentDown += OnWentDown;
        }

        void OnDisable()
        {
            if (_health != null) _health.WentDown -= OnWentDown;
        }

        // ---------------- Buying ----------------

        public PerkBuyResult CanBuy(PerkId perk, bool powered)
        {
            var state = State;
            if (Player.Score == null || state == null) return PerkBuyResult.Busy;
            bool busy = Player.IsDowned || Player.IsReviving || Player.IsDrinkingPerk || Player.IsSpectating;
            bool soloLivesUnused = _health != null && _health.Vitals.Lives > 0;
            return Perks.CanBuy(perk, Player.Score.Score, powered, busy, Solo, state.SoloQuickRevive, soloLivesUnused);
        }

        /// <summary>Pays and starts drinking. The perk arrives when the bottle is finished.</summary>
        public PerkBuyResult TryBuy(PerkId perk, bool powered)
        {
            var result = CanBuy(perk, powered);
            if (result != PerkBuyResult.Ok) return result;

            var state = State;
            if (!Player.Score.TrySpend(PerkInfo.Cost(perk, Solo), state.Intermission)) return PerkBuyResult.NotEnoughPoints;
            _drink = StartCoroutine(Drink(perk));
            return PerkBuyResult.Ok;
        }

        IEnumerator Drink(PerkId perk)
        {
            Player.IsDrinkingPerk = true;            // increment_is_drinking(): weapon lowered
            if (_weapon != null && _weapon.Current != null) _weapon.Current.CancelReload();
            StartedDrinking?.Invoke(perk);

            for (float t = 0f; t < PerkInfo.DrinkSeconds; t += Time.deltaTime)
            {
                if (Player.IsDowned) { Player.IsDrinkingPerk = false; _drink = null; yield break; }
                yield return null;
            }

            Player.IsDrinkingPerk = false;
            _drink = null;
            var state = State;
            if (Player.IsDowned || (state != null && state.Intermission)) yield break;
            Perks.Give(perk);
        }

        // ---------------- Effects ----------------

        void ApplyPerk(PerkId perk)
        {
            switch (perk)
            {
                case PerkId.Juggernog:
                    if (_health != null) _health.Vitals.SetMaxHealth(PerkInfo.JuggernogHealth);
                    break;

                case PerkId.QuickRevive:
                    Player.HasQuickRevive = true;    // PlayerHealth halves revive time for this reviver
                    if (Solo && _health != null)
                    {
                        _health.Vitals.Lives = 1;    // self.lives = 1
                        State?.SoloQuickRevive.RecordPurchase();
                    }
                    break;

                // Speed Cola, Double Tap, Stamin-Up, PhD Flopper, Deadshot and Mule Kick come next.
            }
        }

        void RemovePerk(PerkId perk)
        {
            switch (perk)
            {
                case PerkId.Juggernog:
                    if (_health != null) _health.Vitals.SetMaxHealth(_health.Vitals.Config.MaxHealth); // SetMaxHealth( 100 )
                    break;
                case PerkId.QuickRevive:
                    Player.HasQuickRevive = false;
                    break;
            }
        }

        /// <summary>perk_think() "player_downed": a half-drunk bottle is wasted and every perk is lost.</summary>
        void OnWentDown(PlayerHealth h)
        {
            if (_drink != null) { StopCoroutine(_drink); _drink = null; }
            Player.IsDrinkingPerk = false;           // clear_is_drinking()
            Perks.LoseAll();
        }
    }
}
