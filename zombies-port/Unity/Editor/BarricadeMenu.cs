// BarricadeMenu.cs  (editor only: must stay in a folder named "Editor")
//
// GameObject > Zombies > Window Barricade
//   A wall section with a raised window (the sill keeps the NavMesh from linking through),
//   6 boards, an invisible player blocker on the Ignore Raycast layer (bullets pass, as through
//   BO1 boards), a rebuild zone inside, and a ZombieSpawnPoint 6 m outside wired to the window.
//   Placed in front of the Scene view camera, facing it (the camera side is "inside").
//
// GameObject > Zombies > Wall Buys (M14, Olympia, MP40)
//   Three chalk-outline wall weapons, with a WeaponDefinition asset per gun in Assets/Weapons,
//   added to every player's catalog so the stats can be tuned in the Inspector.

#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;

namespace ZombiesPort.EditorTools
{
    public static class BarricadeMenu
    {
        const int IgnoreRaycastLayer = 2;

        [MenuItem("GameObject/Zombies/Window Barricade", false, 16)]
        static void CreateWindow()
        {
            PlaceInFrontOfView(6f, out Vector3 pos, out Quaternion facingCamera);
            // Forward points INTO the building, i.e. toward the camera.
            var root = new GameObject("Window Barricade");
            Undo.RegisterCreatedObjectUndo(root, "Create Window Barricade");
            root.transform.SetPositionAndRotation(pos, facingCamera);

            // Wall around a 1.4 m x 1.3 m opening whose sill is 0.9 m up.
            const float wallW = 3.4f, wallH = 3f, depth = 0.3f, openW = 1.4f, sill = 0.9f, top = 2.2f;
            Block("Sill", root.transform, new Vector3(0f, sill / 2f, 0f), new Vector3(wallW, sill, depth));
            Block("Lintel", root.transform, new Vector3(0f, (top + wallH) / 2f, 0f), new Vector3(wallW, wallH - top, depth));
            float side = (wallW - openW) / 2f;
            Block("Left", root.transform, new Vector3(-(openW + side) / 2f, (sill + top) / 2f, 0f), new Vector3(side, top - sill, depth));
            Block("Right", root.transform, new Vector3((openW + side) / 2f, (sill + top) / 2f, 0f), new Vector3(side, top - sill, depth));

            // Player blocker over the opening. Ignore Raycast so bullets and aim assist pass through.
            var clip = new GameObject("PlayerClip");
            clip.layer = IgnoreRaycastLayer;
            clip.transform.SetParent(root.transform, false);
            clip.transform.localPosition = new Vector3(0f, (sill + top) / 2f, 0f);
            var clipCol = clip.AddComponent<BoxCollider>();
            clipCol.size = new Vector3(openW, top - sill, depth);

            // Six boards across the opening, on the outside face, alternately tilted.
            var boards = new Transform[6];
            for (int i = 0; i < boards.Length; i++)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = "Board" + (i + 1);
                b.transform.SetParent(root.transform, false);
                float y = sill + 0.15f + i * ((top - sill - 0.3f) / (boards.Length - 1));
                b.transform.localPosition = new Vector3(0f, y, -depth / 2f - 0.03f);
                b.transform.localRotation = Quaternion.Euler(0f, 0f, i % 2 == 0 ? 6f : -6f);
                b.transform.localScale = new Vector3(openW + 0.3f, 0.14f, 0.04f);
                Object.DestroyImmediate(b.GetComponent<Collider>()); // Visual only.
                boards[i] = b.transform;
            }

            // Rebuild zone: trigger inside, radius 96 units (2.44 m).
            var zone = new GameObject("RebuildZone");
            zone.layer = IgnoreRaycastLayer;
            zone.transform.SetParent(root.transform, false);
            zone.transform.localPosition = new Vector3(0f, 1f, 1f);
            var sphere = zone.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = BarricadeState.RepairRadiusUnits * MysteryBoxRules.UnitsToMeters;

            var barricade = root.AddComponent<Barricade>();
            var so = new SerializedObject(barricade);
            var bp = so.FindProperty("boards");
            bp.arraySize = boards.Length;
            for (int i = 0; i < boards.Length; i++) bp.GetArrayElementAtIndex(i).objectReferenceValue = boards[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            // A spawn point outside, linked to this window.
            var spawn = new GameObject("ZombieSpawnPoint (window)");
            Undo.RegisterCreatedObjectUndo(spawn, "Create Window Spawn");
            spawn.transform.SetPositionAndRotation(pos - root.transform.forward * 6f, facingCamera);
            var sp = spawn.AddComponent<ZombieSpawnPoint>();
            var spso = new SerializedObject(sp);
            spso.FindProperty("window").objectReferenceValue = barricade;
            spso.ApplyModifiedPropertiesWithoutUndo();
            var rounds = Object.FindFirstObjectByType<RoundDirector>();
            if (rounds != null) spawn.transform.SetParent(rounds.transform, true);

            FixPlayerRaycastMasks();
            Selection.activeGameObject = root;
            Debug.Log("Window placed. Re-bake the NavMesh so the ground on both sides is walkable " +
                      "(the sill keeps it from connecting through). Zombies from the linked spawn point " +
                      "tear in through this window; stand inside and hold Interact to rebuild.");
        }

        [MenuItem("GameObject/Zombies/Wall Buys (M14, Olympia, MP40)", false, 17)]
        static void CreateWallBuys()
        {
            PlaceInFrontOfView(4f, out Vector3 pos, out Quaternion facingCamera);
            Vector3 right = facingCamera * Vector3.right;
            string[] ids = { "m14_zm", "rottweil72_zm", "mp40_zm" };
            var created = new Object[ids.Length];
            for (int i = 0; i < ids.Length; i++)
                created[i] = WallBuyObject(ids[i], pos + right * ((i - 1) * 2.2f), facingCamera);

            AddDefinitionsToPlayers(ids);
            Selection.objects = created;
            Debug.Log("Wall buys placed. Change a WallBuy's Weapon Id to any of: " +
                      string.Join(", ", WallBuyRules.KinoWallPrices.Keys) + ". Tune stats in Assets/Weapons.");
        }

        static GameObject WallBuyObject(string id, Vector3 position, Quaternion rotation)
        {
            WeaponPresets.TryGet(id, out var stats);
            int price = WallBuyRules.KinoWallPrices[id];
            var root = new GameObject("Wall Buy - " + stats.DisplayName);
            Undo.RegisterCreatedObjectUndo(root, "Create Wall Buy");
            root.transform.SetPositionAndRotation(position, rotation);

            Block("WallPanel", root.transform, new Vector3(0f, 1.5f, -0.05f), new Vector3(1.8f, 1.2f, 0.1f));

            // Chalk outline stand-in: the name and price written on the wall.
            var chalk = new GameObject("ChalkOutline");
            chalk.transform.SetParent(root.transform, false);
            chalk.transform.localPosition = new Vector3(0f, 1.5f, 0.01f);
            var tmp = chalk.AddComponent<TextMeshPro>();
            tmp.text = stats.DisplayName + "\n" + price;
            tmp.fontSize = 3f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(1.7f, 1.1f);
            tmp.rectTransform.localRotation = Quaternion.Euler(0f, 180f, 0f); // Readable from the front.

            // Gun model stand-in, revealed on first purchase.
            var gun = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gun.name = "WeaponModel";
            gun.transform.SetParent(root.transform, false);
            gun.transform.localPosition = new Vector3(0f, 1.5f, 0.05f);
            gun.transform.localScale = new Vector3(0.9f, 0.18f, 0.06f);
            Object.DestroyImmediate(gun.GetComponent<Collider>());

            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1f, 0.9f);
            trigger.size = new Vector3(1.8f, 2f, 1.6f);

            var wb = root.AddComponent<WallBuy>();
            var so = new SerializedObject(wb);
            so.FindProperty("weaponId").stringValue = id;
            so.FindProperty("chalkOutline").objectReferenceValue = chalk;
            so.FindProperty("weaponModel").objectReferenceValue = gun;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        /// <summary>One WeaponDefinition asset per gun, added to every PlayerWeapon's catalog.</summary>
        static void AddDefinitionsToPlayers(string[] ids)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Weapons")) AssetDatabase.CreateFolder("Assets", "Weapons");
            var defs = new WeaponDefinition[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                WeaponPresets.TryGet(ids[i], out var stats);
                string path = "Assets/Weapons/" + stats.DisplayName.Replace("-", "") + ".asset";
                var def = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
                if (def == null)
                {
                    def = ScriptableObject.CreateInstance<WeaponDefinition>();
                    def.stats = stats;
                    AssetDatabase.CreateAsset(def, path);
                }
                defs[i] = def;
            }
            AssetDatabase.SaveAssets();

            foreach (var pw in Object.FindObjectsByType<PlayerWeapon>(FindObjectsSortMode.None))
            {
                var so = new SerializedObject(pw);
                var cat = so.FindProperty("catalog");
                foreach (var def in defs)
                {
                    bool present = false;
                    for (int i = 0; i < cat.arraySize; i++)
                        if (cat.GetArrayElementAtIndex(i).objectReferenceValue == def) present = true;
                    if (present) continue;
                    cat.arraySize++;
                    cat.GetArrayElementAtIndex(cat.arraySize - 1).objectReferenceValue = def;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>
        /// Players built before windows existed may shoot at every layer, which would let the
        /// invisible window blocker stop bullets. Switch them to the default raycast layers.
        /// </summary>
        static void FixPlayerRaycastMasks()
        {
            int mask = Physics.DefaultRaycastLayers;
            foreach (var pw in Object.FindObjectsByType<PlayerWeapon>(FindObjectsSortMode.None))
            {
                var so = new SerializedObject(pw);
                so.FindProperty("hitMask").intValue = mask;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var fpc in Object.FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))
            {
                var so = new SerializedObject(fpc);
                so.FindProperty("frictionMask").intValue = mask;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static GameObject Block(string name, Transform parent, Vector3 localPos, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            return go;
        }

        /// <summary>A point on the ground in front of the Scene view camera, rotated to face the camera.</summary>
        static void PlaceInFrontOfView(float distance, out Vector3 position, out Quaternion facingCamera)
        {
            position = Vector3.zero;
            facingCamera = Quaternion.identity;
            var view = SceneView.lastActiveSceneView;
            if (view == null) return;
            Vector3 fwd = Vector3.ProjectOnPlane(view.rotation * Vector3.forward, Vector3.up).normalized;
            position = view.camera.transform.position + fwd * distance;
            position.y = 0f;
            facingCamera = Quaternion.LookRotation(-fwd, Vector3.up);
        }
    }
}
#endif
