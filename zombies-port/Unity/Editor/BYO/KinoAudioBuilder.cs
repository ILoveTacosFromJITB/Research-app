// KinoAudioBuilder.cs  (editor only)
//
// Zombies > BYO > Add Sounds: builds the SoundAliasBank from Assets/BYO_Kino/sound_aliases.json
// (tools/byo_sounds.py), adds ZombieAudio + GameAudioDirector to the Kino scene, and fills the existing
// sound slots (doors, wall buys, perk machines, windows, power switch) with the game's own aliases.

#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombiesPort;
using Object = UnityEngine.Object;

public static class KinoAudioBuilder
{
    const string Root = "Assets/BYO_Kino";
    const string BankPath = Root + "/SoundAliasBank.asset";
    const string ScenePath = "Assets/Scenes/Kino.unity";

    [Serializable] class Json { public Entry[] aliases; }
    [Serializable]
    class Entry
    {
        public string name; public string[] clips; public string secondary;
        public float volMin, volMax, pitchMin, pitchMax, distMin, distMax; public bool loop; public int limit;
    }

    [MenuItem("Zombies/BYO/Add Sounds")]
    public static void Apply()
    {
        // Open the scene first: OpenScene unloads unused assets, which would destroy a bank built before it.
        if (EditorSceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath);
        var bank = BuildBank();

        var old = GameObject.Find("Audio");
        if (old != null) Object.DestroyImmediate(old);
        var go = new GameObject("Audio");
        var audio = go.AddComponent<ZombieAudio>();
        var so = new SerializedObject(audio);
        so.FindProperty("bank").objectReferenceValue = bank;
        so.ApplyModifiedPropertiesWithoutUndo();
        go.AddComponent<GameAudioDirector>();

        int slots = 0;
        foreach (var d in Object.FindObjectsByType<BuyableDoor>(FindObjectsSortMode.None))
            slots += Slot(d, "openSound", d.name.Contains("fence") ? "zmb_door_fence_open" : "zmb_door_wood_open", bank);
        foreach (var w in Object.FindObjectsByType<WallBuy>(FindObjectsSortMode.None))
            slots += Slot(w, "purchaseSound", "zmb_weap_wall", bank);
        foreach (var p in Object.FindObjectsByType<PerkMachine>(FindObjectsSortMode.None))
        {
            var perk = (PerkId)new SerializedObject(p).FindProperty("perk").enumValueIndex;
            string sting = perk switch
            {
                PerkId.QuickRevive => "mus_perks_revive_sting",
                PerkId.Juggernog => "mus_perks_jugganog_sting",
                PerkId.SpeedCola => "mus_perks_speed_sting",
                PerkId.DoubleTap => "mus_perks_doubletap_sting",
                _ => null,
            };
            slots += Slot(p, "jingle", sting, bank);
        }
        foreach (var b in Object.FindObjectsByType<Barricade>(FindObjectsSortMode.None))
        {
            slots += Slot(b, "tearSound", "zmb_break_boards", bank);
            slots += Slot(b, "repairSound", "zmb_repair_boards", bank);
        }
        foreach (var s in Object.FindObjectsByType<PowerSwitch>(FindObjectsSortMode.None))
            slots += Slot(s, "powerOnSound", "zmb_perks_power_on", bank);

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log($"[KinoAudioBuilder] bank {bank.aliases.Count} aliases, {slots} component sound slots filled");
    }

    static SoundAliasBank BuildBank()
    {
        var json = JsonUtility.FromJson<Json>(File.ReadAllText(Root + "/sound_aliases.json"));
        var bank = AssetDatabase.LoadAssetAtPath<SoundAliasBank>(BankPath);
        if (bank == null)
        {
            bank = ScriptableObject.CreateInstance<SoundAliasBank>();
            AssetDatabase.CreateAsset(bank, BankPath);
        }
        bank.aliases.Clear();
        foreach (var e in json.aliases)
        {
            var clips = e.clips.Select(c => AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/Sounds/{c}")).Where(c => c != null).ToArray();
            if (clips.Length == 0) continue;
            bank.aliases.Add(new SoundAliasBank.Alias
            {
                name = e.name, clips = clips, secondary = e.secondary,
                volMin = e.volMin, volMax = e.volMax, pitchMin = e.pitchMin, pitchMax = e.pitchMax,
                distMin = e.distMin, distMax = e.distMax, loop = e.loop, limit = e.limit,
            });
        }
        EditorUtility.SetDirty(bank);
        AssetDatabase.SaveAssets();
        // Saving reimports the asset and destroys this instance: import synchronously and hand out the reloaded
        // object, or the scene keeps a dead (null) reference.
        AssetDatabase.ImportAsset(BankPath, ImportAssetOptions.ForceSynchronousImport);
        var reloaded = AssetDatabase.LoadAssetAtPath<SoundAliasBank>(BankPath);
        if (reloaded == null) throw new Exception("SoundAliasBank did not reload after saving: " + BankPath);
        return reloaded;
    }

    // Puts the alias's first clip in a component's AudioClip field and gives it a 3D AudioSource.
    static int Slot(Component c, string field, string alias, SoundAliasBank bank)
    {
        var a = bank.Find(alias);
        if (a == null || a.clips.Length == 0) return 0;
        var src = c.GetComponent<AudioSource>();
        if (src == null) src = c.gameObject.AddComponent<AudioSource>(); // Unity null: ?? would not work here
        src.playOnAwake = false;
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = Mathf.Max(1f, a.distMin);
        src.maxDistance = Mathf.Max(15f, a.distMax);
        src.volume = a.volMax;
        var so = new SerializedObject(c);
        var clipProp = so.FindProperty(field);
        var srcProp = so.FindProperty("audioSource");
        if (clipProp == null) return 0;
        clipProp.objectReferenceValue = a.clips[0];
        if (srcProp != null) srcProp.objectReferenceValue = src;
        so.ApplyModifiedPropertiesWithoutUndo();
        return 1;
    }

    public static void DiagnoseBatch()
    {
        EditorSceneManager.OpenScene(ScenePath);
        foreach (var za in Object.FindObjectsByType<ZombieAudio>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var p = new SerializedObject(za).FindProperty("bank");
            Debug.Log($"[KinoAudioDiag] {za.gameObject.name} bank={(p.objectReferenceValue != null ? AssetDatabase.GetAssetPath(p.objectReferenceValue) : "NULL")} instanceId={p.objectReferenceInstanceIDValue}");
        }
        var bank = AssetDatabase.LoadAssetAtPath<SoundAliasBank>(BankPath);
        Debug.Log($"[KinoAudioDiag] bank asset loads: {(bank != null)} aliases={(bank != null ? bank.aliases.Count : -1)} type={AssetDatabase.GetMainAssetTypeAtPath(BankPath)}");
        EditorApplication.Exit(0);
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

// Mobile import settings: music streams, everything else decompresses on load (short effects).
public class KinoAudioPostprocessor : AssetPostprocessor
{
    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith("Assets/BYO_Kino/Sounds/")) return;
        var importer = (AudioImporter)assetImporter;
        var s = importer.defaultSampleSettings;
        bool music = assetPath.Contains("/mus/");
        s.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.CompressedInMemory;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.quality = 0.5f;
        importer.defaultSampleSettings = s;
    }
}
#endif
