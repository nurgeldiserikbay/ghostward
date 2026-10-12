// Холодный след босой ноги на полу — улика «Следы», видна только в тепловизоре.
// Квад лежит на полу, пальцы — в сторону движения (uv.y вверх). _Mirror — левая/правая нога.
Shader "GhostWard/Footprint"
{
    Properties
    {
        _Alpha ("Alpha", Range(0, 1)) = 0
        _Mirror ("Mirror", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+4" "IgnoreProjector" = "True" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _Alpha, _Mirror;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float sdEllipse(float2 p, float2 c, float2 r) { return (length((p - c) / r) - 1.0) * min(r.x, r.y); }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.uv - 0.5;
                if (_Mirror > 0.5) p.x = -p.x;
                p.x *= 0.46;                                   // квад узкий: 0.13 × 0.28 м
                float d = sdEllipse(p, float2(0.0, -0.22), float2(0.07, 0.13));           // пятка
                d = min(d, sdEllipse(p, float2(0.012, 0.06), float2(0.085, 0.2)));        // стопа
                for (int k = 0; k < 5; k++)
                {
                    float2 c = float2(-0.06 + k * 0.03, 0.33 - abs(k - 1.0) * 0.025);
                    d = min(d, sdEllipse(p, c, float2(0.014, 0.022) * (k == 0 ? 1.35 : 1.0)));   // пальцы
                }
                float w = max(fwidth(d), 1e-4);
                float inside = saturate(0.5 - d / w);
                float halo = exp(-max(d, 0.0) * 60.0) * 0.4;
                float3 col = lerp(float3(0.1, 0.15, 0.65), float3(0.45, 0.85, 1.0), inside);
                return fixed4(col, saturate(inside + halo) * _Alpha * (0.85 + 0.15 * sin(_Time.y * 2.0 + i.uv.y * 3.0)));
            }
            ENDCG
        }
    }
}
