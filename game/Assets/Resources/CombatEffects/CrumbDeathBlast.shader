Shader "TrickalFanGame/Crumb Death Blast"
{
    Properties
    {
        _Mode ("0 flash, 1 puff, 2 ring, 3 droplet, 4 crumb, 5 smoke", Float) = 0
        _Opacity ("Layer opacity", Float) = 1
        _Age ("Normalized layer age", Float) = 0
        _Hole ("Puff hollow radius", Float) = 0
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
            // The palette of effect-crumb-death: candy pink with a maroon outline and creamy highlights.
            static const half3 Outline=half3(0.45,0.08,0.13);
            static const half3 PinkDeep=half3(0.93,0.5,0.68);
            static const half3 Pink=half3(1.0,0.72,0.82);
            static const half3 PinkLight=half3(1.0,0.9,0.93);
            half4 Frag(Varyings i):SV_Target
            {
                float2 q=i.uv*2-1;
                float d=length(q);
                float aa=max(fwidth(d),0.01);
                if (_Mode<0.5)
                {
                    // Pink-white flash: a short bloom, opaque only near its center.
                    float glow=pow(saturate(1-d),2.0);
                    return half4(lerp(half3(1,0.62,0.8),half3(1,0.97,0.96),saturate(glow*1.5)),
                        saturate(glow*1.6)*_Opacity);
                }
                if (_Mode<1.5)
                {
                    // Cartoon cloud: a ring of round bubbles around a central one, cel shaded like the drawing,
                    // with a maroon outline. A ragged hollow opens from the center so the art there stays readable.
                    float2 drift=float2(_Seed*3.7,_Seed*5.3);
                    float wobble=(Fbm(q*2.4+drift+_Age*1.2)-0.5)*0.08;
                    float field=d-0.5;
                    for (int k=0;k<7;k++)
                    {
                        float a=(k+Hash(float2(k,_Seed))*0.5)*0.8976+_Seed;
                        float2 c=float2(cos(a),sin(a))*0.44;
                        field=min(field,length(q-c)-(0.27+Hash(float2(_Seed,k))*0.08));
                    }
                    field+=wobble;
                    float body=1-smoothstep(-aa,aa,field);
                    float n=Fbm(q*2.2+drift+_Age*1.1);
                    float m=Fbm(q*3.2-drift+_Age*1.8);
                    float holeDistance=d+(m-0.5)*0.4-_Hole;
                    float hollow=_Hole>0.001?smoothstep(-aa,aa,holeDistance):1;
                    // Light from the upper left, deeper pink toward the lower right and inside the bubble seams.
                    float light=saturate(0.55+dot(q,float2(-0.45,0.5))*0.6+(n-0.5)*0.4+field*1.2);
                    half3 c=lerp(PinkDeep,Pink,smoothstep(0.3,0.42,light));
                    c=lerp(c,PinkLight,smoothstep(0.7,0.78,light)*(1-_Age*0.5));
                    float outline=smoothstep(-0.07-aa,-0.07+aa,field);
                    float innerRim=_Hole>0.001?1-smoothstep(0.05-aa,0.05+aa,holeDistance):0;
                    c=lerp(c,Outline,saturate(outline+innerRim*0.85));
                    return half4(c,body*hollow*_Opacity);
                }
                if (_Mode<2.5)
                {
                    // Shockwave: a thin band whose outer edge never passes the quad (the damage radius).
                    float band=1-smoothstep(_Thickness-aa,_Thickness+aa,abs(d-_Ring));
                    float n=Noise(q*7+_Seed*11.3);
                    band*=smoothstep(_Erode-0.12,_Erode+0.12,n);
                    float inner=saturate((_Ring-d)/max(_Thickness,0.001)*0.5+0.5);
                    half3 c=lerp(PinkLight,half3(0.95,0.36,0.6),saturate(1-inner+0.35));
                    return half4(c,band*_Opacity);
                }
                if (_Mode<3.5)
                {
                    // Goo droplet: a glossy pink bead with a maroon rim and a white highlight, stretched along
                    // its velocity by the renderer.
                    float fill=1-smoothstep(0.82-aa,0.82+aa,d);
                    float rim=smoothstep(0.6-aa,0.6+aa,d);
                    float shine=1-smoothstep(0.12,0.24,length(q-float2(-0.22,0.26)));
                    half3 c=lerp(i.color.rgb,Outline,rim);
                    c=lerp(c,half3(1,1,1),shine*0.9);
                    return half4(c,fill*i.color.a);
                }
                if (_Mode<4.5)
                {
                    // Crumb: a chunky square-ish bite of cake with a lumpy edge, brown outline and a light top.
                    float box=max(abs(q.x),abs(q.y));
                    float shape=lerp(d,box,0.55)+(Noise(q*4.5+i.color.a*31.0)-0.5)*0.22;
                    float fill=1-smoothstep(0.78-aa,0.78+aa,shape);
                    float rim=smoothstep(0.6-aa,0.6+aa,shape);
                    float top=smoothstep(-0.2,0.35,q.y-q.x*0.3);
                    half3 c=lerp(half3(0.86,0.58,0.27),i.color.rgb,top);
                    c=lerp(c,half3(0.42,0.2,0.08),rim);
                    return half4(c,fill);
                }
                // Smoke: a soft pink puff whose outline breaks up with world-space noise.
                float s=Noise(i.world*3.1+_Seed)+Noise(i.world*6.7)*0.5;
                float puff=1-smoothstep(0.35,1,d+(s-0.75)*0.35);
                return half4(i.color.rgb*(0.9+0.15*s),puff*i.color.a);
            }
            ENDHLSL
        }
    }
}
