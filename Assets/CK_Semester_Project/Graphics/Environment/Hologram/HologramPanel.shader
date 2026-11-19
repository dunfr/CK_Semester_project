// 홀로그램 화면(임시 리소스용). 어두운 반투명 바탕 위에 빛나는 선을 얹어서 밝은 방에서도 읽히게 한다.
// 텍스처 채널: R = 선·글자, G = 옅은 면, B = 강조, A = 화면 모양.
// 조명을 받지 않고 스스로 빛난다(HDR 색이 1을 넘으면 블룸이 번진다). 양면이라 뒤에서는 좌우가 뒤집혀 옅게 보인다.
// 뒤따라오는 카메라가 화면을 뚫고 지나갈 때 시야를 가리지 않도록, 카메라에 가까운 부분은 사라진다.
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

        Pass
        {
            Name "Hologram"
            Tags { "LightMode"="UniversalForwardOnly" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _LineColor, _AccentColor, _BackingColor;
                half _FillStrength, _BackingOpacity;
                half _BackFaceStrength, _NearFadeStart, _NearFadeEnd;
                half _ScanCount, _ScanStrength, _ScanSpeed, _SweepStrength, _SweepSpeed, _Flicker;
                half _Beam, _BeamBottomWidth;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 panelUV : TEXCOORD1; float3 positionWS : TEXCOORD2; };

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

            half4 Frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float time = _Time.y;
                half flicker = 1.0h - _Flicker * (0.5h + 0.5h * sin(time * 31.0) * sin(time * 4.7));
                // 카메라가 화면 바로 뒤나 안쪽에 들어오면 시야를 가리므로 가까울수록 지우고, 뒷면은 옅게 그린다.
                half visibility = smoothstep(_NearFadeStart, max(_NearFadeEnd, _NearFadeStart + 0.01h), distance(input.positionWS, _WorldSpaceCameraPos))
                    * IS_FRONT_VFACE(face, 1.0h, _BackFaceStrength);
                flicker *= visibility;

                if (_Beam > 0.5h)
                {
                    // 아래(0)가 책상의 투사 장치, 위(1)가 화면. 아래가 좁고 밝으며 위로 갈수록 퍼지고 옅어진다.
                    half y = input.panelUV.y;
                    half halfWidth = lerp(_BeamBottomWidth, 1.0h, y) * 0.5h;
                    half side = 1.0h - smoothstep(halfWidth * 0.55h, halfWidth, abs(input.panelUV.x - 0.5h));
                    half fade = lerp(1.0h, 0.12h, sqrt(y));
                    half streak = 0.8h + 0.2h * sin(input.panelUV.x * 140.0 + sin(time * 0.8) * 3.0);
                    return half4(_LineColor.rgb * (side * fade * streak * flicker * _FillStrength * 2.0h), 0.0h);
                }

                half4 mask = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

                // 주사선. 멀어서 선이 픽셀보다 가늘어지면 어른거리므로 그만큼 약하게 한다.
                float scanPhase = input.panelUV.y * _ScanCount;
                half scanVisible = saturate(1.0 - fwidth(scanPhase) * 1.5);
                half scan = 1.0h - _ScanStrength * scanVisible * (0.5h + 0.5h * sin((scanPhase + time * _ScanSpeed) * 6.2831853));
                // 위에서 아래로 천천히 지나가는 밝은 띠.
                half sweep = _SweepStrength * smoothstep(0.08, 0.0, abs(input.panelUV.y - (1.0 - frac(time * _SweepSpeed))));

                half3 emissive = _LineColor.rgb * (mask.r + mask.g * _FillStrength) + _AccentColor.rgb * mask.b;
                emissive *= scan * flicker * (1.0h + sweep) * mask.a;

                half backing = mask.a * _BackingOpacity * visibility;
                return half4(emissive + _BackingColor.rgb * backing, backing);
            }
            ENDHLSL
        }
    }
}
