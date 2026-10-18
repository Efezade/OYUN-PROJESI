Shader "BattleTile/Seamless World Ground"
{
    Properties
    {
        [MainTexture] _BaseMap("Stone and Moss", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        _MetersPerTexture("World Metres Per Texture", Float) = 4
        _WorldOffset("World UV Offset", Vector) = (0,0,0,0)
        [Toggle] _Triplanar("Triplanar Cliff Projection", Float) = 0
        _Smoothness("Smoothness", Range(0,1)) = 0.12
        _Relief("Stone Micro Relief", Range(0,0.15)) = 0.018
        _HeightMap("Optional Height", 2D) = "gray" {}
        [Toggle] _UseHeightMap("Use Height Texture", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 250
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_HeightMap); SAMPLER(sampler_HeightMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseMap_TexelSize;
                half4 _BaseColor;
                float4 _WorldOffset;
                float _MetersPerTexture;
                float _Triplanar;
                half _Smoothness;
                float _Relief;
                float _UseHeightMap;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                half3 vertexLighting : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionWS = p.positionWS;
                output.positionCS = p.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(p.positionCS.z);
                output.vertexLighting = VertexLighting(p.positionWS, output.normalWS);
                return output;
            }
            half HeightAt(float2 uv)
            {
                if (_UseHeightMap > 0.5)
                    return SAMPLE_TEXTURE2D(_HeightMap, sampler_HeightMap, uv).r;
                half3 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;
                return dot(c, half3(0.3, 0.59, 0.11));
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float metres = max(_MetersPerTexture, 0.001);
                float3 worldUV = (input.positionWS + _WorldOffset.xyz) / metres;
                half3 normal = NormalizeNormalPerPixel(input.normalWS);
                half3 albedo;
                if (_Triplanar > 0.5)
                {
                    half3 weights = pow(abs(normal), 5.0);
                    weights /= max(dot(weights, half3(1,1,1)), 0.0001);
                    half3 x = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, worldUV.zy).rgb;
                    half3 y = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, worldUV.xz).rgb;
                    half3 z = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, worldUV.xy).rgb;
                    albedo = x * weights.x + y * weights.y + z * weights.z;
                }
                else
                {
                    float2 uv = worldUV.xz;
                    albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;
                    // A fixed world-space finite difference keeps both sides of
                    // each mesh boundary on the same normal/texture function.
                    float stepUV = max(_BaseMap_TexelSize.x * 2, 0.001);
                    half dx = HeightAt(uv + float2(stepUV,0)) - HeightAt(uv - float2(stepUV,0));
                    half dz = HeightAt(uv + float2(0,stepUV)) - HeightAt(uv - float2(0,stepUV));
                    normal = normalize(normal + float3(-dx,0,-dz) * (_Relief / (2 * stepUV * metres)));
                }
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo * _BaseColor.rgb;
                surface.alpha = 1;
                surface.metallic = 0;
                surface.specular = half3(0.04,0.04,0.04);
                surface.smoothness = _Smoothness;
                surface.normalTS = half3(0,0,1);
                surface.occlusion = 1;
                InputData data = (InputData)0;
                data.positionWS = input.positionWS;
                data.normalWS = normal;
                data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                data.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                data.fogCoord = input.fogFactor;
                data.vertexLighting = input.vertexLighting;
                data.bakedGI = SampleSH(normal);
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                data.shadowMask = half4(1,1,1,1);
                half4 color = UniversalFragmentPBR(data, surface);
                color.rgb = MixFog(color.rgb, data.fogCoord);
                return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 ShadowVert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDir = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif
                float4 clip = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
                #if UNITY_REVERSED_Z
                    clip.z = min(clip.z, clip.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    clip.z = max(clip.z, clip.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                return clip;
            }
            half4 ShadowFrag() : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 DepthVert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(input.positionOS.xyz);
            }
            half4 DepthFrag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
