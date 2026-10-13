Shader "TrickalFanGame/Procedural Water Stream"
{
    Properties
    {
        _Droplet ("Droplet mode", Float) = 0
        _Splash ("Contact fan mode", Float) = 0
        _Opacity ("Burst opacity", Float) = 1
        _Clock ("Animation clock", Float) = 0
        _Length ("World length", Float) = 1
        _AtWall ("Clip at wall", Float) = 0
        _EndPlane ("Wall plane", Vector) = (0,0,1,0)
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
            float _Droplet, _Splash, _Opacity, _Clock, _Length, _AtWall;
            float4 _EndPlane;
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
            half4 Frag(Varyings i):SV_Target
            {
                half3 outline=half3(0.035,0.19,0.32);
                half3 blue=half3(0.025,0.49,0.78);
                half3 cyan=half3(0.13,0.81,0.94);
                half3 foam=half3(0.77,0.98,1.0);
                if (_Splash>0.5)
                {
                    // Incoming jet spreads sideways and curls back from the contact plane.
                    float u=i.uv.x, v=abs(i.uv.y);
                    float n=Noise(float2(u*9-_Clock*22,v*7));
                    float fanWidth=lerp(0.10,0.87,smoothstep(0.05,0.95,u));
                    float edge=v-fanWidth-(n-0.5)*0.10;
                    float aa=max(fwidth(edge),0.025);
                    float mask=(1-smoothstep(-aa,aa,edge))*smoothstep(0,0.18,u);
                    float curl=abs(v-(0.20+0.51*u+0.06*sin(u*12-_Clock*35)));
                    float lace=(1-smoothstep(0.04,0.15,curl))*smoothstep(0.25,0.6,n);
                    float contact=smoothstep(0.72,0.98,u)*(1-smoothstep(0.12,0.65,v));
                    half3 c=lerp(blue,cyan,0.55+0.25*n);
                    c=lerp(c,foam,saturate(lace+contact*0.85));
                    return half4(c,mask*_Opacity*(0.58+0.35*contact));
                }
                if (_Droplet>0.5)
                {
                    if (_AtWall>0.5) clip(-dot(i.world-_EndPlane.xy,_EndPlane.zw));
                    float2 q=i.uv*2-1;
                    // A tapered oval, shaded as a water bead rather than a glowing spark.
                    float d=length(float2(q.x,q.y*(1+0.20*q.x)));
                    float aa=max(fwidth(d),0.025);
                    float a=1-smoothstep(0.82-aa,0.82+aa,d);
                    half3 c=lerp(outline,cyan,1-smoothstep(0.57,0.72,d));
                    float glint=1-smoothstep(0.18,0.35,length((q-float2(-0.2,0.22))*float2(0.7,1.3)));
                    return half4(lerp(c,foam,glint),a*i.color.a);
                }
                float u=i.uv.x, v=i.uv.y;
                float flow=u*4.0-_Clock*7;
                float n=Noise(float2(flow,v*2.5));
                float m=Noise(float2(u*8-_Clock*10,v*4+13));
                float bend=(n-0.5)*0.14;
                float edge=abs(v-bend);
                float boundary=0.88+(m-0.5)*0.13;
                float aa=max(fwidth(edge),0.018);
                float alpha=1-smoothstep(boundary-aa,boundary+aa,edge);
                half3 c=lerp(outline,blue,1-smoothstep(boundary-0.16,boundary-0.10,edge));
                float band=sin(v*9+(n-0.5)*4+sin(flow*1.4)*0.8);
                c=lerp(c,cyan,(1-smoothstep(0.55,0.77,edge))*smoothstep(-0.25,0.15,band));
                // Broken, travelling foam streaks; no continuous white laser core.
                float streak=pow(saturate(sin(v*14+n*6)),12)*smoothstep(0.45,0.72,m);
                c=lerp(c,foam,streak*(1-smoothstep(0.55,0.72,edge)));
                // Curved transverse ridges read as pumped water, rather than parallel laser bands.
                float ridge=pow(saturate(cos(u*7.5-_Clock*32+v*v*2.8)),14);
                ridge*=smoothstep(0.18,0.65,m)*(1-smoothstep(0.65,0.84,edge));
                c=lerp(c,foam,ridge*0.75);
                float cap=smoothstep(0,0.035,u)*smoothstep(0,0.035,_Length-u);
                return half4(c,alpha*_Opacity*cap);
            }
            ENDHLSL
        }
    }
}
