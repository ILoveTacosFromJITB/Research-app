// PerkHud.cs
//
// The row of perk icons (perk_hud_create): one slot per perk in purchase order, up to four.
// New icons pulse when gained (perk_hud_flash: 24 -> 32 -> 24 px). BO1 drew them bottom-left,
// which is the movement stick's corner on a phone, so this row sits top-left under the round counter.
//
// Assign real icons per perk in "Icons" (indexed by PerkId). Until then each perk shows as a
// colored circle with a letter, in its BO1 machine color.

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombiesPort
{
    [RequireComponent(typeof(Canvas))]
    public class PerkHud : MonoBehaviour
    {
        [SerializeField] PlayerPerks player;
        [SerializeField] Image[] slots = new Image[PerkInfo.MaxPerks];
        [SerializeField] TMP_Text[] letters = new TMP_Text[PerkInfo.MaxPerks];
        [Tooltip("Optional icon per perk, in PerkId order: QuickRevive, Juggernog, SpeedCola, DoubleTap, StaminUp, PhdFlopper, Deadshot, MuleKick.")]
        [SerializeField] Sprite[] icons = new Sprite[8];
        [SerializeField] Sprite placeholderCircle;

        static readonly Color[] Colors =
        {
            new Color(0.35f, 0.75f, 1f),   // Quick Revive: light blue
            new Color(0.85f, 0.1f, 0.1f),  // Juggernog: red
            new Color(0.2f, 0.8f, 0.3f),   // Speed Cola: green
            new Color(1f, 0.6f, 0.1f),     // Double Tap: orange
            new Color(1f, 0.85f, 0.2f),    // Stamin-Up: yellow
            new Color(0.6f, 0.3f, 0.9f),   // PhD Flopper: purple
            new Color(0.3f, 0.3f, 0.3f),   // Deadshot: dark grey
            new Color(0.35f, 0.6f, 0.2f),  // Mule Kick: olive
        };
        static readonly string[] Letters = { "R", "J", "S", "D", "M", "P", "A", "K" };

        void Awake()
        {
            foreach (var g in GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
        }

        void Start()
        {
            if (player == null) player = FindFirstObjectByType<PlayerPerks>();
            if (player == null) return;
            player.Perks.Gained += OnGained;
            player.Perks.Lost += OnLost;
            Refresh();
        }

        void OnDestroy()
        {
            if (player == null) return;
            player.Perks.Gained -= OnGained;
            player.Perks.Lost -= OnLost;
        }

        void OnGained(PerkId perk)
        {
            Refresh();
            int i = player.Perks.Count - 1;
            if (i >= 0 && i < slots.Length && slots[i] != null) StartCoroutine(Flash(slots[i].rectTransform));
        }

        void OnLost(PerkId perk) => Refresh();

        void Refresh()
        {
            var owned = player.Perks.Owned;
            for (int i = 0; i < slots.Length; i++)
            {
                bool on = i < owned.Count;
                if (slots[i] != null) slots[i].enabled = on;
                if (i < letters.Length && letters[i] != null) letters[i].enabled = false;
                if (!on || slots[i] == null) continue;

                int id = (int)owned[i];
                Sprite icon = icons != null && id < icons.Length ? icons[id] : null;
                if (icon != null)
                {
                    slots[i].sprite = icon;
                    slots[i].color = Color.white;
                }
                else
                {
                    slots[i].sprite = placeholderCircle;
                    slots[i].color = Colors[id];
                    if (i < letters.Length && letters[i] != null)
                    {
                        letters[i].enabled = true;
                        letters[i].text = Letters[id];
                    }
                }
            }
        }

        static IEnumerator Flash(RectTransform r)
        {
            Vector3 normal = Vector3.one;
            Vector3 big = Vector3.one * (32f / 24f);
            for (int pulse = 0; pulse < 2; pulse++)
            {
                r.localScale = big;
                yield return new WaitForSeconds(0.3f);
                r.localScale = normal;
                yield return new WaitForSeconds(0.3f);
            }
        }
    }
}
