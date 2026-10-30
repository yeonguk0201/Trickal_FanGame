Shader "TrickalFanGame/Champion Outline"
{
    // Variant-1: a flat silhouette in the champion colour. EnemyChampionOutline draws offset copies of the body
    // behind it, so the silhouette shows only as a ring around the body, on tightly packed frames and deformed
    // movement meshes alike.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        [PerRendererData] _OutlineColor ("Outline colour", Color) = (1,1,1,1)
    }
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
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            half4 _OutlineColor;
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS); o.uv = v.uv; return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                // A hard edge keeps the ring crisp where the copies overlap.
                return half4(_OutlineColor.rgb, saturate(a * 2) * _OutlineColor.a);
            }
            ENDHLSL
        }
    }
}
