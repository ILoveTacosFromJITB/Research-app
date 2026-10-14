// PowerUps.cs
//
// Power-up rules in plain C# (no engine types). Ported from maps/_zombiemode_powerups.gsc:
//   watch_for_drop()     : a drop is "earned" each time the team's total points pass a threshold
//                          that starts at (players x 500) + 2000 and grows by 14% each time
//   powerup_drop()       : on a player's kill, at most 4 drops a round; 3% of kills drop anyway,
//                          the rest only if a drop has been earned
//   get_valid_powerup()  : a shuffled deck, reshuffled when used up; Carpenter needs 5 fully
//                          broken windows, Fire Sale needs the box to have moved and no sale running
//   powerup_timeout()    : 15 s solid, then 40 blinks that speed up (11.5 s), then gone
//   insta_kill / double_points / fire_sale : 30 s each; grabbing another resets it to 30 s
//
// The six power-ups Kino der Toten includes (include_powerups()) are implemented.

using System;
using System.Collections.Generic;

namespace ZombiesPort
{
    public enum PowerUpId { Nuke, InstaKill, DoublePoints, MaxAmmo, Carpenter, FireSale }

    public static class PowerUpInfo
    {
        public const float GrabRadiusUnits = 64f;       // powerup_grab(): range_squared = 64 * 64
        public const float SolidSeconds = 15f;          // powerup_timeout(): wait 15
        public const int BlinkToggles = 40;
        public const float TimedSeconds = 30f;          // zombie_powerup_*_time = 30
        public const float NukeDelay = 0.5f;            // nuke_powerup(): wait( 0.5 )
        public const float NukeKillGapMin = 0.1f;       // randomfloatrange( 0.1, 0.7 ) between kills
        public const float NukeKillGapMax = 0.7f;
        public const int NukePoints = 400;
        public const int CarpenterPoints = 200;
        public const int CarpenterMinBrokenWindows = 5; // get_num_window_destroyed() < 5 skips it
        public const int FireSaleBoxCost = 10;
        public const int MaxDropsPerRound = 4;          // zombie_powerup_drop_max_per_round
        public const int RandomDropPercent = 3;         // randomint(100) <= 2
        public const float FirstDropIncrement = 2000f;  // zombie_powerup_drop_increment
        public const float IncrementGrowth = 1.14f;

        /// <summary>The blink delay after toggle i: 15 at 0.5 s, 10 at 0.25 s, then 0.1 s.</summary>
        public static float BlinkDelay(int i) => i < 15 ? 0.5f : i < 25 ? 0.25f : 0.1f;

        /// <summary>Total time on the ground: 15 + 7.5 + 2.5 + 1.5 = 26.5 s.</summary>
        public static float Lifetime
        {
            get
            {
                float t = SolidSeconds;
                for (int i = 0; i < BlinkToggles; i++) t += BlinkDelay(i);
                return t;
            }
        }

        public static bool IsTimed(PowerUpId p) =>
            p == PowerUpId.InstaKill || p == PowerUpId.DoublePoints || p == PowerUpId.FireSale;

        public static string DisplayName(PowerUpId p)
        {
            switch (p)
            {
                case PowerUpId.Nuke: return "Kaboom!";
                case PowerUpId.InstaKill: return "Insta-Kill";
                case PowerUpId.DoublePoints: return "Double Points";
                case PowerUpId.MaxAmmo: return "Max Ammo!";
                case PowerUpId.Carpenter: return "Carpenter";
                default: return "Fire Sale";
            }
        }

        /// <summary>Kino der Toten's include_powerups().</summary>
        public static readonly PowerUpId[] Kino =
            { PowerUpId.Nuke, PowerUpId.InstaKill, PowerUpId.DoublePoints, PowerUpId.MaxAmmo, PowerUpId.Carpenter, PowerUpId.FireSale };
    }

    /// <summary>What get_valid_powerup() checks before letting a power-up drop.</summary>
    public struct PowerUpConditions
    {
        public int FullyBrokenWindows;
        public bool FireSaleActive;
        public int ChestMoves;

        public bool Allows(PowerUpId p)
        {
            if (p == PowerUpId.Carpenter) return FullyBrokenWindows >= PowerUpInfo.CarpenterMinBrokenWindows;
            if (p == PowerUpId.FireSale) return !FireSaleActive && ChestMoves >= 1;
            return true;
        }
    }

    /// <summary>When power-ups drop and which one (watch_for_drop + powerup_drop + get_valid_powerup).</summary>
    public class PowerUpDropper
    {
        readonly List<PowerUpId> _deck;
        readonly Random _rng;
        int _index;

        public float Increment { get; private set; } = PowerUpInfo.FirstDropIncrement;
        public float ScoreToDrop { get; private set; }
        /// <summary>level.zombie_vars["zombie_drop_item"]: a drop has been earned by points.</summary>
        public bool DropEarned { get; private set; }
        public int DropsThisRound { get; private set; }

        public PowerUpDropper(int players, int startingPoints, IEnumerable<PowerUpId> included, int? seed = null)
        {
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
            _deck = new List<PowerUpId>(included);
            Shuffle();
            ScoreToDrop = players * startingPoints + Increment;
        }

        /// <summary>
        /// watch_for_drop(): call when anyone's lifetime score changes (teamTotal = the sum of every
        /// player's score_total). Passing the threshold earns a drop and raises the next one.
        /// </summary>
        public void OnTeamScoreTotal(int teamTotal)
        {
            if (teamTotal <= ScoreToDrop) return;
            Increment *= PowerUpInfo.IncrementGrowth;
            ScoreToDrop = teamTotal + Increment;
            DropEarned = true;
        }

        /// <summary>powerup_round_start()</summary>
        public void StartRound() => DropsThisRound = 0;

        /// <summary>
        /// powerup_drop(): a player just killed a zombie. Returns the power-up to drop there, or
        /// null. Records the drop if one happens.
        /// </summary>
        public PowerUpId? TryDrop(PowerUpConditions conditions)
        {
            if (DropsThisRound >= PowerUpInfo.MaxDropsPerRound || _deck.Count == 0) return null;
            bool lucky = _rng.Next(100) < PowerUpInfo.RandomDropPercent;
            if (!lucky && !DropEarned) return null;

            var pick = NextValid(conditions);
            if (!pick.HasValue) return null;
            DropsThisRound++;
            DropEarned = false; // zombie_drop_item = 0, even after a lucky drop
            return pick;
        }

        /// <summary>get_valid_powerup(): draw from the deck, skipping ones not allowed right now.</summary>
        public PowerUpId? NextValid(PowerUpConditions conditions)
        {
            for (int attempts = 0; attempts < _deck.Count * 2; attempts++)
            {
                var p = Next();
                if (conditions.Allows(p)) return p;
            }
            return null;
        }

        PowerUpId Next()
        {
            var p = _deck[_index++];
            if (_index >= _deck.Count) { _index = 0; Shuffle(); }
            return p;
        }

        void Shuffle()
        {
            for (int i = _deck.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                var t = _deck[i];
                _deck[i] = _deck[j];
                _deck[j] = t;
            }
        }
    }

    /// <summary>
    /// The 30-second power-ups. Grabbing one that's running resets it to 30 s. A fixed array (one
    /// slot per PowerUpId), so ticking it every frame allocates nothing.
    /// </summary>
    public class TimedPowerUps
    {
        readonly float[] _remaining = new float[Enum.GetValues(typeof(PowerUpId)).Length];

        public event Action<PowerUpId> Started;
        public event Action<PowerUpId> Ended;

        public bool IsActive(PowerUpId p) => _remaining[(int)p] > 0f;
        public float Remaining(PowerUpId p) => Math.Max(0f, _remaining[(int)p]);

        public void Activate(PowerUpId p)
        {
            bool wasActive = IsActive(p);
            _remaining[(int)p] = PowerUpInfo.TimedSeconds;
            if (!wasActive) Started?.Invoke(p);
        }

        public void Tick(float dt)
        {
            for (int i = 0; i < _remaining.Length; i++)
            {
                if (_remaining[i] <= 0f) continue;
                _remaining[i] -= dt;
                if (_remaining[i] <= 0f) Ended?.Invoke((PowerUpId)i);
            }
        }

        /// <summary>Game over: everything stops (end_game zeroes the timers).</summary>
        public void Clear()
        {
            for (int i = 0; i < _remaining.Length; i++)
            {
                if (_remaining[i] <= 0f) continue;
                _remaining[i] = 0f;
                Ended?.Invoke((PowerUpId)i);
            }
        }
    }
}
