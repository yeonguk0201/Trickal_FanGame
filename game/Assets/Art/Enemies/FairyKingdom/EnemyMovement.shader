Shader "TrickalFanGame/Enemy Movement"
{
    Properties
    {
        [PerRendererData] _MainTex ("Enemy artwork", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _HitStrength ("Hit flash", Range(0,1)) = 0
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
            float4 _MainTex_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _HitStrength;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Tint;
                float2 d = _MainTex_TexelSize.xy * 3;
                half neighbour = min(
                    min(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(d.x,0)).a,
                        SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(d.x,0)).a),
                    min(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(0,d.y)).a,
                        SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(0,d.y)).a));
                half contour = saturate((color.a - neighbour) * 4);
                color.rgb = lerp(color.rgb, half3(1,0.03,0.025), _HitStrength * lerp(0.32,1,contour));
                color.a *= lerp(1,0.75,_HitStrength * (1-contour));
                return color;
            }
            ENDHLSL
        }
    }
}
