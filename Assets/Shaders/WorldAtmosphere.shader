Shader "TacticalRPG/WorldAtmosphere"
{
 Properties { _BaseColor("Mist",Color)=(.29,.36,.36,1) _Mode("0 Mist / 1 Void / 2 Dust",Float)=0 _Born("Born",Float)=-100 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
  Pass
  {
   Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A{float4 vertex:POSITION;float2 uv:TEXCOORD0;};
   struct V{float4 position:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;};
   CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;float _Mode,_Born;float4 _FogCenter,_Fog0,_Fog1,_Fog2,_Fog3,_Fog4,_Fog5;
    float4 _FogColor0,_FogColor1,_FogColor2,_FogColor3,_FogColor4,_FogColor5;
   CBUFFER_END
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
   float fbm(float2 p){return noise(p)*.57+noise(p*2.03+17)*.28+noise(p*4.17)*.15;}
   void blendFog(float2 p,float4 neighbor,float3 tint,float selfDist,inout float sum,inout float alpha,inout float3 color)
   {float difference=dot(p-neighbor.xy,p-neighbor.xy)-selfDist;float w=exp2(-max(0,difference)/.8);sum+=w;alpha+=w*neighbor.z;color+=w*tint;}
   V vert(A a){V o;o.world=TransformObjectToWorld(a.vertex.xyz);if(_Mode<.5)o.world.y+=sin(o.world.x*.31+_Time.y*.12)*.065+cos(o.world.z*.27-_Time.y*.1)*.05;o.position=TransformWorldToHClip(o.world);o.uv=a.uv;return o;}
   half4 frag(V i):SV_Target
   {
    float time=_Time.y;float2 p=i.world.xz;float n=fbm(p*.42+float2(time*.037,time*.022));
    if(_Mode<.5)
    {
     float sum=1,alpha=_BaseColor.a;float3 tint=_BaseColor.rgb;float d=dot(p-_FogCenter.xy,p-_FogCenter.xy);
     blendFog(p,_Fog0,_FogColor0.rgb,d,sum,alpha,tint);blendFog(p,_Fog1,_FogColor1.rgb,d,sum,alpha,tint);blendFog(p,_Fog2,_FogColor2.rgb,d,sum,alpha,tint);blendFog(p,_Fog3,_FogColor3.rgb,d,sum,alpha,tint);blendFog(p,_Fog4,_FogColor4.rgb,d,sum,alpha,tint);blendFog(p,_Fog5,_FogColor5.rgb,d,sum,alpha,tint);
     float light=fbm(p*.24+float2(-time*.014,time*.019));
     float density=saturate(alpha/sum*(.88+n*.22));
     float3 color=tint/sum*lerp(.63,1.38,light)+float3(.025,.033,.032)*n;
     return half4(color,density);
    }
    if(_Mode>1.5)
    {
     float edge=sin(saturate(i.uv.y)*3.14159);float broken=smoothstep(.29,.65,fbm(p*1.9+time*.13));
     return half4(_BaseColor.rgb,_BaseColor.a*edge*broken);
    }
    float2 q=i.uv*2-1;float r=length(q);float angle=atan2(q.y,q.x);
    float age=saturate((time-_Born)/1.1);float opening=smoothstep(0,1,age);
    float core=1-smoothstep(opening*.85,opening+ .03,r);
    float wisp=pow(saturate(sin(angle*3+time*.6+r*19+noise(q*4)*4)),7);
    float rim=exp(-pow((r-.87)/.075,2));
    float3 depth=lerp(float3(.003,.007,.012),float3(.038,.065,.078),r*r);
    depth+=float3(.05,.105,.11)*wisp*pow(r,2)*(.35+.65*n);
    depth+=rim*_BaseColor.rgb*(.45+.55*n);
    return half4(depth,core);
   }
   ENDHLSL
  }
 }
}
