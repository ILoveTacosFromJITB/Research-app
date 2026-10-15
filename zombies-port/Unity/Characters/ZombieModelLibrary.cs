// ZombieModelLibrary.cs
//
// Body and head models plus animation clips per role for ZombieModel. Built by
// Zombies > BYO > Add Real Zombies from zombies_manifest.json (tools/byo_zombies.py); it only references
// assets inside the project's never-shared BYO folder.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombiesPort
{
    [CreateAssetMenu(menuName = "Zombies/Zombie Model Library")]
    public class ZombieModelLibrary : ScriptableObject
    {
        [Serializable]
        public class Role
        {
            public string name;             // idle, walk, run, sprint, attack, tear, window_attack, climb, death
            public List<TextAsset> clips = new List<TextAsset>();
        }

        public GameObject[] bodies = new GameObject[0];
        public GameObject[] heads = new GameObject[0];
        public List<Role> roles = new List<Role>();

        public List<TextAsset> Clips(string role)
        {
            foreach (var r in roles) if (r.name == role) return r.clips;
            return null;
        }
    }
}
