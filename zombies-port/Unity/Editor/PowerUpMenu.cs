// PowerUpMenu.cs  (editor only: must stay in a folder named "Editor")
//
// GameObject > Zombies > Power-Ups
//   - Adds a PowerUpDirector next to the RoundDirector, wired to the Mystery Box and game over
//   - Builds the PowerUpHud: timed power-up icons (top center), the grab announcement, and a
//     full-screen white overlay for the Nuke flash (at the canvas root so it covers the notch too)

#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ZombiesPort.EditorTools
{
    public static class PowerUpMenu
    {
        [MenuItem("GameObject/Zombies/Power-Ups", false, 19)]
        static void CreatePowerUps()
        {
            // ---- Director ----
            var director = Object.FindFirstObjectByType<PowerUpDirector>();
            if (director == null)
            {
                var rounds = Object.FindFirstObjectByType<RoundDirector>();
                if (rounds != null) director = Undo.AddComponent<PowerUpDirector>(rounds.gameObject);
                else
                {
                    var go = new GameObject("Power-Ups");
                    Undo.RegisterCreatedObjectUndo(go, "Create Power-Ups");
                    director = go.AddComponent<PowerUpDirector>();
                }
                var so = new SerializedObject(director);
                so.FindProperty("rounds").objectReferenceValue = Object.FindFirstObjectByType<RoundDirector>();
                so.FindProperty("mysteryBox").objectReferenceValue = Object.FindFirstObjectByType<MysteryBoxDirector>();
                so.FindProperty("lastStand").objectReferenceValue = Object.FindFirstObjectByType<LastStandDirector>();
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (Object.FindFirstObjectByType<PowerUpHud>() != null)
            {
                Selection.activeGameObject = director.gameObject;
                Debug.Log("Power-ups: the scene already has a PowerUpHud; only the director was checked.");
                return;
            }

            // ---- HUD ----
            var safe = MobileHudMenu.GetOrCreateSafeArea();
            var hudGo = MobileHudMenu.NewChild("PowerUpHud", safe.parent);
            Undo.RegisterCreatedObjectUndo(hudGo, "Create Power-Up HUD");
            Stretch(hudGo.GetComponent<RectTransform>());

            // Nuke flash first (behind the icons), full screen.
            var flashGo = MobileHudMenu.NewChild("NukeFlash", hudGo.transform);
            Stretch(flashGo.GetComponent<RectTransform>());
            var flash = flashGo.AddComponent<Image>();
            flash.color = new Color(1f, 1f, 1f, 0f);
            flash.raycastTarget = false;
            flash.enabled = false;

            // Timed icons, top center (chalk is top-left and points top-right).
            var circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            string[] letters = { "IK", "x2", "$" };
            Color[] colors = { new Color(0.95f, 0.95f, 0.9f), new Color(1f, 0.85f, 0.1f), new Color(0.9f, 0.15f, 0.15f) };
            var icons = new Graphic[3];
            for (int i = 0; i < 3; i++)
            {
                var icon = MobileHudMenu.NewChild("Timed_" + letters[i], hudGo.transform);
                var r = icon.GetComponent<RectTransform>();
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 1f);
                r.sizeDelta = new Vector2(90f, 90f);
                r.anchoredPosition = new Vector2((i - 1) * 105f, -50f);
                var img = icon.AddComponent<Image>();
                img.sprite = circle;
                img.color = colors[i];
                img.raycastTarget = false;
                var t = MobileHudMenu.NewText("Label", icon.transform, letters[i], 40, 0f, 1f);
                t.color = Color.black;
                icons[i] = img;
            }

            var announce = MobileHudMenu.NewText("Announcement", hudGo.transform, "", 72, 0f, 1f);
            var ar = announce.rectTransform;
            ar.anchorMin = ar.anchorMax = ar.pivot = new Vector2(0.5f, 0.5f);
            ar.sizeDelta = new Vector2(1200f, 110f);
            ar.anchoredPosition = new Vector2(0f, 330f);
            announce.alignment = TextAlignmentOptions.Center;

            var hud = hudGo.AddComponent<PowerUpHud>(); // adds the nested Canvas
            hudGo.GetComponent<Canvas>().overrideSorting = false;
            var hso = new SerializedObject(hud);
            hso.FindProperty("director").objectReferenceValue = director;
            var ip = hso.FindProperty("timedIcons");
            ip.arraySize = 3;
            for (int i = 0; i < 3; i++) ip.GetArrayElementAtIndex(i).objectReferenceValue = icons[i];
            hso.FindProperty("announcement").objectReferenceValue = announce;
            hso.FindProperty("nukeFlash").objectReferenceValue = flash;
            hso.ApplyModifiedPropertiesWithoutUndo();

            // Keep the Last Stand HUD (game over screen) on top.
            var lastStandHud = Object.FindFirstObjectByType<LastStandHud>();
            if (lastStandHud != null) lastStandHud.transform.SetAsLastSibling();

            Selection.activeGameObject = director.gameObject;
            Debug.Log("Power-ups ready. Kills start dropping them once the team passes 2500 points (solo). " +
                      "Right-click the PowerUpDirector and use the debug items to drop one at your feet.");
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }
    }
}
#endif
