// PlayerSetupMenu.cs  (editor only: must stay in a folder named "Editor")
//
// GameObject > Zombies > Player + Touch Controls (M1911)
// Builds a playable first-person player and the full touch layout in one click:
//
//   Player                CharacterController, ZombiePlayer, PlayerInteractor, PlayerHealth,
//     CameraPivot         PlayerWeapon, FirstPersonController, AudioSource
//       PlayerCamera      Camera (MainCamera) + AudioListener
//   HUD Canvas / SafeArea (reused if it already exists)
//     TouchControls       MobileInput + its own Canvas, drawn behind the rest of the HUD
//       LookArea          right side: drag to aim
//       MoveZone          lower left: floating movement stick
//       Fire, Aim, Reload, Knife, Swap buttons (bottom-right thumb cluster)
//     AmmoHud             "8 / 32" and weapon name (top-right, under points), hint, crosshair
//   Assets/Weapons/M1911.asset   the starting pistol's stats, ready to tune
//
// Layout at 1920x1080 reference, all inside the safe area:
//   Fire (largest) in the corner under the right thumb's resting spot. Aim and Reload sit
//   inward of it, Knife and Swap above it. The interact button moves center-right, out of
//   the way. Fire also aims when dragged, so one thumb can shoot and track.

#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ZombiesPort.EditorTools
{
    public static class PlayerSetupMenu
    {
        const string M1911Path = "Assets/Weapons/M1911.asset";

        [MenuItem("GameObject/Zombies/Player + Touch Controls (M1911)", false, 13)]
        static void CreatePlayerAndControls()
        {
            if (Object.FindFirstObjectByType<ZombiePlayer>() != null)
                Debug.LogWarning("A ZombiePlayer already exists in this scene. A new player was created; " +
                                 "delete the old one so GameBootstrap doesn't count two players.");

            var m1911 = GetOrCreateM1911Asset();
            var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"); // Unity's built-in circle.

            // ---------------- Player ----------------
            var player = new GameObject("Player");
            Undo.RegisterCreatedObjectUndo(player, "Create Player");
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            player.AddComponent<ZombiePlayer>();
            player.AddComponent<PlayerInteractor>();
            player.AddComponent<PlayerHealth>();
            var audio = player.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f; // The player's own gun is heard "in the head", not positioned.

            var pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(player.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 1.6f, 0f); // Eye height.

            // Only one camera and one AudioListener should be active. Turn off any existing ones.
            var oldCam = Camera.main;
            if (oldCam != null)
            {
                Undo.RecordObject(oldCam.gameObject, "Disable old camera");
                oldCam.gameObject.SetActive(false);
                Debug.Log("Disabled the existing camera \"" + oldCam.name + "\"; the player now has its own.");
            }
            var camGo = new GameObject("PlayerCamera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(pivot.transform, false);
            var cam = camGo.GetComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.fieldOfView = 70f;

            var weapon = player.AddComponent<PlayerWeapon>();
            var fpc = player.AddComponent<FirstPersonController>();

            // ---------------- Touch controls ----------------
            var safe = MobileHudMenu.GetOrCreateSafeArea();
            var controls = MobileHudMenu.NewChild("TouchControls", safe);
            Undo.RegisterCreatedObjectUndo(controls, "Create Touch Controls");
            Stretch(controls.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            controls.transform.SetAsFirstSibling(); // Behind the interact button and HUD text.
            // Own Canvas: the stick knob moves every frame while dragging, and this keeps that from
            // rebuilding the rest of the HUD. A nested Canvas needs its own raycaster to get touches.
            controls.AddComponent<Canvas>();
            controls.AddComponent<GraphicRaycaster>();

            var lookGo = MobileHudMenu.NewChild("LookArea", controls.transform);
            Stretch(lookGo.GetComponent<RectTransform>(), new Vector2(0.42f, 0f), Vector2.one);
            lookGo.AddComponent<TouchZoneGraphic>();
            var look = lookGo.AddComponent<TouchLookArea>();

            var moveGo = MobileHudMenu.NewChild("MoveZone", controls.transform);
            Stretch(moveGo.GetComponent<RectTransform>(), Vector2.zero, new Vector2(0.42f, 0.72f));
            moveGo.AddComponent<TouchZoneGraphic>();
            var stick = moveGo.AddComponent<VirtualJoystick>();

            var stickBase = MobileHudMenu.NewChild("StickBase", moveGo.transform);
            var sb = stickBase.GetComponent<RectTransform>();
            sb.anchorMin = sb.anchorMax = Vector2.zero;
            sb.sizeDelta = new Vector2(260f, 260f);
            sb.anchoredPosition = new Vector2(260f, 260f);
            AddCircle(stickBase, sprite, new Color(1f, 1f, 1f, 0.25f));

            var knob = MobileHudMenu.NewChild("Knob", stickBase.transform);
            var kr = knob.GetComponent<RectTransform>();
            kr.anchorMin = kr.anchorMax = new Vector2(0.5f, 0.5f);
            kr.sizeDelta = new Vector2(120f, 120f);
            AddCircle(knob, sprite, new Color(1f, 1f, 1f, 0.65f));

            var fire = MakeButton("FireButton", "FIRE", controls.transform, sprite, 250f, new Vector2(-70f, 150f), look);
            var aim = MakeButton("AimButton", "AIM", controls.transform, sprite, 150f, new Vector2(-350f, 80f), null);
            var reload = MakeButton("ReloadButton", "R", controls.transform, sprite, 140f, new Vector2(-355f, 260f), null);
            var knife = MakeButton("KnifeButton", "KNIFE", controls.transform, sprite, 140f, new Vector2(-110f, 430f), null);
            var swap = MakeButton("SwapButton", "SWAP", controls.transform, sprite, 120f, new Vector2(-70f, 610f), null);

            var input = controls.AddComponent<MobileInput>();
            Wire(input, ("moveStick", stick), ("lookArea", look), ("fireButton", fire), ("aimButton", aim),
                        ("reloadButton", reload), ("knifeButton", knife), ("swapButton", swap));
            Wire(stick, ("stickBase", sb), ("knob", kr));

            // ---------------- Ammo HUD ----------------
            var ammoGo = MobileHudMenu.NewChild("AmmoHud", safe);
            Undo.RegisterCreatedObjectUndo(ammoGo, "Create Ammo HUD");
            Stretch(ammoGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            var clip = Text("Clip", ammoGo.transform, "8", 64, new Vector2(1f, 1f), new Vector2(-180f, -140f), new Vector2(170f, 80f), TextAlignmentOptions.Right);
            var reserve = Text("Reserve", ammoGo.transform, "/ 32", 38, new Vector2(1f, 1f), new Vector2(-40f, -152f), new Vector2(140f, 60f), TextAlignmentOptions.Left);
            var weaponNameText = Text("WeaponName", ammoGo.transform, "M1911", 30, new Vector2(1f, 1f), new Vector2(-40f, -220f), new Vector2(380f, 50f), TextAlignmentOptions.Right);
            var hint = Text("Hint", ammoGo.transform, "", 40, new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(420f, 60f), TextAlignmentOptions.Center);
            var crossGo = MobileHudMenu.NewChild("Crosshair", ammoGo.transform);
            var cr = crossGo.GetComponent<RectTransform>();
            cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(0.5f, 0.5f);
            cr.sizeDelta = new Vector2(14f, 14f);
            var cross = AddCircle(crossGo, sprite, new Color(1f, 1f, 1f, 0.9f));

            var ammo = ammoGo.AddComponent<AmmoHud>();
            ammoGo.GetComponent<Canvas>().overrideSorting = false;
            Wire(ammo, ("weapon", weapon), ("clipText", clip), ("reserveText", reserve), ("weaponName", weaponNameText),
                       ("hint", hint), ("crosshair", cross));

            // Move an existing interact button out of the thumb cluster.
            var interact = Object.FindFirstObjectByType<InteractButton>();
            if (interact != null)
            {
                var ir = (RectTransform)interact.transform;
                Undo.RecordObject(ir, "Move Interact Button");
                ir.anchoredPosition = MobileHudMenu.InteractButtonPosition;
            }

            // ---------------- Wire the player ----------------
            var wso = new SerializedObject(weapon);
            wso.FindProperty("input").objectReferenceValue = input;
            wso.FindProperty("playerCamera").objectReferenceValue = cam;
            wso.FindProperty("audioSource").objectReferenceValue = audio;
            var catalog = wso.FindProperty("catalog");
            catalog.arraySize = 1;
            catalog.GetArrayElementAtIndex(0).objectReferenceValue = m1911;
            wso.ApplyModifiedPropertiesWithoutUndo();
            Wire(fpc, ("input", input), ("cameraPivot", pivot.transform), ("weapon", weapon));

            Selection.activeGameObject = player;
            Debug.Log("Player + touch controls created. Place the Player on your NavMesh floor and press Play. " +
                      "In the editor: WASD, Tab for mouse look, left mouse fire, right mouse aim, R, V, Q.");
        }

        static WeaponDefinition GetOrCreateM1911Asset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(M1911Path);
            if (existing != null) return existing;
            if (!AssetDatabase.IsValidFolder("Assets/Weapons")) AssetDatabase.CreateFolder("Assets", "Weapons");
            var def = ScriptableObject.CreateInstance<WeaponDefinition>();
            def.stats = WeaponStats.M1911();
            AssetDatabase.CreateAsset(def, M1911Path);
            AssetDatabase.SaveAssets();
            return def;
        }

        static TouchButton MakeButton(string name, string label, Transform parent, Sprite sprite, float size,
                                      Vector2 position, TouchLookArea dragToLook)
        {
            var go = MobileHudMenu.NewChild(name, parent);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 0f);
            r.sizeDelta = new Vector2(size, size);
            r.anchoredPosition = position;
            var img = AddCircle(go, sprite, new Color(1f, 1f, 1f, 0.35f));
            img.raycastTarget = true; // The button's own circle catches the touch.

            var text = MobileHudMenu.NewText("Label", go.transform, label, size * 0.2f, 0f, 1f);
            text.color = new Color(0f, 0f, 0f, 0.8f);

            var button = go.AddComponent<TouchButton>();
            Wire(button, ("visual", img), ("dragToLook", dragToLook));
            return button;
        }

        static Image AddCircle(GameObject go, Sprite sprite, Color color)
        {
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Vector2 anchor,
                                    Vector2 position, Vector2 box, TextAlignmentOptions align)
        {
            var t = MobileHudMenu.NewText(name, parent, text, size, 0f, 1f);
            var r = t.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = anchor;
            r.sizeDelta = box;
            r.anchoredPosition = position;
            t.alignment = align;
            return t;
        }

        static void Stretch(RectTransform r, Vector2 min, Vector2 max)
        {
            r.anchorMin = min;
            r.anchorMax = max;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        /// <summary>Sets private [SerializeField] references the way the Inspector does.</summary>
        static void Wire(Object target, params (string field, Object value)[] refs)
        {
            var so = new SerializedObject(target);
            foreach (var r in refs)
            {
                var p = so.FindProperty(r.field);
                if (p == null) { Debug.LogError("PlayerSetupMenu: no field \"" + r.field + "\" on " + target.GetType().Name); continue; }
                p.objectReferenceValue = r.value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
