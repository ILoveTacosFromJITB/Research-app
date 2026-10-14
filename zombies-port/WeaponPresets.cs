// WeaponPresets.cs
//
// Starting stats for Kino der Toten's wall weapons. Names and prices come from the GSC
// (init_weapons); the stats are placeholders, because BO1's weapon files aren't in the dump.
// They're tuned to feel distinct against BO1 zombie health (150 on round 1, 1045 on round 10):
// rifles hit hard and pierce, SMGs spray, shotguns are close-range burst damage.
//
// PlayerWeapon falls back to these when a gun has no WeaponDefinition asset, so a wall gun is
// never shot with M1911 stats by accident. Tune them in each gun's WeaponDefinition.
//
// Simplifications: shotguns fire one high-damage hitscan that pierces 3 zombies instead of
// pellets, and the M16's 3-round burst is modeled as fast automatic fire.

using System.Collections.Generic;

namespace ZombiesPort
{
    public static class WeaponPresets
    {
        static WeaponStats Make(string id, string name, int damage, bool semi, float interval, int mag, int start,
                                int max, float reload, float range, int pen, float hip, float ads)
        {
            var s = WeaponStats.M1911();
            s.Id = id;
            s.DisplayName = name;
            s.Damage = damage;
            s.SemiAuto = semi;
            s.ShotInterval = interval;
            s.MagazineSize = mag;
            s.StartingReserve = start;
            s.MaxReserve = max;
            s.ReloadSeconds = reload;
            s.EmptyReloadSeconds = reload * 1.2f;
            s.Range = range;
            s.Penetration = pen;
            s.HipSpreadDegrees = hip;
            s.AdsSpreadDegrees = ads;
            return s;
        }

        static readonly Dictionary<string, WeaponStats> Table = new Dictionary<string, WeaponStats>
        {
            //                     id               name         dmg  semi   rate   mag start max  reload range pen  hip  ads
            { "m14_zm",        Make("m14_zm",        "M14",       100, true,  0.12f,  8,  40,  96, 2.0f, 80f, 2, 2.0f, 0.2f) },
            { "rottweil72_zm", Make("rottweil72_zm", "Olympia",   150, true,  0.30f,  2,  38,  60, 2.6f, 12f, 3, 4.0f, 3.0f) },
            { "mp5k_zm",       Make("mp5k_zm",       "MP5K",       45, false, 0.07f, 30, 120, 180, 2.0f, 40f, 1, 3.0f, 1.0f) },
            { "mp40_zm",       Make("mp40_zm",       "MP40",       50, false, 0.10f, 32, 160, 192, 2.2f, 40f, 1, 3.0f, 1.0f) },
            { "mpl_zm",        Make("mpl_zm",        "MPL",        45, false, 0.065f,24, 120, 192, 2.0f, 40f, 1, 3.0f, 1.0f) },
            { "pm63_zm",       Make("pm63_zm",       "PM63",       45, false, 0.06f, 20, 100, 100, 2.0f, 35f, 1, 3.5f, 1.2f) },
            { "ak74u_zm",      Make("ak74u_zm",      "AK-74u",     55, false, 0.075f,20, 160, 200, 1.9f, 45f, 1, 3.0f, 0.9f) },
            { "m16_zm",        Make("m16_zm",        "M16",        60, false, 0.08f, 30, 120, 240, 2.2f, 70f, 2, 2.5f, 0.5f) },
            { "ithaca_zm",     Make("ithaca_zm",     "Stakeout",  160, true,  0.70f,  6,  54,  60, 3.0f, 15f, 3, 4.0f, 3.0f) },
        };

        public static bool TryGet(string id, out WeaponStats stats) => Table.TryGetValue(id, out stats);

        public static IEnumerable<string> Ids => Table.Keys;
    }
}
