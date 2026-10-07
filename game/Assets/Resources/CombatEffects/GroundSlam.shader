Shader "TrickalFanGame/Ground Slam"
{
    Properties { [PerRendererData] _MainTex ("Sprite", 2D) = "white" {} }
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
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _ClipRect;
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; float2 world:TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                SetUpSpriteInstanceProperties();
                v.positionOS=UnityFlipSprite(v.positionOS,unity_SpriteProps.xy);
                Varyings o; float3 world=TransformObjectToWorld(v.positionOS);
                o.positionCS=TransformWorldToHClip(world); o.world=world.xy; o.uv=v.uv; o.color=v.color; return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                clip(i.world-_ClipRect.xy);
                clip(_ClipRect.zw-i.world);
                return SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*i.color;
            }
            ENDHLSL
        }
    }
}
