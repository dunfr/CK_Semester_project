Shader "Semester/Character Hybrid"
{
    Properties
    {
        _BaseMap("Color", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1,1,1,1)
        _ShadowColor("Toon shadow tint", Color) = (.35,.32,.4,1)
        _ShadowStep("Shadow threshold", Range(-1,1)) = .15
        _ShadowSoftness("Shadow softness", Range(.01,.5)) = .15
        _Metallic("Metallic", Range(0,1)) = 0
        _Smoothness("Smoothness", Range(0,1)) = .5
        _SpecularStrength("Direct reflection", Range(0,3)) = 1
        _ReflectionStrength("Environment reflection", Range(0,2)) = .5
        _Hair("Hair lobe blend", Range(0,1)) = 0
        _HairShift("Hair lobe shift", Range(-1,1)) = .12
        _HairPower("Hair lobe sharpness", Range(4,128)) = 48
        _HairStrength("Hair highlight", Range(0,3)) = .5
        _HairStrandDetail("UV strand detail", Range(0,1)) = 0
        _SurfaceMap("Surface mask: smoothness / highlight / occlusion", 2D) = "white" {}
        _SurfaceStrength("Surface mask strength", Range(0,1)) = 0
        _BumpMap("Surface normal", 2D) = "bump" {}
        _BumpStrength("Surface normal strength", Range(0,1)) = 0
        _RimStrength("Light-aware rim", Range(0,2)) = .3
        _EmissionMap("Emission mask", 2D) = "black" {}
        [HDR] _EmissionColor("Emission", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
        TEXTURE2D(_SurfaceMap); SAMPLER(sampler_SurfaceMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor, _ShadowColor, _EmissionColor;
            half _ShadowStep, _ShadowSoftness, _Metallic, _Smoothness;
            half _SpecularStrength, _ReflectionStrength, _Hair, _HairShift;
            half _HairPower, _HairStrength, _RimStrength;
            half _SurfaceStrength, _BumpStrength;
            half _HairStrandDetail;
        CBUFFER_END
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float2 uv:TEXCOORD0; };
        struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; half4 tangentWS:TEXCOORD2; float2 uv:TEXCOORD3; };
        Varyings Vert(Attributes v)
        {
            Varyings o;
            o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
            o.positionCS = TransformWorldToHClip(o.positionWS);
            o.normalWS = TransformObjectToWorldNormal(v.normalOS);
            o.tangentWS = half4(TransformObjectToWorldDir(v.tangentOS.xyz),v.tangentOS.w*GetOddNegativeScale());
            o.uv = TRANSFORM_TEX(v.uv,_BaseMap);
            return o;
        }
        ENDHLSL
        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode"="MotionVectors" }
            ColorMask RG
            HLSLPROGRAM
            #pragma vertex MotionVert
            #pragma fragment MotionFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MotionVectorsCommon.hlsl"
            struct MotionInput { float4 positionOS:POSITION; float3 previousOS:TEXCOORD4; };
            struct MotionOutput { float4 positionCS:SV_POSITION; float4 currentCS:TEXCOORD0; float4 previousCS:TEXCOORD1; };
            MotionOutput MotionVert(MotionInput i)
            {
                MotionOutput o;
                o.positionCS=TransformObjectToHClip(i.positionOS.xyz);
                o.currentCS=mul(_NonJitteredViewProjMatrix,mul(UNITY_MATRIX_M,i.positionOS));
                float4 previous=unity_MotionVectorsParams.x==1 ? float4(i.previousOS,1) : i.positionOS;
                o.previousCS=mul(_PrevViewProjMatrix,mul(UNITY_PREV_MATRIX_M,previous));
                return o;
            }
            float4 MotionFrag(MotionOutput i):SV_Target
            {
                return float4(CalcNdcMotionVectorFromCsPositions(i.currentCS,i.previousCS),0,0);
            }
            ENDHLSL
        }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            half3 Shade(Light light, BRDFData brdf, half3 baseColor, half3 n, half3 v, half3 strand, half highlightMask, bool main)
            {
                half nl = dot(n,light.direction);
                half ramp = smoothstep(_ShadowStep-_ShadowSoftness,_ShadowStep+_ShadowSoftness,nl);
                half attenuation = light.distanceAttenuation * light.shadowAttenuation;
                // A shadow floor is used only for the main light; local lights cannot illuminate back faces.
                half3 diffuse = main ? baseColor*lerp(_ShadowColor.rgb,half3(1,1,1),ramp*light.shadowAttenuation)*light.distanceAttenuation
                                     : baseColor*ramp*saturate(nl)*attenuation;
                diffuse *= lerp(1,.38,_Metallic);
                half spec = DirectBRDFSpecular(brdf,n,light.direction,v)*saturate(nl);
                half3 h = SafeNormalize(light.direction+v);
                half3 t = SafeNormalize(strand+n*_HairShift);
                half th = dot(t,h);
                half hairSpec = pow(saturate(1-th*th),_HairPower*.5)*smoothstep(-.2,.3,nl);
                half secondaryTh=dot(SafeNormalize(strand+n*(_HairShift-.22)),h);
                hairSpec += .15*pow(saturate(1-secondaryTh*secondaryTh),_HairPower*.25)*saturate(nl);
                half3 reflection = lerp(brdf.specular*spec*_SpecularStrength,baseColor*hairSpec*_HairStrength,_Hair)*attenuation*highlightMask;
                half rim = pow(1-saturate(dot(n,v)),4)*saturate(nl)*attenuation*_RimStrength;
                return (diffuse+reflection+rim)*light.color;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half3 n=normalize(i.normalWS), v=GetWorldSpaceNormalizeViewDir(i.positionWS);
                half3 strand=SafeNormalize(cross(n,i.tangentWS.xyz)*i.tangentWS.w);
                half3 tangent=SafeNormalize(i.tangentWS.xyz);
                half3 normalTS=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv),_BumpStrength);
                n=SafeNormalize(tangent*normalTS.x+strand*normalTS.y+n*normalTS.z);
                half3 surface=lerp(half3(1,1,1),SAMPLE_TEXTURE2D(_SurfaceMap,sampler_SurfaceMap,i.uv).rgb,_SurfaceStrength);
                half3 baseColor=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb;
                // UV-aligned procedural strand breakup fades out below pixel size to avoid crawling lines.
                float phase=i.uv.x*1131.0+sin(i.uv.y*23.0)*.65;
                float resolved=1-smoothstep(.5,2.5,fwidth(phase));
                half strands=.5+.32*sin(phase)+.18*sin(phase*1.73+i.uv.y*5);
                half detail=_Hair*_HairStrandDetail*resolved;
                baseColor*=1-detail*(1-strands)*.28;
                surface.g*=lerp(1,.3+.7*strands,detail);
                half smoothness=clamp(_Smoothness*surface.r,.05,.92);
                BRDFData brdf; half alpha=1;
                InitializeBRDFData(baseColor,_Metallic,half3(.04,.04,.04),smoothness,alpha,brdf);
                Light main=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 color=Shade(main,brdf,baseColor,n,v,strand,surface.g,true);
                InputData inputData=(InputData)0;
                inputData.positionWS=i.positionWS;
                inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                #if USE_CLUSTER_LIGHT_LOOP
                UNITY_LOOP for(uint lightIndex=0;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);lightIndex++)
                    color+=Shade(GetAdditionalLight(lightIndex,i.positionWS,half4(1,1,1,1)),brdf,baseColor,n,v,strand,surface.g,false);
                #endif
                uint count=GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(count)
                    color+=Shade(GetAdditionalLight(lightIndex,i.positionWS,half4(1,1,1,1)),brdf,baseColor,n,v,strand,surface.g,false);
                LIGHT_LOOP_END
                color+=baseColor*SampleSH(n)*.35;
                half3 env=GlossyEnvironmentReflection(reflect(-v,n),i.positionWS,brdf.perceptualRoughness,1,inputData.normalizedScreenSpaceUV);
                color+=env*lerp(brdf.specular,brdf.grazingTerm,Pow4(1-saturate(dot(n,v))))*_ReflectionStrength*surface.g;
                color*=surface.b;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    color*=GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV).indirectAmbientOcclusion;
                #endif
                color+=SAMPLE_TEXTURE2D(_EmissionMap,sampler_EmissionMap,i.uv).rgb*_EmissionColor.rgb;
                return half4(color,1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment DepthFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection, _LightPosition;
            float4 ShadowVert(Attributes v):SV_POSITION
            {
                float3 p=TransformObjectToWorld(v.positionOS.xyz);
                float3 n=TransformObjectToWorldNormal(v.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirection=normalize(_LightPosition-p);
                #else
                float3 lightDirection=_LightDirection;
                #endif
                float4 clip=TransformWorldToHClip(ApplyShadowBias(p,n,lightDirection));
                #if UNITY_REVERSED_Z
                clip.z=min(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
                #else
                clip.z=max(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
                #endif
                return clip;
            }
            half4 DepthFrag():SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthFrag
            half DepthFrag(Varyings i):SV_Target { return i.positionCS.z; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment NormalFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            half4 NormalFrag(Varyings i):SV_Target
            {
                float3 n=normalize(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(n)*.5+.5)),0);
                #else
                return half4(n,0);
                #endif
            }
            ENDHLSL
        }
    }
}
