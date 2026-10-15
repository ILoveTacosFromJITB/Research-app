// SoundAliasBank.cs
//
// CoD-style sound aliases: one name ("wpn_colt45_fire_plr") -> several variant clips with a volume,
// pitch and distance range. Built by Zombies > BYO > Add Sounds from sound_aliases.json
// (tools/byo_sounds.py); it only references clips inside the project's never-shared BYO folder.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombiesPort
{
    [CreateAssetMenu(menuName = "Zombies/Sound Alias Bank")]
    public class SoundAliasBank : ScriptableObject
    {
        [Serializable]
        public class Alias
        {
            public string name;
            public AudioClip[] clips;
            public float volMin = 1f, volMax = 1f;
            public float pitchMin = 1f, pitchMax = 1f;
            [Tooltip("Metres. 0 = a 2D sound (heard the same everywhere, like the player's own gun).")]
            public float distMin, distMax;
            public bool loop;
            [Tooltip("Most copies of this alias playing at once (CoD limitCount); 0 = no limit.")]
            public int limit;
            [Tooltip("Played together with this one (CoD 'secondaryname', e.g. a gunshot's reverb tail).")]
            public string secondary;
        }

        public List<Alias> aliases = new List<Alias>();

        Dictionary<string, Alias> _lookup;

        public Alias Find(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_lookup == null)
            {
                _lookup = new Dictionary<string, Alias>(StringComparer.OrdinalIgnoreCase);
                foreach (var a in aliases) _lookup[a.name] = a;
            }
            return _lookup.TryGetValue(name, out var alias) ? alias : null;
        }
    }
}
