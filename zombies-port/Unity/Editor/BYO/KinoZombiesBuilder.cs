// KinoZombiesBuilder.cs  (editor only)
//
// Zombies > BYO > Add Real Zombies: builds the ZombieModelLibrary from zombies_manifest.json
// (tools/byo_zombies.py), makes Assets/BYO_Kino/KinoZombie.prefab (the test zombie's colliders, hitboxes
// and logic with its capsule hidden and the real model added), and points the Kino scene's zombie pool at it.

#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombiesPort;
using Object = UnityEngine.Object;

public static class KinoZombiesBuilder
{
    const string Root = "Assets/BYO_Kino";
    const string ScenePath = "Assets/Scenes/Kino.unity";
    const string LibraryPath = Root + "/ZombieModelLibrary.asset";
    const string PrefabPath = Root + "/KinoZombie.prefab";

    [Serializable] class Clips { public string[] idle, walk, run, sprint, attack, tear, window_attack, climb, death; }
    [Serializable] class Manifest { public string[] bodies; public string[] heads; public Clips clips; }

    [MenuItem("Zombies/BYO/Add Real Zombies")]
    public static void Apply()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath);
        var lib = BuildLibrary();

        // ---- prefab from the scene's current (test) zombie ----
        var pool = Object.FindObjectsByType<ZombiePool>(FindObjectsSortMode.None)
            .FirstOrDefault(p => { var a = new SerializedObject(p).FindProperty("prefab").objectReferenceValue as ZombieAgent; return a != null && !a.IsDog; });
        if (pool == null) throw new Exception("No zombie (non-dog) ZombiePool in the Kino scene.");
        var poolSo = new SerializedObject(pool);
        var source = (ZombieAgent)poolSo.FindProperty("prefab").objectReferenceValue;
        string sourcePath = AssetDatabase.GetAssetPath(source);
        if (sourcePath == PrefabPath) sourcePath = "Assets/ZombieTestCapsule.prefab"; // rebuilding: start from the capsule again

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath));
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        instance.name = "KinoZombie";
        foreach (var r in instance.GetComponentsInChildren<Renderer>(true)) r.enabled = false; // keep colliders/hitboxes, hide the capsule
        var model = new GameObject("Model");
        model.transform.SetParent(instance.transform, false);
        var zm = model.AddComponent<ZombieModel>();
        var so = new SerializedObject(zm);
        so.FindProperty("library").objectReferenceValue = lib;
        so.FindProperty("agent").objectReferenceValue = instance.GetComponent<ZombieAgent>();
        so.ApplyModifiedPropertiesWithoutUndo();
        var prefab = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        Object.DestroyImmediate(instance);

        poolSo.FindProperty("prefab").objectReferenceValue = prefab.GetComponent<ZombieAgent>();
        poolSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log($"[KinoZombiesBuilder] {lib.bodies.Length} bodies, {lib.heads.Length} heads, {lib.roles.Sum(r => r.clips.Count)} clips; pool -> {PrefabPath}");
    }

    static ZombieModelLibrary BuildLibrary()
    {
        var m = JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "/zombies_manifest.json"));
        var lib = AssetDatabase.LoadAssetAtPath<ZombieModelLibrary>(LibraryPath);
        if (lib == null)
        {
            lib = ScriptableObject.CreateInstance<ZombieModelLibrary>();
            AssetDatabase.CreateAsset(lib, LibraryPath);
        }
        lib.bodies = m.bodies.Select(b => AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Characters/{b}.glb")).Where(g => g != null).ToArray();
        lib.heads = m.heads.Select(h => AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Characters/{h}.glb")).Where(g => g != null).ToArray();
        lib.roles.Clear();
        foreach (var f in typeof(Clips).GetFields())
        {
            var names = f.GetValue(m.clips) as string[] ?? Array.Empty<string>();
            lib.roles.Add(new ZombieModelLibrary.Role
            {
                name = f.Name,
                clips = names.Select(n => AssetDatabase.LoadAssetAtPath<TextAsset>($"{Root}/Anims/{n}.bytes")).Where(c => c != null).ToList(),
            });
        }
        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(LibraryPath, ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<ZombieModelLibrary>(LibraryPath);
    }
}

// One entry point for the whole Kino rebuild, in dependency order.
public static class KinoPipeline
{
    [MenuItem("Zombies/BYO/Rebuild Everything")]
    public static void BuildAll()
    {
        KinoGameplayBuilder.BuildPlayable();   // scene + gameplay + NavMesh
        KinoWeaponsBuilder.Apply();            // first-person weapons
        KinoAudioBuilder.Apply();              // sound aliases
        KinoZombiesBuilder.Apply();            // real zombies
        KinoLightingBuilder.Apply();           // map shading, lights, grading
    }

    public static void BuildAllBatch()
    {
        try
        {
            BuildAll();
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
