// MysteryBoxRules.cs
//
// The Mystery Box's decisions, ported from maps/_zombiemode_weapons.gsc:
//   treasure_chest_init()                   : cost, list of box locations
//   treasure_chest_think()                  : who can pay, the 12-second grab window
//   treasure_chest_weapon_spawn()           : 40-cycle spin timing, teddy bear chance
//   treasure_chest_ChooseWeightedRandomWeapon(): the weapon you actually get
//   default_box_move_logic()                : where the box goes next
//   can_buy_weapon()                        : when a player may use any buyable
//
// Plain C# with no engine types: the Unity components (MysteryBox, MysteryBoxDirector) own
// the timing and visuals and ask this class what should happen.

using System;
using System.Collections.Generic;

namespace ZombiesPort
{
    /// <summary>One weapon in the box. GSC: add_zombie_weapon() + include_zombie_weapon().</summary>
    [Serializable]
    public class BoxWeapon
    {
        public string Id;
        public string UpgradeId;
        public bool InBox = true;     // include_weapon(name, in_box): false = wall/PaP only
        public int Limit = -1;        // add_limited_weapon(name, n): max copies in play, -1 = unlimited
        /// <summary>weapon_weighting_funcs[]: how many tickets this weapon gets. Null means 1.</summary>
        [NonSerialized] public Func<BoxContext, int> Weighting;

        public BoxWeapon() { }

        public BoxWeapon(string id, string upgradeId, bool inBox = true, int limit = -1)
        {
            Id = id; UpgradeId = upgradeId; InBox = inBox; Limit = limit;
        }
    }

    /// <summary>Everything the weighted roll needs to know about the world.</summary>
    public struct BoxContext
    {
        public int RoundNumber;
        public int ChestMoves;
        public WeaponInventory User;
        public IReadOnlyList<WeaponInventory> AllPlayers;
        /// <summary>Weapons floating in any box right now (they count toward limits).</summary>
        public IEnumerable<string> WeaponsOnDisplay;
    }

    public class MysteryBoxRules
    {
        // ---- Constants straight from the GSC ----
        public const int DefaultCost = 950;            // old_cost = 950
        public const int SpinCycles = 40;              // number_cycles = 40
        public const float GrabWindowSeconds = 12f;    // treasure_chest_timeout(): wait(12)
        public const float LidOpenSeconds = 0.5f;      // treasure_chest_lid_open(): openTime
        public const float LidOpenDegrees = 105f;      // RotateRoll(105, ...)
        public const float CloseCooldownSeconds = 3f;  // "wait 3" after the lid closes
        public const int MinUsesBeforeTeddy = 4;       // level.chest_min_move_usage = 4

        // Teddy bear / relocation timings (treasure_chest_weapon_spawn + treasure_chest_move)
        public const float TeddyPauseSeconds = 0.5f;   // wait .5 before fly away starts
        public const float TeddyHoverSeconds = 2f;     // wait 2
        public const float TeddyRiseSeconds = 4f;      // MoveZ(500, 4, 3)
        public const float BoxShakeSeconds = 5f;       // anchor moveto(+50, 5) with Vibrate
        public const float BoxReappearSeconds = 12f;   // post_selection_wait_duration 7 + 5

        // GSC world units are inches. Multiply by this to get meters for Unity or Godot.
        public const float UnitsToMeters = 0.0254f;
        public const float WeaponFloatHeight = 40f * UnitsToMeters;  // floatHeight = 40
        public const float TeddyRiseHeight = 500f * UnitsToMeters;   // MoveZ(500)

        public IReadOnlyList<BoxWeapon> Weapons => _weapons;
        readonly List<BoxWeapon> _weapons;
        readonly Random _rng;

        /// <summary>level.chest_accessed: pulls since the box last moved.</summary>
        public int ChestAccessed { get; private set; }
        /// <summary>level.chest_moves: how many times the box has moved this game.</summary>
        public int ChestMoves { get; private set; }

        public MysteryBoxRules(IEnumerable<BoxWeapon> weapons, int? seed = null)
        {
            _weapons = new List<BoxWeapon>(weapons);
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        /// <summary>
        /// The spin slows down as it goes: 20 fast cycles, then progressively longer pauses.
        /// The last two cycles have no wait, matching the GSC loop.
        /// </summary>
        public static float CycleDelay(int i)
        {
            if (i < 20) return 0.05f;
            if (i < 30) return 0.1f;
            if (i < 35) return 0.2f;
            if (i < 38) return 0.3f;
            return 0f;
        }

        /// <summary>can_buy_weapon(): blocked while drinking a perk, reviving, or without a weapon in hand.</summary>
        public static bool CanBuyWeapon(bool isDrinkingPerk, bool isReviving, bool isDowned, string currentWeapon)
        {
            return !isDrinkingPerk && !isReviving && !isDowned && !string.IsNullOrEmpty(currentWeapon);
        }

        /// <summary>
        /// treasure_chest_ChooseRandomWeapon(): the fake weapons flashed during cycles 1-39.
        /// Visual only, with no weighting or filtering.
        /// </summary>
        public string ChooseCycleWeapon()
        {
            return _weapons[_rng.Next(_weapons.Count)].Id;
        }

        /// <summary>
        /// treasure_chest_ChooseWeightedRandomWeapon(): the weapon actually awarded on cycle 40.
        /// Skips weapons that aren't in the box or that the user already owns (or owns upgraded),
        /// gives each remaining weapon tickets by its weighting, then removes limited weapons
        /// whose copies are all in play. Returns null if nothing is left.
        /// </summary>
        public string ChooseWeightedWeapon(BoxContext ctx)
        {
            var tickets = new List<string>();
            foreach (var w in _weapons)
            {
                if (!w.InBox) continue;
                if (ctx.User != null && ctx.User.HasWeaponOrUpgrade(w.Id, w.UpgradeId)) continue;

                int entries = w.Weighting != null ? w.Weighting(ctx) : 1;
                for (int j = 0; j < entries; j++) tickets.Add(w.Id);
            }

            foreach (var w in _weapons)
            {
                if (w.Limit < 0) continue;
                int count = 0;
                if (ctx.AllPlayers != null)
                    foreach (var p in ctx.AllPlayers)
                        if (p.HasWeaponOrUpgrade(w.Id, w.UpgradeId)) count++;
                if (ctx.WeaponsOnDisplay != null)
                    foreach (var shown in ctx.WeaponsOnDisplay)
                        if (shown == w.Id) count++;
                if (count >= w.Limit) tickets.RemoveAll(t => t == w.Id);
            }

            return tickets.Count == 0 ? null : tickets[_rng.Next(tickets.Count)];
        }

        /// <summary>
        /// The teddy bear roll from treasure_chest_weapon_spawn(), made after the weapon is chosen.
        ///  - Fewer than 4 pulls since the last move: never.
        ///  - Pulls 5-8 (chest_accessed 4-7): 15%.
        ///  - Box has never moved and chest_accessed is 8+: guaranteed.
        ///  - Box has moved before: 30% at chest_accessed 8-12, 50% at 13+.
        /// canMove is false for maps with only one box location (GSC: chest.no_fly_away).
        /// </summary>
        public bool RollTeddyBear(bool canMove)
        {
            if (!canMove) return false;

            int random = _rng.Next(100);
            int chance;
            if (ChestAccessed < MinUsesBeforeTeddy)
            {
                chance = -1;
            }
            else
            {
                chance = ChestAccessed + 20;
                if (ChestMoves == 0 && ChestAccessed >= 8) chance = 100;
                if (ChestAccessed >= 4 && ChestAccessed < 8) chance = random < 15 ? 100 : -1;
                if (ChestMoves > 0)
                {
                    if (ChestAccessed >= 8 && ChestAccessed < 13) chance = random < 30 ? 100 : -1;
                    if (ChestAccessed >= 13) chance = random < 50 ? 100 : -1;
                }
            }

            if (chance > random)
            {
                ChestAccessed = 0;
                ChestMoves++;
                return true;
            }
            return false;
        }

        /// <summary>
        /// level.chest_accessed += 1. Runs once the grab window closes, whether the weapon was
        /// taken or left to time out. A teddy-bear pull doesn't count.
        /// </summary>
        public void RecordPull() => ChestAccessed++;

        // ---- Box locations (default_box_move_logic) ----

        /// <summary>
        /// A random visiting order for the box locations (array_randomize(level.chests)).
        /// Used at game start and whenever the order runs out.
        /// </summary>
        public int[] ShuffleLocations(int count)
        {
            var order = new int[count];
            for (int i = 0; i < count; i++) order[i] = i;
            for (int i = count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                int tmp = order[i];
                order[i] = order[j];
                order[j] = tmp;
            }
            return order;
        }

        /// <summary>
        /// default_box_move_logic(): step to the next location in the shuffled order. At the
        /// end, reshuffle, and skip ahead one if the reshuffle would put the box right back
        /// where it was. Scripted "moveN" overrides are left out; add them per map if needed.
        /// </summary>
        public int NextLocation(ref int[] order, int index)
        {
            index++;
            if (index >= order.Length)
            {
                int last = order[order.Length - 1];
                order = ShuffleLocations(order.Length);
                index = 0;
                if (order.Length > 1 && order[0] == last) index = 1;
            }
            return index;
        }

        // ---- Optional weighting functions from the GSC ----

        /// <summary>default_cymbal_monkey_weighting_func(): 3 tickets (5 from round 10) until someone owns one.</summary>
        public static int CymbalMonkeyWeighting(BoxContext ctx)
        {
            if (ctx.AllPlayers != null)
                foreach (var p in ctx.AllPlayers)
                    if (p.HasWeaponOrUpgrade("zombie_cymbal_monkey")) return 1;
            return ctx.RoundNumber < 10 ? 3 : 5;
        }

        /// <summary>default_1st_move_weighting_func(): not in the box until it has moved once.</summary>
        public static int AfterFirstMoveWeighting(BoxContext ctx) => ctx.ChestMoves > 0 ? 1 : 0;

        /// <summary>
        /// The box contents of Kino der Toten (maps/zombie_theater.gsc): every include_weapon()
        /// with in_box left true, plus the add_limited_weapon() caps.
        /// </summary>
        public static List<BoxWeapon> KinoDerTotenWeapons() => new List<BoxWeapon>
        {
            new BoxWeapon("python_zm", "python_upgraded_zm"),
            new BoxWeapon("cz75_zm", "cz75_upgraded_zm"),
            new BoxWeapon("g11_lps_zm", "g11_lps_upgraded_zm"),
            new BoxWeapon("famas_zm", "famas_upgraded_zm"),
            new BoxWeapon("spectre_zm", "spectre_upgraded_zm"),
            new BoxWeapon("cz75dw_zm", "cz75dw_upgraded_zm"),
            new BoxWeapon("spas_zm", "spas_upgraded_zm"),
            new BoxWeapon("hs10_zm", "hs10_upgraded_zm"),
            new BoxWeapon("aug_acog_zm", "aug_acog_mk_upgraded_zm"),
            new BoxWeapon("galil_zm", "galil_upgraded_zm"),
            new BoxWeapon("commando_zm", "commando_upgraded_zm"),
            new BoxWeapon("fnfal_zm", "fnfal_upgraded_zm"),
            new BoxWeapon("dragunov_zm", "dragunov_upgraded_zm"),
            new BoxWeapon("l96a1_zm", "l96a1_upgraded_zm"),
            new BoxWeapon("rpk_zm", "rpk_upgraded_zm"),
            new BoxWeapon("hk21_zm", "hk21_upgraded_zm"),
            new BoxWeapon("m72_law_zm", "m72_law_upgraded_zm"),
            new BoxWeapon("china_lake_zm", "china_lake_upgraded_zm"),
            new BoxWeapon("zombie_cymbal_monkey", null),
            new BoxWeapon("ray_gun_zm", "ray_gun_upgraded_zm"),
            new BoxWeapon("thundergun_zm", "thundergun_upgraded_zm", limit: 1),
            new BoxWeapon("crossbow_explosive_zm", "crossbow_explosive_upgraded_zm", limit: 1),
            new BoxWeapon("knife_ballistic_zm", "knife_ballistic_upgraded_zm", limit: 1),
        };
    }
}
