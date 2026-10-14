// ZombiePool.cs
//
// A fixed pool of zombies, created once during loading and reused all game. BO1 capped live
// zombies at 24 (level.zombie_ai_limit), so 24 objects cover every round: round 30's 105
// zombies are the same 24 bodies recycled.
//
// Why this matters on mobile: Instantiate and Destroy during play allocate memory and later
// trigger garbage collection, which shows up as frame hitches and costs battery. Here nothing
// is created or destroyed after loading. Spawning reactivates a sleeping zombie; dying puts it
// back to sleep.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZombiesPort
{
    public class ZombiePool : MonoBehaviour
    {
        [SerializeField] ZombieAgent prefab;
        [Tooltip("Matches level.zombie_ai_limit. Corpses also hold a slot until they sink away.")]
        [SerializeField] int size = 24;
        [Tooltip("Spread creation over frames so loading doesn't freeze on low-end phones.")]
        [SerializeField] int createPerFrame = 4;

        readonly Stack<ZombieAgent> _free = new Stack<ZombieAgent>();
        readonly List<ZombieAgent> _all = new List<ZombieAgent>();

        public bool IsReady { get; private set; }
        public int Capacity => size;
        public int FreeCount => _free.Count;
        public IReadOnlyList<ZombieAgent> All => _all;

        /// <summary>Creates every zombie up front. RoundDirector waits for this before round 1.</summary>
        public IEnumerator Prewarm()
        {
            if (IsReady) yield break;
            if (prefab == null)
            {
                Debug.LogError("ZombiePool: no zombie prefab assigned.", this);
                yield break;
            }

            for (int i = 0; i < size; i++)
            {
                var z = Instantiate(prefab, transform);
                z.name = prefab.name + "_" + i.ToString("00");
                z.gameObject.SetActive(false);
                z.OnCreated(this);
                _all.Add(z);
                _free.Push(z);
                if ((i + 1) % createPerFrame == 0) yield return null;
            }
            IsReady = true;
        }

        /// <summary>Wakes a sleeping zombie at the given spot. Returns null if all are in use.</summary>
        public ZombieAgent Take(Vector3 position, Quaternion rotation)
        {
            if (_free.Count == 0) return null;
            var z = _free.Pop();
            // Position before activating, so the NavMeshAgent wakes up on the right spot.
            z.transform.SetPositionAndRotation(position, rotation);
            z.gameObject.SetActive(true);
            return z;
        }

        /// <summary>Puts a zombie back to sleep. Safe to call twice.</summary>
        public void Release(ZombieAgent z)
        {
            if (z == null || !z.gameObject.activeSelf) return;
            z.gameObject.SetActive(false);
            _free.Push(z);
        }
    }
}
