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
        [PerRendererData] _DoorBaseTex ("Original door surroundings", 2D) = "white" {}
        [PerRendererData] _UseDoorBase ("Blend special doorway joints", Float) = 0
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
            TEXTURE2D(_DoorBaseTex);
            SAMPLER(sampler_DoorBaseTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _PatchRegion;
                float4 _TextureRepeat;
                float _ForegroundCutoff;
                float _UseForegroundMask;
                float _UseDoorBase;
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
                if (_UseDoorBase > 0.5) {
                    // Generated variants can slightly change surrounding grass/stone colors.
                    // Register their perimeter to the original patch, leaving the gate center intact.
                    // Use a narrow exterior blend so projecting crowns/horns stay fully visible.
                    float xBand = _PatchRegion.x < 0.2 ? (local.x < 0.5 ? 2 : 32) :
                        _PatchRegion.x > 0.8 ? (local.x > 0.5 ? 2 : 32) : 12;
                    float yBand = _PatchRegion.y < 0.05 ? (local.y < 0.5 ? 2 : 12) :
                        _PatchRegion.y > 0.75 ? (local.y > 0.5 ? 2 : 12) : 12;
                    float2 joint = smoothstep(0, float2(xBand, yBand) / (_PatchRegion.zw * float2(1672, 941)), edge);
                    half4 basePixel = SAMPLE_TEXTURE2D(_DoorBaseTex, sampler_DoorBaseTex, input.uv);
                    pixel = lerp(basePixel, pixel, min(joint.x, joint.y));
                }
                if (_UseForegroundMask > 0.5) {
                    half maskOriginal = SAMPLE_TEXTURE2D(_ForegroundMask, sampler_ForegroundMask, input.uv).a;
                    half maskRepeated = SAMPLE_TEXTURE2D(_ForegroundMask, sampler_ForegroundMask,
                        _PatchRegion.xy + reflected * _PatchRegion.zw).a;
                    pixel.a *= lerp(maskOriginal, maskRepeated, min(blend.x, blend.y));
                }
                // Suppress low-alpha masking remnants and saturated key-color speckles.
                clip(pixel.a - 0.25h);
                if (pixel.g > 0.85h && pixel.r < 0.35h && pixel.b < 0.25h) discard;
                // Keep shaded ruby-red treasure gems; reject only near-pure red key-color remnants.
                if (pixel.r > 0.97h && pixel.g < 0.05h && pixel.b < 0.05h) discard;
                return pixel;
            }
            ENDHLSL
        }
    }
}
