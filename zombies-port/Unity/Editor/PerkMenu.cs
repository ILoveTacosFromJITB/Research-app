// PerkMenu.cs  (editor only: must stay in a folder named "Editor")
//
// GameObject > Zombies > Perk Machines (Quick Revive + Juggernog)
//   - Adds PlayerPerks to every ZombiePlayer that lacks it
//   - Places a Quick Revive and a Juggernog machine, and a power switch (if the scene has none),
//     in front of the Scene view camera. Each is a placeholder box with a floating label, a
//     solid collider and a trigger zone for the interact button. Swap in real models later.
//   - Adds the perk icon row to the HUD (top-left, under the round counter)

#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ZombiesPort.EditorTools
{
    public static class PerkMenu
    {
        [MenuItem("GameObject/Zombies/Perk Machines (Quick Revive + Juggernog)", false, 15)]
        static void CreatePerkMachines()
        {
            foreach (var p in Object.FindObjectsByType<ZombiePlayer>(FindObjectsSortMode.None))
                if (p.GetComponent<PlayerPerks>() == null) Undo.AddComponent<PlayerPerks>(p.gameObject);

            // Place things a few meters in front of the Scene view camera, on the ground plane.
            Vector3 origin = Vector3.zero;
            Vector3 right = Vector3.right;
            var view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                origin = view.pivot;
                origin.y = 0f;
                right = Vector3.ProjectOnPlane(view.rotation * Vector3.right, Vector3.up).normalized;
            }

            var revive = Machine("Perk Machine - Quick Revive", PerkId.QuickRevive, "QUICK REVIVE", origin - right * 2f);
            var jugg = Machine("Perk Machine - Juggernog", PerkId.Juggernog, "JUGGERNOG", origin + right * 2f);

            if (Object.FindFirstObjectByType<PowerSwitch>() == null) PowerSwitchObject(origin + right * 5f);

            CreatePerkHud();

            Selection.objects = new Object[] { revive, jugg };
            Debug.Log("Perk machines placed. Turn the power on to use Juggernog (and Quick Revive in co-op). " +
                      "Solo Quick Revive costs 500 and works without power.");
        }

        static GameObject Machine(string name, PerkId perk, string label, Vector3 position)
        {
            var root = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(root, "Create " + name);
            root.transform.position = position;

            // Visible body (hidden if the solo Quick Revive machine is used up).
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Model";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            body.transform.localScale = new Vector3(1f, 2f, 0.8f);
            Label(label, body.transform, new Vector3(0f, 0.65f, 0f), new Vector3(1f / 1f, 1f / 2f, 1f / 0.8f)); // undo the body's scale

            // Interaction zone in front of and around the machine.
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1f, 0.6f);
            trigger.size = new Vector3(2.4f, 2f, 2.6f);

            var machine = root.AddComponent<PerkMachine>();
            var so = new SerializedObject(machine);
            so.FindProperty("perk").enumValueIndex = (int)perk;
            so.FindProperty("machineModel").objectReferenceValue = body;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        static void PowerSwitchObject(Vector3 position)
        {
            var root = new GameObject("Power Switch");
            Undo.RegisterCreatedObjectUndo(root, "Create Power Switch");
            root.transform.position = position;

            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Box";
            box.transform.SetParent(root.transform, false);
            box.transform.localPosition = new Vector3(0f, 1.3f, 0f);
            box.transform.localScale = new Vector3(0.5f, 0.7f, 0.2f);
            Label("POWER", root.transform, new Vector3(0f, 1.9f, 0f), Vector3.one);

            var pivot = new GameObject("LeverPivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 1.3f, 0.15f);
            var lever = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lever.name = "Lever";
            lever.transform.SetParent(pivot.transform, false);
            lever.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            lever.transform.localScale = new Vector3(0.06f, 0.4f, 0.06f);
            Object.DestroyImmediate(lever.GetComponent<Collider>());

            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1f, 0.6f);
            trigger.size = new Vector3(1.8f, 2f, 2f);

            var sw = root.AddComponent<PowerSwitch>();
            var so = new SerializedObject(sw);
            so.FindProperty("lever").objectReferenceValue = pivot.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>A floating world-space label, so placeholder boxes are easy to tell apart.</summary>
        static void Label(string text, Transform parent, Vector3 localPosition, Vector3 inverseParentScale)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = inverseParentScale;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = 3f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(3f, 1f);
        }

        static void CreatePerkHud()
        {
            if (Object.FindFirstObjectByType<PerkHud>() != null) return;
            var safe = MobileHudMenu.GetOrCreateSafeArea();
            var circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            var hudGo = MobileHudMenu.NewChild("PerkHud", safe);
            Undo.RegisterCreatedObjectUndo(hudGo, "Create Perk HUD");
            var r = hudGo.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
            r.sizeDelta = new Vector2(4 * 84f, 80f);
            r.anchoredPosition = new Vector2(40f, -190f); // Under the chalk round counter.

            var slots = new Image[PerkInfo.MaxPerks];
            var letters = new TMP_Text[PerkInfo.MaxPerks];
            for (int i = 0; i < PerkInfo.MaxPerks; i++)
            {
                var slot = MobileHudMenu.NewChild("Slot" + (i + 1), hudGo.transform);
                var sr = slot.GetComponent<RectTransform>();
                sr.anchorMin = sr.anchorMax = new Vector2(0f, 0.5f);
                sr.pivot = new Vector2(0f, 0.5f);
                sr.sizeDelta = new Vector2(72f, 72f);
                sr.anchoredPosition = new Vector2(i * 84f, 0f);
                var img = slot.AddComponent<Image>();
                img.sprite = circle;
                img.raycastTarget = false;
                img.enabled = false;
                slots[i] = img;

                var t = MobileHudMenu.NewText("Letter", slot.transform, "", 40, 0f, 1f);
                t.color = Color.white;
                t.enabled = false;
                letters[i] = t;
            }

            var hud = hudGo.AddComponent<PerkHud>(); // adds the nested Canvas
            hudGo.GetComponent<Canvas>().overrideSorting = false;
            var so = new SerializedObject(hud);
            var sp = so.FindProperty("slots");
            var lp = so.FindProperty("letters");
            sp.arraySize = lp.arraySize = PerkInfo.MaxPerks;
            for (int i = 0; i < PerkInfo.MaxPerks; i++)
            {
                sp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
                lp.GetArrayElementAtIndex(i).objectReferenceValue = letters[i];
            }
            so.FindProperty("placeholderCircle").objectReferenceValue = circle;
            so.FindProperty("player").objectReferenceValue = Object.FindFirstObjectByType<PlayerPerks>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
