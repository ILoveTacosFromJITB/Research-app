// PowerUpPickup.cs
//
// A power-up floating where a zombie died. Pooled by PowerUpDirector, so drops never
// Instantiate. Ports from _zombiemode_powerups.gsc:
//   powerup_drop()    : spawned 40 units above the kill spot
//   powerup_wobble()  : slowly turning in the air (a cheap spin-and-bob stands in for the random tumble)
//   powerup_timeout() : solid for 15 s, then 40 blinks that speed up, then gone (26.5 s total)
//   powerup_grab()    : any player whose feet come within 64 units of it picks it up
//                       (downed players included, as in BO1; bled-out players can't)
//
// Without a model, each type shows as a colored sphere with a letter. Colors use a
// MaterialPropertyBlock, so no material assets are created or modified.

using TMPro;
using UnityEngine;

namespace ZombiesPort
{
    public class PowerUpPickup : MonoBehaviour
    {
        [SerializeField] Transform model;
        [SerializeField] TextMeshPro label;
        [SerializeField] float spinDegreesPerSecond = 90f;
        [SerializeField] float bobHeight = 0.08f;

        public PowerUpId Id { get; private set; }
        public bool InUse => gameObject.activeSelf;

        static readonly int ColorId = Shader.PropertyToID("_Color");          // Built-in pipeline
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");  // URP

        PowerUpDirector _director;
        Renderer[] _renderers;
        MaterialPropertyBlock _block;
        float _spawnTime, _nextBlink;
        int _blink;
        bool _visible;
        Vector3 _restPosition;

        internal void Init(PowerUpDirector director)
        {
            _director = director;
            if (model == null) model = transform;
            _renderers = GetComponentsInChildren<Renderer>(true);
            _block = new MaterialPropertyBlock();
            gameObject.SetActive(false);
        }

        internal void Show(PowerUpId id, Vector3 position, Color color, string letter)
        {
            Id = id;
            _restPosition = position;
            transform.position = position;
            _spawnTime = Time.time;
            _blink = 0;
            _nextBlink = _spawnTime + PowerUpInfo.SolidSeconds;

            _block.SetColor(ColorId, color);
            _block.SetColor(BaseColorId, color);
            foreach (var r in _renderers)
            {
                // The letter is TextMeshPro, which needs its own material settings: leave it alone.
                if (r == null || (label != null && r.gameObject == label.gameObject)) continue;
                r.SetPropertyBlock(_block);
            }
            if (label != null) label.text = letter;

            gameObject.SetActive(true);
            SetVisible(true);
        }

        void Update()
        {
            float now = Time.time;

            // powerup_wobble(), simplified.
            model.Rotate(0f, spinDegreesPerSecond * Time.deltaTime, 0f, Space.World);
            transform.position = _restPosition + Vector3.up * (Mathf.Sin((now - _spawnTime) * 2f) * bobHeight);

            // powerup_timeout()
            if (now >= _nextBlink)
            {
                if (_blink >= PowerUpInfo.BlinkToggles) { _director.Release(this); return; }
                SetVisible(_blink % 2 == 0); // "if ( i % 2 ) hide(); else show();"
                _nextBlink = now + PowerUpInfo.BlinkDelay(_blink);
                _blink++;
            }

            // powerup_grab(): DistanceSquared( player.origin, powerup.origin ) < 64 * 64
            var boot = GameBootstrap.Instance;
            if (boot == null) return;
            float r = PowerUpInfo.GrabRadiusUnits * MysteryBoxRules.UnitsToMeters;
            foreach (var p in boot.Players)
            {
                if (p == null || p.IsSpectating) continue;
                if ((p.transform.position - _restPosition).sqrMagnitude < r * r)
                {
                    _director.OnGrabbed(this, p);
                    return;
                }
            }
        }

        void SetVisible(bool on)
        {
            if (_visible == on) return;
            _visible = on;
            foreach (var rend in _renderers) if (rend != null) rend.enabled = on;
        }
    }
}
