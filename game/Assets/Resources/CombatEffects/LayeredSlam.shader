Shader "TrickalFanGame/Layered Slam"
{
    Properties { _MainTex ("Reviewed artwork", 2D) = "white" {} }
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
            float4 _UVRect, _ClipRect;
            float _Opacity, _Reveal, _CropFade;
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS=TransformObjectToHClip(v.positionOS);
                o.uv=v.uv; o.world=TransformObjectToWorld(v.positionOS).xy;
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                clip(i.uv.y-(1-_Reveal));
                float2 edge=min(i.world-_ClipRect.xy,_ClipRect.zw-i.world);
                clip(min(edge.x,edge.y));
                half4 color=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,_UVRect.xy+i.uv*_UVRect.zw);
                // A narrow antialiased room boundary, never the old 0.8-unit vertical blur.
                color.a *= _Opacity*smoothstep(0,0.035,min(edge.x,edge.y));
                if (_CropFade>0) color.a *= smoothstep(0,_CropFade,i.uv.x);
                return color;
            }
            ENDHLSL
        }
    }
}
