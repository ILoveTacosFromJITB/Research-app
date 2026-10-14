// WeaponState.cs
//
// Gun and knife rules in plain C# (no engine types): ammo, fire rate, semi-auto trigger,
// reloads, and turning a hit position into a hit zone. PlayerWeapon (Unity) owns one
// WeaponState per gun the player carries, so swapping keeps each gun's ammo.
//
// About the numbers: the GSC scripts only name weapons ("m1911_zm"). Their stats lived in
// the compiled weapon files (weapons/sp/*), which aren't in the dump. The M1911 and knife
// values below are placeholders chosen to feel right against BO1 zombie health (150 on round 1,
// +100 a round to round 9). Tune them in the WeaponDefinition asset.

using System;

namespace ZombiesPort
{
    [Serializable]
    public struct WeaponStats
    {
        public string Id;               // matches WeaponInventory / Mystery Box ids, e.g. "m1911_zm"
        public string DisplayName;
        public int Damage;              // per bullet, body shot
        public float HeadMultiplier;
        public float NeckMultiplier;
        public float Range;             // meters
        public float ShotInterval;      // seconds between shots (60 / rpm)
        public bool SemiAuto;           // one shot per trigger pull
        public int MagazineSize;
        public int StartingReserve;     // GiveStartAmmo(): ammo outside the magazine on pickup
        public int MaxReserve;          // what Max Ammo fills to
        public float ReloadSeconds;
        public float EmptyReloadSeconds; // reloading from empty takes longer (chambering a round)
        public float HipSpreadDegrees;
        public float AdsSpreadDegrees;
        public int Penetration;         // how many zombies one bullet can pass through
        public float AdsFov;
        public float AdsSeconds;        // time to raise or lower the sights
        public float MoveSpeedScale;    // heavier guns slow the player

        /// <summary>
        /// The starting pistol (level.laststandpistol = "m1911_zm"). Placeholder values: 8-round
        /// magazine with 32 spare, kills a round-1 zombie in 6 body shots or 3 headshots.
        /// </summary>
        public static WeaponStats M1911() => new WeaponStats
        {
            Id = "m1911_zm",
            DisplayName = "M1911",
            Damage = 25,
            HeadMultiplier = 2f,
            NeckMultiplier = 1.5f,
            Range = 60f,
            ShotInterval = 0.1f,
            SemiAuto = true,
            MagazineSize = 8,
            StartingReserve = 32,
            MaxReserve = 80,
            ReloadSeconds = 1.6f,
            EmptyReloadSeconds = 2.0f,
            HipSpreadDegrees = 2.5f,
            AdsSpreadDegrees = 0.4f,
            Penetration = 1,
            AdsFov = 55f,
            AdsSeconds = 0.2f,
            MoveSpeedScale = 1f,
        };

        public int DamageFor(HitZone zone)
        {
            float m = zone == HitZone.Head ? HeadMultiplier : zone == HitZone.Neck ? NeckMultiplier : 1f;
            return (int)Math.Round(Damage * m);
        }
    }

    /// <summary>The knife. A kill with it pays the 80-point melee bonus (130 total).</summary>
    [Serializable]
    public struct MeleeStats
    {
        public int Damage;
        public float Range;           // meters in front of the player
        public float ConeDegrees;     // how far off-center a zombie can be and still get hit
        public float Cooldown;        // seconds between swings
        public float BusySeconds;     // can't shoot during the swing

        /// <summary>Placeholder: 150 kills a round-1 zombie in one swing and round 2 in two.</summary>
        public static MeleeStats Knife() => new MeleeStats
        {
            Damage = 150, Range = 1.6f, ConeDegrees = 50f, Cooldown = 0.7f, BusySeconds = 0.45f,
        };
    }

    public enum FireResult
    {
        None,        // trigger not pulled, or waiting for the next shot
        Fired,       // one bullet left the gun
        DryFire,     // empty with no spare ammo (once per trigger pull)
        AutoReload,  // empty magazine, reload started instead of firing
        Busy,        // reloading
    }

    public class WeaponState
    {
        public WeaponStats Stats { get; }
        public int Clip { get; private set; }
        public int Reserve { get; private set; }
        public bool IsReloading { get; private set; }

        /// <summary>Speed Cola sets 0.5: reloads take half as long.</summary>
        public float ReloadTimeScale { get; set; } = 1f;
        /// <summary>Double Tap sets 0.75: less time between shots.</summary>
        public float FireIntervalScale { get; set; } = 1f;

        float _reloadStarted, _reloadEnds, _nextShot;
        bool _triggerReleased = true;
        bool _dryFiredThisPull;

        public WeaponState(WeaponStats stats)
        {
            Stats = stats;
            RefillToStart();
        }

        /// <summary>GiveStartAmmo(): a full magazine and at least the starting reserve (never takes ammo away).</summary>
        public void RefillToStart()
        {
            Clip = Stats.MagazineSize;
            Reserve = Math.Max(Reserve, Stats.StartingReserve);
            IsReloading = false;
        }

        /// <summary>Fills magazine and reserve completely.</summary>
        public void RefillToMax()
        {
            Clip = Stats.MagazineSize;
            Reserve = Stats.MaxReserve;
        }

        /// <summary>
        /// The Max Ammo power-up: GiveMaxAmmo() fills the spare ammo to the maximum and leaves
        /// what's in the magazine alone.
        /// </summary>
        public void FillReserveToMax() => Reserve = Math.Max(Reserve, Stats.MaxReserve);

        /// <summary>SetWeaponAmmoStock(): set the spare ammo directly (last stand uses this).</summary>
        public void SetReserve(int reserve) => Reserve = Math.Max(0, reserve);

        /// <summary>
        /// Called every frame with the trigger state. holdToRepeat lets a semi-auto keep firing
        /// at its maximum rate while the button is held. On a touch screen, hammering a
        /// button eight times per magazine is tiring, so it's on by default in PlayerWeapon.
        /// </summary>
        public FireResult TryFire(float now, bool triggerHeld, bool holdToRepeat)
        {
            if (!triggerHeld)
            {
                _triggerReleased = true;
                _dryFiredThisPull = false;
                return FireResult.None;
            }
            if (IsReloading) return FireResult.Busy;
            if (now < _nextShot) return FireResult.None;
            if (Stats.SemiAuto && !holdToRepeat && !_triggerReleased) return FireResult.None;

            if (Clip <= 0)
            {
                if (Reserve > 0 && StartReload(now)) return FireResult.AutoReload;
                // One click per trigger hold, including when the trigger was held through the last bullet.
                if (_dryFiredThisPull) return FireResult.None;
                _dryFiredThisPull = true;
                _triggerReleased = false;
                return FireResult.DryFire;
            }

            Clip--;
            _nextShot = now + Stats.ShotInterval * FireIntervalScale;
            _triggerReleased = false;
            return FireResult.Fired;
        }

        public bool StartReload(float now)
        {
            if (IsReloading || Clip >= Stats.MagazineSize || Reserve <= 0) return false;
            IsReloading = true;
            _reloadStarted = now;
            _reloadEnds = now + (Clip == 0 ? Stats.EmptyReloadSeconds : Stats.ReloadSeconds) * ReloadTimeScale;
            return true;
        }

        /// <summary>Swapping weapons mid-reload cancels it. Nothing is lost.</summary>
        public void CancelReload() => IsReloading = false;

        public float ReloadProgress(float now)
        {
            if (!IsReloading) return 0f;
            float length = _reloadEnds - _reloadStarted;
            return length <= 0f ? 1f : Math.Min(1f, (now - _reloadStarted) / length);
        }

        /// <summary>Finishes a reload once its time is up. Call every frame.</summary>
        public void Tick(float now)
        {
            if (!IsReloading || now < _reloadEnds) return;
            int take = Math.Min(Stats.MagazineSize - Clip, Reserve);
            Clip += take;
            Reserve -= take;
            IsReloading = false;
        }
    }

    public static class HitZoneUtil
    {
        /// <summary>
        /// Hit zone from how high up the body the bullet landed (0 = feet, 1 = top of the head).
        /// Used when a zombie has a single collider instead of per-bone hitboxes.
        /// </summary>
        public static HitZone FromHeight(float normalized)
        {
            if (normalized >= 0.86f) return HitZone.Head;
            if (normalized >= 0.80f) return HitZone.Neck;
            if (normalized >= 0.62f) return HitZone.TorsoUpper;
            if (normalized >= 0.45f) return HitZone.TorsoLower;
            return HitZone.Limb;
        }
    }
}
