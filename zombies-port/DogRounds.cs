// DogRounds.cs
//
// Hellhound ("dog") round rules in plain C# (no engine types). Port of
// maps/_zombiemode_ai_dogs.gsc, which Kino der Toten turns on (enable_dog_rounds()):
//   dog_round_tracker()        : the first dog round is round 5, 6 or 7; then every 4-5 rounds
//   dog_round_spawning()       : 6 dogs per player on the first two dog rounds, 8 after; never more
//                                than 2 alive per standing player; a 7 s pause before the first
//   waiting_for_next_dog_spawn : 3 / 2.5 / 2 / 1.5 s between dogs, shrinking as the round goes on
//   dog_health_increase()      : 400 / 900 / 1300 / 1600 health (capped)
//   get_favorite_enemy()       : each dog hunts the player the fewest dogs are already after
//   dog_spawn_factory_logic()  : a random dog spot 400-1000 units from that player, not the last one used
//   dog_round_aftermath()      : the last dog drops a Max Ammo; the round ends 8 s later
//   dog_init()                 : dogs always sprint and bite for 40

using System;
using System.Collections.Generic;

namespace ZombiesPort
{
    public static class DogRules
    {
        public const int FirstDogRoundMin = 5;        // randomintrange( 5, 8 ): 5, 6 or 7
        public const int FirstDogRoundMax = 7;
        public const int GapMin = 4;                  // round_number + randomintrange( 4, 6 ): 4 or 5
        public const int GapMax = 5;
        public const float StartDelay = 7f;           // wait(7) for the announcer
        public const float SpawnFxSeconds = 1.5f;     // dog_spawn_fx(): lightning, wait( 1.5 ), then the dog
        public const float AftermathSeconds = 8f;     // dog_round_aftermath(): wait(2) + wait(6)
        public const int MeleeDamage = 40;            // self.meleeDamage = 40
        public const float SpawnMinUnits = 400f;      // dist_squared > 400 * 400
        public const float SpawnMaxUnits = 1000f;     // dist_squared < 1000 * 1000

        /// <summary>players * 6 on the first two dog rounds, players * 8 after.</summary>
        public static int Count(int dogRoundCount, int players) => players * (dogRoundCount < 3 ? 6 : 8);

        /// <summary>dog_health_increase(): 400, 900, 1300, then 1600 for every dog round after.</summary>
        public static int Health(int dogRoundCount)
        {
            if (dogRoundCount <= 1) return 400;
            if (dogRoundCount == 2) return 900;
            if (dogRoundCount == 3) return 1300;
            return 1600;
        }

        /// <summary>waiting_for_next_dog_spawn(): the base gap minus the fraction already spawned.</summary>
        public static float SpawnWait(int dogRoundCount, int spawned, int max)
        {
            float w = dogRoundCount == 1 ? 3f : dogRoundCount == 2 ? 2.5f : dogRoundCount == 3 ? 2f : 1.5f;
            return w - (max > 0 ? (float)spawned / max : 0f);
        }

        /// <summary>get_enemy_count() >= num_player_valid * 2 holds the next dog back.</summary>
        public static int MaxAlive(int standingPlayers) => Math.Max(1, standingPlayers) * 2;

        /// <summary>
        /// get_favorite_enemy(): the standing player with the fewest dogs on them (the first one on a
        /// tie). Increments their count. Returns -1 if nobody is standing.
        /// </summary>
        public static int PickFavoriteEnemy(int[] huntedBy, bool[] standing)
        {
            int best = -1;
            for (int i = 0; i < huntedBy.Length; i++)
            {
                if (!standing[i]) continue;
                if (best < 0 || huntedBy[i] < huntedBy[best]) best = i;
            }
            if (best >= 0) huntedBy[best]++;
            return best;
        }

        /// <summary>
        /// dog_spawn_factory_logic(): shuffle the spots, take the first one 400-1000 units from the
        /// target that isn't the last spot used; if none qualifies, the first of the shuffle.
        /// Distances are in GSC units. Returns -1 with no spots.
        /// </summary>
        public static int PickSpawn(IReadOnlyList<float> distanceUnits, int lastUsed, Random rng)
        {
            int n = distanceUnits.Count;
            if (n == 0) return -1;
            var order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            for (int i = n - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int t = order[i]; order[i] = order[j]; order[j] = t;
            }
            foreach (int i in order)
            {
                if (i == lastUsed) continue;
                float d = distanceUnits[i];
                if (d > SpawnMinUnits && d < SpawnMaxUnits) return i;
            }
            return order[0];
        }
    }

    /// <summary>dog_round_tracker(): which rounds are dog rounds, and how many have happened.</summary>
    public class DogRoundTracker
    {
        readonly Random _rng;

        /// <summary>level.dog_round_count: 1 during (and before) the first dog round.</summary>
        public int DogRoundCount { get; private set; } = 1;
        public int NextDogRound { get; private set; }

        public DogRoundTracker(int? seed = null)
        {
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
            NextDogRound = _rng.Next(DogRules.FirstDogRoundMin, DogRules.FirstDogRoundMax + 1);
        }

        public bool IsDogRound(int round) => round == NextDogRound;

        /// <summary>A dog round begins: schedule the next one 4-5 rounds later.</summary>
        public void OnDogRoundStarted(int round) =>
            NextDogRound = round + _rng.Next(DogRules.GapMin, DogRules.GapMax + 1);

        /// <summary>Testing: make a specific round the next dog round.</summary>
        public void ScheduleNext(int round) => NextDogRound = round;

        /// <summary>The dog round is over: the next one gets tougher dogs.</summary>
        public void OnDogRoundEnded() => DogRoundCount++;
    }
}
