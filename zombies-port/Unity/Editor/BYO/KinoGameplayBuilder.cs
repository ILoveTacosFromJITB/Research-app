// KinoGameplayBuilder.cs  (editor only)
//
// Wires the Kino der Toten scene (built by KinoSceneBuilder from the user's own game files) into the
// gameplay systems: player + touch controls, rounds, zones, doors, windows, spawners, wall buys, perks,
// power, Mystery Box, power-ups, dog rounds, NavMesh. Positions come from Assets/BYO_Kino/kino_gameplay.json,
// written by tools/byo_kino_gameplay.py from the map's entity list and zone script.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using ZombiesPort;
using ZombiesPort.EditorTools;
using Object = UnityEngine.Object;

public static class KinoGameplayBuilder
{
    const string Root = "Assets/BYO_Kino";
    const string ScenePath = "Assets/Scenes/Kino.unity";
    const int IgnoreRaycastLayer = 2;

    // ---- JSON shapes (see byo_kino_gameplay.py) ----
    [Serializable] class Box { public float[] center; public float[] size; }
    [Serializable] class Piece { public int brushmodel; public bool hasGeometry; public float rotateY; public float[] moveBy; public bool clip; public Box bounds; }
    [Serializable] class Door { public Box trigger; public int cost; public string flag; public bool electric; public bool debris; public string target; public Piece[] pieces; }
    [Serializable] class Window { public int index; public float[] goal; public float[] centre; public float yaw; public Piece[] boards; public Box clip; }
    [Serializable] class Spawner { public float[] pos; public string zone; public int window = -1; public string kind; }
    [Serializable] class DogSpot { public float[] pos; public string zone; }
    [Serializable] class Zone { public string name; public Box[] boxes; }
    [Serializable] class Adj { public string a; public string b; public string flag; }
    [Serializable] class WallBuyE { public Box trigger; public string weapon; public float[] origin; public string target; }
    [Serializable] class PerkE { public Box trigger; public string perk; public string target; public float[] origin; }
    [Serializable] class BoxE { public string name; public Box trigger; public int cost; public bool excludeAtStart; public string lid; public string box; public float[] origin; public float yaw; }
    [Serializable] class PowerE { public Box trigger; public string lever; }
    [Serializable] class EntModel { public string file; public string targetname; public float[] pos; public float[] rot; public float scale; }
    [Serializable] class BrushPlacement { public int brushmodel; public float[] pos; public float[] rot; public string targetname; }
    [Serializable] class PlayerE { public float[] pos; public float yaw; }
    [Serializable]
    class Layout
    {
        public PlayerE player; public string[] initialZones; public Adj[] adjacency; public Zone[] zones;
        public Door[] doors; public Window[] windows; public Spawner[] spawners; public DogSpot[] dogSpots;
        public BrushPlacement[] brushPlacements; public WallBuyE[] wallBuys; public PerkE[] perks; public BoxE[] boxes;
        public PowerE[] power; public EntModel[] entModels;
    }

    static Vector3 V(float[] a) => new Vector3(a[0], a[1], a[2]);

    [MenuItem("Zombies/BYO/Build Playable Kino")]
    public static void BuildPlayable()
    {
        KinoSceneBuilder.Build();
        Apply();
    }

    public static void Apply()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(Root + "/kino_gameplay.json"));
        // JsonUtility can't express null ints; the converter writes null for "no window".
        var raw = File.ReadAllText(Root + "/kino_gameplay.json");
        FixNullWindows(layout, raw);

        try
        {
            EditorPlacement.Set(V(layout.player.pos), Vector3.forward, V(layout.player.pos));
            var world = GameObject.Find("KinoWorld").transform;
            var nodes = new Dictionary<int, Transform>();
            foreach (var t in world.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("brushmodel_") && int.TryParse(t.name.Substring(11), out int i)) nodes[i] = t;

            PlaceBrushModels(layout, nodes);
            var ents = PlaceEntModels(layout);

            // ---- core systems from the existing menus ----
            MobileHudMenu.CreateRoundSystem();
            foreach (var sp in Object.FindObjectsByType<ZombieSpawnPoint>(FindObjectsSortMode.None))
                Object.DestroyImmediate(sp.gameObject); // the menu's placeholder spawn point
            PlayerSetupMenu.CreatePlayerAndControls();
            MobileHudMenu.CreateMobileHud();
            MobileHudMenu.CreateRoundHud();
            LastStandMenu.CreateLastStand();

            var player = Object.FindFirstObjectByType<ZombiePlayer>();
            player.transform.SetPositionAndRotation(V(layout.player.pos) + Vector3.up * 0.05f, Quaternion.Euler(0f, layout.player.yaw, 0f));

            var zones = BuildZones(layout);
            BuildDoors(layout, nodes);
            var windows = BuildWindows(layout, nodes);
            BuildSpawners(layout, windows);
            BuildWallBuys(layout, ents);
            BuildPerksAndPower(layout, ents);
            BuildBoxes(layout, ents);
            PowerUpMenu.CreatePowerUps();
            BuildDogs(layout);

            Invoke(typeof(BarricadeMenu), "FixPlayerRaycastMasks");
            BakeNavMesh();
            Debug.Log($"[KinoGameplayBuilder] zones {zones}, doors {layout.doors.Length}, windows {windows.Count}, " +
                      $"spawners {Object.FindObjectsByType<ZombieSpawnPoint>(FindObjectsSortMode.None).Length}, wall buys {Object.FindObjectsByType<WallBuy>(FindObjectsSortMode.None).Length}, " +
                      $"perks {Object.FindObjectsByType<PerkMachine>(FindObjectsSortMode.None).Length}, box locations {layout.boxes.Length}");
        }
        finally
        {
            EditorPlacement.Clear();
        }

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene, ScenePath);
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        list.RemoveAll(s => s.path == ScenePath);
        list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
    }

    static void FixNullWindows(Layout layout, string raw)
    {
        // "window": null -> -1; JsonUtility would give 0.
        var matches = System.Text.RegularExpressions.Regex.Matches(raw, "\"window\":\\s*(null|\\d+)");
        for (int i = 0; i < matches.Count && i < layout.spawners.Length; i++)
            layout.spawners[i].window = matches[i].Groups[1].Value == "null" ? -1 : int.Parse(matches[i].Groups[1].Value);
    }

    // ---------------- geometry ----------------

    static void PlaceBrushModels(Layout layout, Dictionary<int, Transform> nodes)
    {
        foreach (var p in layout.brushPlacements)
        {
            if (!nodes.TryGetValue(p.brushmodel, out var t)) continue;
            t.SetPositionAndRotation(V(p.pos), new Quaternion(p.rot[0], p.rot[1], p.rot[2], p.rot[3]));
            if (!string.IsNullOrEmpty(p.targetname)) t.name = $"brushmodel_{p.brushmodel} ({p.targetname})";
            foreach (var c in t.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(c.gameObject, 0); // doors and boards move: no static batching
        }
    }

    static Dictionary<string, List<GameObject>> PlaceEntModels(Layout layout)
    {
        var root = new GameObject("KinoEntityModels").transform;
        var byName = new Dictionary<string, List<GameObject>>();
        var cache = new Dictionary<string, GameObject>();
        foreach (var m in layout.entModels)
        {
            if (!cache.TryGetValue(m.file, out var prefab))
                cache[m.file] = prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Props/{m.file}.glb");
            if (prefab == null) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            go.name = string.IsNullOrEmpty(m.targetname) ? m.file : m.targetname;
            go.transform.SetPositionAndRotation(V(m.pos), new Quaternion(m.rot[0], m.rot[1], m.rot[2], m.rot[3]));
            go.transform.localScale = Vector3.one * m.scale;
            if (!string.IsNullOrEmpty(m.targetname))
            {
                if (!byName.TryGetValue(m.targetname, out var l)) byName[m.targetname] = l = new List<GameObject>();
                l.Add(go);
            }
        }
        return byName;
    }

    static GameObject TriggerObject(string name, Box b, Transform parent = null)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = V(b.center);
        var c = go.AddComponent<BoxCollider>();
        c.isTrigger = true;
        c.size = V(b.size);
        return go;
    }

    // ---------------- zones ----------------

    static int BuildZones(Layout layout)
    {
        var go = new GameObject("Zone Manager");
        var zm = go.AddComponent<ZoneManager>();
        var so = new SerializedObject(zm);
        var init = so.FindProperty("initialZones");
        init.arraySize = layout.initialZones.Length;
        for (int i = 0; i < layout.initialZones.Length; i++) init.GetArrayElementAtIndex(i).stringValue = layout.initialZones[i];
        var adj = so.FindProperty("adjacencies");
        adj.arraySize = layout.adjacency.Length;
        for (int i = 0; i < layout.adjacency.Length; i++)
        {
            var e = adj.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("zoneA").stringValue = layout.adjacency[i].a;
            e.FindPropertyRelative("zoneB").stringValue = layout.adjacency[i].b;
            e.FindPropertyRelative("flag").stringValue = layout.adjacency[i].flag;
            e.FindPropertyRelative("oneWay").boolValue = false;
        }
        var flags = so.FindProperty("flagsSetAtStart");
        flags.arraySize = 1;
        flags.GetArrayElementAtIndex(0).stringValue = "always_on";
        so.ApplyModifiedPropertiesWithoutUndo();

        foreach (var z in layout.zones)
            foreach (var b in z.boxes)
            {
                var v = TriggerObject("Zone - " + z.name, b, go.transform);
                v.layer = IgnoreRaycastLayer;
                var zv = v.AddComponent<ZoneVolume>();
                var zso = new SerializedObject(zv);
                zso.FindProperty("zoneName").stringValue = z.name;
                zso.ApplyModifiedPropertiesWithoutUndo();
            }
        return layout.zones.Length;
    }

    // ---------------- doors ----------------

    static void BuildDoors(Layout layout, Dictionary<int, Transform> nodes)
    {
        var root = new GameObject("Doors").transform;
        // Several triggers can open the same door (one each side): one BuyableDoor per target, many colliders.
        foreach (var group in layout.doors.GroupBy(d => d.target))
        {
            var first = group.First();
            var go = TriggerObject($"Door ({first.target}, {first.cost})", first.trigger, root);
            foreach (var extra in group.Skip(1))
            {
                var c = go.AddComponent<BoxCollider>();
                c.isTrigger = true;
                c.center = go.transform.InverseTransformPoint(V(extra.trigger.center));
                c.size = V(extra.trigger.size);
            }

            var door = go.AddComponent<BuyableDoor>();
            var so = new SerializedObject(door);
            so.FindProperty("kind").enumValueIndex = (int)(first.electric ? DoorKind.Electric : DoorKind.Buyable);
            so.FindProperty("cost").intValue = first.cost;
            so.FindProperty("isDebris").boolValue = first.debris;
            so.FindProperty("flags").stringValue = first.flag ?? "";
            var pieces = so.FindProperty("pieces");
            var usable = first.pieces.Where(p => p.hasGeometry && nodes.ContainsKey(p.brushmodel)).ToArray();
            pieces.arraySize = usable.Length;
            for (int i = 0; i < usable.Length; i++)
            {
                var t = nodes[usable[i].brushmodel];
                var e = pieces.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("transform").objectReferenceValue = t;
                e.FindPropertyRelative("moveBy").vector3Value = V(usable[i].moveBy);
                e.FindPropertyRelative("rotateBy").vector3Value = new Vector3(0f, usable[i].rotateY, 0f);
                // Closed doors block zombies; opened ones get out of the way.
                var mf = t.GetComponentInChildren<MeshFilter>();
                var obstacle = t.gameObject.AddComponent<NavMeshObstacle>();
                obstacle.carving = true;
                if (mf != null)
                {
                    obstacle.center = mf.sharedMesh.bounds.center;
                    obstacle.size = mf.sharedMesh.bounds.size + Vector3.one * 0.05f;
                }
                ExcludeFromNavMeshBake(t.gameObject);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void ExcludeFromNavMeshBake(GameObject go)
    {
        var type = Type.GetType("Unity.AI.Navigation.NavMeshModifier, Unity.AI.Navigation");
        if (type == null) return;
        var mod = go.GetComponent(type) ?? go.AddComponent(type);
        var so = new SerializedObject(mod);
        var ignore = so.FindProperty("m_IgnoreFromBuild");
        if (ignore != null) ignore.boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------------- windows + spawners ----------------

    static List<Barricade> BuildWindows(Layout layout, Dictionary<int, Transform> nodes)
    {
        var root = new GameObject("Windows").transform;
        var result = new List<Barricade>();
        foreach (var w in layout.windows)
        {
            var go = new GameObject("Window " + w.index);
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(V(w.centre), Quaternion.Euler(0f, w.yaw, 0f));
            // Forward must point into the building: from the outside goal toward the boards.
            Vector3 inward = V(w.centre) - V(w.goal);
            inward.y = 0f;
            if (inward.sqrMagnitude > 0.01f) go.transform.rotation = Quaternion.LookRotation(inward.normalized, Vector3.up);

            var boards = w.boards.Where(b => nodes.ContainsKey(b.brushmodel)).Select(b => nodes[b.brushmodel]).ToArray();
            foreach (var b in boards) ExcludeFromNavMeshBake(b.gameObject);

            if (w.clip != null)
            {
                var clip = new GameObject("PlayerClip");
                clip.layer = IgnoreRaycastLayer;
                clip.transform.SetParent(go.transform, false);
                clip.transform.SetPositionAndRotation(V(w.clip.center), Quaternion.identity);
                clip.AddComponent<BoxCollider>().size = V(w.clip.size) - new Vector3(0f, 0f, 0f);
            }

            var zone = new GameObject("RebuildZone");
            zone.layer = IgnoreRaycastLayer;
            zone.transform.SetParent(go.transform, false);
            zone.transform.localPosition = new Vector3(0f, 0f, 1f);
            var sphere = zone.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = BarricadeState.RepairRadiusUnits * MysteryBoxRules.UnitsToMeters;

            var barricade = go.AddComponent<Barricade>();
            var so = new SerializedObject(barricade);
            var bp = so.FindProperty("boards");
            bp.arraySize = boards.Length;
            for (int i = 0; i < boards.Length; i++) bp.GetArrayElementAtIndex(i).objectReferenceValue = boards[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            result.Add(barricade);
        }
        return result;
    }

    static void BuildSpawners(Layout layout, List<Barricade> windows)
    {
        var rounds = Object.FindFirstObjectByType<RoundDirector>();
        foreach (var s in layout.spawners)
        {
            var go = new GameObject($"ZombieSpawnPoint ({s.zone}, {s.kind})");
            go.transform.SetParent(rounds.transform, false);
            go.transform.position = V(s.pos);
            var sp = go.AddComponent<ZombieSpawnPoint>();
            var so = new SerializedObject(sp);
            so.FindProperty("zone").stringValue = s.zone;
            if (s.window >= 0 && s.window < windows.Count)
            {
                so.FindProperty("window").objectReferenceValue = windows[s.window];
                go.transform.rotation = Quaternion.LookRotation(Flat(windows[s.window].transform.position - go.transform.position), Vector3.up);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.forward;
    }

    static void BuildDogs(Layout layout)
    {
        DogMenu.CreateDogRounds();
        var rounds = Object.FindFirstObjectByType<RoundDirector>();
        foreach (var sp in Object.FindObjectsByType<ZombieSpawnPoint>(FindObjectsSortMode.None))
            if (new SerializedObject(sp).FindProperty("dogsOnly").boolValue) Object.DestroyImmediate(sp.gameObject);
        foreach (var d in layout.dogSpots)
        {
            var go = new GameObject($"DogSpawnPoint ({d.zone})");
            go.transform.SetParent(rounds.transform, false);
            go.transform.position = V(d.pos);
            var so = new SerializedObject(go.AddComponent<ZombieSpawnPoint>());
            so.FindProperty("zone").stringValue = d.zone;
            so.FindProperty("dogsOnly").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // ---------------- buyables ----------------

    static GameObject FirstEnt(Dictionary<string, List<GameObject>> ents, string name) =>
        name != null && ents.TryGetValue(name, out var l) && l.Count > 0 ? l[0] : null;

    static void BuildWallBuys(Layout layout, Dictionary<string, List<GameObject>> ents)
    {
        var root = new GameObject("Wall Buys").transform;
        var ids = new List<string>();
        foreach (var w in layout.wallBuys)
        {
            if (!WallBuyRules.KinoWallPrices.ContainsKey(w.weapon)) continue; // frag grenades: not in the port yet
            var go = TriggerObject("Wall Buy - " + w.weapon, w.trigger, root);
            var model = FirstEnt(ents, w.target);
            var wb = go.AddComponent<WallBuy>();
            var so = new SerializedObject(wb);
            so.FindProperty("weaponId").stringValue = w.weapon;
            so.FindProperty("weaponModel").objectReferenceValue = model;
            so.ApplyModifiedPropertiesWithoutUndo();
            ids.Add(w.weapon);
        }
        Invoke(typeof(BarricadeMenu), "AddDefinitionsToPlayers", (object)ids.ToArray());
    }

    static void BuildPerksAndPower(Layout layout, Dictionary<string, List<GameObject>> ents)
    {
        var root = new GameObject("Perks").transform;
        foreach (var p in layout.perks)
        {
            if (!Enum.TryParse(p.perk, out PerkId id)) continue;
            var go = TriggerObject("Perk Machine - " + p.perk, p.trigger, root);
            var machine = go.AddComponent<PerkMachine>();
            var so = new SerializedObject(machine);
            so.FindProperty("perk").enumValueIndex = (int)id;
            so.FindProperty("machineModel").objectReferenceValue = FirstEnt(ents, p.target);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        Invoke(typeof(PerkMenu), "CreatePerkHud");

        foreach (var pw in layout.power)
        {
            var go = TriggerObject("Power Switch", pw.trigger);
            var sw = go.AddComponent<PowerSwitch>();
            var so = new SerializedObject(sw);
            var lever = FirstEnt(ents, pw.lever);
            so.FindProperty("lever").objectReferenceValue = lever != null ? lever.transform : null;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void BuildBoxes(Layout layout, Dictionary<string, List<GameObject>> ents)
    {
        var directorGo = new GameObject("Mystery Box");
        var director = directorGo.AddComponent<MysteryBoxDirector>();
        var boxes = new List<MysteryBox>();
        foreach (var b in layout.boxes)
        {
            var root = new GameObject("Box Location (" + b.name + ")");
            root.transform.SetParent(directorGo.transform, false);
            root.transform.SetPositionAndRotation(V(b.origin), Quaternion.Euler(0f, b.yaw, 0f));

            var visual = new GameObject("BoxVisual");
            visual.transform.SetParent(root.transform, false);
            var chest = FirstEnt(ents, b.box);
            var lid = FirstEnt(ents, b.lid);
            if (chest != null) chest.transform.SetParent(visual.transform, true);
            if (lid != null) lid.transform.SetParent(visual.transform, true);

            var anchor = new GameObject("WeaponAnchor");
            anchor.transform.SetParent(root.transform, false);
            anchor.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            var rubble = new GameObject("Rubble");
            rubble.transform.SetParent(root.transform, false);

            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = root.transform.InverseTransformPoint(V(b.trigger.center));
            var size = V(b.trigger.size);
            trigger.size = Mathf.Abs(Mathf.Sin(b.yaw * Mathf.Deg2Rad)) > 0.7f ? new Vector3(size.z, size.y, size.x) : size;

            var box = root.AddComponent<MysteryBox>();
            var so = new SerializedObject(box);
            so.FindProperty("boxVisual").objectReferenceValue = visual;
            so.FindProperty("lid").objectReferenceValue = lid != null ? lid.transform : null;
            so.FindProperty("lidHingeAxis").vector3Value = Vector3.right;
            so.FindProperty("weaponAnchor").objectReferenceValue = anchor.transform;
            so.FindProperty("rubble").objectReferenceValue = rubble;
            so.FindProperty("isStartLocation").boolValue = b.name == "start_chest";
            so.ApplyModifiedPropertiesWithoutUndo();
            boxes.Add(box);
        }
        var dso = new SerializedObject(director);
        var locations = dso.FindProperty("locations");
        locations.arraySize = boxes.Count;
        for (int i = 0; i < boxes.Count; i++) locations.GetArrayElementAtIndex(i).objectReferenceValue = boxes[i];
        if (layout.boxes.Length > 0) dso.FindProperty("cost").intValue = layout.boxes[0].cost;
        dso.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------------- helpers ----------------

    static void BakeNavMesh()
    {
        var type = Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation");
        if (type == null) { Debug.LogWarning("AI Navigation package missing: no NavMesh baked."); return; }
        var go = new GameObject("NavMesh");
        var surface = go.AddComponent(type);
        type.GetMethod("BuildNavMesh", BindingFlags.Public | BindingFlags.Instance)?.Invoke(surface, null);
    }

    static void Invoke(Type type, string method, params object[] args)
    {
        var m = type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (m == null) { Debug.LogWarning($"[KinoGameplayBuilder] {type.Name}.{method} not found"); return; }
        m.Invoke(null, args);
    }

    public static void BuildPlayableBatch()
    {
        try
        {
            BuildPlayable();
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }
}
#endif
