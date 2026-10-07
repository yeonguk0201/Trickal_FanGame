Shader "TrickalFanGame/Charge Warning"
{
    Properties { [PerRendererData] _MainTex ("Arrow", 2D) = "white" {} }
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
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes v)
            { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS); o.uv=v.uv; return o; }
            half4 Frag(Varyings i):SV_Target
            {
                // One group of three advances together, followed by a gap before the next group.
                float group=frac((i.uv.x-_Time.y*0.21+0.266667)/1.5)*1.5-0.266667;
                float chevron=group*3+abs(i.uv.y-0.5)*1.6;
                float cycle=frac(chevron);
                half arrow=smoothstep(0.20,0.22,cycle)*(1-smoothstep(0.78,0.80,cycle))*0.78;
                arrow*=step(0.0,chevron)*step(chevron,3.0);
                float tip=1-saturate((i.uv.x-0.87)/0.13);
                clip(min(i.uv.y,1-i.uv.y)-0.5*(1-tip));
                half border=step(i.uv.y,0.04)+step(0.96,i.uv.y);
                half background=lerp(0.18,0.6,saturate(border));
                half alpha=arrow+background*(1-arrow);
                half3 color=(half3(1,0.04,0.02)*arrow+half3(1,0.53,0.12)*background*(1-arrow))/max(alpha,0.001);
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
