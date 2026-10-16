Shader "TrickalFanGame/Clear Weather Lightning"
{
    Properties { _Opacity ("Opacity", Float) = 1 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Opacity;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS); o.uv=v.uv; return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float edge = abs(i.uv.y * 2 - 1);
                float core = 1 - smoothstep(0.2, 0.65, edge);
                return half4(lerp(half3(0.42,0.72,1), half3(1,1,0.93), core),
                    (1-smoothstep(0.55,1,edge))*_Opacity);
            }
            ENDHLSL
        }
    }
}
