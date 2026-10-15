// KinoWeaponsBuilder.cs  (editor only)
//
// Zombies > BYO > Add First-Person Weapons: builds the ViewModelLibrary from
// Assets/BYO_Kino/weapons_manifest.json (written by tools/byo_weapons.py), adds the ViewModelRig under
// the player camera and a hit marker to the HUD of the open Kino scene.

#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using ZombiesPort;
using Object = UnityEngine.Object;

public static class KinoWeaponsBuilder
{
    const string Root = "Assets/BYO_Kino";
    const string LibraryPath = Root + "/ViewModelLibrary.asset";
    const string ScenePath = "Assets/Scenes/Kino.unity";
    const string HitMarkerTexture = Root + "/Textures/damage_feedback.png";

    [Serializable] class Manifest { public Weapon[] weapons; }
    [Serializable] class Weapon { public string id; public string gunFile; public string handFile; public Anims anims; public Numbers numbers; public Sounds sounds; public string[] hideTags; }
    [Serializable] class Numbers { public float adsZoomFov1; }
    [Serializable]
    class Anims
    {
        public string idleAnim, emptyIdleAnim, fireAnim, lastShotAnim, reloadAnim, reloadEmptyAnim, raiseAnim, dropAnim,
            firstRaiseAnim, quickRaiseAnim, quickDropAnim, sprintInAnim, sprintLoopAnim, sprintOutAnim, adsUpAnim,
            adsDownAnim, adsFireAnim, adsLastShotAnim, meleeAnim;
    }
    [Serializable]
    class Sounds
    {
        public string fireSoundPlayer, fireSound, lastShotSoundPlayer, emptyFireSoundPlayer, emptyFireSound,
            raiseSoundPlayer, raiseSound, firstRaiseSoundPlayer, pickupSoundPlayer;
    }

    [MenuItem("Zombies/BYO/Add First-Person Weapons")]
    public static void Apply()
    {
        EnsureLayer("Viewmodel", 8);
        // Open the scene first: OpenScene unloads unused assets, which would destroy a library built before it.
        if (EditorSceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath);
        var library = BuildLibrary();
        var weapon = Object.FindFirstObjectByType<PlayerWeapon>();
        if (weapon == null) throw new Exception("No PlayerWeapon in the Kino scene. Run Build Playable Kino first.");
        var cam = new SerializedObject(weapon).FindProperty("playerCamera").objectReferenceValue as Camera;
        if (cam == null) cam = weapon.GetComponentInChildren<Camera>();

        var old = cam.transform.Find("ViewModel");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var rigGo = new GameObject("ViewModel");
        rigGo.transform.SetParent(cam.transform, false);
        var rig = rigGo.AddComponent<ViewModelRig>();
        var so = new SerializedObject(rig);
        so.FindProperty("library").objectReferenceValue = library;
        so.FindProperty("weapon").objectReferenceValue = weapon;
        so.FindProperty("controller").objectReferenceValue = weapon.GetComponentInParent<FirstPersonController>() ?? Object.FindFirstObjectByType<FirstPersonController>();
        so.ApplyModifiedPropertiesWithoutUndo();

        AddHitMarker(weapon);

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log($"[KinoWeaponsBuilder] library {library.weapons.Count} weapons, rig under {cam.name}");
    }

    static ViewModelLibrary BuildLibrary()
    {
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "/weapons_manifest.json"));
        var lib = AssetDatabase.LoadAssetAtPath<ViewModelLibrary>(LibraryPath);
        if (lib == null)
        {
            lib = ScriptableObject.CreateInstance<ViewModelLibrary>();
            AssetDatabase.CreateAsset(lib, LibraryPath);
        }
        lib.weapons.Clear();
        foreach (var w in manifest.weapons)
        {
            if (lib.handsPrefab == null && !string.IsNullOrEmpty(w.handFile))
                lib.handsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Viewmodels/{w.handFile}.glb");
            if (w.id == "knife_zm")
            {
                lib.knifeClip = Clip(w.anims.meleeAnim);
                continue;
            }
            var e = new ViewModelLibrary.Entry
            {
                weaponId = w.id,
                gunPrefab = string.IsNullOrEmpty(w.gunFile) ? null : AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Viewmodels/{w.gunFile}.glb"),
                adsFov = w.numbers != null && w.numbers.adsZoomFov1 > 0f ? w.numbers.adsZoomFov1 : 65f,
            };
            if (w.hideTags != null) e.hideTags.AddRange(w.hideTags);
            foreach (var f in typeof(Anims).GetFields())
            {
                var clip = Clip(f.GetValue(w.anims) as string);
                if (clip != null) e.clips.Add(new ViewModelLibrary.ClipRef { key = f.Name, clip = clip });
            }
            if (w.sounds != null)
                foreach (var f in typeof(Sounds).GetFields())
                    if (f.GetValue(w.sounds) is string alias && !string.IsNullOrEmpty(alias))
                    {
                        e.soundKeys.Add(f.Name);
                        e.soundAliases.Add(alias);
                    }
            lib.weapons.Add(e);
        }
        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        return lib;
    }

    static TextAsset Clip(string name) =>
        string.IsNullOrEmpty(name) ? null : AssetDatabase.LoadAssetAtPath<TextAsset>($"{Root}/Anims/{name}.bytes");

    static void AddHitMarker(PlayerWeapon weapon)
    {
        var existing = Object.FindFirstObjectByType<HitMarker>();
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var importer = AssetImporter.GetAtPath(HitMarkerTexture) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.SaveAndReimport();
        }
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(HitMarkerTexture);

        var parent = ZombiesPort.EditorTools.MobileHudMenu.GetOrCreateSafeArea();
        var go = new GameObject("HitMarker", typeof(RectTransform), typeof(Image), typeof(HitMarker));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(64f, 64f);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        var so = new SerializedObject(go.GetComponent<HitMarker>());
        so.FindProperty("weapon").objectReferenceValue = weapon;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void EnsureLayer(string name, int index)
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++)
            if (layers.GetArrayElementAtIndex(i).stringValue == name) return;
        var slot = layers.GetArrayElementAtIndex(index);
        if (!string.IsNullOrEmpty(slot.stringValue)) throw new Exception($"Layer {index} is already '{slot.stringValue}'");
        slot.stringValue = name;
        tagManager.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void ApplyBatch()
    {
        try
        {
            Apply();
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
