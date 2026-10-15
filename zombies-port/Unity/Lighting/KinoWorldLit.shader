// KinoWorldLit.shader
//
// Map and prop shading modelled on BO1's world shader:
//   colour = albedo * (baked + sun * sunVisibility * N.L + local lights)
// baked is the map's own secondary lightmap (decoded by byo_convert_kino.py into an RGBM page, range 8,
// sampled with the lightmap UVs in TEXCOORD1) for world surfaces. Props and entity models get a per-object
// colour (_PropLight, w = 1) from BO1's light grid, the data the game lights models with; anything without one
// falls back to the flat worldspawn ambient.
// In BO1 the sun and the placed spot lights are "primary lights": not in the secondary lightmap, added live.
// sunVisibility comes from the map's own primary lightmap (green channel, sampled with the lightmap
// UVs in TEXCOORD1) for world surfaces, or a per-object value for props (_SunVis). That keeps sunlight
// off interiors even where the map has no roof geometry. Local lights are URP additional lights
// (Kino's placed spot lights), Forward and Forward+.

Shader "Kino/WorldLit"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _SunMask ("Sun Mask (primary lightmap)", 2D) = "white" {}
        _UseSunMask ("Use Sun Mask", Float) = 1
        _SunVis ("Sun Visibility (props)", Range(0,1)) = 1
        _Lightmap ("Baked Lightmap (RGBM)", 2D) = "black" {}
        _PropLight ("Baked Light (props: linear rgb, w=1 to use)", Vector) = (0,0,0,0)
        _UseLightmap ("Use Baked Lightmap", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        [Toggle(_ALPHATEST_ON)] _AlphaTest ("Alpha Test", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(Off,0,On,1)] _ZWrite ("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma shader_feature_local _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_SunMask); SAMPLER(sampler_SunMask);
            TEXTURE2D(_Lightmap); SAMPLER(sampler_Lightmap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _UseSunMask;
                half _SunVis;
                half _UseLightmap;
                half _Cutoff;
                half4 _PropLight;
            CBUFFER_END

            // Set once per scene by KinoLighting (worldspawn values).
            half3 _KinoAmbient;
            half3 _KinoSunColor;
            float3 _KinoSunDir;      // towards the sun
            half _KinoLocalScale;
            half _KinoBakedScale;    // exposure for the baked lightmap (1 = the game's own values)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                half3 normalWS : TEXCOORD3;
                half fog : TEXCOORD4;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.uv2 = v.uv2;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                #if defined(_ALPHATEST_ON)
                    clip(albedo.a - _Cutoff);
                #endif
                half3 n = normalize(i.normalWS);

                half sunVis = _UseSunMask > 0.5 ? SAMPLE_TEXTURE2D(_SunMask, sampler_SunMask, i.uv2).g : _SunVis;
                half3 baked = _PropLight.w > 0.5 ? _PropLight.rgb * _KinoBakedScale : _KinoAmbient;
                if (_UseLightmap > 0.5)
                {
                    half4 rgbm = SAMPLE_TEXTURE2D(_Lightmap, sampler_Lightmap, i.uv2);
                    baked = rgbm.rgb * (rgbm.a * 8.0 * _KinoBakedScale);
                }
                half3 light = baked + _KinoSunColor * sunVis * saturate(dot(n, normalize(_KinoSunDir)));

                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = i.positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                    uint count = GetAdditionalLightsCount();
                    #if USE_FORWARD_PLUS
                        for (uint li = 0; li < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); li++)
                        {
                            Light dl = GetAdditionalLight(li, i.positionWS, half4(1,1,1,1));
                            light += dl.color * dl.distanceAttenuation * saturate(dot(n, dl.direction)) * _KinoLocalScale;
                        }
                    #endif
                    LIGHT_LOOP_BEGIN(count)
                        Light l = GetAdditionalLight(lightIndex, i.positionWS, half4(1,1,1,1));
                        light += l.color * l.distanceAttenuation * saturate(dot(n, l.direction)) * _KinoLocalScale;
                    LIGHT_LOOP_END
                #endif

                half3 color = albedo.rgb * light;
                color = MixFog(color, i.fog);
                return half4(color, albedo.a);
            }
            ENDHLSL
        }

        // Depth only, so post-processing and the depth texture see the world.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ALPHATEST_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; half4 _BaseColor; half _UseSunMask; half _SunVis; half _UseLightmap; half _Cutoff; half4 _PropLight;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A v) { V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = TRANSFORM_TEX(v.uv, _BaseMap); return o; }
            half frag(V i) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
