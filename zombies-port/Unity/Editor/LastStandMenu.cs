// LastStandMenu.cs  (editor only: must stay in a folder named "Editor")
//
// GameObject > Zombies > Last Stand + Game Over
//   - Adds PlayerHealth to every ZombiePlayer that lacks it
//   - Adds LastStandDirector next to the RoundDirector (or on a new object)
//   - Builds the LastStandHud at the HUD Canvas root (overlays must cover the whole screen,
//     notch included): damage tint, downed darkening, status text, bleed-out and revive bars,
//     and a game-over panel with Play Again
//   - Gives an existing interact button a progress ring for hold-to-revive

#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ZombiesPort.EditorTools
{
    public static class LastStandMenu
    {
        [MenuItem("GameObject/Zombies/Last Stand + Game Over", false, 14)]
        static void CreateLastStand()
        {
            // ---- PlayerHealth on every player ----
            var players = Object.FindObjectsByType<ZombiePlayer>(FindObjectsSortMode.None);
            if (players.Length == 0)
                Debug.LogWarning("No ZombiePlayer in the scene yet. Run Zombies > Player + Touch Controls first, then this again.");
            PlayerHealth firstHealth = null;
            foreach (var p in players)
            {
                var h = p.GetComponent<PlayerHealth>();
                if (h == null) h = Undo.AddComponent<PlayerHealth>(p.gameObject);
                if (firstHealth == null) firstHealth = h;
            }

            // ---- Director ----
            var director = Object.FindFirstObjectByType<LastStandDirector>();
            if (director == null)
            {
                var rounds = Object.FindFirstObjectByType<RoundDirector>();
                if (rounds != null) director = Undo.AddComponent<LastStandDirector>(rounds.gameObject);
                else
                {
                    var go = new GameObject("Last Stand");
                    Undo.RegisterCreatedObjectUndo(go, "Create Last Stand");
                    director = go.AddComponent<LastStandDirector>();
                }
                var dso = new SerializedObject(director);
                dso.FindProperty("rounds").objectReferenceValue = Object.FindFirstObjectByType<RoundDirector>();
                dso.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- HUD ----
            var safe = MobileHudMenu.GetOrCreateSafeArea();
            Transform canvasRoot = safe.parent;
            var bar = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var hudGo = MobileHudMenu.NewChild("LastStandHud", canvasRoot);
            Undo.RegisterCreatedObjectUndo(hudGo, "Create Last Stand HUD");
            Stretch(hudGo.GetComponent<RectTransform>());
            hudGo.transform.SetAsLastSibling(); // On top of everything, so Game Over covers the controls.

            var damage = FullScreen("DamageOverlay", hudGo.transform, new Color(0.6f, 0f, 0f, 0f));
            var downed = FullScreen("DownedOverlay", hudGo.transform, new Color(0f, 0f, 0f, 0f));

            var status = MobileHudMenu.NewText("Status", hudGo.transform, "", 46, 0f, 1f);
            Place(status.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 260f), new Vector2(1100f, 80f));
            status.alignment = TextAlignmentOptions.Center;

            var bleed = Bar("BleedoutBar", hudGo.transform, bar, new Color(0.85f, 0.1f, 0.1f, 0.9f), new Vector2(0f, 200f));
            var revive = Bar("ReviveBar", hudGo.transform, bar, new Color(1f, 1f, 1f, 0.9f), new Vector2(0f, 165f));

            // Game over panel: blocks touches (raycastTarget on) so nothing underneath reacts.
            var panel = MobileHudMenu.NewChild("GameOverPanel", hudGo.transform);
            Stretch(panel.GetComponent<RectTransform>());
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0f, 0f, 0f, 0.75f);
            var title = MobileHudMenu.NewText("Title", panel.transform, "GAME OVER", 120, 0f, 1f);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(1200f, 160f));
            var survived = MobileHudMenu.NewText("Survived", panel.transform, "You Survived 1 Round", 56, 0f, 1f);
            Place(survived.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 50f), new Vector2(1200f, 90f));

            var buttonGo = MobileHudMenu.NewChild("PlayAgain", panel.transform);
            Place(buttonGo.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(420f, 130f));
            var bImg = buttonGo.AddComponent<Image>();
            bImg.sprite = bar;
            bImg.type = Image.Type.Sliced;
            bImg.color = new Color(1f, 1f, 1f, 0.9f);
            var button = buttonGo.AddComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var bText = MobileHudMenu.NewText("Label", buttonGo.transform, "Play Again", 48, 0f, 1f);
            bText.color = Color.black;
            panel.SetActive(false);

            var hud = hudGo.AddComponent<LastStandHud>();             // adds the nested Canvas
            hudGo.GetComponent<Canvas>().overrideSorting = false;
            hudGo.AddComponent<GraphicRaycaster>();                    // for the Play Again button

            var so = new SerializedObject(hud);
            so.FindProperty("player").objectReferenceValue = firstHealth;
            so.FindProperty("director").objectReferenceValue = director;
            so.FindProperty("damageOverlay").objectReferenceValue = damage;
            so.FindProperty("downedOverlay").objectReferenceValue = downed;
            so.FindProperty("statusText").objectReferenceValue = status;
            so.FindProperty("bleedoutBar").objectReferenceValue = bleed;
            so.FindProperty("reviveBar").objectReferenceValue = revive;
            so.FindProperty("gameOverPanel").objectReferenceValue = panel;
            so.FindProperty("gameOverTitle").objectReferenceValue = title;
            so.FindProperty("survivedText").objectReferenceValue = survived;
            so.FindProperty("playAgainButton").objectReferenceValue = button;
            so.ApplyModifiedPropertiesWithoutUndo();

            // ---- Revive progress ring on the interact button ----
            var interact = Object.FindFirstObjectByType<InteractButton>();
            if (interact != null) AddProgressRing(interact);

            Selection.activeGameObject = hudGo;
            Debug.Log("Last stand ready. Solo without a self-revive, two hits end the game. " +
                      "Set PlayerHealth > Starting Solo Lives to 1 to test going down and self-reviving. " +
                      "Add the scene to Build Settings so Play Again can reload it on a device.");
        }

        /// <summary>A radial fill behind the interact button's text, driven by InteractPrompt.Progress.</summary>
        internal static void AddProgressRing(InteractButton interact)
        {
            var iso = new SerializedObject(interact);
            var prop = iso.FindProperty("progressFill");
            if (prop.objectReferenceValue != null) return;

            var ring = MobileHudMenu.NewChild("ProgressFill", interact.transform);
            Undo.RegisterCreatedObjectUndo(ring, "Add Progress Ring");
            ring.transform.SetAsFirstSibling(); // Behind the label.
            Stretch(ring.GetComponent<RectTransform>());
            var img = ring.AddComponent<Image>();
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillAmount = 0f;
            img.color = new Color(1f, 1f, 1f, 0.45f);
            img.raycastTarget = false;
            img.enabled = false;

            prop.objectReferenceValue = img;
            iso.ApplyModifiedPropertiesWithoutUndo();
        }

        static Image FullScreen(string name, Transform parent, Color color)
        {
            var go = MobileHudMenu.NewChild(name, parent);
            Stretch(go.GetComponent<RectTransform>());
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            img.enabled = false; // LastStandHud switches it on only while it's visible.
            return img;
        }

        static Image Bar(string name, Transform parent, Sprite sprite, Color color, Vector2 position)
        {
            var go = MobileHudMenu.NewChild(name, parent);
            Place(go.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), position, new Vector2(620f, 22f));
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.color = color;
            img.raycastTarget = false;
            img.enabled = false;
            return img;
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform r, Vector2 anchor, Vector2 position, Vector2 size)
        {
            r.anchorMin = r.anchorMax = r.pivot = anchor;
            r.sizeDelta = size;
            r.anchoredPosition = position;
        }
    }
}
#endif
