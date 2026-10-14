// ZoneMenu.cs  (editor only: must stay in a folder named "Editor")
//
// GameObject > Zombies > Zones + Buyable Door
// Builds a working example in front of the Scene view camera:
//   - a ZoneManager (or reuses the scene's), with "start_zone" as the initial zone
//   - a zone box for start_zone around the camera's focus (if none exists) and one for a new room
//   - a buyable door (1000) between them: a wall with a doorway and a sliding door. The door has
//     a carving NavMeshObstacle so zombies can't path through while it's shut, and is left out of
//     the NavMesh bake, so the doorway is walkable once it opens
//   - the link start_zone <-> new room through the door's flag, and a spawn point in the new room
//     that only starts spawning once the door is bought
// Run it again to chain another room off the starting room.

#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace ZombiesPort.EditorTools
{
    public static class ZoneMenu
    {
        const string StartZone = "start_zone";

        [MenuItem("GameObject/Zombies/Zones + Buyable Door", false, 18)]
        static void CreateZonesAndDoor()
        {
            Vector3 origin = Vector3.zero, fwd = Vector3.forward;
            var view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                fwd = Vector3.ProjectOnPlane(view.rotation * Vector3.forward, Vector3.up).normalized;
                origin = view.pivot;
                origin.y = 0f;
            }
            Quaternion facing = Quaternion.LookRotation(fwd, Vector3.up);

            // ---- Zone manager ----
            var zm = Object.FindFirstObjectByType<ZoneManager>();
            if (zm == null)
            {
                var go = new GameObject("Zone Manager");
                Undo.RegisterCreatedObjectUndo(go, "Create Zone Manager");
                zm = go.AddComponent<ZoneManager>();
                var init = new SerializedObject(zm);
                init.FindProperty("adjacencies").arraySize = 0; // Start clean; the room links are added below.
                init.ApplyModifiedPropertiesWithoutUndo();
            }

            // A fresh room name and flag each time the menu runs.
            int n = 2;
            var volumes = Object.FindObjectsByType<ZoneVolume>(FindObjectsSortMode.None);
            while (Array.Exists(volumes, v => v.ZoneName == "room" + n + "_zone")) n++;
            string room = "room" + n + "_zone";
            string flag = "door_room" + n;

            var so = new SerializedObject(zm);
            var adj = so.FindProperty("adjacencies");
            adj.arraySize++;
            var entry = adj.GetArrayElementAtIndex(adj.arraySize - 1);
            entry.FindPropertyRelative("zoneA").stringValue = StartZone;
            entry.FindPropertyRelative("zoneB").stringValue = room;
            entry.FindPropertyRelative("flag").stringValue = flag;
            entry.FindPropertyRelative("oneWay").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            // ---- Zone volumes ----
            if (!Array.Exists(volumes, v => v.ZoneName == StartZone))
                Volume(StartZone, origin, facing);
            Vector3 roomCenter = origin + fwd * 13f;
            Volume(room, roomCenter, facing);

            // ---- Door between them ----
            var door = DoorObject(origin + fwd * 6.5f, facing, flag);

            // ---- A spawn point in the new room ----
            var spawn = new GameObject("ZombieSpawnPoint (" + room + ")");
            Undo.RegisterCreatedObjectUndo(spawn, "Create Spawn Point");
            spawn.transform.SetPositionAndRotation(roomCenter + fwd * 4f, Quaternion.LookRotation(-fwd));
            var sp = spawn.AddComponent<ZombieSpawnPoint>();
            var spso = new SerializedObject(sp);
            spso.FindProperty("zone").stringValue = room;
            spso.ApplyModifiedPropertiesWithoutUndo();
            var rounds = Object.FindFirstObjectByType<RoundDirector>();
            if (rounds != null) spawn.transform.SetParent(rounds.transform, true);

            Selection.activeGameObject = door;
            Debug.Log("Zones ready: " + StartZone + " <-> " + room + " through \"" + flag + "\" (1000). " +
                      "Resize the zone boxes to fit your rooms, re-bake the NavMesh, and set each spawn " +
                      "point's Zone. Existing spawn points default to " + StartZone + ".");
        }

        static void Volume(string zone, Vector3 center, Quaternion rotation)
        {
            var go = new GameObject("Zone - " + zone);
            Undo.RegisterCreatedObjectUndo(go, "Create Zone Volume");
            go.transform.SetPositionAndRotation(center, rotation);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 2f, 0f);
            box.size = new Vector3(12f, 4f, 12f);
            var v = go.AddComponent<ZoneVolume>();
            var so = new SerializedObject(v);
            so.FindProperty("zoneName").stringValue = zone;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject DoorObject(Vector3 position, Quaternion rotation, string flag)
        {
            var root = new GameObject("Buyable Door (" + flag + ")");
            Undo.RegisterCreatedObjectUndo(root, "Create Buyable Door");
            root.transform.SetPositionAndRotation(position, rotation);

            // Wall with a 2 m x 2.4 m doorway.
            const float wallW = 8f, wallH = 3.2f, depth = 0.3f, doorW = 2f, doorH = 2.4f;
            float side = (wallW - doorW) / 2f;
            Block("WallLeft", root.transform, new Vector3(-(doorW + side) / 2f, wallH / 2f, 0f), new Vector3(side, wallH, depth));
            Block("WallRight", root.transform, new Vector3((doorW + side) / 2f, wallH / 2f, 0f), new Vector3(side, wallH, depth));
            Block("WallTop", root.transform, new Vector3(0f, (doorH + wallH) / 2f, 0f), new Vector3(doorW, wallH - doorH, depth));

            // The door: solid, carves the NavMesh while shut, slides into the right wall when bought.
            var slab = Block("Door", root.transform, new Vector3(0f, doorH / 2f, 0f), new Vector3(doorW, doorH, depth * 0.6f));
            var obstacle = slab.AddComponent<NavMeshObstacle>();
            obstacle.carving = true;
            ExcludeFromNavMeshBake(slab);

            // Interaction zone on both sides of the doorway.
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1f, 0f);
            trigger.size = new Vector3(3f, 2f, 3.4f);

            var door = root.AddComponent<BuyableDoor>();
            var so = new SerializedObject(door);
            so.FindProperty("flags").stringValue = flag;
            var pieces = so.FindProperty("pieces");
            pieces.arraySize = 1;
            var p = pieces.GetArrayElementAtIndex(0);
            p.FindPropertyRelative("transform").objectReferenceValue = slab.transform;
            p.FindPropertyRelative("moveBy").vector3Value = new Vector3(doorW + 0.1f, 0f, 0f);
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        /// <summary>
        /// With the AI Navigation package, a NavMeshModifier set to "Ignore From Build" keeps the
        /// door out of the bake, so the doorway is walkable once the obstacle switches off. Looked
        /// up by name, so this compiles without the package.
        /// </summary>
        static void ExcludeFromNavMeshBake(GameObject go)
        {
            var type = Type.GetType("Unity.AI.Navigation.NavMeshModifier, Unity.AI.Navigation");
            if (type == null)
            {
                Debug.Log("AI Navigation package not found: make sure the door isn't marked Navigation Static before baking.");
                return;
            }
            var modifier = go.AddComponent(type);
            var so = new SerializedObject(modifier);
            var ignore = so.FindProperty("m_IgnoreFromBuild");
            if (ignore != null) { ignore.boolValue = true; so.ApplyModifiedPropertiesWithoutUndo(); }
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
