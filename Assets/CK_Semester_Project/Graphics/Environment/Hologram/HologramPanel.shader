// 홀로그램 화면(임시 리소스용). 어두운 반투명 바탕 위에 빛나는 선을 얹어서 밝은 방에서도 읽히게 한다.
// 텍스처 채널: R = 선·글자, G = 옅은 면, B = 강조, A = 화면 모양.
// 조명을 받지 않고 스스로 빛난다. 양면이라 뒤에서는 좌우가 뒤집혀 옅게 보인다.
// 뒤따라오는 카메라가 화면을 뚫고 지나갈 때 시야를 가리지 않도록, 카메라에 가까운 부분은 사라진다.
//
// 번짐(Bloom)은 두 가지 중 하나로 생긴다.
//   - Volume에 Hologram Bloom이 켜져 있으면: 본체는 상한 아래로 눌러 그리고, 번짐은 "HologramBloom" 패스가 내보낸 빛으로
//     전용 Bloom(HologramBloomFeature)이 따로 만든다. 조명용 Bloom 값을 바꿔도 홀로그램은 그대로다.
//   - 꺼져 있으면: 예전처럼 HDR 색이 1을 넘는 만큼 씬 Bloom이 번지게 한다.
Shader "Semester/Environment/Hologram Panel"
{
    Properties
    {
        _BaseMap("Screen (R lines, G fills, B accent, A panel shape)", 2D) = "black" {}
        [HDR] _LineColor("Line color", Color) = (0.25, 1.1, 2.0, 1)
        [HDR] _AccentColor("Accent color", Color) = (1.4, 2.0, 2.4, 1)
        _FillStrength("Fill strength", Range(0, 1)) = 0.22
        _BackingColor("Backing color", Color) = (0.03, 0.10, 0.20, 1)
        _BackingOpacity("Backing opacity", Range(0, 1)) = 0.55

        [Header(Hologram Bloom)]
        _PanelGlow("Whole screen glow (screen acts as a light)", Range(0, 0.5)) = 0.05

        [Header(Visibility)]
        _BackFaceStrength("Back side strength", Range(0, 1)) = 0.5
        _NearFadeStart("Camera fade: invisible within (m)", Float) = 1.5
        _NearFadeEnd("Camera fade: full beyond (m)", Float) = 4.5

        [Header(Motion)]
        _ScanCount("Scanline count", Float) = 90
        _ScanStrength("Scanline strength", Range(0, 1)) = 0.14
        _ScanSpeed("Scanline scroll speed", Float) = 0.4
        _SweepStrength("Sweep band strength", Range(0, 2)) = 0.5
        _SweepSpeed("Sweep band speed", Float) = 0.12
        _Flicker("Flicker", Range(0, 0.5)) = 0.05

        [Header(Projection beam)]
        [Toggle] _Beam("Draw as projection beam (no texture)", Float) = 0
        _BeamBottomWidth("Beam bottom width", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }

        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "HologramBloomSource.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _LineColor, _AccentColor, _BackingColor;
            half _FillStrength, _BackingOpacity;
            half _BackFaceStrength, _NearFadeStart, _NearFadeEnd;
            half _ScanCount, _ScanStrength, _ScanSpeed, _SweepStrength, _SweepSpeed, _Flicker;
            half _Beam, _BeamBottomWidth;
            half _PanelGlow;
        CBUFFER_END

        // 전용 Bloom이 켜진 카메라에서 HologramBloomFeature가 넣는 본체 밝기 상한. 0이면 전용 Bloom이 꺼진 카메라다.
        half _HologramBodyLimit;

        struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
        struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 panelUV : TEXCOORD1; float3 positionWS : TEXCOORD2; };

        // emissive: 선·글자·면이 내는 빛(HDR), backing: 어두운 바탕 색(이미 가림 정도가 곱해진 값), alpha: 뒤 배경을 가리는 정도,
        // panelLight: 화면 전체가 광원으로서 고르게 내는 빛(전용 Bloom에서만 쓴다).
        struct HologramSurface { half3 emissive; half3 backing; half alpha; half3 panelLight; };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            // 화면 하나 안에서의 0~1 좌표. 주사선·빔처럼 아틀라스 위치와 무관한 무늬에 쓴다.
            output.panelUV = input.uv;
            return output;
        }

        // faceStrength: 앞면 1, 뒷면은 Back side strength.
        HologramSurface EvaluateHologram(Varyings input, half faceStrength)
        {
            HologramSurface surface = (HologramSurface)0;
            float time = _Time.y;
            half flicker = 1.0h - _Flicker * (0.5h + 0.5h * sin(time * 31.0) * sin(time * 4.7));
            // 카메라가 화면 바로 뒤나 안쪽에 들어오면 시야를 가리므로 가까울수록 지우고, 뒷면은 옅게 그린다.
            half visibility = smoothstep(_NearFadeStart, max(_NearFadeEnd, _NearFadeStart + 0.01h), distance(input.positionWS, _WorldSpaceCameraPos))
                * faceStrength;
            flicker *= visibility;

            // 화면 쪽 값. 텍스처 읽기와 fwidth는 분기 밖에서 해야 한다(분기 안에 두면 컴파일러가 경고를 낸다).
            half4 mask = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

            // 주사선. 멀어서 선이 픽셀보다 가늘어지면 어른거리므로 그만큼 약하게 한다.
            float scanPhase = input.panelUV.y * _ScanCount;
            half scanVisible = saturate(1.0 - fwidth(scanPhase) * 1.5);
            half scan = 1.0h - _ScanStrength * scanVisible * (0.5h + 0.5h * sin((scanPhase + time * _ScanSpeed) * 6.2831853));
            // 위에서 아래로 천천히 지나가는 밝은 띠.
            half sweep = _SweepStrength * smoothstep(0.08, 0.0, abs(input.panelUV.y - (1.0 - frac(time * _SweepSpeed))));

            half3 emissive = _LineColor.rgb * (mask.r + mask.g * _FillStrength) + _AccentColor.rgb * mask.b;
            surface.emissive = emissive * (scan * flicker * (1.0h + sweep) * mask.a);
            surface.alpha = mask.a * _BackingOpacity * visibility;
            surface.backing = _BackingColor.rgb * surface.alpha;
            // 선 색의 색조만 쓴다(세기는 Whole screen glow 하나로 정한다). 화면 모양 전체에 고르게 깔린다.
            half3 lineHue = _LineColor.rgb / max(max(_LineColor.r, max(_LineColor.g, _LineColor.b)), 1e-3h);
            surface.panelLight = lineHue * (_PanelGlow * mask.a * flicker);

            if (_Beam > 0.5h)
            {
                // 투사 빔(텍스처 없음). 아래(0)가 책상의 투사 장치, 위(1)가 화면. 아래가 좁고 밝으며 위로 갈수록 퍼지고 옅어진다.
                half y = input.panelUV.y;
                half halfWidth = lerp(_BeamBottomWidth, 1.0h, y) * 0.5h;
                half side = 1.0h - smoothstep(halfWidth * 0.55h, halfWidth, abs(input.panelUV.x - 0.5h));
                half fade = lerp(1.0h, 0.12h, sqrt(y));
                half streak = 0.8h + 0.2h * sin(input.panelUV.x * 140.0 + sin(time * 0.8) * 3.0);
                surface.emissive = _LineColor.rgb * (side * fade * streak * flicker * _FillStrength * 2.0h);
                surface.backing = 0.0h;
                surface.alpha = 0.0h;
                surface.panelLight = 0.0h;
            }

            return surface;
        }

        // 상한의 절반까지는 그대로 두고, 그 위는 상한에 부드럽게 붙인다.
        // 채널마다 따로 눌러서, 밝은 선일수록 흰빛을 띠는 HDR 느낌이 본체에도 남는다.
        half3 LimitBody(half3 color, half limit)
        {
            half knee = limit * 0.5h;
            half shoulder = limit - knee;
            return min(color, knee) + shoulder * (1.0h - exp(-max(color - knee, 0.0h) / shoulder));
        }
        ENDHLSL

        // 화면에 보이는 본체.
        Pass
        {
            Name "Hologram"
            Tags { "LightMode"="UniversalForwardOnly" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                HologramSurface surface = EvaluateHologram(input, IS_FRONT_VFACE(face, 1.0h, _BackFaceStrength));
                half3 color = surface.emissive + surface.backing;
                // 전용 Bloom이 켜진 카메라에서는 씬 Bloom(조명용)에 잡히지 않도록 본체를 상한 아래로 누른다.
                // 눌린 만큼의 빛은 아래 "HologramBloom" 패스를 거쳐 전용 Bloom으로 돌아온다.
                if (_HologramBodyLimit > 0.0h)
                {
                    color = LimitBody(color, _HologramBodyLimit);
                }

                return half4(color, surface.alpha);
            }
            ENDHLSL
        }

        // 전용 Bloom의 재료. 일반 그리기에서는 쓰이지 않고 HologramBloomFeature가 이 패스만 골라 따로 그린다.
        //   - 선·글자: 누르지 않은 빛 중 Volume의 Threshold보다 밝은 부분만 번진다.
        //   - 화면 전체: 조명 판처럼 화면 모양 그대로 고르게 번진다(Threshold와 무관, Whole screen glow로 세기 조절).
        // 바탕 색은 빼고, 뒤 화면을 가리는 정도만 알파로 남긴다.
        Pass
        {
            Name "HologramBloom"
            Tags { "LightMode"="HologramBloom" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBloomSource

            half4 FragBloomSource(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                HologramSurface surface = EvaluateHologram(input, IS_FRONT_VFACE(face, 1.0h, _BackFaceStrength));
                return half4(HologramBloomThreshold(surface.emissive) + surface.panelLight, surface.alpha);
            }
            ENDHLSL
        }
    }
}
