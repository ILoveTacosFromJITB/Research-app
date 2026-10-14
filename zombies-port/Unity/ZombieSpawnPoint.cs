// ZombieSpawnPoint.cs
//
// A place zombies can spawn: one of the GSC "zombie_spawner" entities that the zone
// manager put into level.enemy_spawns. In BO1 only spawners in zones the players had opened
// were active. Until the zone manager is ported, tick "Active At Start" on the spawners for
// the starting room, and have doors set IsActive = true on the rest when they open.

using UnityEngine;

namespace ZombiesPort
{
    public class ZombieSpawnPoint : MonoBehaviour
    {
        [SerializeField] bool activeAtStart = true;

        public bool IsActive { get; set; }

        void Awake() => IsActive = activeAtStart;

        void OnDrawGizmos()
        {
            Gizmos.color = Application.isPlaying && !IsActive ? Color.gray : Color.red;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.4f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.8f);
        }
    }
}
