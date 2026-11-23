Shader "TrickalFanGame/Healing Aura"
{
    // 산사모-우울's heal (EnemyHealAuraVisual), additive. _Mode picks the layer:
    // 0 floor field (the quad spans the real heal radius), 1 link (uv.x runs healer to ally, light flows along it),
    // 2 glow and rising crosses on an ally being healed. All values come per renderer.
    Properties
    {
        _Mode ("Layer: 0 field, 1 link, 2 receive", Float) = 0
        _Clock ("Animation time (s)", Float) = 0
        _Opacity ("Overall opacity", Float) = 1
        _Pulse ("Heal tick flash 0-1", Float) = 0
        _Length ("Link length (world units)", Float) = 1
        _Seed ("Per-ally offset", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off Blend SrcAlpha One
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Mode, _Clock, _Opacity, _Pulse, _Length, _Seed;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS); o.uv=v.uv; return o;
            }

            static const float3 Mint=float3(0.36,1.0,0.58);
            static const float3 Gold=float3(1.0,0.9,0.5);
            static const float3 Pale=float3(0.9,1.0,0.86);
            static const float Tau=6.2831853;

            float Band(float x, float center, float width) { float u=(x-center)/width; return exp(-u*u); }
            float Hash(float n) { return frac(sin(n*91.345)*47453.5); }

            float4 Field(float2 uv)
            {
                float2 q=uv*2-1;
                float d=length(q);
                if (d>1) return 0;
                float a=atan2(q.y,q.x);
                float t=_Clock;
                float breathe=0.85+0.15*sin(t*2.4);

                float fill=lerp(0.012,0.075,d*d*d)*breathe;
                float rim=Band(d,0.95,0.016)*0.85+Band(d,0.92,0.06)*0.2;
                float dashes=Band(d,0.76,0.009)*smoothstep(-0.1,0.5,sin(a*16+t*1.1));
                // A six-petal flower turning against the dashes.
                float petalRadius=0.6*abs(cos(3*(a-t*0.28)));
                float flower=Band(d,petalRadius,0.014)*(1-smoothstep(0.55,0.64,d))*smoothstep(0.03,0.12,d);
                float wave=frac(t*0.55);
                float ripple=Band(d,wave*0.92,0.035)*(1-wave)*(1-wave);
                float tick=Band(d,_Pulse>0?(1-_Pulse)*0.92:-1,0.07)*_Pulse;
                // Twelve lights riding the rim.
                float cell=frac((a/Tau+t*0.05)*12)-0.5;
                float id=floor((a/Tau+t*0.05)*12);
                float twinkle=0.45+0.55*sin(t*3.4+Hash(id)*Tau);
                float lights=exp(-(cell*cell*d*d*(Tau/12)*(Tau/12)+(d-0.95)*(d-0.95))/0.00045)*saturate(twinkle);

                float3 color=Mint*(fill+rim*0.8+dashes*0.5+ripple*0.5)+Gold*(flower*0.5+rim*0.2+tick*0.55)+Pale*lights;
                float edge=1-smoothstep(0.985,1.0,d);
                return float4(color,edge);
            }

            float4 Link(float2 uv)
            {
                float x=uv.x, y=uv.y*2-1, t=_Clock;
                float core=Band(y,0,0.16), halo=Band(y,0,0.62)*0.34;
                // Comets of light travelling from the healer toward the ally.
                float head=frac(x*max(_Length,0.5)*0.85-t*1.5+_Seed);
                // A long tail behind a soft head, with no hard edge where the repeat wraps.
                float comet=pow(head,5)*(1-smoothstep(0.9,1,head))*Band(y,0,0.42);
                float shimmer=0.8+0.2*sin(x*max(_Length,0.5)*9-t*7+_Seed*Tau);
                float ends=smoothstep(0,0.05,x)*(1-smoothstep(0.95,1,x));
                float3 color=Mint*(halo+core*0.65*shimmer)+Pale*core*0.3+Gold*comet*1.25+Pale*comet*0.5;
                return float4(color*ends*(1+_Pulse*0.6),1);
            }

            float Cross(float2 p, float size)
            {
                float2 b=abs(p);
                float bar=min(max(b.x-size,b.y-size*0.3),max(b.y-size,b.x-size*0.3));
                return 1-smoothstep(0,0.028,bar);
            }

            float4 Receive(float2 uv)
            {
                float2 q=uv*2-1;
                float t=_Clock+_Seed*5;
                float body=exp(-dot(q*float2(1.35,1.05),q*float2(1.35,1.05))*1.8)*(0.08+0.24*_Pulse);
                float2 foot=(q-float2(0,-0.72))*float2(1,3.1);
                float footGlow=exp(-dot(foot,foot)*2.4)*(0.5+0.4*_Pulse);
                float crosses=0, motes=0;
                for (int i=0;i<8;i++)
                {
                    float n=i+1+_Seed*13;
                    float h=Hash(n), k=Hash(n+3.7);
                    float life=frac(t*(0.42+0.26*k)+h*7);
                    float2 p=float2(lerp(-0.68,0.68,h)+0.07*sin(life*Tau+h*Tau),-0.82+1.68*life);
                    float fade=sin(life*PI);
                    if (i%3==0) crosses+=Cross(q-p,lerp(0.1,0.15,k))*fade;
                    else { float2 m=q-p; motes+=exp(-dot(m,m)/0.0035)*fade; }
                }
                // Round falloff, so the glow never shows the quad's corners; the crosses fade only at the very edge.
                float glowMask=1-smoothstep(0.35,0.95,length(q));
                float sparkMask=1-smoothstep(0.88,1,max(abs(q.x),abs(q.y)));
                float3 color=Mint*(body+footGlow)*glowMask+(Gold*crosses*0.95+Pale*(motes+crosses*0.25))*sparkMask;
                return float4(color,1);
            }

            half4 Frag(Varyings i):SV_Target
            {
                float4 c=_Mode<0.5?Field(i.uv):_Mode<1.5?Link(i.uv):Receive(i.uv);
                return half4(saturate(c.rgb),saturate(c.a*_Opacity));
            }
            ENDHLSL
        }
    }
}
