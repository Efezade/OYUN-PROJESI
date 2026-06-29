// Karo kesim shader'ı (URP): karoyu, verilen YEREL kutunun dışında kalan parçalardan keser.
// Her küp yüzü kendi kesim çerçevesini (_TileClipW2L) + yanal sınırını (_TileClipExtent) verir;
// normal (yükseklik) ekseni geniş bırakılır → karo yüksekliği (3D ağaç vb.) korunur, sadece
// yanal kenarlar düz kesilir. Çift taraflı (dönerken arka yüz görünür). Basit ana-ışık aydınlatması.
Shader "TacticalRPG/TileClip"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Cull Off

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
            CBUFFER_END

            TEXTURE2D(_BaseMap);  SAMPLER(sampler_BaseMap);

            // Per-yüz kesim çerçevesi (MaterialPropertyBlock ile / global olarak set edilir).
            float4x4 _TileClipW2L;
            float3   _TileClipExtent;

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings   { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS   = TransformObjectToWorldNormal(v.normalOS);
                o.uv         = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Kesim: dünya konumunu yüzün yerel uzayına al, kutunun dışındaysa at.
                float3 lp = mul(_TileClipW2L, float4(i.positionWS, 1.0)).xyz;
                float3 d  = abs(lp) - _TileClipExtent;
                clip(-max(max(d.x, d.y), d.z));

                half4 baseCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;

                Light mainLight = GetMainLight();
                half ndl   = saturate(dot(normalize(i.normalWS), mainLight.direction));
                half3 col  = baseCol.rgb * (mainLight.color * ndl * 0.75 + 0.45); // basit diffuse + ambient
                return half4(col, baseCol.a);
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}
