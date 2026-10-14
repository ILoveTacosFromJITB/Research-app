// VirtualJoystick.cs
//
// The left-thumb movement stick. Put it on a large invisible zone (TouchZoneGraphic) covering
// the lower-left of the screen. With Floating on, the stick appears wherever the thumb lands
// inside the zone, so players don't have to find a fixed spot without looking. It returns
// to its resting place when released.
//
// It follows one finger (by pointer id), so the other thumb can aim and shoot at the same time.

using UnityEngine;
using UnityEngine.EventSystems;

namespace ZombiesPort
{
    public class VirtualJoystick : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
    {
        [SerializeField] RectTransform stickBase;
        [SerializeField] RectTransform knob;
        [Tooltip("How far the knob travels from the center, in canvas units.")]
        [SerializeField] float radius = 110f;
        [Tooltip("Ignore tiny thumb movements near the center (0-1).")]
        [Range(0f, 0.5f)] [SerializeField] float deadZone = 0.12f;
        [SerializeField] bool floating = true;
        [Tooltip("Stick opacity while untouched, so it's visible but not distracting.")]
        [Range(0f, 1f)] [SerializeField] float idleAlpha = 0.35f;

        /// <summary>-1..1 on each axis. y is forward.</summary>
        public Vector2 Value { get; private set; }

        const int NoPointer = int.MinValue;
        int _pointerId = NoPointer;
        Vector2 _restPosition;
        CanvasGroup _baseGroup;

        void Awake()
        {
            if (stickBase != null)
            {
                _restPosition = stickBase.anchoredPosition;
                _baseGroup = stickBase.GetComponent<CanvasGroup>();
                if (_baseGroup == null) _baseGroup = stickBase.gameObject.AddComponent<CanvasGroup>();
                _baseGroup.blocksRaycasts = false; // Touches belong to the zone, not the visuals.
                _baseGroup.alpha = idleAlpha;
            }
        }

        public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;

        public void OnPointerDown(PointerEventData e)
        {
            if (_pointerId != NoPointer || stickBase == null) return;
            _pointerId = e.pointerId;
            _baseGroup.alpha = 1f;

            if (floating && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)stickBase.parent, e.position, e.pressEventCamera, out var local))
            {
                stickBase.localPosition = local;
            }
            UpdateStick(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId == _pointerId) UpdateStick(e);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == _pointerId) Release();
        }

        // A phone call or app switch can swallow the finger-up event.
        void OnDisable() => Release();
        void OnApplicationPause(bool paused) { if (paused) Release(); }

        void UpdateStick(PointerEventData e)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(stickBase, e.position, e.pressEventCamera, out var local))
                return;

            Vector2 clamped = Vector2.ClampMagnitude(local, radius);
            if (knob != null) knob.anchoredPosition = clamped;

            Vector2 v = clamped / radius;
            float mag = v.magnitude;
            // Rescale past the dead zone so the output still ramps smoothly from 0 to 1.
            Value = mag < deadZone ? Vector2.zero : v / mag * ((mag - deadZone) / (1f - deadZone));
        }

        void Release()
        {
            _pointerId = NoPointer;
            Value = Vector2.zero;
            if (knob != null) knob.anchoredPosition = Vector2.zero;
            if (stickBase != null)
            {
                stickBase.anchoredPosition = _restPosition;
                if (_baseGroup != null) _baseGroup.alpha = idleAlpha;
            }
        }
    }
}
