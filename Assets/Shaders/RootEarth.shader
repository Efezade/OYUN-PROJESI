Shader "TacticalRPG/RootEarth"
{
 Properties { _BaseColor("Biome ground",Color)=(.35,.4,.2,1) _SoilColor("Broken earth",Color)=(.22,.15,.08,1) }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
  Pass
  {
   Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseColor,_SoilColor;
   CBUFFER_END
   struct A {float4 p:POSITION;float3 n:NORMAL;float4 c:COLOR;};
   struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;float4 c:COLOR;};
   V Vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);o.c=a.c;return o;}
   half4 Frag(V i):SV_Target
   {
    float grain=frac(sin(dot(floor(i.w.xz*32),float2(12.9898,78.233)))*43758.5453);
    float strata=.86+.14*sin(i.w.y*38+sin(i.w.x*13+i.w.z*17));
    float3 colour=lerp(_SoilColor.rgb*strata,_BaseColor.rgb,i.c.r)*(.88+grain*.24)*i.c.g;
    float3 n=normalize(i.n);Light light=GetMainLight(TransformWorldToShadowCoord(i.w));
    return half4(colour*(SampleSH(n)+light.color*saturate(dot(n,light.direction))*light.shadowAttenuation),1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
