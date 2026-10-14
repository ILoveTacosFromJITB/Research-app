// EditorPlacement.cs  (editor only)
//
// Where the Zombies menu items put things. Normally that's relative to the Scene view camera
// (in front of it, facing it). "Build Test Level" overrides it so every piece lands in a
// fixed, sensible layout around the player's start.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ZombiesPort.EditorTools
{
    internal static class EditorPlacement
    {
        static bool _override;
        static Vector3 _viewer, _forward, _pivot;

        /// <summary>Pretend the Scene view camera stands at viewer, looking along forward, focused on pivot.</summary>
        internal static void Set(Vector3 viewer, Vector3 forward, Vector3 pivot)
        {
            _override = true;
            _viewer = viewer;
            _forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            _pivot = pivot;
        }

        internal static void Clear() => _override = false;

        /// <summary>The viewer position, flat forward direction, and focus point (on the ground).</summary>
        internal static bool TryGet(out Vector3 viewer, out Vector3 forward, out Vector3 pivot)
        {
            if (_override)
            {
                viewer = _viewer; forward = _forward; pivot = _pivot;
                return true;
            }
            var view = SceneView.lastActiveSceneView;
            if (view == null)
            {
                viewer = Vector3.zero; forward = Vector3.forward; pivot = Vector3.zero;
                return false;
            }
            viewer = view.camera.transform.position;
            forward = Vector3.ProjectOnPlane(view.rotation * Vector3.forward, Vector3.up).normalized;
            pivot = view.pivot;
            pivot.y = 0f;
            return true;
        }
    }
}
#endif
