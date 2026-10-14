// TouchZoneGraphic.cs
//
// An invisible, touchable area. uGUI only sends touches to objects with a Graphic, and the
// usual trick (an Image with alpha 0) still costs a draw call and overdraw across half the
// screen. This Graphic builds an empty mesh, so it draws nothing but still catches touches.
// The move-stick zone and the look area use it.

using UnityEngine;
using UnityEngine.UI;

namespace ZombiesPort
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class TouchZoneGraphic : Graphic
    {
        protected override void OnPopulateMesh(VertexHelper vh) => vh.Clear();
    }
}
