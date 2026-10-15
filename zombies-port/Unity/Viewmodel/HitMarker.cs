// HitMarker.cs
//
// The white "X" that flashes in the middle of the screen when a bullet hits a zombie
// (BO1's damage feedback: shown for a moment, then fades).

using UnityEngine;
using UnityEngine.UI;

namespace ZombiesPort
{
    [RequireComponent(typeof(Graphic))]
    public class HitMarker : MonoBehaviour
    {
        [SerializeField] PlayerWeapon weapon;
        [SerializeField] float showSeconds = 0.25f;
        [SerializeField] Color headshotColor = new Color(1f, 0.85f, 0.4f);

        Graphic _graphic;
        float _shownAt = -10f;
        Color _color = Color.white;

        void Awake()
        {
            _graphic = GetComponent<Graphic>();
            if (weapon == null) weapon = FindFirstObjectByType<PlayerWeapon>();
            Set(0f);
        }

        void OnEnable() { if (weapon != null) weapon.ZombieHit += OnHit; }
        void OnDisable() { if (weapon != null) weapon.ZombieHit -= OnHit; }

        void OnHit(ZombieAgent zombie, HitZone zone)
        {
            _shownAt = Time.time;
            _color = zone == HitZone.Head ? headshotColor : Color.white;
        }

        void Update()
        {
            float k = 1f - (Time.time - _shownAt) / showSeconds;
            Set(Mathf.Clamp01(k));
        }

        void Set(float alpha)
        {
            var c = _color;
            c.a = alpha;
            _graphic.color = c;
        }
    }
}
