Shader "TrickalFanGame/PortalAurora"
{
    Properties
    {
        _PortalTex ("Portal rim source", 2D) = "white" {}
        _FrameRect ("Source frame", Vector) = (0,0.5,0.5,0.5)
        _PortalRadius ("Artwork radius", Vector) = (0.82,1.1,0,0)
        _GlowSize ("Glow size", Vector) = (4.8,5.2,0,0)
        _Opacity ("Opacity", Range(0,1)) = 0
        _Phase ("Phase", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off Lighting Off ZWrite Off
        Blend SrcAlpha One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
            sampler2D _PortalTex;
            float4 _FrameRect, _PortalRadius, _GlowSize;
            float _Opacity, _Phase;
            v2f vert(appdata v)
            {
                v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o;
            }
            float4 RimSample(float2 direction, float radius)
            {
                float2 uv=float2(0.525,0.485)+direction*float2(0.275,0.405)*radius;
                float4 c=tex2D(_PortalTex,_FrameRect.xy+uv*_FrameRect.zw);
                return float4(c.rgb*c.a,c.a);
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 position=(i.uv-0.5)*_GlowSize.xy;
                float2 ellipse=position/_PortalRadius.xy;
                float r=length(ellipse);
                float2 direction=ellipse/max(r,0.001);
                float angle=atan2(direction.y,direction.x);
                // Borrow each angle's actual gold/pink/violet/cyan rim color, never a separate palette.
                float4 rim=RimSample(direction,0.86)+RimSample(direction,0.92)+RimSample(direction,0.98);
                float3 color= rim.rgb/max(rim.a,0.001);
                float distance=max(0.0,r-0.96)*min(_PortalRadius.x,_PortalRadius.y);
                // Dense contact glow and a broad tail, both decreasing continuously from the rim.
                float contact=0.62*exp(-distance/0.16);
                float tail=0.38*exp(-distance/0.52);
                // Low-contrast outward undulation; no isolated spikes or detached bright band.
                float drift=sin(angle*5.0-distance*3.0+_Phase)*0.06
                    +sin(angle*8.0-distance*4.0-_Phase*2.0)*0.04;
                float wave=1.0+drift*smoothstep(0.0,0.35,distance);
                // End on a concentric ellipse INSIDE the mesh, never along its four edges.
                float outerRadius=min(_GlowSize.x/(2.0*_PortalRadius.x),
                    _GlowSize.y/(2.0*_PortalRadius.y))*0.9;
                float fadeStart=min(1.05,outerRadius*0.5);
                float edge=1.0-smoothstep(fadeStart,outerRadius,r);
                float light=(contact+tail)*wave*edge;
                return float4(color,saturate(light*_Opacity));
            }
            ENDCG
        }
    }
}
