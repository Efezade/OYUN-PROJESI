Shader "Hidden/TacticalRPG/WorldFogComposite"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_WorldFogField); SAMPLER(sampler_WorldFogField);
            float4 _WorldFogBounds, _WorldFogSettings, _WorldFogTint;

            float4 Field(float2 p)
            {
                float2 axial = float2(.577350269*p.x-p.y/3, p.y*2/3) / _WorldFogSettings.x;
                float2 uv = (axial-_WorldFogBounds.xy+.5)/_WorldFogBounds.zw;
                return SAMPLE_TEXTURE2D_LOD(_WorldFogField,sampler_WorldFogField,uv,0);
            }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p)
            {
                float2 a=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(a),Hash(a+float2(1,0)),f.x),
                    lerp(Hash(a+float2(0,1)),Hash(a+1),f.x),f.y);
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 uv=input.texcoord;
                half4 scene=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv);
                float depth=SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    float nearDepth=1, farDepth=.00001;
                #else
                    depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
                    float nearDepth=UNITY_NEAR_CLIP_VALUE, farDepth=.99999;
                #endif
                float3 nearWorld=ComputeWorldSpacePosition(uv,nearDepth,UNITY_MATRIX_I_VP);
                float3 farWorld=ComputeWorldSpacePosition(uv,farDepth,UNITY_MATRIX_I_VP);
                float3 direction=normalize(farWorld-nearWorld);
                if(direction.y>-.015) return scene;
                float3 surface=ComputeWorldSpacePosition(uv,depth,UNITY_MATRIX_I_VP);
                float groundDistance=(_WorldFogSettings.w-nearWorld.y)/direction.y;
                float distanceToSurface=dot(surface-nearWorld,direction);
                float end=max(0,min(groundDistance,distanceToSurface));
                float begin=max(0,(_WorldFogSettings.y-nearWorld.y)/direction.y);
                float step=max(0,end-begin)/8;
                float optical=0;
                [unroll] for(int n=0;n<8;n++)
                {
                    float3 p=nearWorld+direction*(begin+step*(n+.5));
                    float density=smoothstep(.02,.99,Field(p.xz).a);
                    float height=1-smoothstep(_WorldFogSettings.y*.45,_WorldFogSettings.y,p.y);
                    optical+=-log(max(.001,1-density))*height*step*_WorldFogSettings.z;
                }
                float3 ground=nearWorld+direction*groundDistance;
                float4 field=Field((nearWorld+direction*end).xz);
                // Keep the explored play area readable: mist in front of the camera must not
                // spill backwards over a tile the player has already cleared (especially at night).
                float opacity=(1-exp(-optical))*smoothstep(.015,.48,field.a);
                // Silhouettes remain veiled even when an authored model rises above the cloud
                // layer. Fully unexplored geometry cannot expose its crown or roof.
                opacity=max(opacity,smoothstep(.015,.995,field.a));
                float2 drift=_Time.y*float2(.032,.017);
                float cloud=Noise(ground.xz*.14+drift)*.68+Noise(ground.xz*.38-drift*.7)*.32;
                float3 tint=_WorldFogTint.rgb*lerp(.80,1.19,cloud);
                // Preserve authored corruption/alarm tint without black seams at the island edge.
                float3 tintOffset=field.rgb-float3(.29,.36,.36);
                tint+=tintOffset*.12*saturate(field.a*3);
                return half4(lerp(scene.rgb,max(.015,tint),saturate(opacity)),scene.a);
            }
            ENDHLSL
        }
    }
}
