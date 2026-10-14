// TestLevelMenu.cs  (editor only: must stay in a folder named "Editor")
//
// Zombies > Build Test Level
// One click builds a complete, playable test scene in a new scene file:
//
//                       +Z  room2_zone (behind the 1000-point door), with a box location
//                        |
//      wall buys  ---  [Player]  ---  perk machines + power switch
//                        |       \
//                        |        Mystery Box (start location)
//                       -Z  boarded window, zombies spawn outside it
//
//   - a 60 x 60 m floor, every system from the other Zombies menus, two Mystery Box locations,
//     hellhound rounds with three dog spots around the start
//   - the NavMesh baked (needs the AI Navigation package)
//   - saved as Assets/Scenes/ZombiesTestLevel.unity and added to Build Settings
//   - the app locked to landscape
// Then press Play, or build to a phone.

#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ZombiesPort.EditorTools
{
    public static class TestLevelMenu
    {
        const string ScenePath = "Assets/Scenes/ZombiesTestLevel.unity";

        [MenuItem("Zombies/Build Test Level", false, 0)]
        static void BuildTestLevel()
        {
            if (!EditorUtility.DisplayDialog("Build Test Level",
                    "This creates a new scene (" + ScenePath + ") with the whole game set up for testing. " +
                    "Your current scene stays as it is (you'll be asked to save it).", "Build", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            try
            {
                Build();
            }
            finally
            {
                EditorPlacement.Clear();
            }
        }

        static void Build()
        {
            // ---- Ground ----
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(60f, 1f, 60f);

            // ---- Core systems (order matters: later steps find earlier ones) ----
            MobileHudMenu.CreateRoundSystem();          // GameBootstrap, RoundDirector, ZombiePool, test zombie
            PlayerSetupMenu.CreatePlayerAndControls();  // player at the origin, facing +Z, touch HUD
            MobileHudMenu.CreateMobileHud();            // interact button
            MobileHudMenu.CreateRoundHud();             // chalk round counter + points
            LastStandMenu.CreateLastStand();            // health, revive, game over

            // The round system's default spawn point: move it to the side of the start room.
            var rounds = Object.FindFirstObjectByType<RoundDirector>();
            if (rounds != null)
            {
                foreach (var sp in rounds.GetComponentsInChildren<ZombieSpawnPoint>())
                {
                    if (sp.Window != null) continue;
                    sp.transform.position = new Vector3(10f, 0f, -3f);
                    sp.transform.rotation = Quaternion.LookRotation(Vector3.left);
                }
            }

            // ---- Map pieces, placed around the player ----
            EditorPlacement.Set(Vector3.zero, Vector3.back, Vector3.zero);
            BarricadeMenu.CreateWindow();               // window 6 m behind the player, spawn outside it

            EditorPlacement.Set(Vector3.zero, Vector3.left, Vector3.zero);
            BarricadeMenu.CreateWallBuys();             // 4 m to the left

            EditorPlacement.Set(Vector3.zero, Vector3.forward, new Vector3(8f, 0f, 2f));
            PerkMenu.CreatePerkMachines();              // to the right: Quick Revive, Juggernog, power switch

            EditorPlacement.Set(Vector3.zero, Vector3.forward, Vector3.zero);
            ZoneMenu.CreateZonesAndDoor();              // door 6.5 m ahead, room2 behind it

            CreateMysteryBox();                         // start room + room2 (so the teddy bear can move it)
            PowerUpMenu.CreatePowerUps();

            EditorPlacement.Set(Vector3.zero, Vector3.forward, Vector3.zero);
            DogMenu.CreateDogRounds();                  // hellhound rounds, dog spots 13-16 m around the start

            // ---- NavMesh, scene file, settings ----
            BakeNavMesh();
            SaveAndRegisterScene();
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

            var player = Object.FindFirstObjectByType<ZombiePlayer>();
            if (player != null) Selection.activeGameObject = player.gameObject;
            Debug.Log("Test level built and saved to " + ScenePath + ". Press Play. In the editor: WASD to move, " +
                      "Tab for mouse look, left mouse to shoot, F to interact (hold to rebuild/revive).");
        }

        // ---------------- Mystery Box (no menu item of its own yet) ----------------

        static void CreateMysteryBox()
        {
            var directorGo = new GameObject("Mystery Box");
            Undo.RegisterCreatedObjectUndo(directorGo, "Create Mystery Box");
            var director = directorGo.AddComponent<MysteryBoxDirector>();

            // Each box's front (local -Z) faces the way the player walks up to it.
            var a = BoxLocation("Box Location (start)", new Vector3(4f, 0f, -4f), Quaternion.LookRotation(new Vector3(1f, 0f, -1f)), true);
            var b = BoxLocation("Box Location (room2)", new Vector3(4f, 0f, 14f), Quaternion.LookRotation(Vector3.forward), false);
            a.transform.SetParent(directorGo.transform, true);
            b.transform.SetParent(directorGo.transform, true);

            var so = new SerializedObject(director);
            var locations = so.FindProperty("locations");
            locations.arraySize = 2;
            locations.GetArrayElementAtIndex(0).objectReferenceValue = a;
            locations.GetArrayElementAtIndex(1).objectReferenceValue = b;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static MysteryBox BoxLocation(string name, Vector3 position, Quaternion rotation, bool start)
        {
            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(position, rotation);

            // The chest, with a lid hinged along its back edge.
            var visual = new GameObject("BoxVisual");
            visual.transform.SetParent(root.transform, false);
            Block("Body", visual.transform, new Vector3(0f, 0.3f, 0f), new Vector3(1.3f, 0.6f, 0.6f));
            var hinge = new GameObject("Lid");
            hinge.transform.SetParent(visual.transform, false);
            hinge.transform.localPosition = new Vector3(0f, 0.6f, 0.3f);
            var lidMesh = Block("LidMesh", hinge.transform, new Vector3(0f, 0.04f, -0.3f), new Vector3(1.3f, 0.08f, 0.6f));
            Object.DestroyImmediate(lidMesh.GetComponent<Collider>());

            var anchor = new GameObject("WeaponAnchor");
            anchor.transform.SetParent(root.transform, false);
            anchor.transform.localPosition = new Vector3(0f, 0.55f, 0f);

            var rubble = Block("Rubble", root.transform, new Vector3(0f, 0.08f, 0f), new Vector3(1.2f, 0.16f, 0.5f));
            Object.DestroyImmediate(rubble.GetComponent<Collider>());

            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1f, 0f);
            trigger.size = new Vector3(2.6f, 2f, 2.6f);

            var box = root.AddComponent<MysteryBox>();
            var so = new SerializedObject(box);
            so.FindProperty("boxVisual").objectReferenceValue = visual;
            so.FindProperty("lid").objectReferenceValue = hinge.transform;
            so.FindProperty("lidHingeAxis").vector3Value = Vector3.right;  // hinge at the back: the front edge lifts
            so.FindProperty("weaponAnchor").objectReferenceValue = anchor.transform;
            so.FindProperty("rubble").objectReferenceValue = rubble;
            so.FindProperty("isStartLocation").boolValue = start;
            so.ApplyModifiedPropertiesWithoutUndo();
            return box;
        }

        // ---------------- NavMesh, scene, settings ----------------

        /// <summary>
        /// Adds a NavMeshSurface (AI Navigation package) and bakes it. Looked up by name so this
        /// compiles without the package; without it, it explains what to install.
        /// </summary>
        static void BakeNavMesh()
        {
            var type = Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation");
            if (type == null)
            {
                Debug.LogWarning("Install the AI Navigation package (Window > Package Manager > Unity Registry), " +
                                 "then run Zombies > Build Test Level again. Zombies can't move without a NavMesh.");
                return;
            }
            var go = new GameObject("NavMesh");
            var surface = go.AddComponent(type);
            var build = type.GetMethod("BuildNavMesh", BindingFlags.Public | BindingFlags.Instance);
            if (build != null) build.Invoke(surface, null);
        }

        static void SaveAndRegisterScene()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene, ScenePath);

            // First in Build Settings, so a phone build starts here and Play Again can reload it.
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.RemoveAll(s => s.path == ScenePath);
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
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
    }
}
#endif
