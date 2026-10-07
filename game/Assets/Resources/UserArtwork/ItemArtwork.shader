Shader "TrickalFanGame/ItemArtwork"
{
    Properties
    {
        [PerRendererData] _MainTex ("Artwork", 2D) = "white" {}
        _Card ("Rounded spell card", Float) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="False" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; float4 world : TEXCOORD1; };
            sampler2D _MainTex;
            float _Card;
            float4 _ClipRect;
            v2f vert(appdata v)
            {
                v2f o; o.world = v.vertex; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                if (_Card > 0.5)
                {
                    float2 q = abs(i.uv - 0.5) - 0.445;
                    float d = length(max(q, 0)) + min(max(q.x, q.y), 0) - 0.055;
                    float aa = max(fwidth(d), 0.0005);
                    float outer = 1 - smoothstep(-aa, aa, d);
                    // White frame of the card, about 4% of its side.
                    float border = smoothstep(-0.04-aa, -0.04+aa, d);
                    c.rgb = lerp(c.rgb, float3(1,1,1), border);
                    c.a = outer;
                }
                else
                {
                    // A solid white silhouette follows the transparent artifact, about 3% of its side wide.
                    // Two rings of samples keep the wide outline free of gaps on diagonals and thin shapes.
                    float a = c.a;
                    [unroll] for (int n = 0; n < 12; n++)
                    {
                        float angle = n * 0.523599;
                        float2 offset = float2(cos(angle), sin(angle));
                        a = max(a, tex2D(_MainTex, i.uv + offset * 0.03).a);
                        a = max(a, tex2D(_MainTex, i.uv + offset * 0.015).a);
                    }
                    float alpha = max(c.a, a);
                    c.rgb = lerp(float3(1,1,1), c.rgb, c.a / max(alpha, 0.001));
                    c.a = alpha;
                }
                c *= i.color;
                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.world.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(c.a - 0.001);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
