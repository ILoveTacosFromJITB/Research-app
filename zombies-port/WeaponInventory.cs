// WeaponInventory.cs
//
// The player's weapon slots, ported from treasure_chest_give_weapon() and weapon_give()
// in maps/_zombiemode_weapons.gsc. Plain C# with no engine types, so it can be unit-tested
// and shared by the Mystery Box, wall buys and Pack-a-Punch later.

using System;
using System.Collections.Generic;

namespace ZombiesPort
{
    public class WeaponInventory
    {
        // Weapons that never take a primary slot. In GSC these are the is_offhand_weapon()
        // weapons. The cymbal monkey goes into the tactical slot (player_give_cymbal_monkey).
        static readonly HashSet<string> Tacticals = new HashSet<string>
        {
            "zombie_cymbal_monkey", "zombie_black_hole_bomb", "zombie_nesting_dolls"
        };

        readonly List<string> _primaries = new List<string>();
        public IReadOnlyList<string> Primaries => _primaries;
        public string Current { get; private set; }
        public string Tactical { get; private set; }

        /// <summary>Mule Kick (specialty_additionalprimaryweapon) raises the limit from 2 to 3.</summary>
        public bool HasMuleKick { get; set; }
        public int WeaponLimit => HasMuleKick ? 3 : 2;

        /// <summary>Raised when a weapon is added or refilled: (weaponId, wasRefill).</summary>
        public event Action<string, bool> OnWeaponGiven;
        /// <summary>Raised when the held weapon is swapped out to make room.</summary>
        public event Action<string> OnWeaponTaken;

        public WeaponInventory(string startingPistol = "m1911_zm")
        {
            if (!string.IsNullOrEmpty(startingPistol))
            {
                _primaries.Add(startingPistol);
                Current = startingPistol;
            }
        }

        /// <summary>
        /// has_weapon_or_upgrade(). Pass the upgrade name from the weapon table, because a few
        /// don't follow the pattern (m16_zm upgrades to m16_gl_upgraded_zm). Without one, the
        /// usual "_upgraded_zm" suffix is assumed.
        /// </summary>
        public bool HasWeaponOrUpgrade(string weapon, string upgradeId = null)
        {
            string upgraded = upgradeId
                ?? (weapon.EndsWith("_zm") ? weapon.Substring(0, weapon.Length - 3) + "_upgraded_zm" : null);
            foreach (var w in _primaries)
                if (w == weapon || w == upgraded) return true;
            return Tactical == weapon;
        }

        /// <summary>
        /// Adds a weapon without the slot limit or swapping anything out. Last stand uses this
        /// to hand over the M1911 when both slots hold other guns (GSC GiveWeapon does the same).
        /// </summary>
        public void AddTemporary(string weapon)
        {
            if (!_primaries.Contains(weapon)) _primaries.Add(weapon);
        }

        /// <summary>Takes a weapon away. If it was in hand, switches to the first remaining one.</summary>
        public void Remove(string weapon)
        {
            if (!_primaries.Remove(weapon)) return;
            if (Current == weapon) Current = _primaries.Count > 0 ? _primaries[0] : null;
        }

        /// <summary>
        /// Bleeding out loses everything: on respawn the player starts again with just the
        /// pistol (spectator_respawn gives the default loadout).
        /// </summary>
        public void ResetTo(string startingPistol)
        {
            _primaries.Clear();
            Tactical = null;
            HasMuleKick = false;
            _primaries.Add(startingPistol);
            Current = startingPistol;
        }

        /// <summary>
        /// take_additionalprimaryweapon(): losing Mule Kick takes the third gun (the last one
        /// gained). If it was in hand, the first gun comes out. Returns the gun taken, or null.
        /// </summary>
        public string LoseMuleKick()
        {
            HasMuleKick = false;
            if (_primaries.Count < 3) return null;
            string taken = _primaries[_primaries.Count - 1];
            if (Current == taken) Current = _primaries[0];
            _primaries.RemoveAt(_primaries.Count - 1);
            OnWeaponTaken?.Invoke(taken);
            return taken;
        }

        public void SwitchTo(string weapon)
        {
            if (_primaries.Contains(weapon)) Current = weapon;
        }

        /// <summary>
        /// treasure_chest_give_weapon():
        ///  - Already owned: refill ammo (GiveStartAmmo) and switch to it.
        ///  - Slots full: the weapon in the player's hands is taken away first.
        ///  - Otherwise: give it and switch to it.
        /// </summary>
        public void Give(string weapon)
        {
            if (Tacticals.Contains(weapon))
            {
                bool refill = Tactical == weapon;
                Tactical = weapon;
                OnWeaponGiven?.Invoke(weapon, refill);
                return;
            }

            if (_primaries.Contains(weapon))
            {
                Current = weapon;
                OnWeaponGiven?.Invoke(weapon, true);
                return;
            }

            if (_primaries.Count >= WeaponLimit && Current != null)
            {
                string dropped = Current;
                _primaries.Remove(dropped);
                OnWeaponTaken?.Invoke(dropped);
            }

            _primaries.Add(weapon);
            Current = weapon;
            OnWeaponGiven?.Invoke(weapon, false);
        }
    }
}
