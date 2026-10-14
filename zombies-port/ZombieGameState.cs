// ZombieGameState.cs
//
// Port of the Black Ops 1 Zombies startup logic. The GSC sources it comes from are:
//   maps/_zombiemode.gsc        : main(), init_levelvars(), difficulty_init(),
//                                 ai_calculate_health(), round_spawning(), round_think()
//   maps/_zombiemode_score.gsc  : add_to_player_score(), minus_to_player_score()
//   maps/_zombiemode_spawner.gsc: set_run_speed()
//
// The file uses only plain C# and references neither UnityEngine nor Godot, so it drops
// into a Unity project (Assets/Scripts/) or a Godot 4 .NET project as is. Engine code
// (MonoBehaviour / Node) owns one instance and listens to its events to drive the HUD.

using System;
using System.Collections.Generic;

namespace ZombiesPort
{
    /// <summary>
    /// Tuning values from init_levelvars(). GSC kept these in level.zombie_vars[] and
    /// let rows in "mp/zombiemode.csv" override them through set_zombie_var(). In Unity,
    /// turn this into a ScriptableObject; in Godot, make it a Resource.
    /// </summary>
    [Serializable]
    public class ZombieVars
    {
        // --- Starting state ---
        // The dump hardcodes 3000 as the fallback in set_zombie_var("zombie_score_start_Np", 3000).
        // That fallback only applies when the CSV has no row. The CSV is not in the dump, and
        // the shipped game started every player on 500, so 500 is the default here.
        public int StartingPoints = 500;
        public int StartingRound = 1;           // level.round_number = 1;
        public bool QuickStartMutator = false;  // mutator_quickStart: start on round 5

        // --- Zombie health (ai_calculate_health) ---
        public int HealthStart = 150;               // zombie_health_start
        public int HealthIncrease = 100;            // zombie_health_increase (flat, rounds 2-9)
        public float HealthIncreaseMultiplier = 0.1f; // zombie_health_increase_multiplier (round 10+)

        // --- Spawning (round_spawning) ---
        public float SpawnDelay = 2.0f;        // zombie_spawn_delay, multiplied by 0.95 each round
        public float MinSpawnDelay = 0.08f;    // floor applied in round_think()
        public int MaxAi = 24;                 // zombie_max_ai: base zombie count per round
        public int AiPerPlayer = 6;            // zombie_ai_per_player
        public int AiLimit = 24;               // level.zombie_ai_limit: max zombies alive at once
                                               // (this is also the object pool size)
        public int MoveSpeedMultiplier = 8;    // zombie_move_speed_multiplier

        // --- Timing ---
        public float BetweenRoundTime = 10f;   // zombie_between_round_time
        public float GameStartDelay = 0f;      // game_start_delay

        // --- Scoring (only the values the init step needs; the full table comes later) ---
        public int ScoreKill = 50;             // zombie_score_kill_1player
        public int ScoreDamage = 10;           // zombie_score_damage_normal
        public int ScoreBonusHead = 50;        // zombie_score_bonus_head
        public int ScoreBonusMelee = 80;       // zombie_score_bonus_melee
    }

    /// <summary>
    /// A player's point totals. Mirrors the score, score_total and old_score fields that
    /// difficulty_init() sets on each player entity.
    /// </summary>
    public class PlayerScore
    {
        public int PlayerIndex { get; }
        public int Score { get; private set; }       // spendable points
        public int ScoreTotal { get; private set; }  // lifetime points earned, used for the leaderboard
        public int OldScore { get; private set; }    // previous value, used for the HUD "+50" popups

        public event Action<PlayerScore, int> OnScoreChanged; // (player, delta)

        public PlayerScore(int index, int startingPoints)
        {
            PlayerIndex = index;
            Score = startingPoints;
            ScoreTotal = startingPoints;
            OldScore = startingPoints;
        }

        /// <summary>add_to_player_score(): ignored during the game-over intermission.</summary>
        public void Add(int points, bool addToTotal, bool intermission)
        {
            if (points <= 0 || intermission) return;
            OldScore = Score;
            Score += points;
            if (addToTotal) ScoreTotal += points;
            OnScoreChanged?.Invoke(this, points);
        }

        /// <summary>
        /// minus_to_player_score(). GSC never checked the balance here; every buy trigger
        /// checked it first. TrySpend folds both steps into one call.
        /// </summary>
        public bool TrySpend(int cost, bool intermission)
        {
            if (intermission || cost > Score) return false;
            OldScore = Score;
            Score -= cost;
            OnScoreChanged?.Invoke(this, -cost);
            return true;
        }
    }

    /// <summary>
    /// Match-level state: the GSC "level.*" globals the round system reads.
    /// </summary>
    public class ZombieGameState
    {
        public ZombieVars Vars { get; }

        public int RoundNumber { get; private set; }
        public bool FirstRound { get; private set; }
        public bool Intermission { get; private set; }   // true once the game is over
        public int ZombieTotal { get; private set; }     // zombies still waiting to spawn this round
        public int TotalZombiesKilled { get; private set; }
        public int ZombieHealth { get; private set; }    // health given to each new spawn
        public int ZombieMoveSpeed { get; private set; } // feeds set_run_speed()
        public float SpawnDelay { get; private set; }

        public IReadOnlyList<PlayerScore> Players => _players;
        private readonly List<PlayerScore> _players = new List<PlayerScore>();

        public event Action<int> OnRoundChanged;  // new round number, used for the chalk tally HUD

        public ZombieGameState(ZombieVars vars = null)
        {
            Vars = vars ?? new ZombieVars();
        }

        /// <summary>
        /// Runs init_levelvars() followed by difficulty_init(). The GSC waited on
        /// flag_wait("all_players_connected") before handing out points, so call this once
        /// the player count is final. On mobile that is usually 1.
        /// </summary>
        public void Initialize(int playerCount = 1)
        {
            if (playerCount < 1 || playerCount > 4)
                throw new ArgumentOutOfRangeException(nameof(playerCount), "BO1 supports 1-4 players.");

            // init_levelvars()
            RoundNumber = Vars.StartingRound;   // level.round_number = 1;
            FirstRound = true;                  // level.first_round = true;
            Intermission = false;               // level.intermission = false;
            ZombieTotal = 0;                    // level.zombie_total = 0;
            TotalZombiesKilled = 0;             // level.total_zombies_killed = 0;
            ZombieMoveSpeed = 1;                // level.zombie_move_speed = 1;
            SpawnDelay = Vars.SpawnDelay;

            // difficulty_init(): give every player the starting points
            _players.Clear();
            for (int p = 0; p < playerCount; p++)
                _players.Add(new PlayerScore(p, Vars.StartingPoints));

            // difficulty_init(): the quickStart mutator skips ahead to round 5
            if (Vars.QuickStartMutator)
            {
                FirstRound = false;
                RoundNumber = 5;
            }

            // round_start(): level.zombie_health = zombie_health_start
            ZombieHealth = Vars.HealthStart;

            OnRoundChanged?.Invoke(RoundNumber);
        }

        // ------------------------------------------------------------------
        // Pure formulas, ported directly. They are static so you can unit-test
        // them or plot the difficulty curve without a running scene.
        // ------------------------------------------------------------------

        /// <summary>
        /// ai_calculate_health(): +100 per round through round 9, then +10% compounding
        /// from round 10. Integer truncation matches GSC Int().
        /// </summary>
        public static int CalculateZombieHealth(int round, ZombieVars v)
        {
            int health = v.HealthStart;
            for (int i = 2; i <= round; i++)
            {
                if (i >= 10) health += (int)(health * v.HealthIncreaseMultiplier);
                else health = health + v.HealthIncrease;
            }
            return health;
        }

        /// <summary>
        /// round_spawning() and default_max_zombie_func(): how many zombies spawn in a round.
        /// </summary>
        public static int CalculateZombieTotal(int round, int playerCount, bool firstRound, ZombieVars v)
        {
            int max = v.MaxAi;
            float multiplier = round / 5f;          // GSC divides as floats here
            if (multiplier < 1) multiplier = 1;
            if (round >= 10) multiplier *= round * 0.15f;

            if (playerCount == 1)
                max += (int)((0.5f * v.AiPerPlayer) * multiplier);
            else
                max += (int)(((playerCount - 1) * v.AiPerPlayer) * multiplier);

            // default_max_zombie_func(): early rounds spawn only a fraction of the total
            if (firstRound)     return (int)(max * 0.25f);
            if (round < 3)      return (int)(max * 0.3f);
            if (round < 4)      return (int)(max * 0.5f);
            if (round < 5)      return (int)(max * 0.7f);
            if (round < 6)      return (int)(max * 0.9f);
            return max;
        }
    }
}
