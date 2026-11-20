// 전투 진입 전환 A: 단색 사선 띠가 세 겹(밝은 색 → 진한 색 → 검정)으로 차곡차곡 쌓이며 덮고, 역순으로 걷힌다.
// 번짐·질감 없이 경계가 또렷한 평면 일러스트 느낌. 화면 전체를 덮는 UI(RawImage)에 쓴다.
// _Progress 0 = 투명, 1 = 완전히 덮임.
Shader "CK/UI/BattleTransitionStripe"
{
    Properties
    {
        [PerRendererData] _MainTex ("Unused", 2D) = "white" {}
        _Progress ("Progress", Range(0, 1)) = 0
        _ColorA ("Layer 1 Color (alpha = 비침)", Color) = (0.25, 0.75, 0.7, 0.55)
        _ColorB ("Layer 2 Color (alpha = 비침)", Color) = (0.05, 0.35, 0.36, 0.85)
        _ColorC ("Layer 3 Color (top)", Color) = (0.02, 0.02, 0.03, 1)
        _Angle ("Band Angle (deg)", Float) = -62
        _BandWidth ("Band Width (screen height)", Float) = 0.2
        _Stagger ("Band Stagger", Range(0, 2)) = 0.6
        _LayerDelay ("Layer Delay", Range(0, 0.45)) = 0.25
        _SweepFlip ("Sweep Flip (0/1)", Float) = 0
        _Aspect ("Aspect (w/h)", Float) = 1.7778
        _Seed ("Seed", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" "IgnoreProjector" = "True" "CanUseSpriteAtlas" = "False" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            float _Progress;
            float4 _ColorA;
            float4 _ColorB;
            float4 _ColorC;
            float _Angle;
            float _BandWidth;
            float _Stagger;
            float _LayerDelay;
            float _SweepFlip;
            float _Aspect;
            float _Seed;

            float Hash11(float n)
            {
                return frac(sin(n * 127.1 + _Seed * 31.7) * 43758.5453);
            }

            // 띠 한 겹의 덮임(0/1, 경계만 안티에일리어싱).
            // 각 띠는 무작위 지연 뒤 띠 길이 방향으로 곧게 밀려 들어온다. 앞쪽 끝은 띠와 수직인 직선.
            float LayerCoverage(float2 p, float layerProgress, float bandOffset, float layerSeed)
            {
                float rad = radians(_Angle);
                float2 along = float2(cos(rad), sin(rad));
                float2 across = float2(-along.y, along.x);

                float acrossCoord = dot(p, across) / _BandWidth + bandOffset;
                float bandIndex = floor(acrossCoord);

                float bandHash = Hash11(bandIndex + layerSeed);
                float local = saturate(layerProgress * (1.0 + _Stagger) - bandHash * _Stagger);

                float halfDiagonal = 0.5 * sqrt(_Aspect * _Aspect + 1.0);
                float sweep = saturate(dot(p, along) / (2.0 * halfDiagonal) + 0.5);
                // 띠마다 들어오는 방향을 번갈아 바꿔 엇갈려 쌓이게 한다.
                float alternate = fmod(abs(bandIndex), 2.0);
                sweep = lerp(sweep, 1.0 - sweep, abs(alternate - _SweepFlip));

                float edge = local - sweep;
                float aa = max(fwidth(sweep), 1e-4);
                return saturate(edge / aa + 0.5) * step(1e-4, local);
            }

            float4 BlendOver(float4 below, float3 color, float coverage)
            {
                float alpha = coverage + below.a * (1.0 - coverage);
                float3 rgb = (color * coverage + below.rgb * below.a * (1.0 - coverage)) / max(alpha, 1e-4);
                return float4(rgb, alpha);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 p = (input.uv - 0.5) * float2(_Aspect, 1.0);

                // 세 겹이 _LayerDelay씩 늦게 시작한다. 겹마다 띠 위치를 1/3칸씩 어긋나게 둔다.
                float span = 1.0 - 2.0 * _LayerDelay;
                float progressA = saturate(_Progress / span);
                float progressB = saturate((_Progress - _LayerDelay) / span);
                float progressC = saturate((_Progress - 2.0 * _LayerDelay) / span);

                float4 result = float4(0, 0, 0, 0);
                result = BlendOver(result, _ColorA.rgb, LayerCoverage(p, progressA, 0.0, 11.0) * _ColorA.a);
                result = BlendOver(result, _ColorB.rgb, LayerCoverage(p, progressB, 0.33, 47.0) * _ColorB.a);
                float topCoverage = max(LayerCoverage(p, progressC, 0.66, 83.0), step(0.999, _Progress));
                result = BlendOver(result, _ColorC.rgb, topCoverage * _ColorC.a);

                return float4(result.rgb, result.a * input.color.a);
            }
            ENDCG
        }
    }
}
