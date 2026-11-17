Shader "TrickalFanGame/BannerMist"
{
    // UI Image blur for the first-entry banner: _Blur is the blur radius in UV, _Mist (0..1) washes the art
    // toward pale mist and thins it, as if seen through fog.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Blur ("Blur radius (UV)", Float) = 0
        _Mist ("Mist", Range(0, 1)) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
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
            float _Blur;
            float _Mist;
            float4 _ClipRect;
            v2f vert(appdata v)
            {
                v2f o; o.world = v.vertex; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o;
            }
            float4 Sample(float2 uv)
            {
                // Outside the sprite counts as transparent: the blur does not wrap around, and the padded quad
                // (BannerMist) draws only the halo past the art's edge.
                float inside = step(0, uv.x) * step(0, uv.y) * step(uv.x, 1) * step(uv.y, 1);
                float4 c = tex2D(_MainTex, uv) * inside;
                c.rgb *= c.a; // premultiplied, so transparent texels do not darken the edges
                return c;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                // The banner is much wider than tall: scale the vertical radius so the blur stays round on screen.
                float aspect = fwidth(i.uv.y) / max(fwidth(i.uv.x), 1e-6);
                float2 radius = float2(_Blur, _Blur * aspect);
                float4 sum = Sample(i.uv) * 2;
                float weight = 2;
                [unroll] for (int n = 0; n < 12; n++)
                {
                    float angle = n * 0.523599;
                    float2 offset = float2(cos(angle), sin(angle)) * radius;
                    sum += Sample(i.uv + offset * 0.5) * 1.5;
                    sum += Sample(i.uv + offset);
                    weight += 2.5;
                }
                float4 c = sum / weight;
                c.rgb /= max(c.a, 1e-4);
                c.rgb = lerp(c.rgb, float3(0.97, 0.95, 1.0), _Mist * 0.55);
                c.a *= 1 - _Mist * 0.25;
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
