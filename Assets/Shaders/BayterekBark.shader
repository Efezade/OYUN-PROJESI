Shader "TacticalRPG/BayterekBark"
{
 Properties { _BaseColor("Bark",Color)=(.24,.145,.074,1) _VeinColor("Sap",Color)=(.64,.39,.09,1) }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
  Pass
  {
   Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   struct A{float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;};
   struct V{float4 pos:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;};
   CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor,_VeinColor;
   CBUFFER_END
   V vert(A a){V o;o.world=TransformObjectToWorld(a.vertex.xyz);o.pos=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(a.normal);o.uv=a.uv;return o;}
   half4 frag(V i):SV_Target
   {
    float ridge=sin(i.uv.x*69+sin(i.uv.y*16)*.9);float fine=sin(i.uv.x*197+i.uv.y*4)*.09;
    float3 bark=_BaseColor.rgb*(.73+.34*ridge+fine);
    Light light=GetMainLight(TransformWorldToShadowCoord(i.world));
    float diffuse=saturate(dot(normalize(i.normal),light.direction));
    float vein=pow(saturate(ridge),28)*.12*(.6+.4*sin(_Time.y*1.3+i.world.x*.3));
    return half4(bark*(SampleSH(normalize(i.normal))+.3+light.color*diffuse*light.shadowAttenuation)+_VeinColor.rgb*vein,1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 }
}
