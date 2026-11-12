// 필드 미니맵을 청사진처럼 그린다. _MainTex는 NavMeshFloorPlanBaker가 구운 평면도(경계까지의 거리)이고,
// 지도 중심·범위·안내선은 FieldMinimapBlueprint가 매 프레임 넣는다. 색과 굵기는 이 재질에서 고친다.
Shader "UI/TutorialBlueprintMinimap"
{
    Properties
    {
        [PerRendererData] _MainTex ("Floor Plan", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Blueprint)]
        _OutsideColor ("Outside (solid) color", Color) = (0.067, 0.118, 0.212, 1)
        _FloorColor ("Walkable floor color", Color) = (0.122, 0.278, 0.494, 1)
        _LineColor ("Wall line color", Color) = (0.86, 0.9, 1, 1)
        _LineWidth ("Wall line width (px)", Range(0.5, 4)) = 1.6
        _GlowStrength ("Wall line glow", Range(0, 1)) = 0.35
        _HatchColor ("Wall hatch color (alpha = strength)", Color) = (0.86, 0.9, 1, 0.22)
        _HatchWidth ("Wall hatch band (m)", Range(0, 2)) = 0.8
        _HatchSpacing ("Wall hatch spacing (m)", Range(0.1, 1)) = 0.3
        _GridColor ("Grid color", Color) = (0.37, 0.72, 0.98, 1)
        _GridSize ("Grid cell (m)", Float) = 1
        _GridMinorAlpha ("Grid strength", Range(0, 1)) = 0.12
        _GridMajorAlpha ("Major grid (every 5 cells) strength", Range(0, 1)) = 0.26

        [Header(Route)]
        _RouteColor ("Route color", Color) = (1, 0.796, 0.247, 1)
        _RouteFlowColor ("Route flow highlight", Color) = (1, 0.96, 0.76, 1)
        _RouteOutlineColor ("Route outline (alpha = strength)", Color) = (0.03, 0.06, 0.12, 0.85)
        _RouteWidth ("Route width (px)", Range(1, 8)) = 3.6
        _RouteOutline ("Route outline (px)", Range(0, 3)) = 1.2
        _FlowSpacing ("Flow spacing (m)", Range(0.5, 6)) = 2.4
        _FlowSpeed ("Flow speed (m/s)", Range(0, 6)) = 2.5
        _TargetRadius ("Target ring radius (m)", Range(0.2, 3)) = 0.9

        [Header(Frame)]
        _EdgeShade ("Darken toward rim", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct AppData { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct Varyings { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _OutsideColor, _FloorColor, _LineColor, _HatchColor, _GridColor;
            float _LineWidth, _GlowStrength, _HatchWidth, _HatchSpacing, _GridSize, _GridMinorAlpha, _GridMajorAlpha;
            fixed4 _RouteColor, _RouteFlowColor, _RouteOutlineColor;
            float _RouteWidth, _RouteOutline, _FlowSpacing, _FlowSpeed, _TargetRadius, _EdgeShade;

            // FieldMinimapBlueprint가 넣는 값
            float4 _Center;     // xy = 지도 중심(월드 X, Z)
            float _ViewRadius;  // 중심에서 원 가장자리까지 거리(m)
            float4 _MaskRect;   // 평면도가 덮는 범위(최소 X, 최소 Z, 너비, 깊이)
            float _Range;       // 평면도 0~1이 나타내는 거리(-Range~+Range, m)
            float _WallOffset;  // 바닥 경계에서 실제 벽까지 거리(m)
            float _HasMask;
            float _RouteCount;
            float4 _Route[64];  // xy = 월드 X, Z, z = 목표까지 남은 거리(m)
            float4 _Target;     // xy = 목표 월드 X, Z
            float _HasTarget;

            Varyings Vert(AppData input)
            {
                Varyings output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            // 간격 spacing으로 반복되는 선까지의 거리(px)
            float LineDistancePx(float value, float spacing, float pixel)
            {
                return abs(frac(value / spacing + 0.5) - 0.5) * spacing / pixel;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float r = length(p);
                float rPixel = max(fwidth(r), 1e-5);
                float circle = 1.0 - smoothstep(1.0 - rPixel, 1.0, r);
                clip(circle - 0.001);

                float2 world = _Center.xy + p * _ViewRadius;
                // 화면 1px이 몇 m인지. 선 굵기를 px로 맞출 때 쓴다.
                float pixel = max(max(length(ddx(world)), length(ddy(world))), 1e-4);

                float signedDistance = _Range;
                if (_HasMask > 0.5)
                {
                    float2 maskUv = (world - _MaskRect.xy) / _MaskRect.zw;
                    float stored = tex2D(_MainTex, maskUv).r;
                    float inRect = step(0.0, maskUv.x) * step(0.0, maskUv.y) * step(maskUv.x, 1.0) * step(maskUv.y, 1.0);
                    signedDistance = lerp(_Range, (stored - 0.5) * 2.0 * _Range, inRect);
                }
                float wall = signedDistance - _WallOffset;   // 0보다 작으면 바닥, 0이 벽선, 크면 벽 속

                float floorMask = 1.0 - smoothstep(-pixel, pixel, wall);
                float3 color = lerp(_OutsideColor.rgb, _FloorColor.rgb, floorMask);
                color += _LineColor.rgb * floorMask * exp(-max(-wall, 0.0) / 0.6) * 0.06;

                float minor = 1.0 - saturate(min(LineDistancePx(world.x, _GridSize, pixel), LineDistancePx(world.y, _GridSize, pixel)) - 0.25);
                float majorSize = _GridSize * 5.0;
                float major = 1.0 - saturate(min(LineDistancePx(world.x, majorSize, pixel), LineDistancePx(world.y, majorSize, pixel)) - 0.4);
                float grid = max(minor * _GridMinorAlpha, major * _GridMajorAlpha) * lerp(0.45, 1.0, floorMask);
                color = lerp(color, _GridColor.rgb, grid);

                float hatchBand = step(0.0, wall) * (1.0 - smoothstep(_HatchWidth * 0.55, _HatchWidth, wall));
                float hatch = (1.0 - saturate(LineDistancePx((world.x + world.y) * 0.70710678, _HatchSpacing, pixel) - 0.35)) * hatchBand;
                color = lerp(color, _HatchColor.rgb, hatch * _HatchColor.a);

                float wallPx = abs(wall) / pixel;
                float wallLine = 1.0 - saturate(wallPx - _LineWidth * 0.5 + 0.5);
                float glow = exp(-wallPx / 3.0) * _GlowStrength * 0.6;
                color = lerp(color, _LineColor.rgb, saturate(max(wallLine, glow)));

                int count = (int)_RouteCount;
                if (count >= 2)
                {
                    float best = 1e6;
                    float remaining = 0.0;
                    [loop]
                    for (int i = 0; i < 63; i++)
                    {
                        if (i >= count - 1)
                        {
                            break;
                        }
                        float4 a = _Route[i];
                        float4 b = _Route[i + 1];
                        float2 ab = b.xy - a.xy;
                        float t = saturate(dot(world - a.xy, ab) / max(dot(ab, ab), 1e-5));
                        float d = length(world - a.xy - ab * t);
                        if (d < best)
                        {
                            best = d;
                            remaining = lerp(a.z, b.z, t);
                        }
                    }
                    float routePx = best / pixel;
                    float halfWidth = _RouteWidth * 0.5;
                    float core = 1.0 - saturate(routePx - halfWidth + 0.5);
                    float outline = 1.0 - saturate(routePx - halfWidth - _RouteOutline + 0.5);
                    color = lerp(color, _RouteOutlineColor.rgb, outline * _RouteOutlineColor.a);
                    // 남은 거리를 따라 밝은 마디가 목표 쪽으로 흐른다.
                    float phase = frac((remaining + _Time.y * _FlowSpeed) / _FlowSpacing);
                    float flow = smoothstep(0.0, 0.12, phase) * (1.0 - smoothstep(0.3, 0.45, phase));
                    color = lerp(color, lerp(_RouteColor.rgb, _RouteFlowColor.rgb, flow), core);
                }

                if (_HasTarget > 0.5)
                {
                    float targetDistance = length(world - _Target.xy);
                    float pulse = frac(_Time.y * 0.7);
                    float ringRadius = _TargetRadius * (0.5 + pulse);
                    float ring = (1.0 - saturate(abs(targetDistance - ringRadius) / pixel - 0.8)) * (1.0 - pulse);
                    float centerDot = 1.0 - saturate((targetDistance - _TargetRadius * 0.35) / pixel);
                    color = lerp(color, _RouteColor.rgb, saturate(ring + centerDot));
                }

                color *= 1.0 - _EdgeShade * smoothstep(0.72, 1.0, r);
                return fixed4(color, circle) * input.color;
            }
            ENDCG
        }
    }
}
