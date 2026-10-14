// Barricade.cs
//
// A boarded window. Ports from _zombiemode_blockers.gsc and _zombiemode_spawner.gsc:
//   blocker_attack_spots()  : up to 3 zombies at once, outside the boards (center, and 28 units
//                             to either side, 36 units back)
//   tear_into_building()    : each zombie pulls the nearest free board, one at a time; when every
//                             board is gone they climb through
//   blocker_trigger_think() : players inside, within 96 units, hold Use to put back a random
//                             torn board every second, for 10 points a board (capped per round)
//
// Orientation: this object's forward (blue Z arrow) points INTO the building. Players can never
// pass the window (a solid "clip" collider covers the opening, as in BO1); zombies get through by
// climbing (scripted), so the NavMesh doesn't need to connect through the window.
//
// The tear and climb times come from animations that aren't in the dump, so they're placeholders.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ZombiesPort
{
    public class Barricade : MonoBehaviour, IInteractable
    {
        [Tooltip("The board objects, any order. Each one's starting pose is its 'repaired' pose.")]
        [SerializeField] Transform[] boards = new Transform[6];
        [Tooltip("Placeholder for the tear animation length (one board).")]
        [SerializeField] float tearSeconds = 1.2f;
        [Tooltip("Placeholder for the climb-through animation length.")]
        [SerializeField] float climbSeconds = 1.6f;
        [Tooltip("Where climbing zombies land, meters inside the window.")]
        [SerializeField] float insideDistance = 1.0f;
        [SerializeField] float boardFlingSeconds = 0.35f;
        [SerializeField] float boardRestoreSeconds = 0.2f;
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip tearSound;
        [SerializeField] AudioClip repairSound;

        public BarricadeState State { get; private set; }
        public bool IsOpen => State.IsOpen;
        public float TearSeconds => tearSeconds;
        public float ClimbSeconds => climbSeconds;
        public Vector3 InsidePoint { get; private set; }
        public Quaternion FacingInside => Quaternion.LookRotation(Flat(transform.forward));
        /// <summary>Where a zombie waits when all three attack spots are taken.</summary>
        public Vector3 WaitPoint { get; private set; }
        public Vector3 InteractPosition => RepairCenter;

        Vector3 RepairCenter => transform.position + Flat(transform.forward) * insideDistance;

        readonly ZombieAgent[] _spotOwners = new ZombieAgent[BarricadeState.AttackSpots];
        readonly Vector3[] _spots = new Vector3[BarricadeState.AttackSpots];
        readonly List<float> _distances = new List<float>();
        Vector3[] _restPos;
        Quaternion[] _restRot;
        Coroutine[] _boardAnim;

        ZombiePlayer _repairer;
        float _repairTimer;

        void Awake()
        {
            State = new BarricadeState(boards.Length);
            _restPos = new Vector3[boards.Length];
            _restRot = new Quaternion[boards.Length];
            _boardAnim = new Coroutine[boards.Length];
            for (int i = 0; i < boards.Length; i++)
            {
                if (boards[i] == null) continue;
                _restPos[i] = boards[i].localPosition;
                _restRot[i] = boards[i].localRotation;
            }
            State.BoardTorn += OnBoardTorn;
            State.BoardRepaired += OnBoardRepaired;
        }

        void Start()
        {
            // blocker_attack_spots(), snapped onto the NavMesh (baked by now).
            const float u = MysteryBoxRules.UnitsToMeters;
            Vector3 fwd = Flat(transform.forward);
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 center = transform.position - fwd * (BarricadeState.AttackSpotBackUnits * u);
            _spots[0] = Snap(center);
            _spots[1] = Snap(center + right * (BarricadeState.AttackSpotSideUnits * u));
            _spots[2] = Snap(center - right * (BarricadeState.AttackSpotSideUnits * u));
            WaitPoint = Snap(center - fwd * 1.2f);
            InsidePoint = Snap(transform.position + fwd * insideDistance);
        }

        // ---------------- Zombie side ----------------

        public bool TryClaimSpot(ZombieAgent z, out Vector3 spot)
        {
            for (int i = 0; i < _spotOwners.Length; i++)
            {
                if (_spotOwners[i] == z || _spotOwners[i] == null || !_spotOwners[i].IsAlive)
                {
                    _spotOwners[i] = z;
                    spot = _spots[i];
                    return true;
                }
            }
            spot = WaitPoint;
            return false;
        }

        public void ReleaseSpot(ZombieAgent z)
        {
            for (int i = 0; i < _spotOwners.Length; i++)
                if (_spotOwners[i] == z) _spotOwners[i] = null;
        }

        /// <summary>Claims the nearest intact board nobody else is pulling (-1 if none).</summary>
        public int ClaimBoard(Vector3 from)
        {
            _distances.Clear();
            for (int i = 0; i < boards.Length; i++)
                _distances.Add(boards[i] != null ? (boards[i].position - from).sqrMagnitude : float.MaxValue);
            return State.ClaimBoard(_distances);
        }

        public void ReleaseBoard(int i) => State.ReleaseBoard(i);

        public void TearBoard(int i)
        {
            if (State.Tear(i) && audioSource != null && tearSound != null) audioSource.PlayOneShot(tearSound);
        }

        /// <summary>The Carpenter power-up: every torn board goes back up (no rebuild points).</summary>
        public void RepairAll()
        {
            while (State.RepairRandom() >= 0) { }
        }

        // ---------------- Player side: hold to rebuild ----------------

        void Update()
        {
            if (State.IsFullyIntact) { StopRepair(); return; }

            if (_repairer != null && !IsRepairing(_repairer)) StopRepair();
            if (_repairer == null)
            {
                var boot = GameBootstrap.Instance;
                if (boot == null) return;
                foreach (var p in boot.Players)
                {
                    if (p == null || !IsRepairing(p)) continue;
                    _repairer = p;
                    _repairTimer = BarricadeState.RepairSecondsPerBoard; // First board goes up right away.
                    break;
                }
                if (_repairer == null) return;
            }

            _repairTimer += Time.deltaTime;
            if (_repairTimer < BarricadeState.RepairSecondsPerBoard) return;
            _repairTimer = 0f;

            if (State.RepairRandom() < 0) return;
            if (audioSource != null && repairSound != null) audioSource.PlayOneShot(repairSound);

            // player_add_points( "rebuild_board", 10 ), until the round's cap.
            var state = GameBootstrap.Instance.State;
            var score = _repairer.Score;
            if (score == null) return;
            int points = state.Rebuilds.AwardBoard(score.PlayerIndex, state.PointMultiplier);
            if (points > 0) score.Add(points, true, state.Intermission);
        }

        bool IsRepairing(ZombiePlayer p)
        {
            if (!CanRepair(p)) return false;
            var interactor = p.GetComponent<PlayerInteractor>();
            return interactor != null && interactor.IsHolding && ReferenceEquals(interactor.Current, this);
        }

        bool CanRepair(ZombiePlayer p)
        {
            if (p == null || p.IsDowned || p.IsSpectating || p.IsDrinkingPerk || p.IsReviving) return false;
            float r = BarricadeState.RepairRadiusUnits * MysteryBoxRules.UnitsToMeters;
            Vector3 d = p.transform.position - RepairCenter;
            d.y = 0f;
            return d.sqrMagnitude <= r * r;
        }

        void StopRepair()
        {
            _repairer = null;
            _repairTimer = 0f;
        }

        public InteractPrompt GetPrompt(ZombiePlayer player)
        {
            if (State.IsFullyIntact || !CanRepair(player)) return InteractPrompt.Hidden;
            return new InteractPrompt
            {
                Visible = true,
                Label = "Hold to Rebuild", // "default_reward_barrier_piece"
                Affordable = true,
                Progress = _repairer == player ? _repairTimer / BarricadeState.RepairSecondsPerBoard : 0f,
            };
        }

        public InteractResult Interact(ZombiePlayer player) =>
            CanRepair(player) && !State.IsFullyIntact ? InteractResult.Done : InteractResult.Unavailable;

        // ---------------- Board visuals ----------------

        void OnBoardTorn(int i) => Animate(i, Fling(i));
        void OnBoardRepaired(int i) => Animate(i, Restore(i));

        void Animate(int i, IEnumerator routine)
        {
            if (i < 0 || i >= boards.Length || boards[i] == null) return;
            if (_boardAnim[i] != null) StopCoroutine(_boardAnim[i]);
            _boardAnim[i] = StartCoroutine(routine);
        }

        Vector3 TornOffset(int i) =>
            transform.InverseTransformDirection(-transform.forward * 0.6f + Vector3.down * 0.4f);

        IEnumerator Fling(int i)
        {
            Transform b = boards[i];
            Vector3 from = b.localPosition;
            Quaternion fromRot = b.localRotation;
            Vector3 to = _restPos[i] + TornOffset(i);
            Quaternion toRot = _restRot[i] * Quaternion.Euler(0f, 0f, i % 2 == 0 ? 50f : -50f);
            for (float t = 0f; t < boardFlingSeconds; t += Time.deltaTime)
            {
                float k = t / boardFlingSeconds;
                b.localPosition = Vector3.Lerp(from, to, k);
                b.localRotation = Quaternion.Slerp(fromRot, toRot, k);
                yield return null;
            }
            b.gameObject.SetActive(false);
            _boardAnim[i] = null;
        }

        IEnumerator Restore(int i)
        {
            Transform b = boards[i];
            b.gameObject.SetActive(true);
            Vector3 from = _restPos[i] + TornOffset(i);
            Quaternion fromRot = _restRot[i] * Quaternion.Euler(0f, 0f, i % 2 == 0 ? 50f : -50f);
            for (float t = 0f; t < boardRestoreSeconds; t += Time.deltaTime)
            {
                float k = t / boardRestoreSeconds;
                b.localPosition = Vector3.Lerp(from, _restPos[i], k);
                b.localRotation = Quaternion.Slerp(fromRot, _restRot[i], k);
                yield return null;
            }
            b.localPosition = _restPos[i];
            b.localRotation = _restRot[i];
            _boardAnim[i] = null;
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
        }

        static Vector3 Snap(Vector3 p) =>
            NavMesh.SamplePosition(p, out var hit, 2f, NavMesh.AllAreas) ? hit.position : p;

        void OnDrawGizmosSelected()
        {
            Vector3 fwd = Flat(transform.forward);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, transform.position + fwd * insideDistance);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + fwd * insideDistance, BarricadeState.RepairRadiusUnits * MysteryBoxRules.UnitsToMeters);
        }
    }
}
