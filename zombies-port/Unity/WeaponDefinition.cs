// WeaponDefinition.cs
//
// A weapon's stats as an asset (Create > Zombies > Weapon Definition), so they can be tuned in
// the Inspector without touching code. Wall buys, the Mystery Box and Pack-a-Punch all refer
// to weapons by Id ("m1911_zm"), the same names the GSC used.

using UnityEngine;

namespace ZombiesPort
{
    [CreateAssetMenu(menuName = "Zombies/Weapon Definition", fileName = "NewWeapon")]
    public class WeaponDefinition : ScriptableObject
    {
        public WeaponStats stats = WeaponStats.M1911();

        [Header("Optional presentation")]
        [Tooltip("Model shown in the player's hands (child of the camera).")]
        public GameObject viewModelPrefab;
        public AudioClip fireSound;
        public AudioClip dryFireSound;
        public AudioClip reloadSound;

        /// <summary>A runtime M1911 when no asset is assigned, so the player is never unarmed.</summary>
        public static WeaponDefinition CreateM1911()
        {
            var d = CreateInstance<WeaponDefinition>();
            d.name = "M1911 (runtime)";
            d.stats = WeaponStats.M1911();
            return d;
        }
    }
}
