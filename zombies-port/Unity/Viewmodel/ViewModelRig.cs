// ViewModelRig.cs
//
// The gun and arms you see in first person. Sits under the player camera, renders through an
// overlay camera (its own FOV, never clips into walls) and plays the weapon's own viewmodel
// clips in response to PlayerWeapon events, the way BO1's weapon state machine picks them:
//   raise/first raise on equip, idle (or empty idle), fire / last shot / ADS fire, reload or
//   reload-empty stretched to the weapon's reload time, ADS up/down following the aim amount,
//   sprint loop while sprinting, knife swing on melee. A muzzle flash fires at tag_flash.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ZombiesPort
{
    public class ViewModelRig : MonoBehaviour
    {
        public sealed class Bone
        {
            public Transform Transform;
            public Vector3 BindPosition;
            public Quaternion BindRotation;
            /// <summary>Translations are offsets from the bind pose even in "absolute" clips. True for the gun
            /// model's own bones (j_bolt, tag_flash...): the clips move the slide relative to where the gun model
            /// puts it, while the arms skeleton (tag_weapon, j_shoulder...) is animated absolutely.</summary>
            public bool Relative;
        }

        [SerializeField] ViewModelLibrary library;
        [SerializeField] PlayerWeapon weapon;
        [SerializeField] FirstPersonController controller;
        [SerializeField] float viewmodelFov = 65f;   // BO1 cg_fov default; the viewmodel keeps it while the world zooms
        [SerializeField] float flashSeconds = 0.07f;
        [SerializeField] Color flashColor = new Color(1f, 0.75f, 0.35f);

        public ViewModelLibrary Library => library;

        /// <summary>Fired for each clip notetrack and for weapon events (key = weapon-file sound field).</summary>
        public event Action<string, string> SoundRequested; // (weaponId, sound key or notetrack)

        readonly Dictionary<string, Bone> _bones = new Dictionary<string, Bone>();
        readonly Dictionary<string, ViewModelClip> _clipCache = new Dictionary<string, ViewModelClip>();
        GameObject _hands, _gun;
        Transform _flashPoint;
        GameObject _flash;
        Light _flashLight;
        float _flashUntil;
        int _layer;
        Camera _overlay;
        ViewModelLibrary.Entry _entry;
        bool _firstEquip = true;

        readonly List<Transform> _hidden = new List<Transform>();
        bool _sawReloading;
        ViewModelClip _oneShot;
        float _oneShotTime, _oneShotSpeed = 1f;
        string _oneShotKey;
        int _lastNotify;
        ViewModelClip _idleClip;
        float _idleTime;

        void Awake()
        {
            _layer = LayerMask.NameToLayer("Viewmodel");
            if (_layer < 0) _layer = 0;
            var cam = GetComponentInParent<Camera>();
            if (cam != null && _layer != 0) SetupOverlayCamera(cam);
            if (weapon == null) weapon = GetComponentInParent<PlayerWeapon>();
            if (controller == null) controller = GetComponentInParent<FirstPersonController>();
        }

        void OnEnable()
        {
            if (weapon == null) return;
            weapon.Equipped += OnEquipped;
            weapon.Fired += OnFired;
            weapon.DryFired += OnDryFired;
            weapon.ReloadStarted += OnReload;
            weapon.Knifed += OnKnifed;
        }

        void OnDisable()
        {
            if (weapon == null) return;
            weapon.Equipped -= OnEquipped;
            weapon.Fired -= OnFired;
            weapon.DryFired -= OnDryFired;
            weapon.ReloadStarted -= OnReload;
            weapon.Knifed -= OnKnifed;
        }

        void Start()
        {
            if (library != null && library.handsPrefab != null)
            {
                _hands = Instantiate(library.handsPrefab, transform);
                _hands.name = "Hands";
                AlignToView(_hands);
            }
            if (weapon != null && weapon.Current != null) OnEquipped(weapon.Current);
        }

        void SetupOverlayCamera(Camera main)
        {
            main.cullingMask &= ~(1 << _layer);
            var go = new GameObject("ViewmodelCamera");
            go.transform.SetParent(main.transform, false);
            _overlay = go.AddComponent<Camera>();
            _overlay.cullingMask = 1 << _layer;
            _overlay.fieldOfView = viewmodelFov;
            _overlay.nearClipPlane = 0.01f;
            _overlay.farClipPlane = 10f;
            _overlay.clearFlags = CameraClearFlags.Depth;
            var overlayData = _overlay.GetUniversalAdditionalCameraData();
            overlayData.renderType = CameraRenderType.Overlay;
            main.GetUniversalAdditionalCameraData().cameraStack.Add(_overlay);
        }

        // tag_view is the eye in CoD: put it on the camera, CoD forward (+X) along the view.
        void AlignToView(GameObject model)
        {
            var root = model.transform;
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one * 0.0254f; // the models are in inches
            var tagView = FindDeep(root, "tag_view");
            if (tagView == null) return;
            // In the converted hierarchy CoD forward is local -X and CoD up is local +Z.
            Quaternion want = transform.rotation * Quaternion.Inverse(Quaternion.LookRotation(Vector3.left, Vector3.forward));
            root.rotation = want * Quaternion.Inverse(tagView.rotation) * root.rotation;
            root.position += transform.position - tagView.position;
            SetLayer(model);
        }

        void OnEquipped(WeaponState state)
        {
            _entry = library != null ? library.Find(state.Stats.Id) : null;
            if (_gun != null)
            {
                // Destroy() waits for the end of the frame: detach first so the bone map below can't pick it up.
                _gun.transform.SetParent(null, false);
                _gun.SetActive(false);
                Destroy(_gun);
            }
            _gun = null;
            if (_entry != null && _entry.gunPrefab != null && _hands != null)
            {
                var tagWeapon = FindDeep(_hands.transform, "tag_weapon");
                _gun = Instantiate(_entry.gunPrefab, tagWeapon != null ? tagWeapon : _hands.transform);
                _gun.name = "Gun (" + state.Stats.Id + ")";
                // The gun's skeleton root carries the Z-up -> Y-up conversion; under tag_weapon we're already converted.
                var skel = _gun.transform.childCount > 0 ? FindSkeletonRoot(_gun.transform) : null;
                _gun.transform.localPosition = Vector3.zero;
                _gun.transform.localRotation = Quaternion.identity;
                _gun.transform.localScale = Vector3.one;
                if (skel != null)
                {
                    skel.localPosition = Vector3.zero;
                    skel.localRotation = Quaternion.identity;
                }
                foreach (var smr in _gun.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
                SetLayer(_gun);
                _flashPoint = FindDeep(_gun.transform, "tag_flash");
                BuildFlash();
            }
            if (_hands != null) foreach (var smr in _hands.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
            RebuildBones();
            _hidden.Clear();
            if (_entry != null && _gun != null)
                foreach (var tag in _entry.hideTags)
                {
                    var t = FindDeep(_gun.transform, tag);
                    if (t != null) _hidden.Add(t);
                }

            _idleClip = null;
            bool first = _firstEquip && Has("firstRaiseAnim");
            _firstEquip = false;
            PlayOneShot(first ? "firstRaiseAnim" : "raiseAnim", 0f);
            Sound(first ? "firstRaiseSoundPlayer" : "raiseSoundPlayer");
        }

        void OnFired(WeaponState state)
        {
            bool ads = weapon.AimAmount > 0.5f;
            bool last = state.Clip == 0;
            string key = ads ? (last && Has("adsLastShotAnim") ? "adsLastShotAnim" : "adsFireAnim")
                             : (last && Has("lastShotAnim") ? "lastShotAnim" : "fireAnim");
            PlayOneShot(key, 0f);
            _flashUntil = Time.time + flashSeconds;
            Sound(last && _entry?.Sound("lastShotSoundPlayer") != null ? "lastShotSoundPlayer" : "fireSoundPlayer");
        }

        void OnDryFired(WeaponState state) => Sound("emptyFireSoundPlayer");

        void OnReload(WeaponState state)
        {
            bool empty = state.Clip == 0 && Has("reloadEmptyAnim");
            PlayOneShot(empty ? "reloadEmptyAnim" : "reloadAnim", empty ? state.Stats.EmptyReloadSeconds : state.Stats.ReloadSeconds);
        }

        void OnKnifed(bool hit)
        {
            var clip = library != null ? Get(library.knifeClip) : null;
            if (clip == null) return;
            _oneShot = clip;
            _oneShotKey = "melee";
            _oneShotTime = 0f;
            _oneShotSpeed = 1f;
            _lastNotify = 0;
            SoundRequested?.Invoke(_entry?.weaponId, hit ? "knife_hit" : "knife_swing");
        }

        void LateUpdate()
        {
            if (_bones.Count == 0) return;

            if (_oneShot != null)
            {
                // Reloads are cancelled by swapping, going down... (the weapon may only flag the reload a frame later)
                if (_oneShotKey == "reload" && weapon.Current != null)
                {
                    if (weapon.Current.IsReloading) _sawReloading = true;
                    else if (_sawReloading) _oneShot = null;
                }
            }

            if (_oneShot != null)
            {
                _oneShotTime += Time.deltaTime * _oneShotSpeed;
                _oneShot.Apply(_oneShotTime, _bones);
                FireNotifies(_oneShot, _oneShotTime);
                if (_oneShotTime >= _oneShot.Length) _oneShot = null;
            }
            else
            {
                PoseIdle();
            }

            foreach (var h in _hidden) if (h != null) h.localScale = Vector3.zero; // collapses the attachment's skinned geometry
            UpdateFlash();
        }

        void PoseIdle()
        {
            float aim = weapon != null ? weapon.AimAmount : 0f;
            if (aim > 0.001f && Has("adsUpAnim"))
            {
                // Holding the sights: the ADS-up clip scrubbed by how far the aim has come in.
                var ads = Get("adsUpAnim");
                ads.Apply(aim * ads.Length, _bones);
                return;
            }

            bool sprint = controller != null && controller.IsSprinting && Has("sprintLoopAnim");
            bool empty = weapon != null && weapon.Current != null && weapon.Current.Clip == 0 && Has("emptyIdleAnim");
            var clip = Get(sprint ? "sprintLoopAnim" : empty ? "emptyIdleAnim" : "idleAnim");
            if (clip == null) return;
            if (clip != _idleClip) { _idleClip = clip; _idleTime = 0f; }
            _idleTime += Time.deltaTime;
            float len = Mathf.Max(clip.Length, 0.01f);
            clip.Apply(_idleTime % len, _bones);
        }

        void PlayOneShot(string key, float targetSeconds)
        {
            var clip = Get(key);
            if (clip == null) { _oneShot = null; return; }
            ResetToBind();
            _oneShot = clip;
            _oneShotKey = key == "reloadAnim" || key == "reloadEmptyAnim" ? "reload" : key;
            _oneShotTime = 0f;
            _oneShotSpeed = targetSeconds > 0.01f && clip.Length > 0.01f ? clip.Length / targetSeconds : 1f;
            _lastNotify = 0;
            _sawReloading = false;
        }

        void FireNotifies(ViewModelClip clip, float t)
        {
            float frac = clip.Length > 0f ? t / clip.Length : 1f;
            while (_lastNotify < clip.Notifies.Count && clip.Notifies[_lastNotify].time <= frac)
            {
                SoundRequested?.Invoke(_entry?.weaponId, clip.Notifies[_lastNotify].name);
                _lastNotify++;
            }
        }

        void Sound(string key)
        {
            if (_entry != null && _entry.Sound(key) != null) SoundRequested?.Invoke(_entry.weaponId, key);
        }

        // ---------------- muzzle flash ----------------

        void BuildFlash()
        {
            if (_flashPoint == null) return;
            _flash = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _flash.name = "MuzzleFlash";
            Destroy(_flash.GetComponent<Collider>());
            _flash.transform.SetParent(_flashPoint, false);
            _flash.transform.localScale = Vector3.one * 7f; // inches, the gun hierarchy is unscaled CoD units
            var mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = FlashTexture(), color = flashColor };
            _flash.GetComponent<MeshRenderer>().sharedMaterial = mat;
            _flash.layer = _layer;
            var lightGo = new GameObject("FlashLight");
            lightGo.transform.SetParent(_flashPoint, false);
            _flashLight = lightGo.AddComponent<Light>();
            _flashLight.type = LightType.Point;
            _flashLight.color = flashColor;
            _flashLight.range = 8f;
            _flashLight.intensity = 1.2f;
            _flash.SetActive(false);
            _flashLight.enabled = false;
        }

        void UpdateFlash()
        {
            if (_flash == null) return;
            bool on = Time.time < _flashUntil;
            if (on && !_flash.activeSelf)
            {
                _flash.transform.rotation = Quaternion.LookRotation(transform.forward, transform.up) * Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
                float s = UnityEngine.Random.Range(5f, 8f);
                _flash.transform.localScale = new Vector3(s, s, s);
                // Light the room ahead of the barrel, not the top of the gun (a light on the muzzle blows it out).
                _flashLight.transform.position = _flashPoint.position + transform.forward * 0.6f;
            }
            _flash.SetActive(on);
            _flashLight.enabled = on;
        }

        static Texture2D _flashTex;

        static Texture2D FlashTexture()
        {
            if (_flashTex != null) return _flashTex;
            const int n = 64;
            _flashTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - n / 2f) / (n / 2f), dy = (y - n / 2f) / (n / 2f);
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx);
                    float star = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 3f)), 8f) * 0.6f; // six spikes
                    float a = Mathf.Clamp01(1f - r / (0.35f + star));
                    a = a * a;
                    _flashTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            _flashTex.Apply();
            return _flashTex;
        }

        // ---------------- bones / helpers ----------------

        void RebuildBones()
        {
            _bones.Clear();
            AddBones(_gun, true);   // gun first: its bones win any name clash
            AddBones(_hands, false);
        }

        void AddBones(GameObject model, bool relative)
        {
            if (model == null) return;
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (!_bones.ContainsKey(t.name) && t != model.transform)
                    _bones[t.name] = new Bone { Transform = t, BindPosition = t.localPosition, BindRotation = t.localRotation, Relative = relative };
        }

        void ResetToBind()
        {
            foreach (var b in _bones.Values)
            {
                if (b.Transform == null) continue;
                b.Transform.localPosition = b.BindPosition;
                b.Transform.localRotation = b.BindRotation;
            }
        }

        bool Has(string key) => _entry != null && _entry.Clip(key) != null;

        ViewModelClip Get(string key) => _entry != null ? Get(_entry.Clip(key)) : null;

        ViewModelClip Get(TextAsset asset)
        {
            if (asset == null) return null;
            if (!_clipCache.TryGetValue(asset.name, out var clip)) _clipCache[asset.name] = clip = ViewModelClip.Load(asset);
            return clip;
        }

        void SetLayer(GameObject go)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = _layer;
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        static Transform FindSkeletonRoot(Transform root)
        {
            foreach (Transform c in root) if (c.name.EndsWith("_skel")) return c;
            return null;
        }
    }
}
