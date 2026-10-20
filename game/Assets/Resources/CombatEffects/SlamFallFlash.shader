Shader "TrickalFanGame/Slam Fall Flash"
{
    // Crayon hero slam v22: world-vertical beams fall from above, sweep along the path,
    // flash on the floor and erode from the top. Geometry lives in LayeredSlamEffect.BuildFlash.
    Properties { _Opacity ("Opacity", Float) = 1 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off Blend One OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _ClipRect, _PathDir, _Source;
            float _Age, _Fall, _Linger, _Opacity;
            struct Attributes
            {
                float3 positionOS:POSITION;
                float2 local:TEXCOORD0;   // world units from the beam foot
                float4 beam:TEXCOORD1;    // delay, seed, beam height, width scale
                float2 extra:TEXCOORD2;   // intensity
            };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float2 local:TEXCOORD0;
                float4 beam:TEXCOORD1;
                float2 extra:TEXCOORD2;
                float2 world:TEXCOORD3;
            };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS=TransformObjectToHClip(v.positionOS);
                o.local=v.local; o.beam=v.beam; o.extra=v.extra;
                o.world=TransformObjectToWorld(v.positionOS).xy;
                return o;
            }
            float SlamHash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float SlamNoise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(SlamHash(i),SlamHash(i+float2(1,0)),f.x),
                    lerp(SlamHash(i+float2(0,1)),SlamHash(i+float2(1,1)),f.x),f.y);
            }
            half4 Frag(Varyings i):SV_Target
            {
                float t=_Age-i.beam.x;
                clip(t);
                float seed=i.beam.y, height=i.beam.z, widthScale=i.beam.w, intensity=i.extra.x;
                float dx=i.local.x, dy=i.local.y;
                float fall=saturate(t/_Fall);
                float after=saturate((t-_Fall)/_Linger);
                bool landed=t>=_Fall;

                // Accelerating head: slow at the top, fastest when it strikes the floor.
                float front=height*(1-fall*fall);
                float above=dy-front;
                // A long streak from the first frame that stretches with speed: one downward swipe,
                // never a falling droplet. The sharp tip sits just below the front.
                float tau=lerp(2.2,height*0.9,fall);
                float aboveFront=max(above,0);
                float tip=above<0 ? exp(-Sq(above/0.10)) : 1;
                float trail=exp(-aboveFront/tau)*tip;
                float head=exp(-aboveFront/0.7)*tip*(1-after);
                // Soft top so the beams come in from the sky instead of ending in a flat comb.
                float topFade=1-smoothstep(0.65,1.0,dy/height);

                // Thin hot core inside a softer glow and a faint sheet that joins neighbours.
                // The whole beam narrows while it dissolves.
                float thin=lerp(1,0.35,after)*widthScale;
                float core=exp(-Sq(dx/(0.07*thin)));
                float glow=exp(-Sq(dx/(0.24*thin)))*0.38+exp(-Sq(dx/0.6))*0.1;

                // Downward-flowing streaks inside the beam.
                float flow=0.6+0.55*SlamNoise(float2(dx*5+seed*31,dy*0.55+t*26));

                // Erosion from the top down after the strike, broken up by noise.
                float h=saturate(dy/height);
                float erosion=0.55*(1-h)+0.45*SlamNoise(float2(dx*3+seed*7,dy*1.1));
                float edge=after*1.2-0.1;
                float keep=smoothstep(edge-0.08,edge+0.08,erosion);

                float beam=intensity*(core+glow)*(trail*flow+head*1.6)*keep*topFade*smoothstep(-0.12,0.02,dy);

                // Floor flash where each beam lands; neighbours overlap into one bright line.
                float flare=0;
                if (landed)
                {
                    float since=t-_Fall;
                    float pulse=exp(-since/0.06)*1.6+0.35*(1-after);
                    // Measured along the slam path, so vertical paths get one line instead of stacked rungs.
                    float along=dot(i.local,_PathDir.xy), across=dot(i.local,float2(-_PathDir.y,_PathDir.x));
                    flare=intensity*pulse*(exp(-Sq(along/0.62)-Sq(across/0.09))+
                        0.4*exp(-Sq(along/0.75)-Sq(across/0.22)));
                }
                // Keep the attacking boss readable where beams cross the body.
                beam*=lerp(0.35,1,smoothstep(0.5,1.4,distance(i.world,_Source.xy)));
                float light=beam+flare;

                // White core -> gold -> orange rim, matching the reviewed painted blade colours.
                half3 orange=half3(1,0.42,0.06), gold=half3(1,0.8,0.22), white=half3(1,1,0.94);
                half3 color=lerp(orange,gold,saturate(light*1.6));
                color=lerp(color,white,saturate((light-0.55)*2));
                float alpha=saturate(light*1.15)*_Opacity;

                // Clip to the room sides and floor only; the beams may come in from above the room.
                float2 lower=i.world-_ClipRect.xy;
                float sides=min(min(lower.x,_ClipRect.z-i.world.x),lower.y);
                clip(sides);
                alpha*=smoothstep(0,0.035,sides);
                return half4(color*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
