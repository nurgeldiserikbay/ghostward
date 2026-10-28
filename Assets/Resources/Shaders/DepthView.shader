// Тестовая сборка: что «видит» нейросеть глубины — карта комнаты (RoomMap, _GW_Room) поверх камеры.
// Красное — близко, жёлтое — 1.5–2 м, зелёное — ~3 м, синее — далеко; тонкие линии через каждые 0.5 м;
// серая штриховка — сюда ещё не смотрели. Луч каждого пикселя экрана — по обратной матрице камеры.
Shader "GhostWard/DepthView"
{
    Properties
    {
        _MainTex ("Unused (UI)", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _GW_Room;
            float4 _GW_RoomOrigin;
            float4x4 _GW_InvViewProj;
            float4 _GW_CamPos;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float4 screen : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.screen = ComputeScreenPos(o.pos);
                return o;
            }

            float3 Ramp(float d)
            {
                float t = saturate((d - 0.5) / 4.5);
                float3 c = lerp(float3(1.0, 0.15, 0.1), float3(1.0, 0.85, 0.1), saturate(t * 3.0));
                c = lerp(c, float3(0.2, 0.9, 0.3), saturate(t * 3.0 - 1.0));
                c = lerp(c, float3(0.15, 0.35, 1.0), saturate(t * 3.0 - 2.0));
                return c;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.screen.xy / max(i.screen.w, 1e-4);
                float4 p = mul(_GW_InvViewProj, float4(uv * 2.0 - 1.0, 1.0, 1.0));
                float3 dir = normalize(p.xyz / p.w - _GW_CamPos.xyz);
                if (_GW_RoomOrigin.w < 0.5)
                    return fixed4(0.5, 0.5, 0.5, 0.25 * step(0.5, frac((uv.x + uv.y) * 40.0)));
                float2 ruv = float2(atan2(dir.x, dir.z) / 6.2831853 + 0.5, asin(clamp(dir.y, -1.0, 1.0)) / 3.1415927 + 0.5);
                float d = tex2Dlod(_GW_Room, float4(ruv, 0, 0)).r;
                if (d > 40.0)   // не снято
                    return fixed4(0.5, 0.5, 0.5, 0.3 * step(0.5, frac((uv.x + uv.y) * 40.0)));
                float line_ = 1.0 - smoothstep(0.0, 0.03, abs(frac(d / 0.5 + 0.5) - 0.5));
                float3 c = lerp(Ramp(d), float3(1, 1, 1), line_ * 0.7);
                return fixed4(c, 0.45 + line_ * 0.3);
            }
            ENDCG
        }
    }
}
