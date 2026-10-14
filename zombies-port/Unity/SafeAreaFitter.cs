// SafeAreaFitter.cs
//
// Shrinks this RectTransform to Screen.safeArea so touch controls never sit under a notch,
// a camera cutout, rounded corners, or the iOS home indicator. Put it on a full-screen panel
// directly under the HUD Canvas and parent every button to that panel.
//
// The safe area changes when the phone rotates between landscape-left and landscape-right
// (the notch swaps sides), so it is re-checked each frame. It only does work when the value
// actually changes.

using UnityEngine;

namespace ZombiesPort
{
    [RequireComponent(typeof(RectTransform))]
    [ExecuteAlways]
    public class SafeAreaFitter : MonoBehaviour
    {
        RectTransform _rect;
        Rect _applied;
        Vector2Int _appliedScreen;

        void OnEnable()
        {
            _rect = (RectTransform)transform;
            Apply(force: true);
        }

        void Update() => Apply(force: false);

        void Apply(bool force)
        {
            Rect safe = Screen.safeArea;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (!force && safe == _applied && screen == _appliedScreen) return;
            if (screen.x <= 0 || screen.y <= 0) return;

            _applied = safe;
            _appliedScreen = screen;

            // Anchors are 0-1 fractions of the parent (the full-screen Canvas), so dividing the
            // safe rect by the screen size gives the anchors directly.
            Vector2 min = safe.position;
            Vector2 max = safe.position + safe.size;
            min.x /= screen.x; min.y /= screen.y;
            max.x /= screen.x; max.y /= screen.y;

            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
