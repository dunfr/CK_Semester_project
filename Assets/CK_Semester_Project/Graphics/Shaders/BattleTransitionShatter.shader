// 전투 진입 전환 B: 멈춘 직전 화면(_MainTex)에 금이 가고, 대각선으로 갈라진 조각들이 서로 반대로 미끄러지며 새 화면을 드러낸다.
// 화면 전체를 덮는 UI(RawImage)에 쓰며, 조각이 없는 곳은 투명해서 아래의 실제 화면이 보인다.
Shader "CK/UI/BattleTransitionShatter"
{
    Properties
    {
        [PerRendererData] _MainTex ("Captured Screen", 2D) = "black" {}
        _Crack ("Crack", Range(0, 1)) = 0
        _Flash ("Flash", Range(0, 1)) = 0
        _Split ("Split", Range(0, 1)) = 0
        _CutAngle ("Cut Angle (deg)", Float) = 27
        _CutOffsets ("Cut Offsets (screen height, x/y/z)", Vector) = (0, 0, 0, 0)
        _CutCount ("Cut Count (1-3)", Range(1, 3)) = 1
        _SlideDistance ("Slide Distance (screen height)", Float) = 2.6
        _GapPush ("Gap Push (screen height)", Float) = 0.08
        _SlideMode ("Slide Mode (0 = along cut, 1 = horizontal)", Range(0, 1)) = 1
        _CrackColor ("Crack Color", Color) = (0.75, 0.9, 1, 1)
        _CrackWidth ("Crack Width", Range(0.0005, 0.02)) = 0.0025
        _ShardDensity ("Shard Density", Float) = 7
        _EdgeColor ("Cut Edge Color", Color) = (0.85, 0.95, 1, 1)
        _EdgeWidth ("Cut Edge Width", Range(0.001, 0.05)) = 0.008
        _Darken ("Darken While Moving", Range(0, 1)) = 0.35
        _Dim ("Pre Fade Dim", Range(0, 1)) = 0
        _Light ("Gap Light", Range(0, 1)) = 0
        _LightColor ("Gap Light Color", Color) = (0.85, 0.95, 1, 1)
        _LightWash ("Light Wash (틈 밖 화면 전체 밝기)", Range(0, 1)) = 0.8
        _LightCoreWidth ("Light Core Width (screen height)", Float) = 0.28
        _LightSpread ("Light Halo Spread (core width 배수)", Float) = 3
        _LightEdgeBloom ("Light Bloom On Piece Edges (작을수록 넓게)", Float) = 10
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

            #define MAX_STRIPS 4

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

            sampler2D _MainTex;
            float _Crack;
            float _Flash;
            float _Split;
            float _CutAngle;
            float4 _CutOffsets;
            float _CutCount;
            float _SlideDistance;
            float _GapPush;
            float _SlideMode;
            float4 _CrackColor;
            float _CrackWidth;
            float _ShardDensity;
            float4 _EdgeColor;
            float _EdgeWidth;
            float _Darken;
            float _Dim;
            float _Light;
            float4 _LightColor;
            float _LightWash;
            float _LightCoreWidth;
            float _LightSpread;
            float _LightEdgeBloom;
            float _Aspect;
            float _Seed;

            float Hash11(float n)
            {
                return frac(sin(n * 91.3 + _Seed * 13.1) * 43758.5453);
            }

            float2 Hash22(float2 p)
            {
                float2 q = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(q + _Seed * 7.7) * 43758.5453);
            }

            float ValueNoise1(float x)
            {
                float i = floor(x);
                float f = frac(x);
                return lerp(Hash11(i), Hash11(i + 1.0), f * f * (3.0 - 2.0 * f));
            }

            int CutCount()
            {
                return (int)clamp(round(_CutCount), 1.0, 3.0);
            }

            float CutOffset(int k)
            {
                return k == 0 ? _CutOffsets.x : (k == 1 ? _CutOffsets.y : _CutOffsets.z);
            }

            // 대각선 기준 수직 좌표가 어느 띠(조각)에 속하는지. 자를 선 아래부터 0번.
            int StripIndex(float acrossCoord)
            {
                int index = 0;
                int count = CutCount();
                for (int k = 0; k < 3; k++)
                {
                    if (k < count && acrossCoord > CutOffset(k))
                    {
                        index++;
                    }
                }
                return index;
            }

            // 조각 경계까지의 거리. 조각 테두리 빛에 쓴다.
            float DistanceToStripEdge(float acrossCoord, int strip)
            {
                float best = 10.0;
                int count = CutCount();
                for (int k = 0; k < 3; k++)
                {
                    if (k < count)
                    {
                        best = min(best, abs(acrossCoord - CutOffset(k)));
                    }
                }
                return best;
            }

            // 이웃 조각끼리 같은 속도로 반대 방향 미끄러짐. 자르는 선이 하나면 두 조각이 화면 중심 기준 대칭으로 갈라진다.
            // _SlideMode 0 = 자르는 선을 따라, 1 = 가로로(왼쪽 조각은 왼쪽, 오른쪽 조각은 오른쪽).
            float2 StripDisplacement(int strip, float2 along, float2 across, float split)
            {
                float center = 0.5 * CutCount();
                float side = sign(strip - center);
                float2 horizontal = float2(sign(across.x) * side, 0.0) * _SlideDistance * split;

                float direction = (strip % 2 == 0) ? -1.0 : 1.0;
                float2 alongCut = along * direction * _SlideDistance * split
                    + across * (strip - center) * _GapPush * split;

                return lerp(alongCut, horizontal, _SlideMode);
            }

            // 금: 자를 선을 따라 떨리는 밝은 선과 그 주변의 잔금(보로노이 경계).
            float CrackMask(float2 p, float2 along, float2 across)
            {
                float alongCoord = dot(p, along);
                float acrossCoord = dot(p, across);
                float halfDiagonal = 0.5 * sqrt(_Aspect * _Aspect + 1.0);
                float reach = _Crack * halfDiagonal * 1.2;

                float lines = 0.0;
                float nearCut = 10.0;
                int count = CutCount();
                for (int k = 0; k < 3; k++)
                {
                    if (k < count)
                    {
                        // 금은 화면 중앙에서 양 끝으로 대칭으로 뻗는다.
                        float origin = 0.0;
                        float jitter = (ValueNoise1(alongCoord * 18.0 + k * 5.0) - 0.5) * 0.012;
                        float distanceToLine = abs(acrossCoord - CutOffset(k) + jitter);
                        float grown = step(abs(alongCoord - origin), reach);
                        lines = max(lines, (1.0 - smoothstep(_CrackWidth * 0.5, _CrackWidth * 1.5, distanceToLine)) * grown);
                        nearCut = min(nearCut, abs(acrossCoord - CutOffset(k)) + max(0.0, abs(alongCoord - origin) - reach));
                    }
                }

                // 보로노이 셀 경계로 유리 파편 같은 잔금을 만든다.
                float2 cellPos = p * _ShardDensity;
                float2 cell = floor(cellPos);
                float first = 10.0;
                float second = 10.0;
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 neighbor = cell + float2(x, y);
                        float2 point2 = neighbor + Hash22(neighbor);
                        float d = length(cellPos - point2);
                        if (d < first)
                        {
                            second = first;
                            first = d;
                        }
                        else if (d < second)
                        {
                            second = d;
                        }
                    }
                }
                float shardEdge = 1.0 - smoothstep(0.0, 0.06, second - first);
                float shardMask = exp(-nearCut * 24.0) * _Crack;
                return saturate(max(lines, shardEdge * shardMask * 0.6));
            }

            // 조각이 비운 자리에서 뿜어져 나오는 빛. 자르는 선 근처가 가장 밝고, 선을 따라 빛줄기처럼 굵기가 흔들린다.
            float4 GapLight(float2 p, float2 along, float2 across)
            {
                float distanceToCut = 10.0;
                int count = CutCount();
                for (int k = 0; k < 3; k++)
                {
                    if (k < count)
                    {
                        distanceToCut = min(distanceToCut, abs(dot(p, across) - CutOffset(k)));
                    }
                }

                float rays = 0.6 + 0.8 * ValueNoise1(dot(p, along) * 14.0 + 3.0);
                float width = _LightCoreWidth * rays * (0.4 + _Split);
                float core = exp(-distanceToCut / max(width, 1e-4));
                // 심지 바깥으로 넓게 번지는 후광
                float halo = exp(-distanceToCut / max(width * _LightSpread, 1e-4));

                float3 color = lerp(_LightColor.rgb, float3(1, 1, 1), saturate(core + halo * 0.5));
                float alpha = _Light * saturate(_LightWash + (1.0 - _LightWash) * (core + halo));
                return float4(color, alpha);
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
                float rad = radians(_CutAngle);
                float2 along = float2(cos(rad), sin(rad));
                float2 across = float2(-along.y, along.x);

                // 이 화면 위치에 와 있는 조각을 거꾸로 찾는다. 원래 자리로 되돌렸을 때 같은 조각이면 그 조각의 픽셀이다.
                float2 source = p;
                int hitStrip = -1;
                int stripCount = CutCount() + 1;
                for (int s = 0; s < MAX_STRIPS; s++)
                {
                    if (s < stripCount && hitStrip < 0)
                    {
                        float2 candidate = p - StripDisplacement(s, along, across, _Split);
                        if (StripIndex(dot(candidate, across)) == s)
                        {
                            hitStrip = s;
                            source = candidate;
                        }
                    }
                }

                float4 gapLight = GapLight(p, along, across);
                gapLight.a *= input.color.a;
                if (hitStrip < 0)
                {
                    return gapLight;
                }

                float2 sourceUV = source / float2(_Aspect, 1.0) + 0.5;
                if (any(sourceUV < 0.0) || any(sourceUV > 1.0))
                {
                    return gapLight;
                }

                float3 color = tex2D(_MainTex, sourceUV).rgb;
                color *= (1.0 - _Dim) * (1.0 - _Darken * _Split);

                float crack = CrackMask(source, along, across);
                color = lerp(color, _CrackColor.rgb, crack * _CrackColor.a);

                float edgeDistance = DistanceToStripEdge(dot(source, across), hitStrip);
                float edgeGlow = (1.0 - smoothstep(0.0, _EdgeWidth, edgeDistance)) * saturate(_Split * 8.0);
                color = lerp(color, _EdgeColor.rgb, edgeGlow * _EdgeColor.a);

                // 틈에서 나온 빛이 조각의 잘린 가장자리 위로 번진다.
                float bloom = exp(-edgeDistance * _LightEdgeBloom) * _Light;
                color = lerp(color, float3(1, 1, 1), saturate(bloom * 0.6));
                color += _LightColor.rgb * bloom;

                color = lerp(color, float3(1, 1, 1), _Flash);
                return float4(color, input.color.a);
            }
            ENDCG
        }
    }
}
