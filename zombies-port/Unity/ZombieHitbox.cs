// ZombieHitbox.cs
//
// Optional: put on a child collider of a zombie (for example a sphere on the head bone) to say
// which hit zone it is. Without hitboxes, PlayerWeapon works out the zone from how high on
// the zombie's collider the bullet landed (HitZoneUtil.FromHeight), which is enough for the
// capsule test zombie. Real zombie models should use a head hitbox: it's the 100-point shot.

using UnityEngine;

namespace ZombiesPort
{
    public class ZombieHitbox : MonoBehaviour
    {
        public HitZone zone = HitZone.Head;
    }
}
