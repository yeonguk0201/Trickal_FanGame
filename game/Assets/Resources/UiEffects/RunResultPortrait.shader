Shader "TrickalFanGame/RunResultPortrait"
{
    Properties
    {
        [PerRendererData] _MainTex ("Portrait", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct Vertex
            {
                float4 position : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 circle : TEXCOORD1;
            };

            struct Fragment
            {
                float4 position : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 circle : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _TextureSampleAdd;

            Fragment vert(Vertex v)
            {
                Fragment o;
                o.position = UnityObjectToClipPos(v.position);
                o.color = v.color;
                o.uv = v.uv;
                o.circle = v.circle;
                return o;
            }

            fixed4 frag(Fragment i) : SV_Target
            {
                float distance = length(i.circle) - 1.0;
                float edge = max(fwidth(distance), 0.0001);
                fixed4 color = (tex2D(_MainTex, i.uv) + _TextureSampleAdd) * i.color;
                color.a *= 1.0 - smoothstep(-0.5 * edge, 0.5 * edge, distance);
                return color;
            }
            ENDCG
        }
    }
}
