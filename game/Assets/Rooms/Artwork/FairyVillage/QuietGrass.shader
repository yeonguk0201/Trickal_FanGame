Shader "TrickalFanGame/Quiet Grass"
{
    Properties
    {
        [PerRendererData] _MainTex ("Grass atlas", 2D) = "white" {}
        _GroundColor ("Ground tone", Color) = (0.42, 0.55, 0.20, 1)
        _DetailStrength ("Texture detail", Range(0, 1)) = 0.38
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
            CBUFFER_START(UnityPerMaterial)
                half4 _GroundColor;
                half _DetailStrength;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 grass = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                return half4(lerp(_GroundColor.rgb, grass.rgb, _DetailStrength) * input.color.rgb,
                    grass.a * input.color.a);
            }
            ENDHLSL
        }
    }
}
