// AmmoHud.cs
//
// Ammo counter ("8 / 32"), weapon name, the "Reload" / "No Ammo" hint, and the crosshair,
// which fades out while aiming down sights.
//
// Text is only rewritten when a value actually changes. Rewriting TextMeshPro every frame
// would rebuild its mesh 60 times a second for nothing. Like RoundHud, it has its own nested
// Canvas so these updates don't rebuild the touch controls.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombiesPort
{
    [RequireComponent(typeof(Canvas))]
    public class AmmoHud : MonoBehaviour
    {
        [SerializeField] PlayerWeapon weapon;
        [SerializeField] TMP_Text clipText;
        [SerializeField] TMP_Text reserveText;
        [SerializeField] TMP_Text weaponName;
        [SerializeField] TMP_Text hint;
        [SerializeField] Graphic crosshair;
        [SerializeField] Color normalColor = Color.white;
        [SerializeField] Color lowColor = new Color(1f, 0.3f, 0.3f);
        [Tooltip("The clip turns red and the Reload hint shows at or below this fraction of a magazine.")]
        [Range(0f, 1f)] [SerializeField] float lowFraction = 0.25f;

        int _lastClip = -1, _lastReserve = -1, _lastHint = -1;
        string _lastName;

        void Awake()
        {
            foreach (var g in GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
        }

        void Start()
        {
            if (weapon == null) weapon = FindFirstObjectByType<PlayerWeapon>();
        }

        void LateUpdate()
        {
            var w = weapon != null ? weapon.Current : null;
            if (w == null) return;

            if (w.Clip != _lastClip)
            {
                _lastClip = w.Clip;
                if (clipText != null)
                {
                    clipText.SetText("{0}", w.Clip);
                    clipText.color = w.Clip <= w.Stats.MagazineSize * lowFraction ? lowColor : normalColor;
                }
            }
            if (w.Reserve != _lastReserve)
            {
                _lastReserve = w.Reserve;
                if (reserveText != null) reserveText.SetText("/ {0}", w.Reserve);
            }
            if (w.Stats.DisplayName != _lastName)
            {
                _lastName = w.Stats.DisplayName;
                if (weaponName != null) weaponName.text = _lastName;
            }

            // 0 = none, 1 = "Reload", 2 = "No Ammo", 3 = "Reloading"
            int h = w.IsReloading ? 3
                : w.Clip == 0 && w.Reserve == 0 ? 2
                : w.Clip <= w.Stats.MagazineSize * lowFraction && w.Reserve > 0 ? 1
                : 0;
            if (h != _lastHint)
            {
                _lastHint = h;
                if (hint != null)
                {
                    hint.text = h == 1 ? "Reload" : h == 2 ? "No Ammo" : h == 3 ? "Reloading..." : "";
                    hint.color = h == 2 ? lowColor : normalColor;
                }
            }

            if (crosshair != null)
            {
                var c = crosshair.color;
                float a = 1f - weapon.AimAmount;
                if (!Mathf.Approximately(c.a, a)) { c.a = a; crosshair.color = c; }
            }
        }
    }
}
