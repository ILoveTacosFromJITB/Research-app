// DogMenu.cs  (editor only: must stay in a folder named "Editor")
//
// GameObject > Zombies > Dog Rounds
//   - a DogRoundDirector next to the RoundDirector, with its own pool of 8 dogs
//   - a low, fast capsule test dog saved as Assets/DogTestCapsule.prefab (Is Dog, bites for 40)
//   - three dog spawn spots ("Dogs Only") 12-16 m around the Scene view focus, in start_zone
//   - the DogRoundHud: "HELLHOUNDS!" announcement and a dark red tint during the round

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace ZombiesPort.EditorTools
{
    public static class DogMenu
    {
        const string DogPrefabPath = "Assets/DogTestCapsule.prefab";

        [MenuItem("GameObject/Zombies/Dog Rounds", false, 20)]
        internal static void CreateDogRounds()
        {
            var rounds = Object.FindFirstObjectByType<RoundDirector>();
            if (rounds == null)
            {
                Debug.LogWarning("Run Zombies > Round System first: dog rounds plug into it.");
                return;
            }

            var director = Object.FindFirstObjectByType<DogRoundDirector>();
            if (director == null)
            {
                director = Undo.AddComponent<DogRoundDirector>(rounds.gameObject);

                var poolGo = new GameObject("Dog Pool");
                Undo.RegisterCreatedObjectUndo(poolGo, "Create Dog Pool");
                poolGo.transform.SetParent(rounds.transform, false);
                var pool = poolGo.AddComponent<ZombiePool>();
                var pso = new SerializedObject(pool);
                pso.FindProperty("prefab").objectReferenceValue = GetOrCreateTestDog();
                pso.FindProperty("size").intValue = 8; // 2 alive per player, 4 players
                pso.ApplyModifiedPropertiesWithoutUndo();

                // The round system must keep using the zombie pool, not this one.
                var rso = new SerializedObject(rounds);
                if (rso.FindProperty("pool").objectReferenceValue == null)
                {
                    foreach (var p in Object.FindObjectsByType<ZombiePool>(FindObjectsSortMode.None))
                        if (p != pool) { rso.FindProperty("pool").objectReferenceValue = p; break; }
                    rso.ApplyModifiedPropertiesWithoutUndo();
                }

                var dso = new SerializedObject(director);
                dso.FindProperty("dogPool").objectReferenceValue = pool;
                dso.FindProperty("powerUps").objectReferenceValue = Object.FindFirstObjectByType<PowerUpDirector>();
                dso.ApplyModifiedPropertiesWithoutUndo();
            }

            // Dog spots around the focus point.
            EditorPlacement.TryGet(out _, out _, out var pivot);
            Vector3[] offsets = { new Vector3(13f, 0f, 9f), new Vector3(-13f, 0f, 10f), new Vector3(-11f, 0f, -12f) };
            foreach (var o in offsets)
            {
                var go = new GameObject("DogSpawnPoint");
                Undo.RegisterCreatedObjectUndo(go, "Create Dog Spawn Point");
                go.transform.position = pivot + o;
                go.transform.SetParent(rounds.transform, true);
                var sp = go.AddComponent<ZombieSpawnPoint>();
                var so = new SerializedObject(sp);
                so.FindProperty("dogsOnly").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            CreateHud(director);
            Selection.activeGameObject = director.gameObject;
            Debug.Log("Dog rounds ready: the first is round 5, 6 or 7. To see one now, in Play mode right-click " +
                      "the DogRoundDirector > Debug: Next Round Is a Dog Round, then RoundDirector > Debug: Kill All Zombies.");
        }

        static ZombieAgent GetOrCreateTestDog()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ZombieAgent>(DogPrefabPath);
            if (existing != null) return existing;

            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "DogTestCapsule";
            go.transform.localScale = new Vector3(0.6f, 0.45f, 0.6f); // low and small
            var nav = go.AddComponent<NavMeshAgent>();
            nav.radius = 0.5f;          // In the capsule's scaled space: about 0.3 m.
            nav.height = 2f;
            nav.baseOffset = 1f;
            var agent = go.AddComponent<ZombieAgent>();

            var so = new SerializedObject(agent);
            so.FindProperty("isDog").boolValue = true;
            so.FindProperty("meleeDamage").intValue = DogRules.MeleeDamage;
            // Dogs always sprint; a hellhound is faster than a sprinting zombie.
            so.FindProperty("walkSpeed").floatValue = 6f;
            so.FindProperty("runSpeed").floatValue = 6f;
            so.FindProperty("sprintSpeed").floatValue = 6f;
            so.FindProperty("attackRange").floatValue = 1.1f; // ai_meleeRange 100 during dog rounds
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, DogPrefabPath);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<ZombieAgent>();
        }

        static void CreateHud(DogRoundDirector director)
        {
            if (Object.FindFirstObjectByType<DogRoundHud>() != null) return;
            var safe = MobileHudMenu.GetOrCreateSafeArea();
            var hudGo = MobileHudMenu.NewChild("DogRoundHud", safe.parent);
            Undo.RegisterCreatedObjectUndo(hudGo, "Create Dog Round HUD");
            var r = hudGo.GetComponent<RectTransform>();
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            hudGo.transform.SetSiblingIndex(1); // Over the controls and counters, under the power-up and game-over layers.

            var tintGo = MobileHudMenu.NewChild("Tint", hudGo.transform);
            var tr = tintGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
            var tint = tintGo.AddComponent<Image>();
            tint.color = new Color(0.35f, 0.02f, 0f, 0f);
            tint.raycastTarget = false;
            tint.enabled = false;

            var text = MobileHudMenu.NewText("Announcement", hudGo.transform, "HELLHOUNDS!", 110, 0f, 1f);
            var ar = text.rectTransform;
            ar.anchorMin = ar.anchorMax = ar.pivot = new Vector2(0.5f, 0.5f);
            ar.sizeDelta = new Vector2(1400f, 160f);
            ar.anchoredPosition = new Vector2(0f, 200f);
            text.color = new Color(1f, 0.35f, 0.1f);
            text.enabled = false;

            var hud = hudGo.AddComponent<DogRoundHud>(); // adds the nested Canvas
            hudGo.GetComponent<Canvas>().overrideSorting = false;
            var so = new SerializedObject(hud);
            so.FindProperty("director").objectReferenceValue = director;
            so.FindProperty("tint").objectReferenceValue = tint;
            so.FindProperty("announcement").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
