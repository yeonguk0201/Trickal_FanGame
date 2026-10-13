Shader "TrickalFanGame/Melee Warning"
{
    Properties { [PerRendererData] _MainTex ("Unused", 2D) = "white" {} }
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
            // x length, y width, z corner radius, w near-end fade length, all in world units.
            float4 _Shape;
            // x fill progress toward the hit, y direction-lock flash.
            float4 _State;
            float4 _Tint;
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes v)
            { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS); o.uv=v.uv; return o; }
            half4 Frag(Varyings i):SV_Target
            {
                // Rounded box in world units, so the outline keeps its thickness at any size.
                float2 extent=_Shape.xy*0.5;
                float2 p=(i.uv-0.5)*_Shape.xy;
                float2 q=abs(p)-extent+_Shape.z;
                float d=length(max(q,0))+min(max(q.x,q.y),0)-_Shape.z;
                float aa=max(fwidth(d),0.0001);
                half inside=saturate(0.5-d/aa);
                // The fill sweeps from the attacker to the far end and arrives when the hit lands.
                float along=p.x+extent.x;
                float front=_State.x*_Shape.x;
                half filled=saturate((front-along)/aa+0.5);
                half lead=exp(-abs(along-front)*14)*step(_State.x,0.999)*0.35;
                // The lock thickens the outline inward instead of growing the area past the real hit range.
                float thickness=0.03*(1+_State.y*2);
                half outline=inside*saturate(0.5+(d+thickness)/aa);
                half rim=exp(d*5)*0.22;
                half alpha=max(inside*(lerp(0.12,0.36,filled)+rim+lead),outline*0.92);
                if(_Shape.w>0) alpha*=lerp(0.2,1,smoothstep(0,_Shape.w,along));
                half3 color=lerp(_Tint.rgb,half3(1,1,1),saturate(outline*0.35+lead+_State.y*0.75));
                return half4(color,saturate(alpha*(1+_State.y*0.9)));
            }
            ENDHLSL
        }
    }
}
