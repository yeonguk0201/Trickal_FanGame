Shader "TrickalFanGame/Slam Light"
{
    Properties { _MainTex ("Painted blade sheet", 2D) = "white" {} }
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
            float4 _ClipRect;
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            float _Strength, _Frame, _Reverse, _EdgeFadeDistance, _EndFade;
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float2 ground:TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS);
                o.uv=v.uv;
                o.ground=TransformObjectToWorld(v.positionOS).xy;
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                // Fade the actual artwork near room edges instead of uniformly shrinking or sharply clipping it.
                float u=lerp(i.uv.x,1-i.uv.x,_Reverse);
                float2 cell=float2(fmod(_Frame,4),1-floor(_Frame/4));
                float2 local=clamp(float2(u,i.uv.y),_MainTex_TexelSize.xy*float2(2,1),
                    1-_MainTex_TexelSize.xy*float2(2,1));
                half4 color=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,(cell+local)/float2(4,2));
                float2 edgeDistance=min(i.ground-_ClipRect.xy,_ClipRect.zw-i.ground);
                float roomFade=smoothstep(0,_EdgeFadeDistance,min(edgeDistance.x,edgeDistance.y));
                float progress=lerp(1-i.uv.x,i.uv.x,_Reverse);
                float tipFade=smoothstep(0,_EndFade,1-progress);
                color.a *= _Strength * roomFade * tipFade;
                return color;
            }
            ENDHLSL
        }
    }
}
