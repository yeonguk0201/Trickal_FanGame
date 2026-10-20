Shader "TrickalFanGame/Damage Aura"
{
    // 광기의 가면 (DamageAuraField). A pale red field on the floor whose edge wobbles like disturbed water, with
    // faint ripples flowing outward inside it and a brighter ring that runs out on every damage tick. The quad
    // spans the damage radius (uv 0-1 maps to -R..R) and the wobbling edge only ever moves inward from R, so the
    // drawing never claims more than the real damage range. All values come per renderer.
    Properties
    {
        _Clock ("Animation time (s)", Float) = 0
        _Opacity ("Overall opacity", Float) = 1
        _Wobble ("Edge wobble depth (share of radius)", Float) = 0.09
        _Energy ("Stack energy 0-1", Float) = 0
        _Pulse ("Tick ring progress 0-1 (outside 0-1: none)", Float) = -1
        _TickSeed ("Tick ring brush seed (fixed per tick)", Float) = 0
        _Layers ("Preview layers: fill, ripples, rim, tick ring", Vector) = (1, 1, 1, 1)
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
            float _Clock, _Opacity, _Wobble, _Energy, _Pulse, _TickSeed;
            float4 _Layers;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS); o.uv=v.uv; return o;
            }
            // Crimson palette: deep body, brighter rim, hot tick ring.
            static const half3 Deep=half3(0.82,0.02,0.03);
            static const half3 Rim=half3(0.93,0.05,0.05);
            static const half3 Hot=half3(0.8,0.0,0.02);
            static const float Tau=6.2831853;

            float Hash(float2 p)
            {
                p=frac(p*float2(123.34,456.21));
                p+=dot(p,p+45.32);
                return frac(p.x*p.y);
            }
            float ValueNoise(float2 p)
            {
                float2 i=floor(p), f=frac(p);
                f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            float Fbm(float2 p)
            {
                return 0.55*ValueNoise(p)+0.3*ValueNoise(p*2.13+7.3)+0.15*ValueNoise(p*4.37+3.1);
            }

            // Edge shape in [-1, 1]: three angular waves travelling at different speeds and directions so the
            // outline never settles into a regular star. Integer angular counts keep it seamless at the seam.
            float EdgeWave(float a, float t, float speed)
            {
                float w=0.5*sin(5*a+t*1.9*speed)
                       +0.3*sin(8*a-t*2.7*speed+1.3)
                       +0.2*sin(13*a+t*4.3*speed+2.1);
                // A slow swell lifts and drops whole sides, so the field also leans and breathes.
                w=w*0.8+0.2*sin(2*a-t*0.8*speed+0.5);
                return w;
            }

            half4 Frag(Varyings i):SV_Target
            {
                float2 q=i.uv*2-1;
                float d=length(q);
                if (d>1) return 0;
                float a=atan2(q.y,q.x);
                float t=_Clock;
                float speed=1+0.35*_Energy;

                // The edge sits between (0.985 - _Wobble) and 0.985 of the radius; the margin keeps its soft edge inside the quad.
                float edge=0.985-_Wobble*(0.5+0.5*EdgeWave(a,t,speed));
                float aa=max(fwidth(d)*1.2,0.004);
                float inside=1-smoothstep(edge-aa,edge+aa,d);
                float r=saturate(d/edge);

                // Body: nearly clear at the player, thicker toward the edge.
                float fill=lerp(0.05,0.2,pow(r,2.4));

                // Ripples: rings flowing outward, bent by angle so they wobble with the edge instead of
                // reading as a clean target. Kept away from the player in the middle.
                float bend=0.05*sin(3*a-t*1.3*speed)+0.035*sin(7*a+t*2.1*speed+0.7);
                float ring=sin(((r+bend)*3.4-t*0.75*speed)*Tau);
                float ripples=smoothstep(0.45,1,ring)*smoothstep(0.12,0.45,r)*(1-smoothstep(0.82,0.97,r));

                // Rim: a soft band just inside the wobbling edge with a thin bright lip on it.
                float band=exp(-pow((edge-d-0.05)/0.06,2));
                float lip=exp(-pow((edge-d-0.012)/0.016,2));
                float rim=saturate(band*0.55+lip*0.75);

                // Tick ring: a circular brush sweep. It bursts out fast and slows, keeps going through the
                // wobbling edge and vanishes where it crosses it, so the stroke slides out of the field instead
                // of stopping inside. Red is strongest at the soft front and thins toward the tail. The tail is
                // a brush stroke: its length differs by direction, bristle grain runs outward and its end
                // breaks up like a dry brush. Noise is sampled on the direction vector, so there is no seam,
                // and seeded per tick, so the stroke holds still while it travels.
                float tick=0;
                float tickHeat=0;
                if (_Pulse>=0 && _Pulse<1)
                {
                    // Two phases: a burst to 43% of the radius in the first 10% of the time, then a sudden drop
                    // to a slow, nearly even crawl out through the edge.
                    const float Burst=0.1;
                    const float Knee=0.43;
                    float front;
                    if (_Pulse<Burst)
                    {
                        float u=_Pulse/Burst;
                        front=0.1+(Knee-0.1)*u*(1.2-0.2*u);
                    }
                    else
                    {
                        float w=(_Pulse-Burst)/(1-Burst);
                        front=Knee+(1.3-Knee)*w*(1.3-0.3*w);
                    }
                    float p=saturate((front-0.1)/1.2);
                    float2 dir=q/max(d,1e-4);
                    float2 seed=float2(_TickSeed*7.13,_TickSeed*3.71);
                    float x=d-front;
                    float lengthNoise=Fbm(dir*2.2+seed);
                    float tailLength=lerp(0.14,0.5,p)*lerp(0.6,1.4,lengthNoise);
                    float along=saturate(-x/tailLength);
                    // Soft leading edge, then a long fade inward.
                    float lead=x>0?exp(-pow(x/0.05,2)):pow(1-along,1.1);
                    // Fine bristle streaks inside coarser clumps of paint.
                    float bristles=Fbm(dir*(34+x*2)+seed*1.7);
                    float clumps=Fbm(dir*9+seed*0.9);
                    float grain=lerp(1,lerp(0.25,1.15,bristles)*lerp(0.6,1.1,clumps),saturate(along*1.6+0.2));
                    float dry=smoothstep(along-0.35,along+0.1,Fbm(dir*(12+x*2)+seed*2.3));
                    float stroke=lead*grain*lerp(1,dry,along);
                    float meltAtEdge=1-smoothstep(edge-0.035,edge,d);
                    tick=stroke*meltAtEdge*smoothstep(0.03,0.2,d)*(1-smoothstep(0.75,1,_Pulse))*0.62;
                    tickHeat=x>0?1:1-along;
                }

                float fillA=fill*_Layers.x;
                float rippleA=ripples*(0.05+0.025*_Energy)*_Layers.y;
                float rimA=rim*(0.28+0.08*_Energy)*_Layers.z;
                float tickA=tick*_Layers.w;
                float alpha=saturate(fillA+rippleA+rimA+tickA);
                half3 tickColor=lerp(Deep,Hot,tickHeat);
                half3 color=(Deep*(fillA+rippleA)+Rim*rimA+tickColor*tickA)/max(fillA+rippleA+rimA+tickA,1e-4);
                return half4(color,alpha*inside*_Opacity);
            }
            ENDHLSL
        }
    }
}
