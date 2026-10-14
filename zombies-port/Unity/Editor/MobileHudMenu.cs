// MobileHudMenu.cs  (editor only: must stay in a folder named "Editor")
//
// One-click scene setup:
//   GameObject > Zombies > Mobile HUD (Interact Button)
//   GameObject > Zombies > Round HUD (Chalk + Points)
//   GameObject > Zombies > Round System (Pool + Test Zombie)
// The HUD items share one HUD Canvas: the second one reuses the canvas the first created.
//
//   HUD Canvas            MobileHudCanvas (Overlay, Scale With Screen Size 1920x1080, match height)
//     SafeArea            SafeAreaFitter
//       InteractButton    Image, Button, CanvasGroup, nested Canvas, GraphicRaycaster, InteractButton
//         Label           TextMeshProUGUI "Mystery Box"
//         Cost            TextMeshProUGUI "950"
//   EventSystem           with the input module for the project's input backend
//
// The interact button sits center-right, inside the right thumb's reach but clear of the
// Fire/Aim/Reload/Knife cluster in the corner. Move it in the Scene view if your layout differs.
// Player + touch controls: see PlayerSetupMenu.cs.

#if UNITY_EDITOR // Belt and braces: a phone build won't break even if this file leaves the Editor folder.
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace ZombiesPort.EditorTools
{
    public static class MobileHudMenu
    {
        /// <summary>Finds the scene's HUD Canvas and its SafeArea panel, creating them if missing.</summary>
        internal static Transform GetOrCreateSafeArea()
        {
            var existing = Object.FindFirstObjectByType<MobileHudCanvas>();
            if (existing != null)
            {
                var fitter = existing.GetComponentInChildren<SafeAreaFitter>(true);
                if (fitter != null) return fitter.transform;
            }

            var canvasGo = existing != null
                ? existing.gameObject
                : new GameObject("HUD Canvas", typeof(RectTransform), typeof(MobileHudCanvas));
            if (existing == null)
            {
                Undo.RegisterCreatedObjectUndo(canvasGo, "Create HUD Canvas");
                canvasGo.layer = LayerMask.NameToLayer("UI");
            }

            // SafeArea panel: stretches to the full screen, then SafeAreaFitter trims it.
            var safe = NewChild("SafeArea", canvasGo.transform);
            var safeRect = safe.GetComponent<RectTransform>();
            safeRect.anchorMin = Vector2.zero;
            safeRect.anchorMax = Vector2.one;
            safeRect.offsetMin = safeRect.offsetMax = Vector2.zero;
            safe.AddComponent<SafeAreaFitter>();

            MobileHudCanvas.EnsureEventSystem();
            return safe.transform;
        }

        /// <summary>Center-right, clear of the Fire/Aim/Reload/Knife cluster in the bottom-right corner.</summary>
        internal static readonly Vector2 InteractButtonPosition = new Vector2(-560f, 440f);

        [MenuItem("GameObject/Zombies/Mobile HUD (Interact Button)", false, 10)]
        static void CreateMobileHud()
        {
            var safe = GetOrCreateSafeArea();

            // Interact button: 260x260 canvas units (about 2.4 cm on a typical phone), anchored to the bottom-right corner.
            var buttonGo = NewChild("InteractButton", safe);
            Undo.RegisterCreatedObjectUndo(buttonGo, "Create Interact Button");
            var br = buttonGo.GetComponent<RectTransform>();
            br.anchorMin = br.anchorMax = new Vector2(1f, 0f);
            br.pivot = new Vector2(1f, 0f);
            br.sizeDelta = new Vector2(260f, 260f);
            br.anchoredPosition = InteractButtonPosition;

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
            LastStandMenu.AddProgressRing(interact); // Shows hold-to-revive progress.

            Selection.activeGameObject = buttonGo;
        }

        [MenuItem("GameObject/Zombies/Round HUD (Chalk + Points)", false, 11)]
        static void CreateRoundHud()
        {
            var safe = GetOrCreateSafeArea();

            // Full-screen panel with its own Canvas (RequireComponent on RoundHud adds it).
            var hudGo = NewChild("RoundHud", safe);
            Undo.RegisterCreatedObjectUndo(hudGo, "Create Round HUD");
            var hr = hudGo.GetComponent<RectTransform>();
            hr.anchorMin = Vector2.zero;
            hr.anchorMax = Vector2.one;
            hr.offsetMin = hr.offsetMax = Vector2.zero;

            // Chalk counter, top-left (bottom-left is the movement stick's corner on mobile).
            var tally1 = NewImage("Tally1", hudGo.transform, new Vector2(0f, 1f), new Vector2(40f, -40f));
            var tally2 = NewImage("Tally2", hudGo.transform, new Vector2(0f, 1f), new Vector2(180f, -40f));
            var number = NewText("RoundNumber", hudGo.transform, "1", 110, 0f, 1f);
            var nr = number.rectTransform;
            nr.anchorMin = nr.anchorMax = new Vector2(0f, 1f);
            nr.pivot = new Vector2(0f, 1f);
            nr.sizeDelta = new Vector2(280f, 140f);
            nr.anchoredPosition = new Vector2(40f, -30f);
            number.alignment = TextAlignmentOptions.Left;

            // Points, top-right.
            var points = NewText("Points", hudGo.transform, "500", 64, 0f, 1f);
            var pr = points.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(1f, 1f);
            pr.pivot = new Vector2(1f, 1f);
            pr.sizeDelta = new Vector2(360f, 90f);
            pr.anchoredPosition = new Vector2(-40f, -40f);
            points.alignment = TextAlignmentOptions.Right;

            var hud = hudGo.AddComponent<RoundHud>();
            hudGo.GetComponent<Canvas>().overrideSorting = false;

            var so = new SerializedObject(hud);
            so.FindProperty("tally1").objectReferenceValue = tally1;
            so.FindProperty("tally2").objectReferenceValue = tally2;
            so.FindProperty("roundNumber").objectReferenceValue = number;
            so.FindProperty("points").objectReferenceValue = points;
            so.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = hudGo;
        }

        /// <summary>
        /// GameBootstrap (if missing), a "Round System" object with RoundDirector + ZombiePool,
        /// one spawn point, and a capsule test zombie saved as a prefab so the pool works
        /// right away. Swap the prefab for your real zombie later.
        /// </summary>
        [MenuItem("GameObject/Zombies/Round System (Pool + Test Zombie)", false, 12)]
        static void CreateRoundSystem()
        {
            if (Object.FindFirstObjectByType<GameBootstrap>() == null)
            {
                var boot = new GameObject("GameBootstrap", typeof(GameBootstrap));
                Undo.RegisterCreatedObjectUndo(boot, "Create GameBootstrap");
            }

            var root = new GameObject("Round System");
            Undo.RegisterCreatedObjectUndo(root, "Create Round System");
            var pool = root.AddComponent<ZombiePool>();
            var director = root.AddComponent<RoundDirector>();

            var spawn = new GameObject("ZombieSpawnPoint", typeof(ZombieSpawnPoint));
            spawn.transform.SetParent(root.transform, false);
            spawn.transform.localPosition = new Vector3(0f, 0f, 10f);

            var so = new SerializedObject(pool);
            so.FindProperty("prefab").objectReferenceValue = GetOrCreateTestZombie();
            so.ApplyModifiedPropertiesWithoutUndo();

            var dso = new SerializedObject(director);
            dso.FindProperty("pool").objectReferenceValue = pool;
            dso.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = root;
            Debug.Log("Round System created. Bake a NavMesh, put a ZombiePlayer in the scene, " +
                      "move the spawn point somewhere walkable, then press Play.");
        }

        const string TestZombiePath = "Assets/ZombieTestCapsule.prefab";

        static ZombieAgent GetOrCreateTestZombie()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ZombieAgent>(TestZombiePath);
            if (existing != null) return existing;

            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule); // Comes with a CapsuleCollider.
            go.name = "ZombieTestCapsule";
            var nav = go.AddComponent<NavMeshAgent>();
            nav.radius = 0.35f;
            nav.height = 1.8f;
            nav.baseOffset = 1f; // The capsule's pivot is at its center.
            go.AddComponent<ZombieAgent>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, TestZombiePath);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<ZombieAgent>();
        }

        internal static Image NewImage(string name, Transform parent, Vector2 anchor, Vector2 position)
        {
            var go = NewChild(name, parent);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = anchor;
            r.sizeDelta = new Vector2(128f, 128f);
            r.anchoredPosition = position;
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.enabled = false; // RoundHud turns it on once tally sprites are assigned.
            return img;
        }

        internal static GameObject NewChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            return go;
        }

        internal static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, float yMin, float yMax)
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
