// ZombiePlayer.cs
//
// Minimal player component that the Mystery Box (and later wall buys, perks and doors) talks to.
// It gathers the per-player state GSC kept on the player entity: score, weapons, and the
// flags can_buy_weapon() checks (is_drinking, in_revive_trigger, laststand).

using System;
using UnityEngine;

namespace ZombiesPort
{
    public class ZombiePlayer : MonoBehaviour
    {
        [SerializeField] string startingPistol = "m1911_zm"; // level.laststandpistol

        public PlayerScore Score { get; private set; }
        public WeaponInventory Weapons { get; private set; }

        // Set by the perk, revive and last-stand systems.
        [NonSerialized] public bool IsDrinkingPerk;
        [NonSerialized] public bool IsReviving;
        [NonSerialized] public bool IsDowned;
        [NonSerialized] public bool IsSpectating;      // bled out; back next round
        [NonSerialized] public bool HasQuickRevive;     // set by the perk system once it exists

        public bool CanBuyWeapon =>
            Score != null && !IsSpectating &&
            MysteryBoxRules.CanBuyWeapon(IsDrinkingPerk, IsReviving, IsDowned, Weapons.Current);

        void Awake()
        {
            Weapons = new WeaponInventory(startingPistol);
        }

        /// <summary>Called by GameBootstrap after ZombieGameState.Initialize() creates the scores.</summary>
        public void Bind(PlayerScore score) => Score = score;
    }
}
