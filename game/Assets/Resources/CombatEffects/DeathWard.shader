Shader "TrickalFanGame/Death Ward"
{
    // 레비의 단도 (DeathWardGlow). Mode 0 draws a gold outline around the player drawing with a light band that
    // sweeps across the body; the quad covers the sprite rect plus a margin and samples only inside _UvRect, so
    // neighbouring frames of a sheet never bleed in. Modes 1-3 are the burst when the lethal hit is cancelled:
    // a gold-white flash, a gold ring and four-point sparkles. All values come per renderer.
    Properties
    {
        _MainTex ("Player sprite texture (mode 0)", 2D) = "white" {}
        _Mode ("0 outline, 1 flash, 2 ring, 3 sparkle", Float) = 0
        _Opacity ("Layer opacity", Float) = 1
        _UvRect ("Sprite rect in texture UV (min xy, max zw)", Vector) = (0, 0, 1, 1)
        _OutlineUv ("Outline width in texture UV", Vector) = (0.01, 0.01, 0, 0)
        _Shine ("Light band position across the body", Float) = 0.5
        _Ring ("Ring center radius", Float) = 0.5
        _Thickness ("Ring half thickness", Float) = 0.05
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
            CBUFFER_START(UnityPerMaterial)
            float _Mode, _Opacity, _Shine, _Ring, _Thickness;
            float4 _UvRect, _OutlineUv;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS); o.uv=v.uv; return o;
            }
            // Gold palette: deep rim, light inner rim, pale shine.
            static const half3 RimDeep=half3(0.96,0.66,0.12);
            static const half3 RimLight=half3(1.0,0.9,0.46);
            static const half3 Shine=half3(1.0,0.96,0.74);
            float SpriteAlpha(float2 uv)
            {
                float inside=step(_UvRect.x,uv.x)*step(uv.x,_UvRect.z)*step(_UvRect.y,uv.y)*step(uv.y,_UvRect.w);
                return SAMPLE_TEXTURE2D_LOD(_MainTex,sampler_MainTex,uv,0).a*inside;
            }
            half4 Outline(float2 uv)
            {
                float body=SpriteAlpha(uv);
                float outer=body, inner=body;
                [unroll] for (int n=0;n<12;n++)
                {
                    float angle=n*0.523599;
                    float2 offset=float2(cos(angle),sin(angle))*_OutlineUv.xy;
                    outer=max(outer,SpriteAlpha(uv+offset));
                    inner=max(inner,SpriteAlpha(uv+offset*0.5));
                }
                float rim=saturate(outer-body);
                float rimInner=saturate(inner-body);
                // A diagonal light band sweeps up the body; a faint gold tint stays under it.
                float2 p=(uv-_UvRect.xy)/max(_UvRect.zw-_UvRect.xy,1e-5);
                float s=p.y*0.8+p.x*0.4;
                float band=exp(-pow((s-_Shine)/0.08,2));
                float shine=body*(0.1+0.5*band);
                half3 rimColor=lerp(RimDeep,RimLight,saturate(rimInner*1.4));
                float alpha=saturate(rim+shine);
                half3 color=(rimColor*rim+Shine*shine)/max(rim+shine,1e-4);
                return half4(color,alpha*_Opacity);
            }
            half4 Frag(Varyings i):SV_Target
            {
                if (_Mode<0.5) return Outline(i.uv);
                float2 q=i.uv*2-1;
                float d=length(q);
                float aa=max(fwidth(d),0.01);
                if (_Mode<1.5)
                {
                    // White core fading to gold: a short bloom, opaque only near its center.
                    float glow=pow(saturate(1-d),2.2);
                    return half4(lerp(RimLight,half3(1,1,0.96),saturate(glow*1.6)),glow*_Opacity);
                }
                if (_Mode<2.5)
                {
                    // Gold band whose outer edge sits at _Ring + _Thickness, brighter on its outside.
                    float band=1-smoothstep(_Thickness-aa,_Thickness+aa,abs(d-_Ring));
                    float edge=saturate((d-_Ring+_Thickness)/max(2*_Thickness,1e-4));
                    return half4(lerp(RimDeep,RimLight,edge),band*_Opacity);
                }
                // Four-point sparkle: two thin crossed spikes around a soft core.
                float2 a=abs(q);
                float spikes=saturate(1-(a.x*a.y*18+max(a.x,a.y)*0.9));
                float core=pow(saturate(1-d*1.8),2);
                float shape=saturate(max(spikes,core));
                return half4(lerp(RimLight,half3(1,1,0.92),core),shape*_Opacity);
            }
            ENDHLSL
        }
    }
}
