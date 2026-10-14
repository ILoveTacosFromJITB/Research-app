// ZoneVolume.cs
//
// One box of a zone (the GSC "info_volume" entities named after the zone). A zone can be made of
// several boxes; give them the same Zone Name. ZoneManager checks once a second which zones
// have players in them.
//
// The box is a trigger, so it never blocks anything. Bullets ignore triggers, and the interact
// system skips it because it isn't interactable.

using UnityEngine;

namespace ZombiesPort
{
    [RequireComponent(typeof(BoxCollider))]
    public class ZoneVolume : MonoBehaviour
    {
        [SerializeField] string zoneName = "start_zone";

        public string ZoneName => zoneName;

        BoxCollider _box;

        void Awake()
        {
            _box = GetComponent<BoxCollider>();
            _box.isTrigger = true;
        }

        /// <summary>IsTouching( volume ), using the box's own rotated space.</summary>
        public bool Contains(Vector3 worldPoint)
        {
            if (_box == null) _box = GetComponent<BoxCollider>();
            Vector3 local = transform.InverseTransformPoint(worldPoint) - _box.center;
            Vector3 half = _box.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.08f);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
