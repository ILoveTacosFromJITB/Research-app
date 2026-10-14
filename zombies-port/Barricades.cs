// Barricades.cs
//
// Wall-buy and window-barricade rules in plain C# (no engine types). Ported from:
//   _zombiemode_weapons.gsc  add_zombie_weapon(), weapon_spawn_think(), ammo_give()
//   _zombiemode_blockers.gsc blocker_trigger_think(), blocker_attack_spots(), rebuild_barrier_reward_reset()
//   _zombiemode_spawner.gsc  tear_into_building(), should_attack_player_thru_boards()
//   _zombiemode.gsc          round_think(): rebuild_barrier_cap_per_round = min(50 * round, 500)

using System;
using System.Collections.Generic;

namespace ZombiesPort
{
    // ---------------------------------------------------------------------------------------
    // Wall buys
    // ---------------------------------------------------------------------------------------

    public enum WallBuyAction { BuyWeapon, BuyAmmo, AmmoFull, NotEnoughPoints, CannotBuy }

    public static class WallBuyRules
    {
        /// <summary>Ammo for a Pack-a-Punched gun always costs 4500.</summary>
        public const int UpgradedAmmoCost = 4500;

        /// <summary>add_zombie_weapon(): ammo_cost = round_up_to_ten( int( cost * 0.5 ) ).</summary>
        public static int AmmoCost(int weaponCost) => ZombieGameState.RoundUpToTen((int)(weaponCost * 0.5));

        /// <summary>
        /// weapon_spawn_think(): a player without the gun buys it; a player with it (or its
        /// upgrade) buys ammo, but only if their spare ammo is below the starting amount
        /// (ammo_give: currStock - clipCount >= WeaponStartAmmo means no sale, no charge).
        /// </summary>
        public static WallBuyAction Decide(bool canBuyWeapon, bool ownsWeapon, bool ownsUpgrade,
                                           int reserve, int startingReserve, int score, int weaponCost,
                                           out int price)
        {
            price = 0;
            if (!canBuyWeapon) return WallBuyAction.CannotBuy;
            if (!ownsWeapon && !ownsUpgrade)
            {
                price = weaponCost;
                return score >= price ? WallBuyAction.BuyWeapon : WallBuyAction.NotEnoughPoints;
            }
            price = ownsUpgrade ? UpgradedAmmoCost : AmmoCost(weaponCost);
            if (reserve >= startingReserve) return WallBuyAction.AmmoFull;
            return score >= price ? WallBuyAction.BuyAmmo : WallBuyAction.NotEnoughPoints;
        }

        /// <summary>
        /// Kino der Toten's wall weapons and their prices from init_weapons(). These are the
        /// guns the map includes with in_box = false.
        /// </summary>
        public static readonly Dictionary<string, int> KinoWallPrices = new Dictionary<string, int>
        {
            { "m14_zm", 500 }, { "rottweil72_zm", 500 },
            { "mp5k_zm", 1000 }, { "mp40_zm", 1000 }, { "mpl_zm", 1000 }, { "pm63_zm", 1000 },
            { "ak74u_zm", 1200 }, { "m16_zm", 1200 },
            { "ithaca_zm", 1500 },
        };
    }

    // ---------------------------------------------------------------------------------------
    // Window barricades
    // ---------------------------------------------------------------------------------------

    public enum BoardState { Intact, Torn }

    /// <summary>The boards on one window (level.exterior_goals[i].barrier_chunks).</summary>
    public class BarricadeState
    {
        public const float TearReachUnits = 90f;           // should_attack_player_thru_boards(): distance2d <= 90
        public const int ReachInChancePercent = 50;        // zombie_reachin_freq 50
        public const float RepairSecondsPerBoard = 1f;     // blocker_trigger_think(): wait( 1 )
        public const float RepairRadiusUnits = 96f;        // trigger_radius 96
        public const int RepairPoints = 10;                // cost = 10 ("rebuild_board")
        public const int AttackSpots = 3;                  // blocker_attack_spots(): center, +28, -28
        public const float AttackSpotSideUnits = 28f;
        public const float AttackSpotBackUnits = 36f;      // dist = Distance2d(...) - 36

        readonly BoardState[] _boards;
        readonly bool[] _targeted;                         // chunk "target_by_zombie": one zombie per board
        readonly Random _rng;

        public int Count => _boards.Length;
        public int IntactCount { get; private set; }
        public bool IsFullyIntact => IntactCount == _boards.Length;
        public bool IsOpen => IntactCount == 0;            // all_chunks_destroyed(): zombies climb in

        public event Action<int> BoardTorn;
        public event Action<int> BoardRepaired;

        public BarricadeState(int boards = 6, int? seed = null)
        {
            _boards = new BoardState[boards];
            _targeted = new bool[boards];
            IntactCount = boards;
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public BoardState this[int i] => _boards[i];

        /// <summary>
        /// get_closest_non_destroyed_chunk(): a zombie claims the intact board nearest to it that
        /// no other zombie is already pulling. distances[i] is the zombie's distance to board i.
        /// Returns -1 if every intact board is taken.
        /// </summary>
        public int ClaimBoard(IReadOnlyList<float> distances)
        {
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < _boards.Length; i++)
            {
                if (_boards[i] != BoardState.Intact || _targeted[i]) continue;
                float d = distances != null && i < distances.Count ? distances[i] : i;
                if (d < bestD) { bestD = d; best = i; }
            }
            if (best >= 0) _targeted[best] = true;
            return best;
        }

        public void ReleaseBoard(int i)
        {
            if (i >= 0 && i < _targeted.Length) _targeted[i] = false;
        }

        /// <summary>The tear animation's notetrack pulls the board off.</summary>
        public bool Tear(int i)
        {
            ReleaseBoard(i);
            if (i < 0 || i >= _boards.Length || _boards[i] != BoardState.Intact) return false;
            _boards[i] = BoardState.Torn;
            IntactCount--;
            BoardTorn?.Invoke(i);
            return true;
        }

        /// <summary>get_random_destroyed_chunk() + replace_chunk(): put back a random torn board.</summary>
        public int RepairRandom()
        {
            int torn = _boards.Length - IntactCount;
            if (torn == 0) return -1;
            int pick = _rng.Next(torn);
            for (int i = 0; i < _boards.Length; i++)
            {
                if (_boards[i] != BoardState.Torn) continue;
                if (pick-- > 0) continue;
                _boards[i] = BoardState.Intact;
                _targeted[i] = false;
                IntactCount++;
                BoardRepaired?.Invoke(i);
                return i;
            }
            return -1;
        }
    }

    /// <summary>
    /// Points for rebuilding boards: 10 a board, capped per player per round at 50 x round
    /// (max 500). As in GSC, the board's 10 is added to the running total before the check, so
    /// round 1 pays for 4 boards (40), not 5.
    /// </summary>
    public class RebuildRewards
    {
        public const int CapPerRoundStep = 50;
        public const int CapMax = 500;

        readonly Dictionary<int, int> _earned = new Dictionary<int, int>();
        public int Cap { get; private set; } = CapPerRoundStep;

        /// <summary>round_think(): new cap; rebuild_barrier_reward_reset() for everyone.</summary>
        public void StartRound(int round)
        {
            Cap = Math.Min(CapPerRoundStep * round, CapMax);
            _earned.Clear();
        }

        /// <summary>Returns the points to award for one board (0 once the cap is reached).</summary>
        public int AwardBoard(int playerIndex, int pointMultiplier)
        {
            _earned.TryGetValue(playerIndex, out int total);
            total += BarricadeState.RepairPoints;              // player.rebuild_barrier_reward += cost
            _earned[playerIndex] = total;
            if (total >= Cap) return 0;                        // only "if( reward < cap )" pays
            return pointMultiplier * ZombieGameState.RoundUpScore(BarricadeState.RepairPoints, 5);
        }
    }
}
