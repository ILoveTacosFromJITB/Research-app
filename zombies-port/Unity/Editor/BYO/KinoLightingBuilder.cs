// KinoLightingBuilder.cs  (editor only)
//
// Zombies > BYO > Apply Kino Lighting: switches the map and props to Kino/WorldLit (baked light from the map's
// decoded secondary lightmaps, sun mask from its primary lightmap, per-prop sun visibility and light-grid colour
// from kino_layout.json, entity models lit from Lighting/lightgrid.bytes), places the map's compiled spot and point
// lights (ComWorld primary lights), sets sun / ambient from worldspawn, adds colour grading modelled on
// vision/zombie_theater.vision, and turns on Forward+ so the many local lights render.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ZombiesPort;
using Object = UnityEngine.Object;

public static class KinoLightingBuilder
{
    const string Root = "Assets/BYO_Kino";
    const string ScenePath = "Assets/Scenes/Kino.unity";
    const string MaterialDir = Root + "/Materials";

    [Serializable] class Env { public float[] sunColor; public float sunIntensity; public float[] sunDir; public float[] ambientColor; public float ambientIntensity; public float exposure; }
    [Serializable] class PLight { public int index; public string type; public float[] pos; public float[] dir; public float[] color; public float range; public float cosOuter; public float cosInner; }
    [Serializable] class Gameplay { public Env environment; public PLight[] primaryLights; }
    [Serializable] class Prop { public string file; public float[] pos; public float sun; public float[] light; }
    [Serializable] class Layout { public Prop[] props; }

    static V3 Vec(float[] a) => new V3(a[0], a[1], a[2]);
    struct V3 { public float x, y, z; public V3(float a, float b, float c) { x = a; y = b; z = c; } public Vector3 U => new Vector3(x, y, z); }

    static Texture2D[] lightmaps = new Texture2D[2];

    [MenuItem("Zombies/BYO/Apply Kino Lighting")]
    public static void Apply()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath);
        var shader = Shader.Find("Kino/WorldLit");
        if (shader == null) throw new Exception("Kino/WorldLit shader not found");
        var gp = JsonUtility.FromJson<Gameplay>(File.ReadAllText(Root + "/kino_gameplay.json"));
        var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(Root + "/kino_layout.json"));
        if (!AssetDatabase.IsValidFolder(MaterialDir)) AssetDatabase.CreateFolder(Root, "Materials");

        var masks = new Texture2D[2];
        lightmaps = new Texture2D[2];
        for (int i = 0; i < 2; i++)
        {
            string p = $"{Root}/Lighting/sunmask{i}.png";
            if (AssetImporter.GetAtPath(p) is TextureImporter ti && (ti.sRGBTexture || ti.mipmapEnabled == false))
            {
                ti.sRGBTexture = false; // a mask, not a colour
                ti.mipmapEnabled = true;
                ti.maxTextureSize = 2048;
                ti.SaveAndReimport();
            }
            masks[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }
        for (int i = 0; i < 2; i++)
        {
            string p = $"{Root}/Lighting/lightmap{i}.png";
            if (AssetImporter.GetAtPath(p) is TextureImporter ti &&
                (ti.sRGBTexture || ti.textureCompression != TextureImporterCompression.Uncompressed || !ti.mipmapEnabled))
            {
                ti.sRGBTexture = false; // RGBM: rgb * a * 8, linear
                ti.alphaIsTransparency = false;
                ti.mipmapEnabled = true;
                ti.textureCompression = TextureImporterCompression.Uncompressed; // block compression smears RGBM
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.SaveAndReimport();
            }
            lightmaps[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }

        var cache = new Dictionary<string, Material>();
        int converted = 0;

        // ---- map ----
        var world = GameObject.Find("KinoWorld");
        foreach (var r in world.GetComponentsInChildren<Renderer>(true))
        {
            r.sharedMaterials = r.sharedMaterials.Select(m => Convert(m, shader, masks, cache, ref converted)).ToArray();
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ---- props: per-instance sun visibility (floor under them) and baked colour (BO1 light grid) ----
        var grid = LightGridVolume.Load(Root + "/Lighting/lightgrid.bytes");
        int propsLit = 0, entsLit = 0;
        var propRoot = GameObject.Find("KinoProps");
        if (propRoot != null)
        {
            int k = 0;
            foreach (Transform t in propRoot.transform)
            {
                var prop = k < layout.props.Length ? layout.props[k] : null;
                k++;
                foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                {
                    r.sharedMaterials = r.sharedMaterials.Select(m => Convert(m, shader, null, cache, ref converted)).ToArray();
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }
                var pl = PropLight(t.gameObject);
                pl.sunVisibility = prop != null ? prop.sun : 1f;
                pl.bakedLight = prop != null && prop.light != null && prop.light.Length == 3
                    ? new Vector4(prop.light[0], prop.light[1], prop.light[2], 1f) : Vector4.zero;
                pl.Apply();
                if (pl.bakedLight.w > 0f) propsLit++;
            }
        }
        var ents = GameObject.Find("KinoEntityModels");
        if (ents != null)
            foreach (var r in ents.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterials = r.sharedMaterials.Select(m => Convert(m, shader, null, cache, ref converted)).ToArray();
                var pl = PropLight(r.gameObject);
                pl.sunVisibility = 0.15f; // gameplay props (perks, box, wall guns) all stand indoors on Kino
                pl.bakedLight = grid != null ? grid.Sample(r.bounds.center) : Vector4.zero;
                pl.Apply();
                if (grid != null) entsLit++;
            }
        // The Mystery Box visuals were reparented under the Mystery Box object.
        var box = GameObject.Find("Mystery Box");
        if (box != null)
            foreach (var r in box.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterials = r.sharedMaterials.Select(m => Convert(m, shader, null, cache, ref converted)).ToArray();
                var pl = PropLight(r.gameObject);
                pl.sunVisibility = 0.15f;
                pl.bakedLight = grid != null ? grid.Sample(r.bounds.center) : Vector4.zero;
                pl.Apply();
                if (grid != null) entsLit++;
            }
        Debug.Log($"[KinoLightingBuilder] light grid: {propsLit} props, {entsLit} entity renderers lit" + (grid == null ? " (no lightgrid.bytes)" : ""));
        AssetDatabase.SaveAssets();

        // ---- sun, ambient, local lights ----
        var env = gp.environment;
        var old = GameObject.Find("KinoLights");
        if (old != null) Object.DestroyImmediate(old);
        var lightsRoot = new GameObject("KinoLights");
        var kl = lightsRoot.AddComponent<KinoLighting>();
        var sunCol = new Color(env.sunColor[0], env.sunColor[1], env.sunColor[2]);
        var ambCol = new Color(env.ambientColor[0], env.ambientColor[1], env.ambientColor[2]);
        kl.Configure(sunCol, 1.6f, Vec(env.sunDir).U, ambCol, 0.45f * env.ambientIntensity);

        // Characters and the viewmodel use URP's own lighting: one soft directional "sun" and flat ambient.
        var sunGo = GameObject.Find("Sun");
        if (sunGo != null)
        {
            var sun = sunGo.GetComponent<Light>();
            sun.color = sunCol;
            sun.intensity = 0.6f;
            sun.transform.rotation = Quaternion.LookRotation(-Vec(env.sunDir).U);
        }
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambCol * 0.55f;

        int made = 0;
        foreach (var pl in gp.primaryLights ?? Array.Empty<PLight>())
        {
            var go = new GameObject($"Light {pl.index} ({pl.type})");
            go.transform.SetParent(lightsRoot.transform, false);
            go.transform.position = Vec(pl.pos).U;
            var l = go.AddComponent<Light>();
            // CoD allows negative "darkening" lights; Unity can't, and they tint things cyan. Clamp them out.
            for (int c = 0; c < 3; c++) pl.color[c] = Mathf.Max(0f, pl.color[c]);
            float peak = Mathf.Max(pl.color[0], Mathf.Max(pl.color[1], pl.color[2]));
            if (peak < 0.01f) { Object.DestroyImmediate(go); continue; }
            l.color = new Color(pl.color[0] / peak, pl.color[1] / peak, pl.color[2] / peak);
            l.intensity = Mathf.Clamp(peak / 6f, 0.2f, 5f);   // CoD primary light colours are HDR (intensity folded in)
            l.range = Mathf.Max(1f, pl.range * 1.2f);
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForcePixel;
            if (pl.type == "spot")
            {
                l.type = LightType.Spot;
                go.transform.rotation = Quaternion.LookRotation(Vec(pl.dir).U);
                l.spotAngle = Mathf.Clamp(2f * Mathf.Acos(Mathf.Clamp(pl.cosOuter, -1f, 1f)) * Mathf.Rad2Deg, 1f, 179f);
                l.innerSpotAngle = Mathf.Clamp(2f * Mathf.Acos(Mathf.Clamp(pl.cosInner, -1f, 1f)) * Mathf.Rad2Deg, 0f, l.spotAngle);
            }
            else l.type = LightType.Point;
            made++;
        }

        // ---- colour grading (vision/zombie_theater.vision: desaturated, slight warm mids, S-curve) ----
        var volumeGo = GameObject.Find("KinoGrading");
        if (volumeGo != null) Object.DestroyImmediate(volumeGo);
        volumeGo = new GameObject("KinoGrading");
        var volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        string profilePath = Root + "/KinoGrading.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
        }
        foreach (var c in profile.components.ToArray()) { profile.Remove(c.GetType()); Object.DestroyImmediate(c, true); }
        var tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.Neutral);
        var adj = profile.Add<ColorAdjustments>(true);
        adj.postExposure.Override(Mathf.Log(env.exposure > 0 ? env.exposure : 1f, 2f));
        adj.saturation.Override(-28f);                         // r_filmSaturation ~0.67-0.75
        adj.contrast.Override(12f);                            // r_sCurve
        adj.colorFilter.Override(new Color(1.0f, 0.97f, 0.93f)); // r_filmMidTint / colour temperature, slightly warm
        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(1.0f);
        bloom.intensity.Override(0.35f);
        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.28f);
        foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var data = cam.GetUniversalAdditionalCameraData();
            if (data.renderType == CameraRenderType.Base) data.renderPostProcessing = true;
        }

        // ---- Forward+ (dozens of local lights) ----
        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
        {
            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
            if (rd != null && rd.renderingMode != RenderingMode.ForwardPlus)
            {
                rd.renderingMode = RenderingMode.ForwardPlus;
                EditorUtility.SetDirty(rd);
            }
        }
        AssetDatabase.SaveAssets();

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log($"[KinoLightingBuilder] {converted} materials converted, {made} map lights placed, grading + Forward+ on");
    }

    // Per-object sun / baked light lives on a serialized component: property blocks alone are lost when the scene reloads.
    static KinoPropLight PropLight(GameObject go)
    {
        var pl = go.GetComponent<KinoPropLight>();
        return pl != null ? pl : go.AddComponent<KinoPropLight>();
    }

    // BO1's light grid as written by byo_convert_kino.py (LightGrid.write_volume): mean colour per sample,
    // float16 rgb[z][y][x] in lightmap units, on a lattice in CoD inches.
    class LightGridVolume
    {
        int nx, ny, nz;
        Vector3 origin, step;
        float[] rgb;

        public static LightGridVolume Load(string path)
        {
            if (!File.Exists(path)) return null;
            using var br = new BinaryReader(File.OpenRead(path));
            if (new string(br.ReadChars(4)) != "LGV1") return null;
            var v = new LightGridVolume { nx = br.ReadInt32(), ny = br.ReadInt32(), nz = br.ReadInt32() };
            v.origin = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            v.step = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            int n = v.nx * v.ny * v.nz * 3;
            v.rgb = new float[n];
            for (int i = 0; i < n; i++) v.rgb[i] = Mathf.HalfToFloat(br.ReadUInt16());
            return v;
        }

        Vector3 At(int x, int y, int z)
        {
            x = Mathf.Clamp(x, 0, nx - 1); y = Mathf.Clamp(y, 0, ny - 1); z = Mathf.Clamp(z, 0, nz - 1);
            int i = ((z * ny + y) * nx + x) * 3;
            return new Vector3(rgb[i], rgb[i + 1], rgb[i + 2]);
        }

        // Trilinear lookup at a Unity world position; w = 1 so Kino/WorldLit uses it.
        public Vector4 Sample(Vector3 unity)
        {
            var cod = new Vector3(-unity.x, -unity.z, unity.y) / 0.0254f; // inverse of (-x, z, -y) * INCH
            var f = new Vector3((cod.x - origin.x) / step.x, (cod.y - origin.y) / step.y, (cod.z - origin.z) / step.z);
            int x = Mathf.FloorToInt(f.x), y = Mathf.FloorToInt(f.y), z = Mathf.FloorToInt(f.z);
            float tx = f.x - x, ty = f.y - y, tz = f.z - z;
            Vector3 c00 = Vector3.Lerp(At(x, y, z), At(x + 1, y, z), tx), c10 = Vector3.Lerp(At(x, y + 1, z), At(x + 1, y + 1, z), tx);
            Vector3 c01 = Vector3.Lerp(At(x, y, z + 1), At(x + 1, y, z + 1), tx), c11 = Vector3.Lerp(At(x, y + 1, z + 1), At(x + 1, y + 1, z + 1), tx);
            var c = Vector3.Lerp(Vector3.Lerp(c00, c10, ty), Vector3.Lerp(c01, c11, ty), tz);
            return new Vector4(c.x, c.y, c.z, 1f);
        }
    }

    // Chalk outlines, glows and light shafts are additive in BO1 (black means "nothing").
    const string AdditivePattern = "chalk|scrawl|glow|_add|lightray|light_beam|godray|flare";

    static void SetBlend(Material mat, bool additive)
    {
        mat.SetFloat("_SrcBlend", (float)(additive ? BlendMode.One : BlendMode.SrcAlpha));
        mat.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        EditorUtility.SetDirty(mat);
    }

    // One Kino/WorldLit material per (source material, lightmap page), saved as an asset.
    static Material Convert(Material src, Shader shader, Texture2D[] masks, Dictionary<string, Material> cache, ref int converted)
    {
        if (src == null) return src;
        var m = Regex.Match(src.name, "#lm(\\d+|x)");
        int page = masks != null && m.Success && int.TryParse(m.Groups[1].Value, out var pg) ? pg : -1;
        if (src.shader == shader)
        {
            // Already converted on an earlier run: refresh its blend setup and baked lightmap in place.
            if (src.renderQueue >= 3000) SetBlend(src, Regex.IsMatch(src.name.ToLowerInvariant(), AdditivePattern));
            if (page >= 0 && page < lightmaps.Length && lightmaps[page] != null)
            {
                src.SetTexture("_Lightmap", lightmaps[page]);
                src.SetFloat("_UseLightmap", 1f);
                EditorUtility.SetDirty(src);
            }
            return src;
        }
        string key = src.GetInstanceID() + "_" + page;
        if (cache.TryGetValue(key, out var done)) return done;

        var mat = new Material(shader) { name = src.name };
        var tex = src.HasProperty("baseColorTexture") ? src.GetTexture("baseColorTexture") : src.mainTexture;
        mat.SetTexture("_BaseMap", tex);
        if (src.HasProperty("baseColorFactor")) mat.SetColor("_BaseColor", src.GetColor("baseColorFactor"));
        else if (src.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", src.GetColor("_BaseColor"));
        bool blend = src.renderQueue >= 3000;
        bool cutout = !blend && (src.IsKeywordEnabled("_ALPHATEST_ON") || src.renderQueue == 2450);
        if (cutout)
        {
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetFloat("_AlphaTest", 1f);
            mat.SetFloat("_Cutoff", src.HasProperty("alphaCutoff") ? src.GetFloat("alphaCutoff") : 0.5f);
            mat.renderQueue = 2450;
        }
        if (blend)
        {
            SetBlend(mat, Regex.IsMatch(src.name.ToLowerInvariant(), AdditivePattern));
            mat.SetFloat("_ZWrite", 0f);
            mat.renderQueue = 3000;
            mat.SetOverrideTag("RenderType", "Transparent");
        }
        mat.SetFloat("_Cull", 0f); // CoD materials are effectively double sided for our purposes
        if (page >= 0 && page < masks.Length && masks[page] != null)
        {
            mat.SetTexture("_SunMask", masks[page]);
            mat.SetFloat("_UseSunMask", 1f);
            if (lightmaps[page] != null)
            {
                mat.SetTexture("_Lightmap", lightmaps[page]);
                mat.SetFloat("_UseLightmap", 1f);
            }
        }
        else
        {
            mat.SetFloat("_UseSunMask", 0f);
            mat.SetFloat("_SunVis", masks != null ? 0.3f : 1f);
        }
        string path = AssetDatabase.GenerateUniqueAssetPath($"{MaterialDir}/{Regex.Replace(src.name, "[^A-Za-z0-9_#-]", "_")}.mat");
        AssetDatabase.CreateAsset(mat, path);
        cache[key] = mat;
        converted++;
        return mat;
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
