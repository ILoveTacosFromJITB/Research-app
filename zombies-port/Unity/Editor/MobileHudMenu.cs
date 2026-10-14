// MobileHudMenu.cs  (editor only: must stay in a folder named "Editor")
//
// GameObject > Zombies > Mobile HUD (Interact Button)
// Builds the complete, correctly configured touch HUD in one click:
//
//   HUD Canvas            MobileHudCanvas (Overlay, Scale With Screen Size 1920x1080, match height)
//     SafeArea            SafeAreaFitter
//       InteractButton    Image, Button, CanvasGroup, nested Canvas, GraphicRaycaster, InteractButton
//         Label           TextMeshProUGUI "Mystery Box"
//         Cost            TextMeshProUGUI "950"
//   EventSystem           with the input module for the project's input backend
//
// The button is anchored bottom-right, inside the right thumb's natural arc and above where
// a fire button would sit. Move it in the Scene view if your layout differs.

#if UNITY_EDITOR // Belt and braces: a phone build won't break even if this file leaves the Editor folder.
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ZombiesPort.EditorTools
{
    public static class MobileHudMenu
    {
        [MenuItem("GameObject/Zombies/Mobile HUD (Interact Button)", false, 10)]
        static void CreateMobileHud()
        {
            // HUD Canvas
            var canvasGo = new GameObject("HUD Canvas", typeof(RectTransform), typeof(MobileHudCanvas));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Mobile HUD");
            canvasGo.layer = LayerMask.NameToLayer("UI");

            // SafeArea panel: stretches to the full screen, then SafeAreaFitter trims it.
            var safe = NewChild("SafeArea", canvasGo.transform);
            var safeRect = safe.GetComponent<RectTransform>();
            safeRect.anchorMin = Vector2.zero;
            safeRect.anchorMax = Vector2.one;
            safeRect.offsetMin = safeRect.offsetMax = Vector2.zero;
            safe.AddComponent<SafeAreaFitter>();

            // Interact button: bottom-right, 260x260 canvas units (about 2.4 cm on a typical phone).
            var buttonGo = NewChild("InteractButton", safe.transform);
            var br = buttonGo.GetComponent<RectTransform>();
            br.anchorMin = br.anchorMax = new Vector2(1f, 0f);
            br.pivot = new Vector2(1f, 0f);
            br.sizeDelta = new Vector2(260f, 260f);
            br.anchoredPosition = new Vector2(-340f, 300f);

            var image = buttonGo.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.55f);
            var button = buttonGo.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            // A Button grabs controller/keyboard focus by default, which is meaningless on touch
            // and can leave it stuck "selected". Turn navigation off.
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            // Label fills the top half and the cost the bottom half.
            var label = NewText("Label", buttonGo.transform, "Mystery Box", 40, 0.5f, 1f);
            var cost = NewText("Cost", buttonGo.transform, "950", 52, 0f, 0.5f);

            // AddComponent pulls in CanvasGroup, Canvas and GraphicRaycaster via RequireComponent.
            var interact = buttonGo.AddComponent<InteractButton>();
            var nested = buttonGo.GetComponent<Canvas>();
            nested.overrideSorting = false; // Draw in the HUD's order. The nested canvas only isolates rebuilds.

            // The fields are private [SerializeField], so wire them through SerializedObject
            // the way the Inspector does.
            var so = new SerializedObject(interact);
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("costText").objectReferenceValue = cost;
            so.ApplyModifiedPropertiesWithoutUndo();

            MobileHudCanvas.EnsureEventSystem();

            Selection.activeGameObject = buttonGo;
        }

        static GameObject NewChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            return go;
        }

        static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, float yMin, float yMax)
        {
            var go = NewChild(name, parent);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0f, yMin);
            r.anchorMax = new Vector2(1f, yMax);
            r.offsetMin = new Vector2(12f, 0f);   // Small side padding so long labels don't touch the edge.
            r.offsetMax = new Vector2(-12f, 0f);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.enableAutoSizing = true;   // "Trade Weapons" is longer than "950"; shrink to fit instead of overflowing.
            tmp.fontSizeMin = 18f;
            tmp.fontSizeMax = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
#endif
