// ZombieSpawnPoint.cs
//
// A place zombies can spawn: one of the GSC "zombie_spawner" entities that the zone
// manager put into level.enemy_spawns. Set Zone to the zone it belongs to: with a ZoneManager in
// the scene, it's only used while that zone is open and occupied (or next to an occupied zone).
// "Active At Start" is a manual on/off switch on top of that, for scripted events.

using UnityEngine;

namespace ZombiesPort
{
    public class ZombieSpawnPoint : MonoBehaviour
    {
        [SerializeField] bool activeAtStart = true;
        [Tooltip("The window zombies from here must tear through. Leave empty for spawns that are already inside.")]
        [SerializeField] Barricade window;
        [Tooltip("The zone this spawner belongs to (the GSC zone volume's target). Empty = not zone-restricted.")]
        [SerializeField] string zone = "start_zone";

        public bool IsActive { get; set; }
        public Barricade Window => window;
        public string Zone => zone;

        void Awake() => IsActive = activeAtStart;

        void OnDrawGizmos()
        {
            Gizmos.color = Application.isPlaying && !IsActive ? Color.gray : Color.red;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.4f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.8f);
        }
    }
}
