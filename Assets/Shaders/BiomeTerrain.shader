Shader "TacticalRPG/Biome Terrain"
{
    Properties
    {
        [MainTexture] _BaseMap("Colour atlas",2D)="white"{}
        [MainColor] _BaseColor("Fog / corruption multiplier",Color)=(1,1,1,1)
        _Cull("Cull",Float)=0
        _UseHeightMask("Legacy authored mesh mask",Float)=0
        _GroundReference("Ground luminance reference",Float)=.095
        _Cutoff("Alpha Cutoff",Range(0,1))=.5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST, _BaseColor;
            float _UseHeightMask, _GroundReference, _Cull, _Cutoff;
            float4 _BiomeCenter, _BiomeGround, _BiomeFoliage, _BiomeStone, _BiomeStrength;
            float4 _Neighbour0, _Neighbour1, _Neighbour2, _Neighbour3, _Neighbour4, _Neighbour5;
            float4 _Leaf0, _Leaf1, _Leaf2, _Leaf3, _Leaf4, _Leaf5;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;float4 mask:COLOR;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;float3 mask:TEXCOORD3;float fog:TEXCOORD4;};
            Varyings Vert(Attributes i)
            {
                Varyings o;o.world=TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(i.normalOS);
                o.uv=TRANSFORM_TEX(i.uv,_BaseMap);o.mask=i.mask.rgb;
                o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            void AddNeighbour(float2 delta,float2 direction,float4 colour,float4 leaves,
                inout float3 soil,inout float3 foliage,inout float sum,float scale)
            {
                // Soft Voronoi weights: equal colour mix exactly on a shared hex edge,
                // and a three-way mix at shared vertices, independent of random layout.
                float spacing=1.7320508*_BiomeCenter.w;
                float difference=spacing*spacing-2*spacing*dot(delta,direction);
                float w=exp2(-max(0,difference)/scale)*colour.a;
                soil+=colour.rgb*w;foliage+=leaves.rgb*w;sum+=w;
            }
            half4 Frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC):SV_Target
            {
                float3 original=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
                float3 mask=saturate(i.mask);
                if(_UseHeightMask>.5)
                {
                    float low=1-smoothstep(.36,.58,i.world.y-_BiomeCenter.y);
                    float green=smoothstep(.005,.055,original.g-max(original.r*.9,original.b));
                    mask=float3(low,green*(1-low),0);
                }
                float3 soil=_BiomeGround.rgb,foliage=_BiomeFoliage.rgb;float sum=1;
                float2 delta=i.world.xz-_BiomeCenter.xz;
                float variation=.9+.1*sin(i.world.x*5.1+sin(i.world.z*4.7));
                float scale=max(.04,_BiomeGround.a*variation*_BiomeCenter.w);
                AddNeighbour(delta,float2(1,0),_Neighbour0,_Leaf0,soil,foliage,sum,scale);
                AddNeighbour(delta,float2(.5,-.8660254),_Neighbour1,_Leaf1,soil,foliage,sum,scale);
                AddNeighbour(delta,float2(-.5,-.8660254),_Neighbour2,_Leaf2,soil,foliage,sum,scale);
                AddNeighbour(delta,float2(-1,0),_Neighbour3,_Leaf3,soil,foliage,sum,scale);
                AddNeighbour(delta,float2(-.5,.8660254),_Neighbour4,_Leaf4,soil,foliage,sum,scale);
                AddNeighbour(delta,float2(.5,.8660254),_Neighbour5,_Leaf5,soil,foliage,sum,scale);
                soil/=sum;foliage/=sum;
                float lum=dot(original,float3(.2126,.7152,.0722));
                float detail=clamp(lum/max(.025,_GroundReference),.85,1.15);
                float3 albedo=lerp(original,soil*detail,mask.r*_BiomeStrength.x);
                albedo=lerp(albedo,foliage*clamp(lum/.09,.8,1.2),mask.g*_BiomeStrength.y);
                albedo=lerp(albedo,_BiomeStone.rgb*clamp(lum/.10,.65,1.3),mask.b*_BiomeStrength.z);
                float3 n=IS_FRONT_VFACE(face,normalize(i.normal),-normalize(i.normal));
                Light main=GetMainLight(TransformWorldToShadowCoord(i.world));
                float diffuse=saturate(dot(n,main.direction));
                float3 light=SampleSH(n)+main.color*diffuse*main.shadowAttenuation;
                float3 rgb=albedo*_BaseColor.rgb*light;
                return half4(MixFog(rgb,i.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
