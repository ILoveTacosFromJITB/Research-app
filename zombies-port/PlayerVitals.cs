// PlayerVitals.cs
//
// Player health, regeneration and last stand, in plain C# (no engine types). Ported from:
//   _zombiemode.gsc  player_damage_override()  : zombie hits do 50; one attacker can't hit
//                                                 again for 0.4 s; a hit that would reach 0 downs you
//                    player_laststand(), wait_and_revive(), last_stand_pistol_swap()
//   _laststand.gsc   PlayerLastStand(), Laststand_Bleedout(), revive_do_revive(), can_revive()
//   _gameskill.gsc   playerHealthRegen() at "normal" skill (zombies force skill 1, frac 0.75)
//
// The Unity side (PlayerHealth, LastStandDirector) owns timing and visuals and calls these.

using System;
using System.Collections.Generic;

namespace ZombiesPort
{
    public enum LifeState { Alive, Downed, Spectating }

    public enum HitResult { Ignored, Hurt, Lethal }

    /// <summary>What happens when a hit would down someone (decided for the whole team).</summary>
    public enum DownOutcome { Downed, DownedSelfRevive, GameOver }

    public enum VitalsEvent { None, Revived, BledOut }

    [Serializable]
    public class VitalsConfig
    {
        public int MaxHealth = 100;               // engine default; intermission resets players to 100
        public int ZombieHitDamage = 50;          // player_damage_override: iDamage = 50 without meleeDamage
        public float SameAttackerCooldown = 0.4f; // level.ignore_enemy_timer

        // playerHealthRegen() at normal difficulty (current_frac 0.75)
        public float RegenDelay = 2.4f;           // playerHealth_RegularRegenDelay 2400 ms
        public float VeryHurtFraction = 0.2f;     // healthOverlayCutoff 0.2: red flashing overlay
        public float LongRegenDelay = 5f;         // longRegenTime 5000 ms (only when very hurt)
        public float LongRegenPerSecond = 2f;     // regenRate 0.1 of max health per 0.05 s tick

        public float BleedoutSeconds = 45f;       // SetDvar( "player_lastStandBleedoutTime", "45" )
        public float ReviveSeconds = 3f;          // revive_do_revive(): reviveTime = 3
        public float QuickReviveMultiplier = 0.5f;// specialty_quickrevive halves it
        public float SoloSelfReviveSeconds = 10f; // wait_and_revive(): solo_revive_time = 10.0
        public float ReviveRadiusUnits = 40f;     // revive_trigger_radius 40
        public float ReviveFacingDot = 0.9f;      // is_facing(): dot > 0.9
    }

    public class PlayerVitals
    {
        public VitalsConfig Config { get; }
        public int MaxHealth { get; private set; } // Juggernog raises this (zombie_perk_juggernaut_health = 160)
        public float Health { get; private set; }
        public LifeState State { get; private set; } = LifeState.Alive;

        /// <summary>Solo self-revives left (Solo Quick Revive sets this to 1 in BO1).</summary>
        public int Lives { get; set; }

        public int PointsLostWhenDowned { get; private set; }  // self.score_lost_when_downed
        public float BleedoutRemaining { get; private set; }
        public float ReviveProgress { get; private set; }      // 0..1, teammate revive or solo self-revive
        public bool BeingRevived { get; private set; }
        public bool SelfReviving { get; private set; }

        public float HealthFraction => MaxHealth > 0 ? Health / MaxHealth : 0f;
        public bool IsVeryHurt => State == LifeState.Alive && Health <= MaxHealth * Config.VeryHurtFraction;

        float _lastHurtTime = float.NegativeInfinity;
        bool _veryHurtLatched;
        object _lastAttacker;
        float _ignoreAttackerUntil;
        float _selfReviveRemaining;

        public PlayerVitals(VitalsConfig config = null)
        {
            Config = config ?? new VitalsConfig();
            MaxHealth = Config.MaxHealth;
            Health = MaxHealth;
        }

        /// <summary>
        /// SetMaxHealth(): Juggernog sets 160, losing it sets 100. Current health is clamped to
        /// the new maximum; when raised, regeneration tops the player up.
        /// </summary>
        public void SetMaxHealth(int max)
        {
            MaxHealth = Math.Max(1, max);
            if (Health > MaxHealth) Health = MaxHealth;
        }

        /// <summary>
        /// player_damage_override(). Returns Lethal instead of downing the player, because whether
        /// a lethal hit is a down, a solo self-revive or game over depends on the whole team
        /// (TeamLifeRules). Call GoDown() or end the game based on that.
        /// </summary>
        public HitResult TakeHit(int damage, object attacker, float now)
        {
            if (State != LifeState.Alive || damage <= 0) return HitResult.Ignored;

            // self.ignoreAttacker: the same zombie can't land a second hit within 0.4 s.
            if (attacker != null && ReferenceEquals(attacker, _lastAttacker) && now < _ignoreAttackerUntil)
                return HitResult.Ignored;
            _lastAttacker = attacker;
            _ignoreAttackerUntil = now + Config.SameAttackerCooldown;
            _lastHurtTime = now;

            // "if( iDamage < self.health ) return finalDamage;" otherwise last stand.
            if (damage < Health)
            {
                Health -= damage;
                if (IsVeryHurt) _veryHurtLatched = true;
                return HitResult.Hurt;
            }
            return HitResult.Lethal;
        }

        /// <summary>PlayerLastStand(): health 1, bleed-out timer starts.</summary>
        public void GoDown(int pointsLost, bool selfRevive)
        {
            State = LifeState.Downed;
            Health = 1f;
            PointsLostWhenDowned = pointsLost;
            BleedoutRemaining = Config.BleedoutSeconds;
            ReviveProgress = 0f;
            BeingRevived = false;
            SelfReviving = selfRevive;
            _selfReviveRemaining = Config.SoloSelfReviveSeconds;
        }

        /// <summary>
        /// Call every frame. Alive: regenerates health. Downed: counts down the bleed-out (or
        /// the solo self-revive). Returns Revived or BledOut on the frame it happens.
        /// </summary>
        public VitalsEvent Tick(float now, float dt)
        {
            if (State == LifeState.Alive)
            {
                Regen(now, dt);
                return VitalsEvent.None;
            }
            if (State != LifeState.Downed) return VitalsEvent.None;

            if (SelfReviving)
            {
                _selfReviveRemaining -= dt;
                ReviveProgress = 1f - Math.Max(0f, _selfReviveRemaining) / Config.SoloSelfReviveSeconds;
                if (_selfReviveRemaining <= 0f)
                {
                    Lives--; // self.lives-- after auto_revive
                    Revive();
                    return VitalsEvent.Revived;
                }
                return VitalsEvent.None;
            }

            BleedoutRemaining = Math.Max(0f, BleedoutRemaining - dt);
            // Laststand_Bleedout(): at 0 it waits while someone is mid-revive.
            if (BleedoutRemaining <= 0f && !BeingRevived)
            {
                State = LifeState.Spectating;
                ReviveProgress = 0f;
                return VitalsEvent.BledOut;
            }
            return VitalsEvent.None;
        }

        /// <summary>
        /// playerHealthRegen(): once 2.4 s pass without a hit, health snaps back to full. If it
        /// dropped to 20% or less, it waits 5 s after the last hit, then refills quickly.
        /// </summary>
        void Regen(float now, float dt)
        {
            if (Health >= MaxHealth) { Health = MaxHealth; _veryHurtLatched = false; return; }
            if (now - _lastHurtTime < Config.RegenDelay) return;

            if (!_veryHurtLatched)
            {
                Health = MaxHealth;
                return;
            }
            if (now > _lastHurtTime + Config.LongRegenDelay)
            {
                Health = Math.Min(MaxHealth, Health + MaxHealth * Config.LongRegenPerSecond * dt);
                if (Health >= MaxHealth) _veryHurtLatched = false;
            }
        }

        /// <summary>
        /// revive_do_revive(): call every frame with whether a valid reviver is holding the button.
        /// Letting go resets progress, as in BO1. Returns true on the frame the revive completes.
        /// </summary>
        public bool UpdateRevive(bool reviverHolding, bool reviverHasQuickRevive, float dt)
        {
            if (State != LifeState.Downed || SelfReviving) return false;
            if (!reviverHolding)
            {
                BeingRevived = false;
                ReviveProgress = 0f;
                return false;
            }
            BeingRevived = true;
            float time = Config.ReviveSeconds * (reviverHasQuickRevive ? Config.QuickReviveMultiplier : 1f);
            ReviveProgress += dt / time;
            if (ReviveProgress >= 1f)
            {
                Revive();
                return true;
            }
            return false;
        }

        void Revive()
        {
            State = LifeState.Alive;
            Health = MaxHealth;
            ReviveProgress = 0f;
            BeingRevived = false;
            SelfReviving = false;
            _veryHurtLatched = false;
        }

        /// <summary>spectator_respawn(): back in at the end of the round.</summary>
        public void Respawn()
        {
            Revive();
            PointsLostWhenDowned = 0;
        }

        /// <summary>The points the reviver earns back are handed out once, then cleared.</summary>
        public int TakePointsLostWhenDowned()
        {
            int p = PointsLostWhenDowned;
            PointsLostWhenDowned = 0;
            return p;
        }
    }

    public static class TeamLifeRules
    {
        /// <summary>
        /// player_damage_override(): if anyone else is still standing, the victim goes down and
        /// can be revived. If the victim was the last one standing, the game is over, unless
        /// it's solo and they have a self-revive (Solo Quick Revive).
        /// </summary>
        public static DownOutcome OnLethalHit(int victim, IReadOnlyList<LifeState> states, int victimLives)
        {
            for (int i = 0; i < states.Count; i++)
                if (i != victim && states[i] == LifeState.Alive) return DownOutcome.Downed;

            bool solo = states.Count == 1;
            if (solo && victimLives > 0) return DownOutcome.DownedSelfRevive;
            return DownOutcome.GameOver;
        }

        /// <summary>level.pistol_values: the order last stand ranks pistols in (best last).</summary>
        public static readonly string[] PistolRank =
        {
            "m1911_zm", "cz75_zm", "cz75dw_zm", "python_zm", "python_upgraded_zm", "cz75_upgraded_zm",
            "cz75dw_upgraded_zm", "m1911_upgraded_zm", "ray_gun_zm", "freezegun_zm", "ray_gun_upgraded_zm",
            "freezegun_upgraded_zm", "microwavegundw_zm", "microwavegundw_upgraded_zm",
        };

        /// <summary>
        /// last_stand_best_pistol(): the highest-ranked pistol the player owns that still has
        /// ammo, otherwise the M1911. In solo, anything ranked at or below the upgraded Python
        /// is swapped for the M1911 (BO1's _special_solo_pistol_swap).
        /// </summary>
        public static string BestPistol(IReadOnlyList<string> primaries, Func<string, bool> hasAmmo, bool solo,
                                        string defaultPistol = "m1911_zm")
        {
            int best = -1;
            string bestId = null;
            foreach (var w in primaries)
            {
                int rank = Array.IndexOf(PistolRank, w);
                if (rank < 0) continue;                                   // not a pistol
                if (w != defaultPistol && hasAmmo != null && !hasAmmo(w)) continue;
                if (rank > best) { best = rank; bestId = w; }
            }
            if (bestId == null) return defaultPistol;
            if (solo && best <= 4) return defaultPistol;
            return bestId;
        }
    }
}
