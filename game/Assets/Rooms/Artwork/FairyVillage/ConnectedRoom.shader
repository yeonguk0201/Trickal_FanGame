Shader "TrickalFanGame/Connected Room"
{
    Properties
    {
        [PerRendererData] _MainTex ("Room master", 2D) = "white" {}
        [PerRendererData] _PatchRegion ("Registered atlas region", Vector) = (0,0,1,1)
        [PerRendererData] _TextureRepeat ("Native texture scale and edge blend", Vector) = (1,1,0.02,0.02)
        [PerRendererData] _ForegroundCutoff ("Foreground upper edge", Float) = 1
        [PerRendererData] _ForegroundMask ("Transparent wall silhouette", 2D) = "white" {}
        [PerRendererData] _UseForegroundMask ("Use wall silhouette", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_ForegroundMask);
            SAMPLER(sampler_ForegroundMask);
            CBUFFER_START(UnityPerMaterial)
                float4 _PatchRegion;
                float4 _TextureRepeat;
                float _ForegroundCutoff;
                float _UseForegroundMask;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input) {
                Varyings output; output.positionCS = TransformObjectToHClip(input.positionOS); output.uv = input.uv; return output; }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 local = saturate((input.uv - _PatchRegion.xy) / _PatchRegion.zw);
                clip(_ForegroundCutoff - local.y);
                float2 reflected = 1 - abs(frac(local * _TextureRepeat.xy * 0.5) * 2 - 1);
                float2 edge = min(local, 1 - local);
                float2 blend = smoothstep(0, _TextureRepeat.zw / _TextureRepeat.xy, edge);
                // Blend sampled colors, not distant UV coordinates: UV interpolation creates
                // a compressed streak across the atlas near the registered joints.
                half4 original = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 repeated = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, _PatchRegion.xy + reflected * _PatchRegion.zw);
                half4 pixel = lerp(original, repeated, min(blend.x, blend.y));
                if (_UseForegroundMask > 0.5) {
                    half maskOriginal = SAMPLE_TEXTURE2D(_ForegroundMask, sampler_ForegroundMask, input.uv).a;
                    half maskRepeated = SAMPLE_TEXTURE2D(_ForegroundMask, sampler_ForegroundMask,
                        _PatchRegion.xy + reflected * _PatchRegion.zw).a;
                    pixel.a *= lerp(maskOriginal, maskRepeated, min(blend.x, blend.y));
                }
                // Suppress low-alpha masking remnants and saturated key-color speckles.
                clip(pixel.a - 0.25h);
                if (pixel.g > 0.85h && pixel.r < 0.35h && pixel.b < 0.25h) discard;
                if (pixel.r > 0.85h && pixel.g < 0.2h && pixel.b < 0.2h) discard;
                return pixel;
            }
            ENDHLSL
        }
    }
}
