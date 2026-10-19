Shader "TrickalFanGame/Bomb Fuse Flash"
{
    // A red-hot copy of the bomb drawing, laid over it at _Strength (set per renderer: Unity 6 sprite renderers
    // do not hand their color to custom shaders through vertex color).
    // It keeps the drawing's shading, so the bomb reads as glowing red rather than a flat pink sticker.
    Properties { [PerRendererData] _MainTex ("Sprite", 2D) = "white" {} _Strength ("Flash strength", Float) = 0 }
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
            CBUFFER_START(UnityPerMaterial)
            float _Strength;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS); o.uv=v.uv; o.color=v.color; return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half4 t=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
                half luma=dot(t.rgb,half3(0.3,0.59,0.11));
                // Dark iron turns deep red; highlights heat toward orange.
                half3 hot=lerp(half3(0.62,0.04,0.03),half3(1.0,0.32,0.12),saturate(luma*1.6));
                return half4(hot,t.a*_Strength);
            }
            ENDHLSL
        }
    }
}
