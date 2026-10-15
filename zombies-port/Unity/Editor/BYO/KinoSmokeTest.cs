// KinoSmokeTest.cs  (editor only)
//
// Headless Play-mode check of the Kino scene: enters Play mode, watches the first rounds, buys a door,
// a wall gun and a perk through the same code paths the HUD uses, takes screenshots from the player camera
// and logs what happened, then quits. Survives the domain reload on entering Play mode via SessionState.
//
//   Unity.exe -batchmode -projectPath <proj> -executeMethod KinoSmokeTest.Run -logFile <log>
// (no -quit: the test calls EditorApplication.Exit itself)

#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombiesPort;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class KinoSmokeTest
{
    const string Key = "KinoSmokeTest.active";
    const float Duration = 75f;
    static float _start = -1f;
    static int _step;

    static KinoSmokeTest()
    {
        if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick;
    }

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Kino.unity");
        SessionState.SetBool(Key, true);
        EditorApplication.update += Tick;
        Application.runInBackground = true; // an unfocused editor otherwise never advances Play mode
        EditorApplication.EnterPlaymode();
    }

    static void Log(string msg) => Debug.Log("[KinoSmoke] " + msg);

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (_start < 0f) { _start = Time.realtimeSinceStartup; Log("play mode started"); }
        float t = Time.realtimeSinceStartup - _start;

        try
        {
            if (_step == 0 && t > 2f) { _step++; Report("t=2s"); Shot("play_spawn"); }
            if (_step == 1 && t > 8f) { _step++; Report("t=8s"); GivePoints(20000); TryBuyNearest<BuyableDoor>(d => !Describe(d).Contains("Kind=Electric") && !d.name.Contains("theater_to_foyer")); }
            if (_step == 2 && t > 14f) { _step++; Report("t=14s"); TryBuyNearest<WallBuy>(); TryBuyNearest<PerkMachine>(); TryBuyNearest<PowerSwitch>(); }
            if (_step == 3 && t > 20f) { _step++; Report("t=20s (power on)"); TryBuyNearest<PerkMachine>(m => Describe(m).Contains("Juggernog")); }
            if (_step == 4 && t > 30f) { _step++; Report("t=30s"); TryBuyNearest<MysteryBox>(b => b.name.Contains("start_chest")); ShotNearestZombie("play_zombie"); Shot("play_spawn_30s"); }
            if (_step == 5 && t > Duration) { _step++; Report("end"); Finish(0); }
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

    static void Report(string when)
    {
        var player = Object.FindFirstObjectByType<ZombiePlayer>();
        var zombies = Object.FindObjectsByType<ZombieAgent>(FindObjectsSortMode.None).Where(z => z.isActiveAndEnabled).ToArray();
        var agents = zombies.Select(z => z.GetComponent<UnityEngine.AI.NavMeshAgent>()).Where(a => a != null).ToArray();
        string round = Describe(Object.FindFirstObjectByType<RoundDirector>());
        string points = player != null && player.Score != null ? Describe(player.Score) : "?";
        float nearest = zombies.Length > 0 && player != null ? zombies.Min(z => Vector3.Distance(z.transform.position, player.transform.position)) : -1f;
        Log($"{when}: player at {player?.transform.position} grounded={player?.GetComponent<CharacterController>()?.isGrounded} " +
            $"round[{round}] points[{points}] zombies active={zombies.Length} onNavMesh={agents.Count(a => a.isOnNavMesh)} " +
            $"moving={agents.Count(a => a.velocity.sqrMagnitude > 0.05f)} nearest={nearest:F1}m " +
            $"openDoors={Object.FindObjectsByType<BuyableDoor>(FindObjectsSortMode.None).Count(d => Describe(d).Contains("IsOpen=True"))}");
    }

    // Public properties of a component, for the log (avoids depending on exact API names).
    static string Describe(object o)
    {
        if (o == null) return "null";
        var props = o.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 && (p.PropertyType.IsPrimitive || p.PropertyType == typeof(string) || p.PropertyType.IsEnum))
            .Where(p => p.DeclaringType != typeof(Component) && p.DeclaringType != typeof(Behaviour) && p.DeclaringType != typeof(MonoBehaviour) && p.DeclaringType != typeof(Object))
            .Select(p => { try { return $"{p.Name}={p.GetValue(o)}"; } catch { return null; } })
            .Where(s => s != null);
        return string.Join(" ", props);
    }

    static void GivePoints(int amount)
    {
        var player = Object.FindFirstObjectByType<ZombiePlayer>();
        var score = player?.Score;
        if (score == null) { Log("no score object"); return; }
        var m = score.GetType().GetMethods().FirstOrDefault(x => (x.Name.StartsWith("Add") || x.Name.StartsWith("Give")) &&
                                                               x.GetParameters().Length >= 1 && x.GetParameters()[0].ParameterType == typeof(int));
        if (m == null) { Log("no AddPoints-like method on " + score.GetType().Name); return; }
        var args = m.GetParameters().Select((p, i) => i == 0 ? (object)amount : (p.HasDefaultValue ? p.DefaultValue : null)).ToArray();
        m.Invoke(score, args);
        Log($"gave {amount} points via {score.GetType().Name}.{m.Name}: {Describe(score)}");
    }

    // Teleport next to the nearest T and interact with it the way the interact button does.
    static void TryBuyNearest<T>(Func<T, bool> filter = null) where T : Component
    {
        var player = Object.FindFirstObjectByType<ZombiePlayer>();
        var target = Object.FindObjectsByType<T>(FindObjectsSortMode.None).Where(x => filter == null || filter(x))
            .OrderBy(x => Vector3.Distance(x.transform.position, player.transform.position)).FirstOrDefault();
        if (target == null) { Log("no " + typeof(T).Name); return; }
        var interactable = target as IInteractable;
        var methods = typeof(IInteractable).GetMethods().Select(m => m.Name);
        Log($"{typeof(T).Name} '{target.name}' at {target.transform.position:F1}, {Vector3.Distance(target.transform.position, player.transform.position):F1} m away; IInteractable members: {string.Join(",", methods)}");
        var use = typeof(IInteractable).GetMethods().FirstOrDefault(m => m.Name == "Interact");
        if (interactable == null || use == null) return;
        var args = use.GetParameters().Select(p => p.ParameterType.IsInstanceOfType(player) ? player
            : p.ParameterType == typeof(GameObject) ? (object)player.gameObject
            : p.ParameterType.IsAssignableFrom(typeof(PlayerInteractor)) ? player.GetComponent<PlayerInteractor>()
            : p.HasDefaultValue ? p.DefaultValue : null).ToArray();
        var result = use.Invoke(interactable, args);
        Log($"  {use.Name} -> {result}; after: {Describe(target)} points[{Describe(player.Score)}]");
    }

    static void Shot(string name)
    {
        var cam = Camera.main ?? Object.FindFirstObjectByType<Camera>();
        if (cam == null) { Log("no camera"); return; }
        Capture(cam, name);
    }

    static void ShotNearestZombie(string name)
    {
        var player = Object.FindFirstObjectByType<ZombiePlayer>();
        var z = Object.FindObjectsByType<ZombieAgent>(FindObjectsSortMode.None).Where(a => a.isActiveAndEnabled)
            .OrderBy(a => Vector3.Distance(a.transform.position, player.transform.position)).FirstOrDefault();
        if (z == null) { Log("no zombie to photograph"); return; }
        var go = new GameObject("SmokeCam");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 70f;
        var from = z.transform.position + (player.transform.position - z.transform.position).normalized * 3f + Vector3.up * 1.8f;
        go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(z.transform.position + Vector3.up - from));
        Capture(cam, name);
        Object.Destroy(go);
    }

    static void Capture(Camera cam, string name)
    {
        var rt = new RenderTexture(1280, 720, 24);
        var prev = cam.targetTexture;
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
        cam.targetTexture = prev;
        Object.Destroy(rt);
        Object.Destroy(tex);
        Log("screenshot " + name);
    }
}
#endif
