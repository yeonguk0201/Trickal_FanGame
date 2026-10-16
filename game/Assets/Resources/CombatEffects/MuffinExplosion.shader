Shader "TrickalFanGame/Muffin Explosion"
{
    Properties
    {
        _Mode ("0 flash, 1 fireball, 2 ring, 3 spark, 4 smoke, 5 ember", Float) = 0
        _Opacity ("Layer opacity", Float) = 1
        _Age ("Normalized layer age", Float) = 0
        _Hole ("Fireball hollow radius", Float) = 0
        _Ring ("Ring center radius", Float) = 0.5
        _Thickness ("Ring half thickness", Float) = 0.05
        _Erode ("Ring erosion", Float) = 0
        _Seed ("Noise seed", Float) = 0
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
            CBUFFER_START(UnityPerMaterial)
            float _Mode, _Opacity, _Age, _Hole, _Ring, _Thickness, _Erode, _Seed;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; half4 color:COLOR; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS=TransformObjectToHClip(v.positionOS);
                o.world=TransformObjectToWorld(v.positionOS).xy;
                o.uv=v.uv; o.color=v.color;
                return o;
            }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p)
            {
                float2 b=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(b),Hash(b+float2(1,0)),f.x),
                            lerp(Hash(b+float2(0,1)),Hash(b+1),f.x),f.y);
            }
            float Fbm(float2 p) { return Noise(p)*0.62+Noise(p*2.13+7.1)*0.38; }
            // Heat 1 is the white core; 0 the dark red edge of the flame.
            half3 Fire(float heat)
            {
                half3 c=lerp(half3(0.55,0.08,0.04),half3(0.93,0.27,0.05),smoothstep(0.0,0.35,heat));
                c=lerp(c,half3(1.0,0.62,0.12),smoothstep(0.3,0.6,heat));
                c=lerp(c,half3(1.0,0.9,0.42),smoothstep(0.55,0.82,heat));
                return lerp(c,half3(1.0,0.99,0.88),smoothstep(0.82,1.0,heat));
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 q=i.uv*2-1;
                float d=length(q);
                float aa=max(fwidth(d),0.01);
                if (_Mode<0.5)
                {
                    // Hot core flash: a short radial bloom, opaque only near its center.
                    float glow=pow(saturate(1-d),2.2);
                    return half4(lerp(half3(1,0.72,0.2),half3(1,0.98,0.8),saturate(glow*1.4)),
                        saturate(glow*1.5)*_Opacity);
                }
                if (_Mode<1.5)
                {
                    // Lumpy fireball. Its edge billows, then a ragged hollow eats it from the center out
                    // so the hit enemy is uncovered well before the effect ends.
                    float2 drift=float2(_Seed*3.7,_Seed*5.3);
                    float n=Fbm(q*2.6+drift+_Age*1.4);
                    float boundary=0.8+(n-0.5)*0.34;
                    float body=1-smoothstep(boundary-aa,boundary+aa,d);
                    float m=Fbm(q*3.4-drift+_Age*2.2);
                    float hollow=smoothstep(_Hole-aa-0.02,_Hole+aa+0.02,d+(m-0.5)*0.42);
                    float heat=saturate(1-d/boundary)*(1.0-_Age*0.6);
                    // Torn inner rim keeps burning as the hole widens.
                    float rim=_Hole>0.01?(1-smoothstep(0,0.16,d+(m-0.5)*0.42-_Hole)):0;
                    heat=saturate(heat+rim*0.35*(1-_Age)+(n-0.5)*0.3);
                    // A darker billow edge keeps the silhouette readable on the bright floor.
                    float edge=smoothstep(boundary-0.14,boundary-0.02,d);
                    return half4(Fire(heat)*(1-edge*0.3),body*hollow*_Opacity);
                }
                if (_Mode<2.5)
                {
                    // Shockwave: a thin band whose outer edge never passes the quad (the damage radius).
                    float band=1-smoothstep(_Thickness-aa,_Thickness+aa,abs(d-_Ring));
                    float n=Noise(q*7+_Seed*11.3);
                    band*=smoothstep(_Erode-0.12,_Erode+0.12,n);
                    float inner=saturate((_Ring-d)/max(_Thickness,0.001)*0.5+0.5);
                    half3 c=lerp(half3(1.0,0.97,0.78),half3(1.0,0.58,0.16),saturate(1-inner+0.25));
                    return half4(c,band*_Opacity);
                }
                if (_Mode<3.5)
                {
                    // Spark: a soft bead with a white-hot center, stretched along its velocity by the renderer.
                    float core=1-smoothstep(0.0,0.55,d);
                    half3 c=lerp(i.color.rgb,half3(1,1,0.9),core*0.8);
                    return half4(c,(1-smoothstep(0.45,1,d))*i.color.a);
                }
                if (_Mode>4.5)
                {
                    // Ember: a solid glowing bead, hot yellow inside and a deeper rim that reads on bright floors.
                    float core=1-smoothstep(0.0,0.75,d);
                    half3 c=lerp(half3(0.86,0.24,0.06),lerp(i.color.rgb,half3(1,0.82,0.25),0.5),core);
                    return half4(c,(1-smoothstep(0.75,1,d))*i.color.a);
                }
                // Smoke: a soft puff whose outline breaks up with world-space noise.
                float s=Noise(i.world*3.1+_Seed)+Noise(i.world*6.7)*0.5;
                float puff=1-smoothstep(0.35,1,d+(s-0.75)*0.35);
                return half4(i.color.rgb*(0.85+0.3*s),puff*i.color.a);
            }
            ENDHLSL
        }
    }
}
