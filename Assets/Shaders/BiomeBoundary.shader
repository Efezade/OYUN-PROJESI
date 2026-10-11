Shader "TacticalRPG/Biome Boundary"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-20" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float3 world : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.world = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.world);
                o.color = input.color; o.uv = input.uv; return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float grain = frac(sin(dot(floor(i.world.xz * 38), float2(12.9898,78.233))) * 43758.5453);
                float edge = 1 - smoothstep(.45 + grain * .1, 1, abs(i.uv.y));
                float seam = 1 - smoothstep(.04, .19, abs(i.uv.y));
                // Muted soil colour with a fine dark crease, feathered into each biome.
                Light main = GetMainLight();
                float3 light = SampleSH(float3(0,1,0)) + main.color * saturate(main.direction.y);
                return half4(i.color.rgb * light * (.83 + grain * .22) * (1 - seam * .38), i.color.a * edge);
            }
            ENDHLSL
        }
    }
}
