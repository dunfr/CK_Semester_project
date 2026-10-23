Shader "CK/Prototype/Fuchsia/Environment"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Color Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        _Metallic("Metallic", Range(0,1)) = 0
        _MetallicGlossMap("Metallic R / Smoothness A", 2D) = "white" {}
        _Smoothness("Smoothness", Range(0,1)) = 0.5
        _SmoothnessLimit("Maximum Smoothness", Range(0,1)) = 0.78
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Strength", Range(0,2)) = 1
        _OcclusionMap("Occlusion G", 2D) = "white" {}
        _OcclusionStrength("Occlusion Strength", Range(0,1)) = 1
        [HDR] _EmissionColor("Emission Color", Color) = (0,0,0,1)
        _EmissionMap("Emission Map", 2D) = "white" {}
        _EmissionStrength("Emission Strength", Range(0,2)) = 1

        [Header(Indirect Lighting)]
        _IndirectTint("Indirect Light Tint", Color) = (0.95,0.98,1,1)
        _IndirectStrength("Indirect Light Strength", Range(0,2)) = 1

        [Header(Coating)]
        _ClearCoatMask("Clear Coat Amount", Range(0,1)) = 0
        _ClearCoatSmoothness("Clear Coat Smoothness", Range(0,1)) = 0.7
        _ClearCoatMap("Clear Coat R / Smoothness G", 2D) = "white" {}

        [Header(Detail)]
        _DetailMask("Detail Mask A", 2D) = "white" {}
        _DetailAlbedoMap("Detail Albedo", 2D) = "linearGrey" {}
        _DetailAlbedoMapScale("Detail Albedo Scale", Range(0,2)) = 1
        [Normal] _DetailNormalMap("Detail Normal", 2D) = "bump" {}
        _DetailNormalMapScale("Detail Normal Scale", Range(0,2)) = 1
        _DetailStrength("Detail Influence", Range(0,1)) = 0.35

        [Header(Texture Features)]
        [Toggle(_NORMALMAP)] _UseNormalMap("Use Normal Map", Float) = 0
        [Toggle(_METALLICSPECGLOSSMAP)] _UseMetallicMap("Use Metallic Map", Float) = 0
        [Toggle(_OCCLUSIONMAP)] _UseOcclusionMap("Use Occlusion Map", Float) = 0
        [Toggle(_EMISSION)] _UseEmission("Use Emission", Float) = 0
        [Toggle(_CLEARCOAT)] _UseClearCoat("Use Clear Coat", Float) = 0
        [Toggle(_CLEARCOATMAP)] _UseClearCoatMap("Use Clear Coat Map", Float) = 0
        [Toggle(_DETAIL_SCALED)] _UseDetail("Use Detail Maps", Float) = 0
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clip", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2

        // Keep URP Lit property names so existing corridor texture bindings survive.
        [HideInInspector] _WorkflowMode("Workflow", Float) = 1
        [HideInInspector] _SpecColor("Specular Color", Color) = (0.2,0.2,0.2,1)
        [HideInInspector] _SpecGlossMap("Specular Map", 2D) = "white" {}
        [HideInInspector] _SmoothnessTextureChannel("Smoothness Channel", Float) = 0
        [HideInInspector] _Parallax("Parallax", Float) = 0.005
        [HideInInspector] _ParallaxMap("Height", 2D) = "black" {}
        [HideInInspector] _Surface("Surface", Float) = 0
        [HideInInspector] _ReceiveShadows("Receive Shadows", Float) = 1
        [HideInInspector] _SpecularHighlights("Specular Highlights", Float) = 1
        [HideInInspector] _EnvironmentReflections("Environment Reflections", Float) = 1
        [HideInInspector] _QueueOffset("Queue Offset", Float) = 0
        [HideInInspector] _AddPrecomputedVelocity("Add Precomputed Velocity", Float) = 0
        [HideInInspector][NoScaleOffset] unity_Lightmaps("Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_LightmapsInd("Directional Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_ShadowMasks("Shadow Masks", 2DArray) = "" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "Queue" = "Geometry"
        }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ParallaxMapping.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"

        #if defined(_DETAIL_MULX2) || defined(_DETAIL_SCALED)
            #define _DETAIL
        #endif

        // A single layout is shared by forward, shadow, depth, normals and bake passes.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseMap_TexelSize;
            float4 _DetailAlbedoMap_ST;
            half4 _BaseColor;
            half4 _SpecColor;
            half4 _EmissionColor;
            half4 _IndirectTint;
            half _Cutoff;
            half _Smoothness;
            half _Metallic;
            half _BumpScale;
            half _Parallax;
            half _OcclusionStrength;
            half _ClearCoatMask;
            half _ClearCoatSmoothness;
            half _DetailAlbedoMapScale;
            half _DetailNormalMapScale;
            half _Surface;
            half _SmoothnessLimit;
            half _IndirectStrength;
            half _DetailStrength;
            half _EmissionStrength;
            UNITY_TEXTURE_STREAMING_DEBUG_VARS;
        CBUFFER_END

        TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
        TEXTURE2D(_SpecGlossMap); SAMPLER(sampler_SpecGlossMap);
        TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
        TEXTURE2D(_ClearCoatMap); SAMPLER(sampler_ClearCoatMap);
        TEXTURE2D(_DetailMask); SAMPLER(sampler_DetailMask);
        TEXTURE2D(_DetailAlbedoMap); SAMPLER(sampler_DetailAlbedoMap);
        TEXTURE2D(_DetailNormalMap); SAMPLER(sampler_DetailNormalMap);
        TEXTURE2D(_ParallaxMap); SAMPLER(sampler_ParallaxMap);

        void ApplyPerPixelDisplacement(half3 viewDirTS, inout float2 uv)
        {
            #if defined(_PARALLAXMAP)
                uv += ParallaxMapping(TEXTURE2D_ARGS(_ParallaxMap, sampler_ParallaxMap), viewDirTS, _Parallax, uv);
            #endif
        }

        half3 ApplyDetailNormal(float2 detailUv, half3 normalTS, half detailMask)
        {
            #if defined(_DETAIL)
                half3 detailNormal = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailNormalMap, detailUv), _DetailNormalMapScale);
                half3 combinedNormal = BlendNormalRNM(normalTS, normalize(detailNormal));
                return normalize(lerp(normalTS, combinedNormal, saturate(detailMask * _DetailStrength)));
            #else
                return normalTS;
            #endif
        }

        // The packed map convention matches URP Lit/Complex Lit, including alpha smoothness.
        void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surfaceData)
        {
            surfaceData = (SurfaceData)0;
            half4 baseSample = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
            surfaceData.alpha = Alpha(baseSample.a, _BaseColor, _Cutoff);
            surfaceData.albedo = baseSample.rgb * _BaseColor.rgb;
            half4 metalSpecGloss = half4(_Metallic.xxx, _Smoothness);
            #if defined(_SPECULAR_SETUP)
                metalSpecGloss.rgb = _SpecColor.rgb;
            #endif
            #if defined(_METALLICSPECGLOSSMAP)
                #if defined(_SPECULAR_SETUP)
                    metalSpecGloss = SAMPLE_TEXTURE2D(_SpecGlossMap, sampler_SpecGlossMap, uv);
                #else
                    metalSpecGloss = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv);
                #endif
                metalSpecGloss.a *= _Smoothness;
            #endif
            #if defined(_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A)
                metalSpecGloss.a = baseSample.a * _Smoothness;
            #endif
            #if defined(_SPECULAR_SETUP)
                surfaceData.metallic = 1;
                surfaceData.specular = metalSpecGloss.rgb;
            #else
                surfaceData.metallic = saturate(metalSpecGloss.r);
            #endif

            // A roughness floor keeps coated wall panels distinct from polished floor metal.
            surfaceData.smoothness = min(saturate(metalSpecGloss.a), _SmoothnessLimit);
            surfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
            surfaceData.occlusion = 1;
            #if defined(_OCCLUSIONMAP)
                half occlusion = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).g;
                surfaceData.occlusion = lerp(1, occlusion, _OcclusionStrength);
            #endif
            surfaceData.emission = SampleEmission(uv, _EmissionColor.rgb,
                TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap)) * _EmissionStrength;

            #if defined(_CLEARCOAT) || defined(_CLEARCOATMAP)
                half2 coat = half2(_ClearCoatMask, _ClearCoatSmoothness);
                #if defined(_CLEARCOATMAP)
                    coat *= SAMPLE_TEXTURE2D(_ClearCoatMap, sampler_ClearCoatMap, uv).rg;
                #endif
                surfaceData.clearCoatMask = saturate(coat.r);
                surfaceData.clearCoatSmoothness = min(saturate(coat.g), _SmoothnessLimit);
            #endif

            #if defined(_DETAIL)
                half mask = SAMPLE_TEXTURE2D(_DetailMask, sampler_DetailMask, uv).a;
                float2 detailUv = uv * _DetailAlbedoMap_ST.xy + _DetailAlbedoMap_ST.zw;
                half3 detailAlbedo = SAMPLE_TEXTURE2D(_DetailAlbedoMap, sampler_DetailAlbedoMap, detailUv).rgb;
                #if defined(_DETAIL_SCALED)
                    detailAlbedo = 1 + (detailAlbedo * 2 - 1) * _DetailAlbedoMapScale;
                #else
                    detailAlbedo *= 2;
                #endif
                surfaceData.albedo *= lerp(1, detailAlbedo, saturate(mask * _DetailStrength));
                surfaceData.normalTS = ApplyDetailNormal(detailUv, surfaceData.normalTS, mask);
            #endif
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex LitPassVertex
            #pragma fragment EnvironmentFragment
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _ _CLEARCOAT _CLEARCOATMAP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

            void EnvironmentFragment(Varyings input, out half4 outColor : SV_Target0
                #ifdef _WRITE_RENDERING_LAYERS
                    , out uint outRenderingLayers : SV_Target1
                #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                #if defined(_PARALLAXMAP)
                    ApplyPerPixelDisplacement(input.viewDirTS, input.uv);
                #endif
                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(input.positionCS);
                #endif
                SurfaceData surfaceData;
                InitializeStandardLitSurfaceData(input.uv, surfaceData);
                InputData inputData;
                InitializeInputData(input, surfaceData.normalTS, inputData);
                SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));
                #if defined(_DBUFFER)
                    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
                #endif
                InitializeBakedGIData(input, inputData);
                // Tint actual GI rather than adding an unlit color over the shadowed surface.
                inputData.bakedGI *= _IndirectTint.rgb * _IndirectStrength;
                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                outColor = half4(MixFog(color.rgb, inputData.fogCoord), surfaceData.alpha);
                #ifdef _WRITE_RENDERING_LAYERS
                    outRenderingLayers = EncodeMeshRenderingLayer();
                #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormalsOnly" }
            ZWrite On
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }
            Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaLit
            #pragma shader_feature EDITOR_VISUALIZATION
            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }
            ColorMask RG
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 4.5
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_vertex _ADD_PRECOMPUTED_VELOCITY
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ObjectMotionVectors.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
