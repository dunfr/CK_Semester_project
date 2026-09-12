Shader "Semester/Energy Effect"
{
 Properties {
  [HDR] _Color("Color",Color)=(3,1,.15,1)
  _Shape("Shape: flash / ring / streak / spark",Float)=0
  _Opacity("Opacity",Range(0,1))=1
  _Progress("Progress",Range(0,1))=0
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
  Pass {
   Tags {"LightMode"="SRPDefaultUnlit"}
   Blend SrcAlpha One
   ZWrite Off Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   half4 _Color;float _Shape,_Opacity,_Progress;
   CBUFFER_END
   struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
   struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
   V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;return o;}
   half4 frag(V i):SV_Target {
    float2 p=i.uv*2-1;float r=length(p),a=0;
    if(_Shape<.5){float core=exp(-r*r*26);float rays=pow(saturate(1-abs(p.x*p.y)*38),3)*pow(saturate(1-r),1.4);a=saturate(core+rays*.85);}
    else if(_Shape<1.5){float angle=atan2(p.y,p.x);float wave=sin(angle*11+_Progress*7)*.009;float width=max(fwidth(r)*1.5,.025);a=1-smoothstep(width,width*2,abs(r-(.76+wave)));a*=smoothstep(0,.2,1-r);}
    else if(_Shape<2.5){a=pow(saturate(1-abs(p.y)),4)*smoothstep(0,.12,i.uv.x)*pow(saturate(1-i.uv.x),.35);}
    else {a=pow(saturate(1-abs(p.x)-abs(p.y)),1.5);}
    return half4(_Color.rgb,a*_Opacity*_Color.a);
   }
   ENDHLSL
  }
 }
}
