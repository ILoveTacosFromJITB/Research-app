// ZombieGameState.cs
//
// Port of the Black Ops 1 Zombies startup logic. The GSC sources it comes from are:
//   maps/_zombiemode.gsc        : main(), init_levelvars(), difficulty_init(),
//                                 ai_calculate_health(), round_spawning(), round_think()
//   maps/_zombiemode_score.gsc  : add_to_player_score(), minus_to_player_score()
//   maps/_zombiemode_spawner.gsc: set_run_speed(), zombie_damage()
//   maps/_zombiemode_score.gsc  : player_add_points(), player_add_points_kill_bonus()
//   maps/_zombiemode_utility.gsc: round_up_score()
//
// The file uses only plain C# and references neither UnityEngine nor Godot, so it drops
// into a Unity project (Assets/Scripts/) or a Godot 4 .NET project as is. Engine code
// (MonoBehaviour / Node) owns one instance and listens to its events to drive the HUD.

using System;
using System.Collections.Generic;

namespace ZombiesPort
{
    /// <summary>set_run_speed(): the three movement tiers a zombie can roll.</summary>
    public enum ZombieSpeed { Walk, Run, Sprint }

    /// <summary>Hit locations that change kill points (player_add_points_kill_bonus).</summary>
    public enum HitZone { Head, Neck, TorsoUpper, TorsoLower, Limb }

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
        public int ScoreBonusNeck = 20;        // zombie_score_bonus_neck
        public int ScoreBonusTorso = 10;       // zombie_score_bonus_torso

        // --- Last stand point penalties (init_levelvars) ---
        public float PenaltyDowned = 0.05f;    // penalty_downed: lost on going down, earned back by the reviver
        public float PenaltyNoRevive = 0.10f;  // penalty_no_revive: everyone else loses this when a player bleeds out
        public float PenaltyDied = 0.0f;       // penalty_died: taken from the player who bled out, on respawn

        // --- Stuck-zombie failsafe (round_spawn_failsafe) ---
        public bool UseFailsafe = true;        // zombie_use_failsafe
        public float FailsafeIntervalSeconds = 30f;  // wait( 30 )
        public float FailsafeMinMoveUnits = 24f;     // DistanceSquared < 576
        public float BelowWorldUnits = -1000f;       // below_world_check
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
        /// player_reduce_points(): penalties take points without a balance check, never below 0.
        /// Returns how many points were actually lost.
        /// </summary>
        public int Lose(int points, bool intermission)
        {
            if (intermission || points <= 0) return 0;
            int lost = Math.Min(points, Score);
            OldScore = Score;
            Score -= lost;
            if (lost > 0) OnScoreChanged?.Invoke(this, -lost);
            return lost;
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

        /// <summary>Board-rebuild points earned this round, per player, and the round's cap.</summary>
        public RebuildRewards Rebuilds { get; } = new RebuildRewards();

        /// <summary>level.solo_lives_given: solo Quick Revive can only be bought 3 times per game.</summary>
        public SoloQuickReviveTracker SoloQuickRevive { get; } = new SoloQuickReviveTracker();

        /// <summary>level.zombie_vars["zombie_insta_kill"]: any player hit kills (check_for_instakill).</summary>
        public bool InstaKill { get; set; }

        /// <summary>Double Points sets this to 2 (zombie_point_scalar).</summary>
        public int PointMultiplier { get; set; } = 1;

        readonly Random _rng;

        public ZombieGameState(ZombieVars vars = null, int? seed = null)
        {
            Vars = vars ?? new ZombieVars();
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
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
        // Round lifecycle: round_think(), round_spawning() and round_wait().
        // The engine-side RoundDirector calls these in order:
        //   BeginRound -> TryTakeSpawnTicket (per spawn) -> IsRoundOver (poll) -> EndRound
        // ------------------------------------------------------------------

        /// <summary>
        /// Start of round_spawning(): sets this round's zombie health and how many zombies
        /// will spawn (level.zombie_total).
        /// </summary>
        public void BeginRound()
        {
            ZombieHealth = CalculateZombieHealth(RoundNumber, Vars);
            ZombieTotal = CalculateZombieTotal(RoundNumber, _players.Count, FirstRound, Vars);
            Rebuilds.StartRound(RoundNumber); // rebuild_barrier_cap_per_round = min(50 * round, 500)
        }

        /// <summary>dog_round_spawning(): level.zombie_total = the round's dog count instead.</summary>
        public void StartDogQueue(int dogs) => ZombieTotal = Math.Max(0, dogs);

        /// <summary>Takes one zombie off the queue for spawning (level.zombie_total--).</summary>
        public bool TryTakeSpawnTicket()
        {
            if (ZombieTotal <= 0) return false;
            ZombieTotal--;
            return true;
        }

        /// <summary>
        /// round_spawn_failsafe(): a stuck zombie is killed and put back in the queue
        /// (level.put_timed_out_zombies_back_in_queue), so the round can't soft-lock.
        /// </summary>
        public void ReturnSpawnTicket() => ZombieTotal++;

        public void RecordKill() => TotalZombiesKilled++;

        /// <summary>round_wait(): over when nothing is alive and nothing is left to spawn.</summary>
        public bool IsRoundOver(int aliveCount) => aliveCount <= 0 && ZombieTotal <= 0 && !Intermission;

        /// <summary>
        /// End of round_think(), after the 10-second chalk interlude: the spawn delay shrinks
        /// by 5% (floor 0.08 s), zombies speed up for next round, and the round number goes up.
        /// </summary>
        public void EndRound()
        {
            FirstRound = false;

            if (SpawnDelay > Vars.MinSpawnDelay) SpawnDelay *= 0.95f;
            else if (SpawnDelay < Vars.MinSpawnDelay) SpawnDelay = Vars.MinSpawnDelay;

            // GSC uses the number of the round just finished, so round 2 runs at 8, round 3 at 16...
            ZombieMoveSpeed = RoundNumber * Vars.MoveSpeedMultiplier;
            RoundNumber++;
            OnRoundChanged?.Invoke(RoundNumber);
        }

        /// <summary>Game over: stops all scoring (level.intermission).</summary>
        public void SetIntermission() => Intermission = true;

        /// <summary>
        /// set_run_speed(): roll from [move_speed, move_speed + 35). 35 or under walks, 70 or
        /// under runs, anything higher sprints. Round 1 is all walkers; by round 9 nearly all sprint.
        /// </summary>
        public ZombieSpeed RollSpeed()
        {
            int rand = _rng.Next(ZombieMoveSpeed, ZombieMoveSpeed + 35);
            if (rand <= 35) return ZombieSpeed.Walk;
            if (rand <= 70) return ZombieSpeed.Run;
            return ZombieSpeed.Sprint;
        }

        /// <summary>
        /// zombie_speed_up(): from round 4 to round 9, the last zombie standing starts sprinting
        /// so the round doesn't drag. GSC only checks this on rounds below 10.
        /// </summary>
        public bool ShouldSpeedUpLastZombie => RoundNumber > 3 && RoundNumber < 10;

        public int RandomIndex(int count) => _rng.Next(count);

        // ------------------------------------------------------------------
        // Points: player_add_points() in _zombiemode_score.gsc
        // ------------------------------------------------------------------

        /// <summary>
        /// A hit that doesn't kill is worth 10 ("damage" / "damage_light"). The killing hit
        /// pays only the kill points: _gameskill.gsc sends a lethal hit straight to the death
        /// handler without calling zombie_damage().
        /// </summary>
        public int PointsForHit() => PointMultiplier * RoundUpScore(Vars.ScoreDamage, 5);

        /// <summary>
        /// The "death" event: 50 plus a bonus (knife 80, otherwise head 50, neck 20, torso 10,
        /// limbs 0), rounded up to a multiple of 5, then times the Double Points multiplier.
        /// A body kill pays 60, a headshot 100, a knife kill 130.
        /// </summary>
        public int PointsForKill(HitZone zone, bool melee)
        {
            int bonus;
            if (melee) bonus = Vars.ScoreBonusMelee;
            else if (zone == HitZone.Head) bonus = Vars.ScoreBonusHead;
            else if (zone == HitZone.Neck) bonus = Vars.ScoreBonusNeck;
            else if (zone == HitZone.TorsoUpper || zone == HitZone.TorsoLower) bonus = Vars.ScoreBonusTorso;
            else bonus = 0;
            return PointMultiplier * RoundUpScore(Vars.ScoreKill + bonus, 5);
        }

        // ------------------------------------------------------------------
        // Last stand points: player_reduce_points() and the "reviver" event
        // ------------------------------------------------------------------

        /// <summary>Going down costs 5% of your points, rounded up to the next 10.</summary>
        public int PointsLostWhenDowned(int score) => RoundUpToTen((int)(score * (double)Vars.PenaltyDowned));

        /// <summary>When a teammate bleeds out, everyone else loses 10%, rounded up to the next 10.</summary>
        public int NoRevivePenalty(int score) => RoundUpToTen((int)(score * (double)Vars.PenaltyNoRevive));

        /// <summary>Taken from the bled-out player when they respawn (0% in BO1).</summary>
        public int DiedPenalty(int score) => RoundUpToTen((int)(score * (double)Vars.PenaltyDied));

        /// <summary>The reviver earns the downed player's lost points (rounded to 5, times Double Points).</summary>
        public int PointsForRevive(int pointsLost) => pointsLost <= 0 ? 0 : PointMultiplier * RoundUpScore(pointsLost, 5);

        /// <summary>"nuke_powerup" (400) and "carpenter_powerup" (200): rounded to 5, times Double Points.</summary>
        public int PointsForPowerUp(int raw) => PointMultiplier * RoundUpScore(raw, 5);

        /// <summary>round_up_to_ten()</summary>
        public static int RoundUpToTen(int score) => RoundUpScore(score, 10);

        /// <summary>round_up_score(): round up to the next multiple of value.</summary>
        public static int RoundUpScore(int score, int value)
        {
            int rounded = score - score % value;
            if (rounded < score) rounded += value;
            return rounded;
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
