// KinoViewmodelTest.cs  (editor only)
//
// Headless check of the first-person weapon: enters Play mode in the Kino scene, then idles, fires,
// aims down the sights and empties the magazine into a reload through MobileInput's scripted inputs,
// logging bone positions in camera space and saving screenshots (world + viewmodel camera stack).
//
//   Unity.exe -batchmode -projectPath <proj> -executeMethod KinoViewmodelTest.Run -logFile <log>   (no -quit)

#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using ZombiesPort;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class KinoViewmodelTest
{
    const string Key = "KinoViewmodelTest.active";
    static float _start = -1f;
    static int _step;

    static KinoViewmodelTest()
    {
        if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick;
    }

    static bool _hooked;
    static int _fireFrame;

    // One frame of the shot: the player's view and a side close-up of the gun, plus where the slide bones are.
    static void FireFrame(int i)
    {
        Shot($"vm_fireframe{i}");
        var rig = Object.FindFirstObjectByType<ViewModelRig>();
        var cam = Camera.main.transform;
        string P(string n) { var tr = rig.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == n); return tr == null ? "none" : cam.InverseTransformPoint(tr.position).ToString("F3") + (tr.lossyScale.x < 1e-4f ? " SCALE0" : ""); }
        Log($"fireframe {i}: j_gun {P("j_gun")} j_bolt {P("j_bolt")} tag_iron_sights {P("tag_iron_sights")} tag_upgraded_sights {P("tag_upgraded_sights")} tag_flash {P("tag_flash")}");
    }
    static float _reloadAt = -1f;
    static readonly System.Collections.Generic.Dictionary<string, int> _sounds = new System.Collections.Generic.Dictionary<string, int>();
    static bool _reloadShot;

    static void Hook()
    {
        if (_hooked) return;
        var w = Object.FindFirstObjectByType<PlayerWeapon>();
        if (w == null) return;
        _hooked = true;
        w.ReloadStarted += s => { Log($"ReloadStarted clip={s.Clip} reloading={s.IsReloading} t={Time.time:F2}"); if (_reloadAt < 0f) _reloadAt = Time.realtimeSinceStartup; };
        w.Fired += s => Log($"Fired clip={s.Clip} t={Time.time:F2}");
        var za = Object.FindFirstObjectByType<ZombieAudio>();
        Log($"audio: instance={(ZombieAudio.Instance != null)} found={(za != null)} bank={(za != null && za.Bank != null ? za.Bank.aliases.Count : -1)} director={(Object.FindFirstObjectByType<GameAudioDirector>()?.enabled)} audioDisabled={AudioSettings.GetConfiguration().dspBufferSize}");
        if (ZombieAudio.Instance != null)
            ZombieAudio.Instance.Played += (alias, clip) => { _sounds[alias] = _sounds.TryGetValue(alias, out var n) ? n + 1 : 1; if (clip == null || clip.loadState == AudioDataLoadState.Failed) Log("BAD CLIP " + alias); };
    }

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Kino.unity");
        SessionState.SetBool(Key, true);
        EditorApplication.update += Tick;
        Application.runInBackground = true; // an unfocused editor otherwise never advances Play mode
        EditorApplication.EnterPlaymode();
    }

    static void Log(string m) => Debug.Log("[VMTest] " + m);

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (_start < 0f) _start = Time.realtimeSinceStartup;
        float t = Time.realtimeSinceStartup - _start;
        Hook();
        var input = Object.FindFirstObjectByType<MobileInput>();
        var weapon = Object.FindFirstObjectByType<PlayerWeapon>();
        try
        {
            if (_step == 0 && t > 4f) { _step++; Bones("idle"); Shot("vm_idle"); HitMarkerState(); GunCloseups("idle"); }
            if (_step == 1 && t > 5f) { _step++; input.ScriptedFire = true; }
            if (_step == 2 && _fireFrame < 8) { FireFrame(_fireFrame++); return; }
            if (_step == 2 && t > 5.06f) { _step++; Shot("vm_fire"); Log($"after fire: clip {weapon.Current.Clip}"); input.ScriptedFire = false; }
            if (_step == 3 && t > 6f) { _step++; input.ScriptedAim = true; }
            if (_step == 4 && t > 7f) { _step++; Log($"aim amount {weapon.AimAmount:F2} fov {Camera.main.fieldOfView:F1}"); Bones("ads"); Shot("vm_ads"); GunCloseups("ads"); }
            if (_step == 5 && t > 7.5f) { _step++; input.ScriptedAim = false; input.ScriptedFire = true; }
            if (_step == 6 && t > 10f) { _step++; input.ScriptedFire = false; Log($"reloading={weapon.Current.IsReloading} clip={weapon.Current.Clip} reserve={weapon.Current.Reserve}"); }
            if (!_reloadShot && _reloadAt > 0f && Time.realtimeSinceStartup > _reloadAt + 0.6f) { _reloadShot = true; Bones("reload"); Shot("vm_reload"); }
            if (_step == 7 && t > 10.3f) { _step++; }
            if (_step == 8 && t > 13f) { _step++; Log($"after reload: clip={weapon.Current.Clip} reserve={weapon.Current.Reserve}"); Log("sounds played: " + string.Join(", ", _sounds.Select(kv => $"{kv.Key} x{kv.Value}"))); Finish(0); }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            Finish(1);
        }
    }

    static void Finish(int code)
    {
        SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick;
        EditorApplication.ExitPlaymode();
        if (Application.isBatchMode) EditorApplication.Exit(code); // leave an interactive editor open
    }

    static void Bones(string label)
    {
        var rig = Object.FindFirstObjectByType<ViewModelRig>();
        var cam = Camera.main.transform;
        string Pos(string n)
        {
            var tr = rig.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == n);
            return tr == null ? "none" : cam.InverseTransformPoint(tr.position).ToString("F2");
        }
        var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var one = rig.GetType().GetField("_oneShot", bf).GetValue(rig) as ViewModelClip;
        Log($"{label}: oneShot={one?.Name} key={rig.GetType().GetField("_oneShotKey", bf).GetValue(rig)} t={rig.GetType().GetField("_oneShotTime", bf).GetValue(rig)} " +
            $"speed={rig.GetType().GetField("_oneShotSpeed", bf).GetValue(rig)} len={one?.Length} saw={rig.GetType().GetField("_sawReloading", bf).GetValue(rig)}");
        var renderers = rig.GetComponentsInChildren<Renderer>(true);
        Log($"{label}: cam-space tag_view {Pos("tag_view")} tag_weapon {Pos("tag_weapon")} j_gun {Pos("j_gun")} tag_flash {Pos("tag_flash")} " +
            $"j_wrist_ri {Pos("j_wrist_ri")} j_wrist_le {Pos("j_wrist_le")} renderers {renderers.Length} enabled {renderers.Count(r => r.enabled && r.gameObject.activeInHierarchy)} " +
            $"layer {LayerMask.LayerToName(rig.gameObject.layer)}/{(renderers.Length > 0 ? LayerMask.LayerToName(renderers[0].gameObject.layer) : "-")}");
    }

    static void HitMarkerState()
    {
        var hm = Object.FindFirstObjectByType<HitMarker>();
        var img = hm != null ? hm.GetComponent<Image>() : null;
        Log($"hitmarker: sprite {(img?.sprite != null ? img.sprite.name : "NULL")} alpha {img?.color.a:F2}");
    }

    // Viewmodel-only renders of the gun: from the right side, from above, and down the sights (narrow FOV).
    static void GunCloseups(string label)
    {
        var rig = Object.FindFirstObjectByType<ViewModelRig>();
        var gun = rig.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "j_gun");
        if (gun == null) { Log("no j_gun"); return; }
        var main = Camera.main;
        var go = new GameObject("CloseupCam");
        var cam = go.AddComponent<Camera>();
        cam.cullingMask = 1 << LayerMask.NameToLayer("Viewmodel");
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.2f, 0.25f, 0.3f);
        cam.nearClipPlane = 0.005f;
        var centre = gun.position + main.transform.forward * 0.08f;
        void Take(string n, Vector3 from, float fov)
        {
            cam.fieldOfView = fov;
            go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(centre - from, main.transform.up));
            Render(cam, $"vm_{label}_{n}");
        }
        Take("side", centre + main.transform.right * 0.35f, 30f);
        Take("top", centre + main.transform.up * 0.35f + main.transform.forward * -0.05f, 30f);
        Take("sights", main.transform.position, 12f);
        // The player's exact view (camera axis), narrow FOV, with and without the weapon-file hideTags.
        cam.fieldOfView = 20f;
        go.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
        Render(cam, $"vm_{label}_eye");
        var hidden = (System.Collections.Generic.List<Transform>)rig.GetType().GetField("_hidden", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(rig);
        foreach (var h in hidden) h.localScale = Vector3.one;
        Render(cam, $"vm_{label}_eye_unhidden");
        Log($"{label} hidden tags: {string.Join(",", hidden.Select(h => h.name))}");
        Object.Destroy(go);
    }

    static void Render(Camera cam, string name)
    {
        var rt = new RenderTexture(1024, 768, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        var dir = Path.GetFullPath("BYO_Previews");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.Destroy(rt);
        Object.Destroy(tex);
    }

    static void Shot(string name)
    {
        var cam = Camera.main;
        var rt = new RenderTexture(1280, 720, 24);
        var prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render(); // URP renders the base camera's overlay stack into the same target
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        var dir = Path.GetFullPath("BYO_Previews");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = prev;
        Object.Destroy(rt);
        Object.Destroy(tex);
        Log("screenshot " + name);
    }
}
#endif
