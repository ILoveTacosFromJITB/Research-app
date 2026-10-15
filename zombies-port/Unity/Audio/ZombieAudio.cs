// ZombieAudio.cs
//
// Plays sound aliases from a SoundAliasBank through a small pool of AudioSources:
// a random variant, random volume/pitch inside the alias's range, 2D for player sounds (no distance)
// and 3D with the alias's min/max distance otherwise, plus the alias's secondary sound.

using System.Collections.Generic;
using UnityEngine;

namespace ZombiesPort
{
    public class ZombieAudio : MonoBehaviour
    {
        public static ZombieAudio Instance { get; private set; }

        [SerializeField] SoundAliasBank bank;
        [SerializeField] int voices = 24;
        [SerializeField, Range(0f, 2f)] float masterVolume = 0.7f;   // headroom: gunshot + tail + foley stack up
        [SerializeField, Range(0f, 2f)] float sfx2DVolume = 0.8f;      // the player's own sounds (no distance)

        readonly List<AudioSource> _pool = new List<AudioSource>();
        readonly Dictionary<AudioSource, string> _playing = new Dictionary<AudioSource, string>();
        AudioSource _music;
        int _next;

        public SoundAliasBank Bank => bank;

        /// <summary>Raised for every sound started (alias, clip) - for tests and debug overlays.</summary>
        public event System.Action<string, AudioClip> Played;

        void Awake()
        {
            Instance = this;
            for (int i = 0; i < voices; i++)
            {
                var go = new GameObject("Voice " + i);
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.rolloffMode = AudioRolloffMode.Linear;
                _pool.Add(src);
            }
            _music = gameObject.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.spatialBlend = 0f;
            _music.loop = true;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Plays an alias; at a position for 3D aliases (pass null for "at the listener").</summary>
        public AudioSource Play(string alias, Vector3? position = null, float volumeScale = 1f) => Play(alias, position, volumeScale, 0);

        AudioSource Play(string aliasName, Vector3? position, float volumeScale, int depth)
        {
            var alias = bank != null ? bank.Find(aliasName) : null;
            if (alias == null || alias.clips == null || alias.clips.Length == 0) return null;
            var clip = alias.clips[Random.Range(0, alias.clips.Length)];
            if (clip == null) return null;
            if (alias.limit > 0 && CountPlaying(alias.name) >= alias.limit) return null; // CoD limitCount

            var src = NextVoice();
            bool is3D = alias.distMax > 0.01f && position.HasValue;
            src.transform.position = position ?? Vector3.zero;
            src.spatialBlend = is3D ? 1f : 0f;
            // Full volume inside the alias's min distance, then the natural inverse falloff (CoD's default curve
            // is close to this); the max distance only cuts it off.
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = Mathf.Max(1.5f, alias.distMin);
            src.maxDistance = Mathf.Max(src.minDistance + 1f, alias.distMax);
            src.dopplerLevel = 0f;
            src.clip = clip;
            src.loop = false;
            src.volume = Random.Range(alias.volMin, alias.volMax) * volumeScale * masterVolume * (is3D ? 1f : sfx2DVolume);
            _playing[src] = alias.name;
            src.pitch = Random.Range(alias.pitchMin, alias.pitchMax);
            src.Play();
            Played?.Invoke(alias.name, clip);

            if (depth == 0 && !string.IsNullOrEmpty(alias.secondary) && alias.secondary != alias.name)
                Play(alias.secondary, position, volumeScale, depth + 1);
            return src;
        }

        public void PlayMusic(string aliasName, float volumeScale = 1f)
        {
            var alias = bank != null ? bank.Find(aliasName) : null;
            if (alias == null || alias.clips.Length == 0) return;
            _music.clip = alias.clips[0];
            _music.volume = alias.volMax * volumeScale * masterVolume;
            _music.loop = true;
            _music.Play();
            Played?.Invoke(alias.name, _music.clip);
        }

        public void StopMusic() => _music.Stop();

        int CountPlaying(string alias)
        {
            int n = 0;
            foreach (var kv in _playing) if (kv.Key.isPlaying && kv.Value == alias) n++;
            return n;
        }

        AudioSource NextVoice()
        {
            // Prefer a free voice; otherwise steal the oldest one.
            for (int i = 0; i < _pool.Count; i++)
            {
                var s = _pool[(_next + i) % _pool.Count];
                if (!s.isPlaying) { _next = (_next + i + 1) % _pool.Count; return s; }
            }
            var steal = _pool[_next];
            _next = (_next + 1) % _pool.Count;
            steal.Stop();
            return steal;
        }
    }
}
