Shader "TrickalFanGame/Hit Spark"
{
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
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS); o.uv=v.uv; o.color=v.color; return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                // uv.x runs across a soft-edged shape: 0.5 on its spine, 0 and 1 on its edges.
                // uv.y is how far the spine whitens; the vertex colour gives the rest.
                float edge = abs(i.uv.x * 2 - 1);
                float core = 1 - smoothstep(0.15, 0.6, edge);
                return half4(lerp(i.color.rgb, half3(1,1,0.93), core*i.uv.y),
                    (1-smoothstep(0.55,1,edge))*i.color.a);
            }
            ENDHLSL
        }
    }
}
