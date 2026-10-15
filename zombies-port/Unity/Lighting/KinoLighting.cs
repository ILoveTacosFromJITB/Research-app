// KinoLighting.cs
//
// Scene-wide inputs for the Kino/WorldLit shader: the map's sun (colour, direction) and ambient light,
// from its worldspawn, plus how strongly the placed lights and the baked lightmap contribute. Runs in edit mode too, so the
// Scene view matches the game.

using UnityEngine;

namespace ZombiesPort
{
    [ExecuteAlways]
    public class KinoLighting : MonoBehaviour
    {
        [SerializeField] Color sunColor = new Color(0.82f, 0.76f, 0.62f);
        [SerializeField] float sunIntensity = 1.6f;
        [Tooltip("Direction towards the sun, world space.")]
        [SerializeField] Vector3 sunDirection = new Vector3(-0.18f, 0.75f, 0.63f);
        [SerializeField] Color ambientColor = new Color(0.596f, 0.608f, 0.62f);
        [SerializeField] float ambientIntensity = 0.45f;
        [SerializeField] float localLightScale = 1f;
        [Tooltip("Exposure of the map's baked lightmap (1 = BO1's own values).")]
        [SerializeField] float bakedScale = 1f;

        static readonly int Ambient = Shader.PropertyToID("_KinoAmbient");
        static readonly int SunColor = Shader.PropertyToID("_KinoSunColor");
        static readonly int SunDir = Shader.PropertyToID("_KinoSunDir");
        static readonly int LocalScale = Shader.PropertyToID("_KinoLocalScale");
        static readonly int BakedScale = Shader.PropertyToID("_KinoBakedScale");

        void OnEnable() => Apply();
        void OnValidate() => Apply();

        public void Apply()
        {
            Shader.SetGlobalColor(Ambient, ambientColor.linear * ambientIntensity);
            Shader.SetGlobalColor(SunColor, sunColor.linear * sunIntensity);
            Shader.SetGlobalVector(SunDir, sunDirection.normalized);
            Shader.SetGlobalFloat(LocalScale, localLightScale);
            Shader.SetGlobalFloat(BakedScale, bakedScale);
            // Characters and the viewmodel use Unity's ambient: keep it in step and refresh the ambient probe,
            // or it keeps whatever was baked last (that showed up as cyan hands).
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor * (ambientIntensity * 1.2f);
            DynamicGI.UpdateEnvironment();
        }

        public void Configure(Color sun, float sunStrength, Vector3 towardsSun, Color ambient, float ambientStrength)
        {
            sunColor = sun;
            sunIntensity = sunStrength;
            sunDirection = towardsSun;
            ambientColor = ambient;
            ambientIntensity = ambientStrength;
            Apply();
        }
    }
}
