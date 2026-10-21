Shader "TrickalFanGame/Shield Bubble"
{
    // 날먹보호막 (ShieldBubble). A soap bubble around the player: a nearly clear inside, a thin rim that thickens
    // toward the edge like a real film, slowly drifting rainbow colours, a window highlight at the top left and a
    // small spot at the bottom right. A hit sends a ripple across the surface and brightens the rim; the end pops
    // the bubble into rim fragments and droplets. The bubble radius is BubbleRadius of the quad (uv 0-1 maps to
    // -1..1), leaving room for the pop to expand. All values come per renderer.
    Properties
    {
        _Clock ("Animation time (s)", Float) = 0
        _Opacity ("Overall opacity", Float) = 1
        _Scale ("Bubble scale (appear overshoot)", Float) = 1
        _Ripple ("Hit ripple progress 0-1 (outside 0-1: none)", Float) = -1
        _Pop ("Pop progress 0-1 (outside 0-1: none)", Float) = -1
        _Layers ("Preview layers: film, rim, highlight, ripple", Vector) = (1, 1, 1, 1)
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
            float _Clock, _Opacity, _Scale, _Ripple, _Pop;
            float4 _Layers;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS); o.uv=v.uv; return o;
            }
            // Keep in sync with ShieldBubble.BubbleRadius.
            static const float BubbleRadius=0.66;
            static const float Tau=6.2831853;
            // Pale sky blue base, so the bubble reads as a shield even where the rainbow film is thin.
            static const half3 Base=half3(0.62,0.86,1.0);

            float Hash(float n) { return frac(sin(n*91.345)*47453.5453); }

            // Soft rainbow of a thin film, kept pastel so it never turns into a neon ring.
            half3 Film(float hue)
            {
                half3 rainbow=0.5+0.5*cos(Tau*(hue+half3(0.0,0.33,0.67)));
                return lerp(Base,rainbow,0.55);
            }

            float Ellipse(float2 p, float2 centre, float2 size, float angle)
            {
                float2 d=p-centre;
                float c=cos(angle), s=sin(angle);
                d=float2(c*d.x+s*d.y,-s*d.x+c*d.y)/size;
                return saturate(1-dot(d,d));
            }

            half4 Frag(Varyings i):SV_Target
            {
                float2 q=i.uv*2-1;
                float d=length(q);
                float a=atan2(q.y,q.x);
                float t=_Clock;
                bool popping=_Pop>=0 && _Pop<1;
                float pop=popping?_Pop:0;
                float popEase=1-(1-pop)*(1-pop)*(1-pop);

                // Edge: a gentle breathing wobble, a decaying jiggle after a hit and a fast swell when popping.
                float wobble=0.012*sin(3*a+t*2.1)+0.008*sin(5*a-t*2.9+1.1);
                float ripple=_Ripple>=0 && _Ripple<1?_Ripple:-1;
                if (ripple>=0) wobble+=0.05*pow(1-ripple,2)*sin(4*a+ripple*22);
                float edge=BubbleRadius*_Scale*(1+wobble)*(1+0.22*popEase);
                float aa=max(fwidth(d)*1.2,0.004);
                float inside=1-smoothstep(edge-aa,edge+aa,d);
                float s=saturate(d/edge);

                // Film: rainbow bands drifting with angle and time, strongest toward the rim.
                float hue=s*0.85+0.12*sin(2*a+t*0.6)+0.06*sin(5*a-t*0.9)+t*0.04;
                float filmA=(0.04+0.22*pow(s,3))*_Layers.x;

                // Rim: a soft fresnel band with a thin bright lip, brighter for a moment after a hit.
                float hitGlow=ripple>=0?pow(1-ripple,1.5):0;
                float rimA=(pow(s,7)*0.42+exp(-pow((1-s)/0.03,2))*0.45)*(1+1.1*hitGlow)*_Layers.y;

                // Highlights: a curved window at the top left and a small spot at the bottom right.
                float2 p=q/edge;
                float window=Ellipse(p,float2(-0.4,0.42),float2(0.32,0.13),0.78);
                window=smoothstep(0,0.5,window)*(1-smoothstep(0.9,1,Ellipse(p,float2(-0.33,0.36),float2(0.3,0.1),0.78)));
                float spot=smoothstep(0,0.6,Ellipse(p,float2(0.42,-0.44),float2(0.09,0.06),0.7));
                float highlightA=(window*0.7+spot*0.6)*_Layers.z;

                // Ripple: a bright ring that runs in from the rim after a hit.
                float rippleA=0;
                if (ripple>=0)
                {
                    float front=1-ripple*0.85;
                    rippleA=exp(-pow((s-front)/0.06,2))*(1-ripple)*0.55*_Layers.w;
                }

                // Pop: the rim breaks into arcs that thin out, everything else fades quickly.
                float body=1;
                if (popping)
                {
                    float arcs=frac(a/Tau*11+0.13*sin(a*3));
                    float gap=smoothstep(0.85-pop*0.9,0.95-pop*0.9,abs(arcs*2-1));
                    rimA*=(1-gap)*(1-pop);
                    float fast=1-smoothstep(0,0.45,pop);
                    filmA*=fast;
                    highlightA*=fast;
                    rippleA=0;
                    body=1-pop;
                }

                float alpha=saturate(filmA+rimA+highlightA+rippleA)*inside;
                half3 film=Film(hue);
                half3 color=(film*filmA+lerp(Base,half3(1,1,1),0.55)*rimA+half3(1,1,1)*(highlightA+rippleA))/
                    max(filmA+rimA+highlightA+rippleA,1e-4);

                // Droplets: ten small drops flung out from the rim, outside the bubble's edge.
                if (popping)
                {
                    float dropA=0;
                    for (int k=0;k<10;k++)
                    {
                        float angle=(k+0.35*Hash(k+1.7))*Tau/10;
                        float reach=BubbleRadius*(1+0.5*Hash(k+3.1))*(1+0.45*popEase);
                        float2 centre=float2(cos(angle),sin(angle))*reach;
                        float size=0.035*(1-pop)*(0.7+0.6*Hash(k+5.9));
                        dropA=max(dropA,1-smoothstep(size*0.5,size,length(q-centre)));
                    }
                    dropA*=(1-smoothstep(0.5,1,pop))*0.85;
                    color=lerp(color,half3(0.9,0.97,1),saturate(dropA/max(alpha+dropA,1e-4)));
                    alpha=saturate(alpha+dropA);
                }

                return half4(color,alpha*body*_Opacity);
            }
            ENDHLSL
        }
    }
}
