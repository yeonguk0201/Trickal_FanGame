Shader "TrickalFanGame/Crayon Rift"
{
    Properties
    {
        _Age ("Age", Float) = 0.12
        _Layer ("Warning / incision / silver artwork / flash / splinters", Float) = 1
        _BladeArtwork ("Reviewed concept sheet", 2D) = "black" {}
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
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            TEXTURE2D(_BladeArtwork); SAMPLER(sampler_BladeArtwork);
            float _Age, _Layer;
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS); o.uv=v.uv; return o;
            }
            float Hash(float2 p)
            {
                p=frac(p*float2(123.34,456.21)); p+=dot(p,p+45.32); return frac(p.x*p.y);
            }
            float Noise(float2 p)
            {
                float2 cell=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(cell),Hash(cell+float2(1,0)),f.x),
                    lerp(Hash(cell+float2(0,1)),Hash(cell+1),f.x),f.y);
            }
            float Segment(float2 p,float2 a,float2 b)
            {
                float2 ab=b-a;
                return length(p-a-ab*saturate(dot(p-a,ab)/max(dot(ab,ab),0.00001)));
            }
            half4 Blade(float2 p)
            {
                // Use the complete fifth concept frame as brush material, not the misaligned whole sheet.
                // Undo its painted -40 degree axis here; the effect transform provides the actual rotation.
                float2 axis=float2(0.76604444,-0.64278761), side=float2(0.64278761,0.76604444);
                float2 q=float2(0.50,0.49)+axis*p.x*0.65+side*p.y*0.27;
                float inside=step(0.004,q.x)*step(q.x,0.996)*step(0.004,q.y)*step(q.y,0.996);
                float2 uv=clamp(q,0.004,0.996)*float2(0.25,0.5);
                half4 art=SAMPLE_TEXTURE2D(_BladeArtwork,sampler_BladeArtwork,uv);
                half gray=dot(art.rgb,half3(0.2126,0.7152,0.0722));
                art.rgb=lerp(gray.xxx,art.rgb,0.12);
                art.a*=inside;
                return art;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 p=i.uv*2-1;
                float x=p.x, ax=abs(x);
                float aa=max(fwidth(p.y),0.0015);
                float tip=saturate(1-ax);
                if (_Layer<0.5)
                {
                    float d=abs(p.y)-0.21*pow(saturate(1-x*x),0.35);
                    float fill=1-smoothstep(-aa,aa,d);
                    float rim=exp(-abs(d)*90);
                    // Red artwork with low opacity; even the outline stays translucent.
                    return half4(0.95,0.015,0.035,(fill*0.12+rim*0.14)*smoothstep(0,0.08,tip));
                }
                float t=max(0,_Age);
                float front=lerp(-1.16,1.25,saturate(t/0.048));
                float reveal=1-smoothstep(front-0.11,front,x);
                float fade=1-smoothstep(0.29,0.48,t);
                float grain=Noise(float2(x*44,p.y*27));
                float dissolve=smoothstep(-0.12,0.16,grain-smoothstep(0.24,0.48,t));
                float alive=reveal*fade;
                if (_Layer<1.5)
                {
                    // Width is fixed from birth: the cut travels along X and never opens sideways.
                    float jagged=(Noise(float2(x*39,7))-0.5)*0.018;
                    float width=(0.085+jagged)*pow(saturate(1-x*x),0.65);
                    float d=abs(p.y)-width;
                    float mask=1-smoothstep(-aa,aa,d);
                    float silver=exp(-abs(d)*170)*0.6;
                    return half4(lerp(half3(0.001,0.001,0.003),half3(0.65,0.67,0.7),silver),
                        mask*alive*dissolve);
                }
                if (_Layer<2.5)
                {
                    half4 art=Blade(p);
                    // Brushed silver streaks hold their shape after the slicing front has passed.
                    float strength=1-smoothstep(0.13,0.39,t);
                    art.rgb*=1.1;
                    art.a*=alive*dissolve*(0.72+0.28*strength);
                    return art;
                }
                if (_Layer<3.5)
                {
                    float d=abs(p.y-0.008*x);
                    float needle=exp(-d*310)*pow(tip,0.3);
                    float wake=exp(-abs(p.y+0.035)*160)*pow(tip,0.6)*0.45;
                    float slash=needle+wake;
                    float2 cuttingTip=float2(clamp(front-0.05,-0.98,0.98),0);
                    float head=exp(-Segment(p,cuttingTip-float2(0.2,0.055),cuttingTip+float2(0.12,0.04))*260);
                    float ray=exp(-Segment(p,float2(-0.12,-0.29),float2(0.2,0.25))*320);
                    float impact=exp(-pow((t-0.053)/0.027,2));
                    float tailFade=exp(-max(0,t-0.055)*30)*(1-smoothstep(0.1,0.16,t));
                    float light=slash*tailFade*reveal*1.4+head*exp(-t*20)+ray*impact;
                    return half4(0.96,0.98,1,saturate(light));
                }
                float sparks=0;
                [unroll] for (int k=0;k<12;k++)
                {
                    float h=Hash(float2(k,4)), side=(k%2==0)?1:-1;
                    float bx=lerp(-0.78,0.7,Hash(float2(k,1)));
                    float localBirth=(bx+1.16)/2.41*0.048;
                    float flight=max(0,t-localBirth);
                    float2 velocity=float2(0.5+h*0.75,side*(0.12+h*0.45));
                    float2 head=float2(bx,side*0.09)+velocity*flight*1.1;
                    float2 tail=head-velocity*(0.035+0.06*h);
                    float streak=exp(-Segment(p,tail,head)*(480+h*240));
                    sparks+=streak*step(localBirth,t)*(1-smoothstep(0.10+h*0.08,0.24+h*0.1,flight));
                }
                return half4(0.83,0.9,1,saturate(sparks)*alive);
            }
            ENDHLSL
        }
    }
}
