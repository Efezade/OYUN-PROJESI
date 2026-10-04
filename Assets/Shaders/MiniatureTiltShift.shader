// MİNYATÜR (tilt-shift) efekti — bkz TacticalRPG.Core.MiniatureEffect.
//
// URP'nin kendi Depth of Field'ı ORTOGRAFİK kamerada çalışmıyor (derinliği perspektif formülüyle
// doğrusallaştırıyor), bizim kamera ise ortografik. Bu yüzden kendi geçişimiz:
//   Pass 0 — yatay Gauss bulanıklığı (tam çözünürlük → küçük doku)
//   Pass 1 — dikey Gauss bulanıklığı (küçük doku → küçük doku)
//   Pass 2 — birleştirme: her piksel, odak noktasına DERİNLİK farkına göre net ↔ bulanık karışır.
//            Uzak taraf tam bulanır, yakın taraf yalnız _MiniFocus.w kadar.
Shader "Hidden/TacticalRPG/MiniatureTiltShift"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        TEXTURE2D_X(_MiniatureBlurTex);
        float4 _MiniBlurTexel;  // xy: bir bulanıklık adımı (UV)
        float4 _MiniFocus;      // x: odak göz-derinliği, y: net bant yarı genişliği, z: geçiş, w: yakın güç
        float4 _MiniCamera;     // x: near, y: far, z: 1 = ortografik
        float  _MiniIntensity;  // 0..1 (ayar aç/kapa geçişi + savaş/overworld solması)

        // 9 vuruşluk Gauss — doğrusal örnekleme hilesiyle 5 okuma.
        half4 Blur(float2 uv, float2 dir)
        {
            float2 o1 = dir * 1.3846153846;
            float2 o2 = dir * 3.2307692308;
            half4 c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv) * 0.2270270270;
            c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + o1) * 0.3162162162;
            c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - o1) * 0.3162162162;
            c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + o2) * 0.0702702703;
            c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - o2) * 0.0702702703;
            return c;
        }

        // Ortografikte derinlik tamponu ZATEN doğrusal: near..far arası lerp. LinearEyeDepth
        // yalnız perspektif için doğru (URP DoF'un ortografikte bozulmasının sebebi bu).
        float MiniEyeDepth(float raw)
        {
            if (_MiniCamera.z > 0.5)
            {
            #if UNITY_REVERSED_Z
                raw = 1.0 - raw;
            #endif
                return lerp(_MiniCamera.x, _MiniCamera.y, raw);
            }
            return LinearEyeDepth(raw, _ZBufferParams);
        }

        half4 FragBlurH(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            return Blur(input.texcoord, float2(_MiniBlurTexel.x, 0.0));
        }

        half4 FragBlurV(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            return Blur(input.texcoord, float2(0.0, _MiniBlurTexel.y));
        }

        half4 FragComposite(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord;
            half4 sharp = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);
            half4 blur  = SAMPLE_TEXTURE2D_X(_MiniatureBlurTex, sampler_LinearClamp, uv);

            float d       = MiniEyeDepth(SampleSceneDepth(uv)) - _MiniFocus.x; // + uzak, - yakın
            float falloff = max(_MiniFocus.z, 1e-4);
            float farC    = saturate(( d - _MiniFocus.y) / falloff);
            float nearC   = saturate((-d - _MiniFocus.y) / falloff) * _MiniFocus.w;
            float coc     = smoothstep(0.0, 1.0, max(farC, nearC)) * _MiniIntensity;
            return lerp(sharp, blur, coc);
        }
        ENDHLSL

        Pass
        {
            Name "MiniatureBlurH"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlurH
            ENDHLSL
        }

        Pass
        {
            Name "MiniatureBlurV"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlurV
            ENDHLSL
        }

        Pass
        {
            Name "MiniatureComposite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite
            ENDHLSL
        }
    }
}
