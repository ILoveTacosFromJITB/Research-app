// Perks.cs
//
// Perk-a-Cola rules in plain C# (no engine types). Ported from maps/_zombiemode_perks.gsc:
//   vending_trigger_think() : prices, the 4-perk limit, power requirement, who may buy
//   give_perk()             : Juggernog raises max health to 160; solo Quick Revive grants a life
//   perk_think()            : every perk is lost when the player goes down
//   solo_revive_buy_trigger_move() : the solo Quick Revive machine is unavailable while its
//                             life is unused, and gone after 3 purchases (level.solo_lives_given)
//
// All eight BO1 perks are listed with their real prices so machines can be added later. The
// effects are implemented for Quick Revive and Juggernog.

using System;
using System.Collections.Generic;

namespace ZombiesPort
{
    public enum PerkId { QuickRevive, Juggernog, SpeedCola, DoubleTap, StaminUp, PhdFlopper, Deadshot, MuleKick }

    public enum PerkBuyResult
    {
        Ok,
        NoPower,          // &"ZOMBIE_NEED_POWER"
        AlreadyOwned,     // "perk_deny"
        NotEnoughPoints,  // "evt_perk_deny"
        PerkLimit,        // num_perks >= 4: the "sigh" line
        Busy,             // downed, reviving, already drinking
        Unavailable,      // solo Quick Revive: life unused, or all 3 bought
    }

    public static class PerkInfo
    {
        public const int MaxPerks = 4;                  // player.num_perks >= 4
        public const int JuggernogHealth = 160;         // zombie_perk_juggernaut_health
        public const int SoloQuickReviveLimit = 3;      // level.solo_lives_given >= 3
        /// <summary>
        /// Bottle animation length. Placeholder: the GSC waits for "weapon_change_complete" from
        /// the bottle weapon, whose timing lives in weapon files that aren't in the dump.
        /// </summary>
        public const float DrinkSeconds = 3f;

        /// <summary>The GSC perk name (SetPerk / HasPerk).</summary>
        public static string GscName(PerkId p)
        {
            switch (p)
            {
                case PerkId.QuickRevive: return "specialty_quickrevive";
                case PerkId.Juggernog: return "specialty_armorvest";
                case PerkId.SpeedCola: return "specialty_fastreload";
                case PerkId.DoubleTap: return "specialty_rof";
                case PerkId.StaminUp: return "specialty_longersprint";
                case PerkId.PhdFlopper: return "specialty_flakjacket";
                case PerkId.Deadshot: return "specialty_deadshot";
                default: return "specialty_additionalprimaryweapon";
            }
        }

        public static string DisplayName(PerkId p)
        {
            switch (p)
            {
                case PerkId.QuickRevive: return "Quick Revive";
                case PerkId.Juggernog: return "Juggernog";
                case PerkId.SpeedCola: return "Speed Cola";
                case PerkId.DoubleTap: return "Double Tap";
                case PerkId.StaminUp: return "Stamin-Up";
                case PerkId.PhdFlopper: return "PhD Flopper";
                case PerkId.Deadshot: return "Deadshot Daiquiri";
                default: return "Mule Kick";
            }
        }

        /// <summary>vending_trigger_think(): the price table. Solo Quick Revive is 500.</summary>
        public static int Cost(PerkId p, bool solo)
        {
            switch (p)
            {
                case PerkId.Juggernog: return 2500;
                case PerkId.QuickRevive: return solo ? 500 : 1500;
                case PerkId.SpeedCola: return 3000;
                case PerkId.DoubleTap: return 2000;
                case PerkId.StaminUp: return 2000;
                case PerkId.PhdFlopper: return 2000;
                case PerkId.Deadshot: return 1500;
                default: return 4000;
            }
        }

        /// <summary>Every machine waits for "&lt;perk&gt;_power_on", except Quick Revive in solo.</summary>
        public static bool NeedsPower(PerkId p, bool solo) => !(solo && p == PerkId.QuickRevive);
    }

    /// <summary>level.solo_lives_given: shared by the solo game, not per machine.</summary>
    public class SoloQuickReviveTracker
    {
        public int Purchases { get; private set; }
        public bool Exhausted => Purchases >= PerkInfo.SoloQuickReviveLimit;
        public void RecordPurchase() => Purchases++;
    }

    /// <summary>The perks one player holds.</summary>
    public class PerkSet
    {
        readonly List<PerkId> _owned = new List<PerkId>(PerkInfo.MaxPerks);
        public IReadOnlyList<PerkId> Owned => _owned;   // in purchase order, for the HUD row
        public int Count => _owned.Count;
        public bool Has(PerkId p) => _owned.Contains(p);

        public event Action<PerkId> Gained;
        public event Action<PerkId> Lost;

        /// <summary>
        /// The checks vending_trigger_think() makes before taking the money, in the same order.
        /// soloLivesUnused: the player still has an unspent solo self-revive.
        /// </summary>
        public PerkBuyResult CanBuy(PerkId p, int score, bool powered, bool busy, bool solo,
                                    SoloQuickReviveTracker soloTracker, bool soloLivesUnused)
        {
            if (PerkInfo.NeedsPower(p, solo) && !powered) return PerkBuyResult.NoPower;
            if (solo && p == PerkId.QuickRevive && (soloLivesUnused || (soloTracker != null && soloTracker.Exhausted)))
                return PerkBuyResult.Unavailable;
            if (busy) return PerkBuyResult.Busy;
            if (Has(p)) return PerkBuyResult.AlreadyOwned;
            if (score < PerkInfo.Cost(p, solo)) return PerkBuyResult.NotEnoughPoints;
            if (Count >= PerkInfo.MaxPerks) return PerkBuyResult.PerkLimit;
            return PerkBuyResult.Ok;
        }

        public void Give(PerkId p)
        {
            if (Has(p)) return;
            _owned.Add(p);
            Gained?.Invoke(p);
        }

        /// <summary>perk_think(): "player_downed" takes every perk away.</summary>
        public void LoseAll()
        {
            for (int i = _owned.Count - 1; i >= 0; i--)
            {
                var p = _owned[i];
                _owned.RemoveAt(i);
                Lost?.Invoke(p);
            }
        }
    }
}
