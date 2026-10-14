// MysteryBoxDirector.cs
//
// Manages every box location on the map: the level.chests / level.chest_index side of
// treasure_chest_init() and treasure_chest_move(). It owns the MysteryBoxRules, picks the
// start location, keeps the floating weapon models pooled, and moves the box after a teddy bear.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZombiesPort
{
    public class MysteryBoxDirector : MonoBehaviour
    {
        [Serializable]
        public class WeaponDisplay
        {
            public string weaponId;
            public GameObject prefab;
        }

        [Tooltip("Every place the box can appear. Mark one as the start location, or turn on Random Start.")]
        [SerializeField] List<MysteryBox> locations = new List<MysteryBox>();
        [SerializeField] int cost = MysteryBoxRules.DefaultCost;
        [Tooltip("GSC: level.random_pandora_box_start. Off = use the location marked as the start.")]
        [SerializeField] bool randomStart;
        [Tooltip("Leave empty to use the Kino der Toten box contents.")]
        [SerializeField] List<BoxWeapon> customWeapons = new List<BoxWeapon>();
        [Tooltip("Weapon models shown floating in the box. Missing ones get a placeholder cube.")]
        [SerializeField] List<WeaponDisplay> displays = new List<WeaponDisplay>();
        [SerializeField] GameObject teddyBearPrefab;
        [Tooltip("0 = random every game. Set a number to make box results repeatable while testing.")]
        [SerializeField] int seed;

        public MysteryBoxRules Rules { get; private set; }
        /// <summary>950, or 10 during a Fire Sale (toggle_fire_sale_on).</summary>
        public int Cost => FireSaleActive ? PowerUpInfo.FireSaleBoxCost : cost;
        public bool FireSaleActive { get; private set; }
        public int ChestMoves => Rules != null ? Rules.ChestMoves : 0;
        public bool IsMoving { get; private set; }
        public bool CanMove => locations.Count > 1;
        public bool Intermission => GameBootstrap.Instance != null && GameBootstrap.Instance.State.Intermission;
        public MysteryBox ActiveBox => locations.Count > 0 ? locations[_order[_index]] : null;

        // Hooks for audio and VFX: the box jingle, the teddy laugh, the "poof".
        public event Action<MysteryBox> BoxOpened;
        public event Action<MysteryBox> TeddyBearRolled;
        public event Action<MysteryBox> BoxMoved;

        int[] _order;
        readonly List<MysteryBox> _fireSaleBoxes = new List<MysteryBox>();
        int _index;
        readonly Dictionary<string, GameObject> _displayPool = new Dictionary<string, GameObject>();
        GameObject _teddy;

        void Start()
        {
            Rules = new MysteryBoxRules(
                customWeapons.Count > 0 ? customWeapons : MysteryBoxRules.KinoDerTotenWeapons(),
                seed == 0 ? (int?)null : seed);

            BuildDisplayPool();

            foreach (var box in locations)
            {
                box.Init(this);
                box.SetPresent(false);
            }
            if (locations.Count == 0) return;

            // treasure_chest_init(): shuffle the locations, then start at the marked
            // "start_chest" location unless random start is on.
            _order = Rules.ShuffleLocations(locations.Count);
            _index = 0;
            if (!randomStart)
            {
                for (int k = 0; k < _order.Length; k++)
                    if (locations[_order[k]].IsStartLocation) { _index = k; break; }
            }
            ActiveBox.SetPresent(true);
        }

        // ---- Floating weapon models, pooled ----
        // The GSC spawned a new script_model on every pull. Here every model is created once
        // at load and switched on and off, so the 40-cycle spin allocates nothing.

        void BuildDisplayPool()
        {
            var prefabs = new Dictionary<string, GameObject>();
            foreach (var d in displays)
                if (d != null && !string.IsNullOrEmpty(d.weaponId) && d.prefab != null)
                    prefabs[d.weaponId] = d.prefab;

            foreach (var w in Rules.Weapons)
            {
                GameObject go;
                if (prefabs.TryGetValue(w.Id, out var prefab))
                {
                    go = Instantiate(prefab, transform);
                }
                else
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.transform.SetParent(transform, false);
                    go.transform.localScale = new Vector3(0.6f, 0.15f, 0.08f);
                    Destroy(go.GetComponent<Collider>()); // Visual only: must not block bullets or triggers.
                }
                go.name = "BoxDisplay_" + w.Id;
                go.SetActive(false);
                _displayPool[w.Id] = go;
            }

            _teddy = teddyBearPrefab != null ? Instantiate(teddyBearPrefab, transform) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _teddy.name = "BoxDisplay_TeddyBear";
            var teddyCollider = _teddy.GetComponent<Collider>();
            if (teddyCollider != null) Destroy(teddyCollider);
            _teddy.SetActive(false);
        }

        public GameObject GetDisplay(string weaponId) =>
            weaponId != null && _displayPool.TryGetValue(weaponId, out var go) ? go : null;

        public GameObject TeddyBear => _teddy;

        // ---- Rolls ----

        /// <summary>Builds the context the weighted roll needs, then rolls.</summary>
        public string ChooseWeapon(ZombiePlayer user)
        {
            var allPlayers = new List<WeaponInventory>();
            int round = 1;
            if (GameBootstrap.Instance != null)
            {
                round = GameBootstrap.Instance.State.RoundNumber;
                foreach (var p in GameBootstrap.Instance.Players) allPlayers.Add(p.Weapons);
            }
            else
            {
                allPlayers.Add(user.Weapons);
            }

            var onDisplay = new List<string>();
            foreach (var box in locations)
                if (box.OfferedWeapon != null) onDisplay.Add(box.OfferedWeapon);

            return Rules.ChooseWeightedWeapon(new BoxContext
            {
                RoundNumber = round,
                ChestMoves = Rules.ChestMoves,
                User = user.Weapons,
                AllPlayers = allPlayers,
                WeaponsOnDisplay = onDisplay,
            });
        }

        // ---- Fire Sale (toggle_fire_sale_on) ----

        /// <summary>Every box location appears and the box costs 10 for 30 seconds.</summary>
        public void StartFireSale()
        {
            if (FireSaleActive) return;
            FireSaleActive = true;
            foreach (var box in locations)
            {
                if (box == null || box == ActiveBox || !box.IsHidden || _fireSaleBoxes.Contains(box)) continue;
                box.SetPresent(true);   // show_chest(), hide_rubble()
                _fireSaleBoxes.Add(box);
            }
        }

        /// <summary>The sale is over: the extra boxes go once nobody is using them.</summary>
        public void EndFireSale()
        {
            if (!FireSaleActive) return;
            FireSaleActive = false;
            StartCoroutine(HideFireSaleBoxes());
        }

        IEnumerator HideFireSaleBoxes()
        {
            var wait = new WaitForSeconds(0.5f);
            while (_fireSaleBoxes.Count > 0 && !FireSaleActive)
            {
                for (int i = _fireSaleBoxes.Count - 1; i >= 0; i--)
                {
                    var box = _fireSaleBoxes[i];
                    if (box == ActiveBox) { _fireSaleBoxes.RemoveAt(i); continue; } // The real box landed here.
                    if (!box.IsIdle) continue;                                     // Still spinning or offering.
                    box.SetPresent(false);
                    _fireSaleBoxes.RemoveAt(i);
                }
                yield return wait;
            }
        }

        // ---- Called by MysteryBox ----

        internal void NotifyOpened(MysteryBox box) => BoxOpened?.Invoke(box);

        internal void BeginMove(MysteryBox box)
        {
            IsMoving = true;
            TeddyBearRolled?.Invoke(box);
        }

        /// <summary>The old box has vanished. Wait, then show it at the next location.</summary>
        internal void FinishLeaving(MysteryBox box) => StartCoroutine(ReappearRoutine());

        IEnumerator ReappearRoutine()
        {
            yield return new WaitForSeconds(MysteryBoxRules.BoxReappearSeconds);
            _index = Rules.NextLocation(ref _order, _index);
            // During a Fire Sale the new spot may already be showing as a sale box: keep it as is.
            bool wasSaleBox = _fireSaleBoxes.Remove(ActiveBox);
            if (!wasSaleBox || ActiveBox.IsHidden) ActiveBox.SetPresent(true);
            IsMoving = false;
            BoxMoved?.Invoke(ActiveBox);
        }
    }
}
