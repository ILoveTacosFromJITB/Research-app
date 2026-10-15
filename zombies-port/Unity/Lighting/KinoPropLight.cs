// KinoPropLight.cs
//
// Per-object lighting inputs for Kino/WorldLit on props and entity models: sun visibility and the baked colour
// from BO1's light grid (set by KinoLightingBuilder). Material property blocks are not saved with a scene, so this
// keeps the values serialized and re-applies them to its renderers whenever it is enabled, in edit mode too.

using UnityEngine;

namespace ZombiesPort
{
    [ExecuteAlways, DisallowMultipleComponent]
    public class KinoPropLight : MonoBehaviour
    {
        [Range(0, 1)] public float sunVisibility = 1f;
        [Tooltip("Baked light, linear rgb in lightmap units; w = 1 to use it (else the worldspawn ambient).")]
        public Vector4 bakedLight;

        static readonly int SunVis = Shader.PropertyToID("_SunVis");
        static readonly int PropLight = Shader.PropertyToID("_PropLight");
        static MaterialPropertyBlock block;

        void OnEnable() => Apply();
        void OnValidate() => Apply();

        public void Apply()
        {
            block ??= new MaterialPropertyBlock();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetComponentInParent<KinoPropLight>(true) != this) continue; // a nested one owns it
                r.GetPropertyBlock(block);
                block.SetFloat(SunVis, sunVisibility);
                block.SetVector(PropLight, bakedLight);
                r.SetPropertyBlock(block);
            }
        }
    }
}
