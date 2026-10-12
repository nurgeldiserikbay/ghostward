// Холодное пятно в логове — видно только в тепловизоре. Мягкий синий круг с «дыханием».
Shader "GhostWard/ColdSpot"
{
    Properties
    {
        _Alpha ("Alpha", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+5" "IgnoreProjector" = "True" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _Alpha;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.uv - 0.5;
                float wobble = 0.04 * sin(atan2(p.y, p.x) * 5.0 + _Time.y * 2.0) * saturate(length(p) * 5.0);
                float d = length(p) * 2.0 + wobble;
                float core = saturate(1.0 - d);
                float3 col = lerp(float3(0.15, 0.1, 0.7), float3(0.4, 0.8, 1.0), core * core);
                return fixed4(col, pow(core, 1.5) * _Alpha * (0.85 + 0.15 * sin(_Time.y * 3.0)));
            }
            ENDCG
        }
    }
}
