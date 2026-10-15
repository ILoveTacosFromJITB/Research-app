// ZombieModel.cs
//
// The visible zombie: a random body + head (like character/c_ger_honorguard_zt.gsc picks from its
// xmodelalias lists) animated with the game's own AI clips through ViewModelClip. ZombieAgent stays in
// charge of movement and logic; this listens to its AnimationCue events and ground speed:
//   locomotion  walk / run / sprint clip by ZombieSpeed, played at a rate matched to the actual speed
//   cues        "tear" board tear, "attack" swipe, "climb" window traverse, "die" death (holds last frame)

using System.Collections.Generic;
using UnityEngine;

namespace ZombiesPort
{
    public class ZombieModel : MonoBehaviour
    {
        [SerializeField] ZombieModelLibrary library;
        [SerializeField] ZombieAgent agent;
        [Tooltip("Ground speed (m/s) the clips were authored at, used to match playback rate.")]
        [SerializeField] float walkClipSpeed = 0.95f, runClipSpeed = 2.2f, sprintClipSpeed = 4.0f;
        [SerializeField] float blendSeconds = 0.15f;

        readonly Dictionary<string, ViewModelRig.Bone> _body = new Dictionary<string, ViewModelRig.Bone>();
        readonly Dictionary<string, ViewModelRig.Bone> _head = new Dictionary<string, ViewModelRig.Bone>();
        static readonly Dictionary<TextAsset, ViewModelClip> Cache = new Dictionary<TextAsset, ViewModelClip>();
        static readonly HashSet<string> Skip = new HashSet<string> { "tag_origin" }; // carries the Z-up -> Y-up conversion

        GameObject _bodyGo, _headGo;
        ViewModelClip _loop, _oneShot;
        float _loopTime, _oneShotTime, _blend = 1f;
        bool _dead;
        Renderer[] _renderers;

        void Awake()
        {
            if (agent == null) agent = GetComponentInParent<ZombieAgent>();
            if (agent != null) agent.AnimationCue += OnCue;
        }

        void OnDestroy()
        {
            if (agent != null) agent.AnimationCue -= OnCue;
        }

        void OnCue(string cue)
        {
            switch (cue)
            {
                case "spawn": Respawn(); break;
                case "tear": PlayOneShot("tear"); break;
                case "attack": PlayOneShot(agent != null && agent.IsOutside ? "window_attack" : "attack"); break;
                case "climb": PlayOneShot("climb"); break;
                case "die": _dead = true; PlayOneShot("death"); break;
            }
        }

        void Respawn()
        {
            _dead = false;
            _oneShot = null;
            _loop = null;
            if (_bodyGo == null) Build();
        }

        // One random body and head per pooled zombie (kept across respawns: pooled zombies are reused).
        void Build()
        {
            if (library == null || library.bodies.Length == 0) return;
            _bodyGo = Instantiate(library.bodies[Random.Range(0, library.bodies.Length)], transform);
            _bodyGo.transform.localPosition = Vector3.zero;
            _bodyGo.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); // CoD forward (+X) -> Unity forward (+Z)
            _bodyGo.transform.localScale = Vector3.one * 0.0254f;            // inches -> metres
            Collect(_bodyGo.transform, _body);

            if (library.heads.Length > 0)
            {
                _headGo = Instantiate(library.heads[Random.Range(0, library.heads.Length)], _bodyGo.transform);
                // CoD attach(head, "", true): the head's skeleton rides on the body's bones of the same name.
                Transform headRoot = null;
                foreach (var t in _headGo.GetComponentsInChildren<Transform>(true))
                    if (t.name == "j_spine4") { headRoot = t; break; }
                if (headRoot != null && _body.TryGetValue("j_spine4", out var bodySpine))
                {
                    headRoot.SetParent(bodySpine.Transform, false);
                    headRoot.localPosition = Vector3.zero;
                    headRoot.localRotation = Quaternion.identity;
                    headRoot.localScale = Vector3.one;
                }
                Collect(_headGo.transform, _head);
                _head.Remove("j_spine4");
            }
            _renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in _renderers)
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = false;
        }

        static void Collect(Transform root, Dictionary<string, ViewModelRig.Bone> into)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && !Skip.Contains(t.name) && !into.ContainsKey(t.name))
                    into[t.name] = new ViewModelRig.Bone { Transform = t, BindPosition = t.localPosition, BindRotation = t.localRotation };
        }

        void PlayOneShot(string role)
        {
            var clip = Pick(role);
            if (clip == null) return;
            _oneShot = clip;
            _oneShotTime = 0f;
            _blend = 0f;
        }

        ViewModelClip Pick(string role)
        {
            var list = library != null ? library.Clips(role) : null;
            if (list == null || list.Count == 0) return null;
            var asset = list[Random.Range(0, list.Count)];
            if (!Cache.TryGetValue(asset, out var clip)) Cache[asset] = clip = ViewModelClip.Load(asset);
            return clip;
        }

        void LateUpdate()
        {
            if (_bodyGo == null || agent == null) return;

            // Skip off-screen zombies (24 on screen is the budget on a phone).
            bool visible = false;
            foreach (var r in _renderers) if (r != null && r.isVisible) { visible = true; break; }

            float dt = Time.deltaTime;
            if (_oneShot != null)
            {
                _oneShotTime += dt;
                if (_oneShotTime >= _oneShot.Length && !_dead) _oneShot = null;
                else
                {
                    _blend = Mathf.Min(1f, _blend + dt / blendSeconds);
                    if (visible) Apply(_oneShot, Mathf.Min(_oneShotTime, _oneShot.Length - 0.001f), _blend);
                    return;
                }
            }
            if (_dead) return;

            // Locomotion: the clip for this zombie's speed class, at a rate that keeps the feet planted.
            float speed = agent.MoveSpeed;
            string role = speed < 0.15f ? "idle" : agent.Speed == ZombieSpeed.Walk ? "walk" : agent.Speed == ZombieSpeed.Run ? "run" : "sprint";
            if (_loop == null || _loopRole != role) { _loop = Pick(role); _loopRole = role; _loopTime = Random.value * 2f; _blend = 0f; }
            if (_loop == null) return;
            float authored = role == "walk" ? walkClipSpeed : role == "run" ? runClipSpeed : role == "sprint" ? sprintClipSpeed : 0f;
            float rate = authored > 0f ? Mathf.Clamp(speed / authored, 0.5f, 2f) : 1f;
            _loopTime += dt * rate;
            _blend = Mathf.Min(1f, _blend + dt / blendSeconds);
            if (visible) Apply(_loop, _loopTime % Mathf.Max(0.01f, _loop.Length), _blend);
        }

        string _loopRole;

        void Apply(ViewModelClip clip, float t, float weight)
        {
            clip.Apply(t, _body, weight);
            if (_head.Count > 0) clip.Apply(t, _head, weight);
        }
    }
}
