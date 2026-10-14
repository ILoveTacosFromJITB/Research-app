// TouchButton.cs
//
// A hold-able on-screen button for Fire, Aim, Reload, Knife and Swap. Unlike uGUI's Button,
// it reports "held" every frame (needed for shooting), fires on touch-down, and can forward
// drags to the look area so the same thumb shoots and aims.
//
// Presses are latched: ConsumePress() returns true once per tap, so a tap shorter than a
// frame is never missed.

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZombiesPort
{
    public class TouchButton : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IDragHandler, IInitializePotentialDragHandler
    {
        [Tooltip("Optional: drags that start on this button also turn the camera.")]
        [SerializeField] TouchLookArea dragToLook;
        [SerializeField] Graphic visual;
        [SerializeField] Color normalColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] Color pressedColor = new Color(1f, 1f, 1f, 0.7f);
        [SerializeField] float pressedScale = 0.92f;

        const int NoPointer = int.MinValue;
        int _pointerId = NoPointer;
        bool _pressLatched;
        Vector3 _baseScale = Vector3.one;

        public bool IsHeld => _pointerId != NoPointer;

        void Awake()
        {
            if (visual == null) visual = GetComponent<Graphic>();
            _baseScale = transform.localScale;
            SetVisual(false);
        }

        /// <summary>True once for each tap since the last call.</summary>
        public bool ConsumePress()
        {
            bool p = _pressLatched;
            _pressLatched = false;
            return p;
        }

        public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;

        public void OnPointerDown(PointerEventData e)
        {
            if (IsHeld) return;
            _pointerId = e.pointerId;
            _pressLatched = true;
            SetVisual(true);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == _pointerId) Release();
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId == _pointerId && dragToLook != null) dragToLook.AddDelta(e.delta);
        }

        void OnDisable() => Release();
        void OnApplicationPause(bool paused) { if (paused) Release(); }

        void Release()
        {
            _pointerId = NoPointer;
            SetVisual(false);
        }

        void SetVisual(bool pressed)
        {
            if (visual != null) visual.color = pressed ? pressedColor : normalColor;
            transform.localScale = pressed ? _baseScale * pressedScale : _baseScale;
        }
    }
}
