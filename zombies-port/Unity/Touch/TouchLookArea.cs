// TouchLookArea.cs
//
// The right side of the screen: drag to turn and aim. Sits behind the action buttons in the
// hierarchy, so a touch on a button goes to the button and a touch anywhere else aims.
// TouchButtons with "drag to look" also feed into it, so a player can hold Fire and
// keep aiming with the same thumb, as most mobile shooters allow.
//
// Reports raw pixel movement; MobileInput turns it into degrees using the screen's DPI,
// so sensitivity feels the same on a small phone and a large tablet.

using UnityEngine;
using UnityEngine.EventSystems;

namespace ZombiesPort
{
    public class TouchLookArea : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
    {
        const int NoPointer = int.MinValue;
        int _pointerId = NoPointer;
        Vector2 _pixels;

        /// <summary>Pixels dragged since the last call.</summary>
        public Vector2 ConsumeDelta()
        {
            Vector2 d = _pixels;
            _pixels = Vector2.zero;
            return d;
        }

        /// <summary>Lets buttons forward their drags (Fire with drag-to-look).</summary>
        public void AddDelta(Vector2 pixels) => _pixels += pixels;

        // Respond on the first pixel of movement, not after Unity's default drag threshold.
        public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;

        public void OnPointerDown(PointerEventData e)
        {
            if (_pointerId == NoPointer) _pointerId = e.pointerId;
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId == _pointerId) _pixels += e.delta;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == _pointerId) _pointerId = NoPointer;
        }

        void OnDisable()
        {
            _pointerId = NoPointer;
            _pixels = Vector2.zero;
        }
    }
}
