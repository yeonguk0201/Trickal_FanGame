Shader "TrickalFanGame/PortalInterior"
{
    Properties
    {
        [PerRendererData] _MainTex ("Portal sheet", 2D) = "white" {}
        _PortalPhase ("Interior phase", Float) = 0
        _CellSize ("Cell UV size", Vector) = (0.5,0.5,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
        Cull Off Lighting Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            sampler2D _MainTex;
            float4 _CellSize;
            float _PortalPhase;
            v2f vert(appdata v)
            {
                v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o;
            }
            float2 FrameUV(float2 local, float frame)
            {
                float2 origin=float2(fmod(frame,2.0)*_CellSize.x, frame<2.0 ? 1.0-_CellSize.y : 0.0);
                return origin+local*_CellSize.xy;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 local=(i.uv-float2(0,1.0-_CellSize.y))/_CellSize.xy;
                // A single immutable silhouette/alpha for the entire cycle.
                fixed4 anchor=tex2D(_MainTex,i.uv);
                float radius=length((local-float2(0.525,0.485))/float2(0.215,0.335));
                float interior=1.0-smoothstep(0.72,1.0,radius);
                float phase=fmod(_PortalPhase,4.0);
                float first=floor(phase);
                float blend=smoothstep(0.0,1.0,frac(phase));
                fixed3 a=tex2D(_MainTex,FrameUV(local,first)).rgb;
                fixed3 b=tex2D(_MainTex,FrameUV(local,fmod(first+1.0,4.0))).rgb;
                anchor.rgb=lerp(anchor.rgb,lerp(a,b,blend),interior);
                return anchor*i.color;
            }
            ENDCG
        }
    }
}
