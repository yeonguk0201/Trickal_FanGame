Shader "TrickalFanGame/Procedural Water Stream"
{
    Properties
    {
        _Droplet ("Droplet mode", Float) = 0
        _Splash ("Contact fan mode", Float) = 0
        _Muzzle ("Nozzle gush mode", Float) = 0
        _Mist ("Mist mode", Float) = 0
        _Opacity ("Burst opacity", Float) = 1
        _Clock ("Animation clock", Float) = 0
        _BurstAge ("Normalized burst age", Float) = 0
        _Length ("World length", Float) = 1
        _Width ("Hit width", Float) = 0.2
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
            // Strip half-width past the water edge, in edge units. Matches ProceduralWaterStream.BeadMargin.
            #define HALO 1.18
            CBUFFER_START(UnityPerMaterial)
            float _Droplet, _Splash, _Muzzle, _Mist, _Opacity, _Clock, _Length, _Width, _AtWall, _BurstAge;
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
                    // The origin is the wall contact. Lobes radiate BACK into the room;
                    // a hollow, torn rim replaces the solid triangular end cap.
                    float2 q=float2((1-i.uv.x)*1.15,i.uv.y);
                    float r=length(q), angle=atan2(q.y,max(q.x,0.0001));
                    float fingers=0.5+0.5*sin(angle*11+0.7*sin(angle*7)+_BurstAge*3);
                    float n=Noise(q*10+float2(-_Clock*13,_Clock*7));
                    float boundary=0.63+0.22*pow(fingers,3)+(n-0.5)*0.07;
                    float aa=max(fwidth(r),0.014);
                    float mask=1-smoothstep(boundary-aa,boundary+aa,r);
                    float rim=smoothstep(boundary-0.17,boundary-0.06,r);
                    float spokes=pow(fingers,5)*(1-smoothstep(0.65,0.9,r));
                    float contact=1-smoothstep(0.08,0.32,r);
                    float holes=smoothstep(0.28,0.48,r)*(1-rim)*
                        smoothstep(0.35,0.65,n)*(1-spokes);
                    mask*=1-holes*0.88;
                    half3 c=lerp(blue,cyan,0.65+0.25*n);
                    float flash=(1-smoothstep(0.05,0.3,_BurstAge))*contact;
                    c=lerp(c,foam,saturate(rim*0.95+spokes*0.55+contact+flash));
                    return half4(c,mask*_Opacity*lerp(0.48,0.98,saturate(rim+contact+spokes)));
                }
                if (_Muzzle>0.5)
                {
                    // Nozzle at (0,0); the quad reaches ~1.76 spread-units forward. A forward lump of
                    // gushing water plus a torn crown of petals that splay sideways out of the jar mouth.
                    float2 q=float2((i.uv.x-0.1)*1.76,i.uv.y);
                    float n=Noise(q*6+float2(-_Clock*14,_Clock*5));
                    float flash=1-smoothstep(0.0,0.3,_BurstAge);
                    // The lump is wider than the stream so it shows as a collar around the stream's base.
                    float d=length((q-float2(0.5,0))/float2(0.55,0.62+0.15*flash));
                    float r=length(q), angle=atan2(q.y,max(q.x,0.0001));
                    float petals=pow(0.5+0.5*cos(angle*7+(n-0.5)*2+_Clock*9),3);
                    float fan=1-smoothstep(1.05,1.35,abs(angle));
                    float reach=(0.5+0.55*petals*fan)*(1+0.25*flash);
                    float pr=r/max(reach,0.001);
                    float shape=min(d,pr)+(n-0.5)*0.14;
                    float aa=max(fwidth(shape),0.02);
                    float mask=1-smoothstep(1-aa,1+aa,shape);
                    mask*=smoothstep(-0.18,-0.02,q.x)*(1-smoothstep(0.8,1.0,i.uv.x));
                    half3 c=lerp(outline,blue,1-smoothstep(0.8,0.92,shape));
                    c=lerp(c,cyan,(1-smoothstep(0.4,0.75,shape))*(0.55+0.45*n));
                    float tips=smoothstep(0.6,0.95,pr)*petals*fan*step(pr,d)*(1-smoothstep(0.92,1.0,shape));
                    c=lerp(c,foam,saturate(tips*0.8+flash*(1-smoothstep(0.15,0.5,r))*0.7));
                    return half4(c,mask*_Opacity*0.95);
                }
                if (_Mist>0.5)
                {
                    if (_AtWall>0.5) clip(-dot(i.world-_EndPlane.xy,_EndPlane.zw));
                    // Soft, lumpy haze. Particle rotation varies the noise, so each puff differs.
                    float2 q=i.uv*2-1;
                    float d=length(q);
                    float lumps=Noise(q*2.6+7.1)*0.6+Noise(q*5.3+2.3)*0.4;
                    float a=exp(-d*d*2.2)*(1-smoothstep(0.8,1,d))*smoothstep(0.1,0.5,lumps+0.3*(1-d));
                    return half4(lerp(cyan,foam,0.65),a*i.color.a*0.62);
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
                // v is in water-edge units: |v|=0.88 is the edge, the strip itself reaches HALO.
                float u=i.uv.x, v=i.uv.y*HALO;
                float flow=u*4.0-_Clock*7;
                float n=Noise(float2(flow,v*2.5));
                float m=Noise(float2(u*8-_Clock*10,v*4+13));
                float bend=(n-0.5)*0.14;
                float edge=abs(v-bend);
                // Lumps travel forward on each side independently, so the outline never reads as a ruler.
                float lump=sin(u*23-_Clock*41+step(0,v)*2.1)*0.035;
                float boundary=0.88+(m-0.5)*0.13+lump;
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
                // A broken specular line on one side gives the stream a round, glassy volume.
                float glint=1-smoothstep(0.035,0.09,abs(v-bend-0.46));
                glint*=smoothstep(0.45,0.75,Noise(float2(u*3-_Clock*9,7.3)));
                c=lerp(c,foam,glint*0.7*(1-smoothstep(0.6,0.8,edge)));
                // Beads clinging to the outline, carried forward with the flow. One candidate per cell.
                float s=u-_Clock*5.5, cellWidth=0.11;
                float cell=floor(s/cellWidth);
                float h=Hash(float2(cell,3.7)), h2=Hash(float2(cell,9.1)), h3=Hash(float2(cell,5.3));
                float beadSide=h2>0.5?1:-1;
                float beadRadius=0.09+0.07*h3;
                float2 dq=float2((s-(cell+0.5+(h3-0.5)*0.3)*cellWidth)/(_Width*0.72),v-beadSide*(0.93+0.07*h));
                float bd=length(dq)/beadRadius;
                float bead=step(0.55,h)*(1-smoothstep(0.85,1.0,bd));
                bead*=smoothstep(0.25,0.6,u)*smoothstep(0.2,0.5,_Length-u)*smoothstep(0.15,0.4,_BurstAge);
                half3 beadColor=lerp(outline,cyan,1-smoothstep(0.5,0.78,bd));
                beadColor=lerp(beadColor,foam,1-smoothstep(0.15,0.35,length(dq/beadRadius-float2(-0.25,0.3*beadSide))));
                c=lerp(c,beadColor,saturate(bead-alpha));
                alpha=max(alpha,bead);
                // Free ends crumble into lumps instead of a clean fade; wall ends stay solid into the splash.
                float tail=saturate((_Length-u)/max(0.01,min(0.32,_Length*0.4)));
                float crumble=Noise(float2(u*16-_Clock*22,v*3+5));
                float dissolve=smoothstep(crumble-0.12,crumble+0.12,tail*1.25);
                // Rounded rear so the stream leaves the gush instead of starting at a cut edge.
                float rear=0.1*saturate(_Length*4);
                float back=saturate((rear-u)/max(rear,0.001));
                float rounded=u<rear?1-smoothstep(0.9,1.0,length(float2(back,edge/boundary))):1;
                float cap=rounded*smoothstep(0,0.035,u)*lerp(smoothstep(0,0.035,_Length-u)*dissolve,1,step(0.5,_AtWall));
                return half4(c,alpha*_Opacity*cap);
            }
            ENDHLSL
        }
    }
}
