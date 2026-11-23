Shader "TrickalFanGame/SP Absorb"
{
    // Trial SP pickup (PlayerRecoveryEffect), additive. Blue comets spiral into the player, then a flash, a four-point
    // glint and one ring leave the center. _Progress runs 0 to 1 once.
    Properties
    {
        _Progress ("Progress 0-1", Float) = 0
        _Opacity ("Overall opacity", Float) = 1
        _Seed ("Variation", Float) = 0
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
            float _Progress, _Opacity, _Seed;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS); o.uv=v.uv; return o;
            }

            static const float3 Blue=float3(0.1,0.5,1.0);
            static const float3 Cyan=float3(0.35,0.9,1.0);
            static const float3 White=float3(0.92,0.98,1.0);
            static const float Tau=6.2831853;
            static const int CometCount=10;
            static const int TailSteps=14;

            float Hash(float n) { return frac(sin(n*91.345)*47453.5); }
            float Band(float x, float center, float width) { float u=(x-center)/width; return exp(-u*u); }

            half4 Frag(Varyings i):SV_Target
            {
                float2 q=i.uv*2-1;
                float d=length(q);
                if (d>1) return 0;
                float p=_Progress;
                float heads=0, tails=0;
                for (int c=0;c<CometCount;c++)
                {
                    float h=Hash(c+1+_Seed), k=Hash(c+5.3+_Seed);
                    float life=saturate((p-h*0.2)/0.54);
                    if (life<=0 || life>=1) continue;
                    float start=(float)c/CometCount*Tau+k*0.5;
                    float reach=lerp(0.8,0.94,k);
                    float appear=smoothstep(0,0.14,life);
                    for (int s=0;s<TailSteps;s++)
                    {
                        float back=(float)s/(TailSteps-1);
                        float l=max(0,life-back*0.17);
                        float r=lerp(reach,0.03,l*l);
                        float a=start+l*2.5;
                        float2 m=q-float2(cos(a),sin(a)*0.84)*r;
                        float size=lerp(s==0?0.04:0.026,0.01,back);
                        float glow=exp(-dot(m,m)/(size*size))*appear;
                        // Overlapping samples read as one smooth tail; max keeps them from beading.
                        if (s==0) heads+=glow; else tails=max(tails,glow*(1-back)*0.85);
                    }
                }

                float flash=smoothstep(0.46,0.6,p)*(1-smoothstep(0.64,0.92,p));
                // Kept soft so the character stays readable through the flash.
                float core=exp(-d*d/0.012)*flash*0.5;
                float bloom=exp(-d*d/0.14)*flash*0.2;
                float glint=(exp(-abs(q.x)*24)*exp(-abs(q.y)*3.4)+exp(-abs(q.y)*24)*exp(-abs(q.x)*3.4))*flash;
                float rp=saturate((p-0.56)/0.42);
                float ease=1-(1-rp)*(1-rp);
                float ring=rp>0?Band(d,lerp(0.08,0.88,ease),lerp(0.05,0.012,rp))*pow(1-rp,1.5):0;

                float3 color=Blue*(tails*0.8+bloom+ring*0.55)+Cyan*(tails*0.5+heads*0.7+ring*0.7+glint*0.5)+
                             White*(heads*0.8+core+glint*0.6);
                float edge=1-smoothstep(0.92,1,d);
                return half4(saturate(color*edge),saturate(_Opacity));
            }
            ENDHLSL
        }
    }
}
