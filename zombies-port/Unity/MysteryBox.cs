// MysteryBox.cs
//
// One Mystery Box location. Port of treasure_chest_think(), treasure_chest_weapon_spawn(),
// treasure_chest_lid_open()/close(), timer_til_despawn() and treasure_chest_move() from
// maps/_zombiemode_weapons.gsc.
//
// Input change: GSC waited for `self waittill("trigger", user)` (the player pressed Use in the
// trigger volume). Here the box implements IInteractable. When the player stands in its trigger
// collider, the on-screen InteractButton shows "Mystery Box  950" and tapping it calls Interact().
//
// Flow:
//   Idle --tap, pay 950--> Spinning (lid opens, 40 cycles, about 3.9 s)
//     |-> weapon:     Offering (12 s; the buyer taps "Trade Weapons") -> Closing (3 s) -> Idle
//     '-> teddy bear: Leaving (refund, teddy flies up, box shakes and vanishes) -> next location
//
// Hierarchy:
//   MysteryBox (this + a trigger BoxCollider about 2 m around the box)
//     BoxVisual     the chest mesh (hidden when the box is elsewhere)
//       Lid         pivot placed on the hinge
//     WeaponAnchor  empty, at the bottom inside the box
//     Rubble        the "box was here" debris (shown when the box is elsewhere)

using System.Collections;
using UnityEngine;

namespace ZombiesPort
{
    public class MysteryBox : MonoBehaviour, IInteractable
    {
        enum State { Hidden, Idle, Spinning, Offering, Closing, Leaving }

        [SerializeField] GameObject boxVisual;
        [SerializeField] Transform lid;
        [Tooltip("Local axis the lid rotates around. GSC used RotateRoll, which is usually X or Z depending on the mesh.")]
        [SerializeField] Vector3 lidHingeAxis = Vector3.right;
        [SerializeField] Transform weaponAnchor;
        [SerializeField] GameObject rubble;
        [Tooltip("GSC: script_noteworthy contains \"start_chest\".")]
        [SerializeField] bool isStartLocation;

        public bool IsStartLocation => isStartLocation;
        public Vector3 InteractPosition => transform.position;

        /// <summary>The weapon floating in this box right now. It counts toward limited-weapon caps.</summary>
        public string OfferedWeapon => _state == State.Offering ? _offered : null;

        static readonly WaitForSeconds Wait005 = new WaitForSeconds(0.05f);
        static readonly WaitForSeconds Wait01 = new WaitForSeconds(0.1f);
        static readonly WaitForSeconds Wait02 = new WaitForSeconds(0.2f);
        static readonly WaitForSeconds Wait03 = new WaitForSeconds(0.3f);

        MysteryBoxDirector _director;
        State _state = State.Hidden;
        ZombiePlayer _user;          // self.chest_user
        int _paid;                   // user_cost, refunded on a teddy bear
        string _offered;             // self.chest_origin.weapon_string
        bool _grabbed;
        GameObject _shown;
        Quaternion _lidClosed;
        Vector3 _boxVisualHome;

        public void Init(MysteryBoxDirector director)
        {
            _director = director;
            if (lid != null) _lidClosed = lid.localRotation;
            if (boxVisual != null) _boxVisualHome = boxVisual.transform.localPosition;
            if (weaponAnchor == null) weaponAnchor = transform;
        }

        /// <summary>show_chest()/hide_rubble() when true, hide_chest()/show_rubble() when false.</summary>
        public void SetPresent(bool present)
        {
            if (boxVisual != null)
            {
                boxVisual.SetActive(present);
                boxVisual.transform.localPosition = _boxVisualHome;
            }
            if (rubble != null) rubble.SetActive(!present);
            if (lid != null) lid.localRotation = _lidClosed;
            _state = present ? State.Idle : State.Hidden;
        }

        // ---------------- IInteractable: what the button shows and does ----------------

        public InteractPrompt GetPrompt(ZombiePlayer player)
        {
            if (player == null || !player.CanBuyWeapon) return InteractPrompt.Hidden; // decide_hide_show_hint()

            switch (_state)
            {
                case State.Idle:
                    if (_director.IsMoving) return InteractPrompt.Hidden;
                    return new InteractPrompt
                    {
                        Visible = true,
                        Label = "Mystery Box",
                        Cost = _director.Cost,
                        Affordable = player.Score.Score >= _director.Cost,
                    };

                case State.Offering:
                    // &"ZOMBIE_TRADE_WEAPONS". Only the buyer sees it (grabber == user).
                    if (player != _user) return InteractPrompt.Hidden;
                    return new InteractPrompt { Visible = true, Label = "Trade Weapons", Affordable = true };

                default:
                    return InteractPrompt.Hidden; // Spinning, closing, moving, or someone else's weapon.
            }
        }

        public InteractResult Interact(ZombiePlayer player)
        {
            if (player == null || !player.CanBuyWeapon) return InteractResult.Unavailable;

            if (_state == State.Idle && !_director.IsMoving)
            {
                // treasure_chest_think(): `user.score >= self.zombie_cost`, otherwise the "no_money" line.
                if (!player.Score.TrySpend(_director.Cost, _director.Intermission))
                    return InteractResult.NotEnoughPoints;

                _user = player;
                _paid = _director.Cost;
                StartCoroutine(OpenRoutine());
                return InteractResult.Done;
            }

            if (_state == State.Offering && player == _user)
            {
                _grabbed = true; // Picked up by OfferRoutine on the next frame.
                return InteractResult.Done;
            }

            return InteractResult.Unavailable;
        }

        // ---------------- The sequence ----------------

        IEnumerator OpenRoutine()
        {
            _state = State.Spinning;
            _director.NotifyOpened(this);
            yield return RotateLid(open: true);

            // treasure_chest_weapon_spawn(): 40 cycles that slow down. Cycles 1-39 flash random
            // models; cycle 40 is the real, weighted pick. The model rises while it spins.
            string pick = null;
            float spinStart = Time.time;
            const float spinLength = 3.9f; // sum of all CycleDelay() values
            for (int i = 0; i < MysteryBoxRules.SpinCycles; i++)
            {
                var wait = CycleWait(i);
                if (wait != null) yield return wait;

                bool last = i + 1 == MysteryBoxRules.SpinCycles;
                string id = last ? (pick = _director.ChooseWeapon(_user)) : _director.Rules.ChooseCycleWeapon();
                Show(_director.GetDisplay(id));
                PlaceShown(Mathf.Clamp01((Time.time - spinStart) / spinLength));
            }

            if (pick == null)
            {
                // Nothing left the player could get (they own everything). The GSC never handled
                // this case; refund and close instead of handing out nothing.
                Refund();
                yield return CloseRoutine();
                yield break;
            }

            if (_director.Rules.RollTeddyBear(_director.CanMove))
            {
                yield return LeaveRoutine();
                yield break;
            }

            yield return OfferRoutine(pick);
            yield return CloseRoutine();
        }

        /// <summary>
        /// The 12-second window. The weapon sinks back into the box (timer_til_despawn) and the
        /// buyer can tap "Trade Weapons". Either way it counts as a pull.
        /// </summary>
        IEnumerator OfferRoutine(string weapon)
        {
            _offered = weapon;
            _grabbed = false;
            _state = State.Offering;

            float t = 0f;
            while (t < MysteryBoxRules.GrabWindowSeconds && !_grabbed && _user != null)
            {
                t += Time.deltaTime;
                PlaceShown(1f - Smooth(t / MysteryBoxRules.GrabWindowSeconds));
                yield return null;
            }

            if (_grabbed && _user != null)
                _user.Weapons.Give(weapon); // treasure_chest_give_weapon()

            _director.Rules.RecordPull();    // level.chest_accessed += 1
            _offered = null;
            Show(null);
        }

        IEnumerator CloseRoutine()
        {
            _state = State.Closing;
            _user = null;
            yield return RotateLid(open: false);
            yield return new WaitForSeconds(MysteryBoxRules.CloseCooldownSeconds);
            if (_state == State.Closing) _state = State.Idle;
        }

        /// <summary>Teddy bear: refund, laugh, the bear flies up, the box shakes and vanishes.</summary>
        IEnumerator LeaveRoutine()
        {
            _state = State.Leaving;
            Show(_director.TeddyBear);
            PlaceShown(1f);
            Refund(); // GSC refunds as soon as randomization is done.
            _director.BeginMove(this);

            yield return new WaitForSeconds(MysteryBoxRules.TeddyPauseSeconds + MysteryBoxRules.TeddyHoverSeconds);

            // MoveZ(500, 4, 3): rises 500 units over 4 s, accelerating.
            Vector3 from = _shown.transform.position;
            for (float t = 0f; t < MysteryBoxRules.TeddyRiseSeconds; t += Time.deltaTime)
            {
                float k = t / MysteryBoxRules.TeddyRiseSeconds;
                _shown.transform.position = from + Vector3.up * (MysteryBoxRules.TeddyRiseHeight * k * k);
                yield return null;
            }
            Show(null);

            // treasure_chest_move(): lid closes, the box lifts 50 units over 5 s while vibrating, then poofs.
            yield return RotateLid(open: false);
            if (boxVisual != null)
            {
                Transform bt = boxVisual.transform;
                float rise = 50f * MysteryBoxRules.UnitsToMeters;
                for (float t = 0f; t < MysteryBoxRules.BoxShakeSeconds; t += Time.deltaTime)
                {
                    float k = t / MysteryBoxRules.BoxShakeSeconds;
                    Vector3 shake = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * 0.03f;
                    bt.localPosition = _boxVisualHome + Vector3.up * (rise * k) + shake;
                    yield return null;
                }
            }

            _user = null;
            SetPresent(false);              // Poof: the rubble is left behind.
            _director.FinishLeaving(this);  // The director shows the next location after 12 s.
        }

        // ---------------- Helpers ----------------

        void Refund()
        {
            if (_user != null && _user.Score != null)
                _user.Score.Add(_paid, false, _director.Intermission); // add_to_player_score(user_cost, false)
        }

        static WaitForSeconds CycleWait(int i)
        {
            float d = MysteryBoxRules.CycleDelay(i);
            if (d <= 0f) return null;
            if (d < 0.075f) return Wait005;
            if (d < 0.15f) return Wait01;
            if (d < 0.25f) return Wait02;
            return Wait03;
        }

        void Show(GameObject display)
        {
            if (_shown == display) return;
            if (_shown != null) _shown.SetActive(false);
            _shown = display;
            if (_shown == null) return;
            _shown.transform.SetParent(weaponAnchor, false);
            _shown.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); // angles + (0, 90, 0)
            _shown.SetActive(true);
        }

        /// <summary>0 = inside the box, 1 = fully raised (floatHeight = 40 units).</summary>
        void PlaceShown(float height01)
        {
            if (_shown == null) return;
            _shown.transform.localPosition = Vector3.up * (MysteryBoxRules.WeaponFloatHeight * height01);
        }

        IEnumerator RotateLid(bool open)
        {
            if (lid == null) yield break;
            Quaternion from = lid.localRotation;
            Quaternion to = open
                ? _lidClosed * Quaternion.AngleAxis(MysteryBoxRules.LidOpenDegrees, lidHingeAxis)
                : _lidClosed;
            for (float t = 0f; t < MysteryBoxRules.LidOpenSeconds; t += Time.deltaTime)
            {
                lid.localRotation = Quaternion.Slerp(from, to, Smooth(t / MysteryBoxRules.LidOpenSeconds));
                yield return null;
            }
            lid.localRotation = to;
        }

        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }
}
