using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Assembles the Kino der Toten scene from assets converted out of the user's own Black Ops install
// (tools/byo_convert_kino.py writes them to Assets/BYO_Kino). Nothing here contains game data.
public static class KinoSceneBuilder
{
    const string Root = "Assets/BYO_Kino";
    const string ScenePath = "Assets/Scenes/Kino.unity";
    const float Inch = 0.0254f;

    [Serializable] class Layout { public Prop[] props; }
    [Serializable] class Prop { public string model; public string file; public float[] pos; public float[] rot; public float scale; }

    // CoD (right-handed, Z-up, inches) -> Unity, matching glTF (x, z, -y) followed by glTFast's X flip.
    public static Vector3 CodToUnity(Vector3 cod) => new Vector3(-cod.x, cod.z, -cod.y) * Inch;

    public static Quaternion CodYawToUnity(float yawDegrees)
    {
        float r = yawDegrees * Mathf.Deg2Rad;
        return Quaternion.LookRotation(new Vector3(-Mathf.Cos(r), 0f, -Mathf.Sin(r)), Vector3.up);
    }

    [MenuItem("Zombies/BYO/Build Kino Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var world = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/World/kino_world.glb");
        if (world == null)
            throw new Exception("kino_world.glb did not import. Is com.unity.cloud.gltfast installed?");
        var worldGo = (GameObject)PrefabUtility.InstantiatePrefab(world);
        worldGo.name = "KinoWorld";
        int colliders = 0;
        foreach (var mf in worldGo.GetComponentsInChildren<MeshFilter>())
        {
            mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            GameObjectUtility.SetStaticEditorFlags(mf.gameObject, (StaticEditorFlags)~0);
            colliders++;
        }

        var propsRoot = new GameObject("KinoProps").transform;
        var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(Root + "/kino_layout.json"));
        var cache = new Dictionary<string, GameObject>();
        int placed = 0, missing = 0;
        foreach (var p in layout.props)
        {
            if (!cache.TryGetValue(p.file, out var prefab))
                cache[p.file] = prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Props/{p.file}.glb");
            if (prefab == null) { missing++; continue; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, propsRoot);
            go.transform.SetLocalPositionAndRotation(new Vector3(p.pos[0], p.pos[1], p.pos[2]), new Quaternion(p.rot[0], p.rot[1], p.rot[2], p.rot[3]));
            go.transform.localScale = Vector3.one * p.scale;
            GameObjectUtility.SetStaticEditorFlags(go, (StaticEditorFlags)~0);
            placed++;
        }

        // Lighting until lightmaps are exported: a key light plus bright ambient.
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.1f;
        sun.shadows = LightShadows.None;
        sun.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.55f, 0.55f, 0.6f);
        RenderSettings.ambientEquatorColor = new Color(0.4f, 0.38f, 0.36f);
        RenderSettings.ambientGroundColor = new Color(0.2f, 0.18f, 0.16f);

        var spawn = new GameObject("PlayerSpawn").transform;
        if (TryFindPlayerStart(out var origin, out var yaw))
        {
            spawn.SetPositionAndRotation(CodToUnity(origin), CodYawToUnity(yaw));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[KinoSceneBuilder] saved {ScenePath}: world colliders {colliders}, props placed {placed}, missing {missing}, spawn {spawn.position}");
    }

    static bool TryFindPlayerStart(out Vector3 origin, out float yaw)
    {
        origin = Vector3.zero;
        yaw = 0f;
        var ents = File.ReadAllText(Root + "/zombie_theater_ents.txt");
        var m = Regex.Match(ents, "\"classname\" \"info_player_start\"\\s*\"angles\" \"[-\\d.]+ ([-\\d.]+) [-\\d.]+\"\\s*\"origin\" \"([-\\d.]+) ([-\\d.]+) ([-\\d.]+)\"");
        if (!m.Success)
            return false;
        yaw = float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        origin = new Vector3(float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
                             float.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture),
                             float.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture));
        return true;
    }

    // Renders verification shots of the saved scene to <project>/../BYO_Previews (outside Assets).
    [MenuItem("Zombies/BYO/Capture Kino Previews")]
    public static void Capture()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var spawn = GameObject.Find("PlayerSpawn").transform;
        var outDir = Path.GetFullPath("BYO_Previews");
        Directory.CreateDirectory(outDir);

        var cam = new GameObject("PreviewCam").AddComponent<Camera>();
        cam.fieldOfView = 75f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 1000f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.1f);

        var eye = spawn.position + Vector3.up * 1.6f;
        Shot(cam, outDir, "spawn_forward", eye, spawn.rotation);
        Shot(cam, outDir, "spawn_left", eye, spawn.rotation * Quaternion.Euler(0, -90, 0));
        Shot(cam, outDir, "spawn_right", eye, spawn.rotation * Quaternion.Euler(0, 90, 0));
        Shot(cam, outDir, "spawn_back", eye, spawn.rotation * Quaternion.Euler(0, 180, 0));
        Shot(cam, outDir, "overview", spawn.position + new Vector3(30f, 45f, -30f), Quaternion.LookRotation(spawn.position - (spawn.position + new Vector3(30f, 45f, -30f))));
        UnityEngine.Object.DestroyImmediate(cam.gameObject);
        Debug.Log("[KinoSceneBuilder] previews written to " + outDir);
    }

    static void Shot(Camera cam, string dir, string name, Vector3 pos, Quaternion rot)
    {
        cam.transform.SetPositionAndRotation(pos, rot);
        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(tex);
    }

    public static void BuildAndCaptureBatch()
    {
        try
        {
            Build();
            Capture();
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }
}

// Mobile-sized import settings for converted textures.
public class KinoTexturePostprocessor : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/BYO_Kino/Textures/"))
            return;
        var importer = (TextureImporter)assetImporter;
        importer.maxTextureSize = 1024;
        importer.mipmapEnabled = true;
        importer.textureCompression = TextureImporterCompression.Compressed;
    }
}
