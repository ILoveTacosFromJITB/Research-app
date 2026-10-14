// WallBuy.cs
//
// A chalk-outline weapon on the wall. Port of weapon_spawn_think() from _zombiemode_weapons.gsc:
//   - The gun shows as a chalk outline until someone first buys it (or its ammo); then the
//     real model slides into place (weapon_show).
//   - Without the gun: "Buy MP40  1000". It's given like a Mystery Box gun, replacing the one in
//     your hands if both slots are full.
//   - With the gun: "MP40 Ammo  500", half the price rounded up to 10, or 4500 if it's
//     Pack-a-Punched. Ammo is only sold while spare ammo is below the starting amount.
//
// Setup: a trigger collider in front of the wall, this component, and optionally a chalk-outline
// object and a gun model (child objects).

using System.Collections;
using UnityEngine;

namespace ZombiesPort
{
    public class WallBuy : MonoBehaviour, IInteractable
    {
        [Tooltip("Weapon id, e.g. mp40_zm, m14_zm, ak74u_zm (see WallBuyRules.KinoWallPrices).")]
        [SerializeField] string weaponId = "mp40_zm";
        [Tooltip("0 = use the BO1 price for this weapon.")]
        [SerializeField] int costOverride;
        [Tooltip("Pack-a-Punched id. Leave empty for the usual <name>_upgraded_zm.")]
        [SerializeField] string upgradeId;
        [SerializeField] GameObject chalkOutline;
        [SerializeField] GameObject weaponModel;
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip purchaseSound;
        [SerializeField] AudioClip denySound;

        public string WeaponId => weaponId;
        public Vector3 InteractPosition => transform.position;

        public int Cost => costOverride > 0 ? costOverride
            : WallBuyRules.KinoWallPrices.TryGetValue(weaponId, out int c) ? c : 1000;

        string DisplayName => WeaponPresets.TryGet(weaponId, out var s) ? s.DisplayName : weaponId;

        string UpgradeId => !string.IsNullOrEmpty(upgradeId) ? upgradeId
            : weaponId.EndsWith("_zm") ? weaponId.Substring(0, weaponId.Length - 3) + "_upgraded_zm" : weaponId + "_upgraded";

        bool _revealed;
        Vector3 _modelRest;

        void Awake()
        {
            if (weaponModel != null)
            {
                _modelRest = weaponModel.transform.localPosition;
                weaponModel.SetActive(false); // model hide()
            }
            if (chalkOutline != null) chalkOutline.SetActive(true);
        }

        WallBuyAction Evaluate(ZombiePlayer player, out int price, out WeaponState ownedState)
        {
            price = 0;
            ownedState = null;
            if (player == null || player.Score == null) return WallBuyAction.CannotBuy;

            var inv = player.Weapons;
            bool owns = Contains(inv, weaponId);
            bool ownsUpgrade = !owns && Contains(inv, UpgradeId);

            int reserve = 0, start = 0;
            var weapon = player.GetComponent<PlayerWeapon>();
            if ((owns || ownsUpgrade) && weapon != null)
            {
                ownedState = weapon.GetState(owns ? weaponId : UpgradeId);
                reserve = ownedState.Reserve;
                start = ownedState.Stats.StartingReserve;
            }
            return WallBuyRules.Decide(player.CanBuyWeapon, owns, ownsUpgrade, reserve, start,
                                       player.Score.Score, Cost, out price);
        }

        static bool Contains(WeaponInventory inv, string id)
        {
            foreach (var w in inv.Primaries) if (w == id) return true;
            return false;
        }

        public InteractPrompt GetPrompt(ZombiePlayer player)
        {
            var action = Evaluate(player, out int price, out var owned);
            bool hasGun = owned != null || (player != null && Contains(player.Weapons, weaponId));
            switch (action)
            {
                case WallBuyAction.CannotBuy:
                    return InteractPrompt.Hidden;
                case WallBuyAction.AmmoFull:
                    return new InteractPrompt { Visible = true, Label = DisplayName + " Ammo Full", Affordable = false };
                default:
                    return new InteractPrompt
                    {
                        Visible = true,
                        Label = hasGun ? DisplayName + " Ammo" : "Buy " + DisplayName,
                        Cost = price,
                        Affordable = action != WallBuyAction.NotEnoughPoints,
                    };
            }
        }

        public InteractResult Interact(ZombiePlayer player)
        {
            var action = Evaluate(player, out int price, out var owned);
            var state = GameBootstrap.Instance != null ? GameBootstrap.Instance.State : null;
            bool intermission = state != null && state.Intermission;

            switch (action)
            {
                case WallBuyAction.BuyWeapon:
                    if (!player.Score.TrySpend(price, intermission)) return InteractResult.NotEnoughPoints;
                    player.Weapons.Give(weaponId); // weapon_give(): same slot rules as the Mystery Box
                    Reveal(player);
                    Play(purchaseSound);
                    return InteractResult.Done;

                case WallBuyAction.BuyAmmo:
                    if (!player.Score.TrySpend(price, intermission)) return InteractResult.NotEnoughPoints;
                    owned.RefillToStart();         // GiveStartAmmo()
                    Reveal(player);
                    Play(purchaseSound);
                    return InteractResult.Done;

                case WallBuyAction.NotEnoughPoints:
                    Play(denySound);               // "no_purchase"
                    return InteractResult.NotEnoughPoints;

                case WallBuyAction.AmmoFull:
                    return InteractResult.Denied;  // GSC: nothing happens, no charge

                default:
                    return InteractResult.Unavailable;
            }
        }

        /// <summary>weapon_show(): the first purchase swaps the chalk drawing for the real gun.</summary>
        void Reveal(ZombiePlayer buyer)
        {
            if (_revealed) return;
            _revealed = true;
            if (chalkOutline != null) chalkOutline.SetActive(false);
            if (weaponModel != null) StartCoroutine(SlideIn(buyer));
        }

        IEnumerator SlideIn(ZombiePlayer buyer)
        {
            // Starts 8 units off to the buyer's side and slides home over 1 s.
            Transform m = weaponModel.transform;
            float side = Vector3.Dot(buyer.transform.position - transform.position, transform.right) >= 0f ? 1f : -1f;
            Vector3 start = _modelRest + transform.InverseTransformDirection(transform.right * side) * (8f * MysteryBoxRules.UnitsToMeters);
            weaponModel.SetActive(true);
            for (float t = 0f; t < 1f; t += Time.deltaTime)
            {
                m.localPosition = Vector3.Lerp(start, _modelRest, t);
                yield return null;
            }
            m.localPosition = _modelRest;
        }

        void Play(AudioClip clip)
        {
            if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
        }
    }
}
