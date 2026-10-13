// Сущность по фотореалистичной картинке (Resources/Entities/<Kind>.png).
// Картинка — фигура на чёрном фоне или с прозрачностью; чёрный фон вырезается по яркости.
// Поверх — эффекты плёночного хоррора: зерно, мерцание, разрывы строк, цветовой сдвиг на сбоях,
// «дыхание» фигуры, холодная тонировка. Те же параметры, что у процедурного шейдера:
// _Fade, _Flash, _Dissolve, _Flip, _Locked и трафарет UI.
Shader "GhostWard/EntityPhoto"
{
    Properties
    {
        _MainTex ("Picture", 2D) = "black" {}
        _Color ("Accent", Color) = (0.5, 0.7, 1, 1)
        _Fade ("Fade", Range(0, 1)) = 1
        _Flash ("Flash", Range(0, 1)) = 0
        _Dissolve ("Dissolve", Range(0, 1)) = 0
        _Seed ("Seed", Float) = 0
        _Flip ("Flip", Float) = 0
        _Locked ("Locked", Float) = 0
        _Key ("Black key", Float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Stencil
            {
                Ref [_Stencil]
                Comp [_StencilComp]
                Pass [_StencilOp]
                ReadMask [_StencilReadMask]
                WriteMask [_StencilWriteMask]
            }
            ColorMask [_ColorMask]
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            float _Fade, _Flash, _Dissolve, _Seed, _Flip, _Locked, _Key;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float hash11(float x) { return frac(sin(x * 127.1) * 43758.5453); }
            float hash21(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

            // цвет и непрозрачность картинки в точке: прозрачность из PNG и вырезка чёрного фона
            float4 Sample(float2 uv)
            {
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) return 0;
                float4 c = tex2D(_MainTex, uv);
                float lum = max(c.r, max(c.g, c.b));
                float a = c.a * lerp(1.0, smoothstep(0.012, 0.05, lum), _Key);
                return float4(c.rgb, a);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y + _Seed * 13.0;
                float tf = floor(t * 12.0);
                float2 uv = i.uv;
                if (_Flip > 0.5) uv.y = 1.0 - uv.y;

                // «дыхание» и дрожь: фигура чуть тянется и вздрагивает
                uv.y = 0.5 + (uv.y - 0.5) * (1.0 + 0.008 * sin(t * 1.7));
                uv.x += (hash11(tf + _Seed) - 0.5) * 0.004;

                // сбой: строки рвутся в стороны, каналы расходятся
                float glitch = step(0.93, hash11(tf * 0.37 + _Seed));
                float tear = (hash21(float2(floor(uv.y * 40.0), tf)) - 0.5) * 0.06 * glitch;
                uv.x += tear;
                float split = 0.012 * glitch;                              // каналы расходятся только на сбое

                float4 c = Sample(uv);
                float r = Sample(uv + float2(split, 0)).r;
                float b = Sample(uv - float2(split, 0)).b;
                float3 col = float3(r, c.g, b);
                float alpha = c.a;

                // лёгкая холодная тонировка: картинка уже сделана страшной, её цвета не ломаем
                float lum = dot(col, float3(0.3, 0.59, 0.11));
                col = lerp(col, lum.xxx, 0.15);
                col *= lerp(float3(1.0, 1.0, 1.0), float3(0.9, 0.96, 1.08), 0.5);

                // мерцание и плёночное зерно — только по самой фигуре
                float flicker = 0.93 + 0.07 * hash11(tf * 1.3 + _Seed);
                col *= flicker;
                col += (hash21(i.uv * 400.0 + tf) - 0.5) * 0.035 * step(0.5, alpha);

                // края квада гасим, чтобы не было рамки
                float edge = saturate(min(min(i.uv.x, 1.0 - i.uv.x), min(i.uv.y, 1.0 - i.uv.y)) * 12.0);
                alpha *= edge;

                // закрытая запись дневника — только тёмный силуэт
                if (_Locked > 0.5) { col = float3(0.13, 0.12, 0.15); alpha = step(0.35, alpha) * 0.9; }

                float n = hash21(floor(i.uv * 40.0) + _Seed);
                if (_Dissolve > 0.0)
                {
                    clip(n - _Dissolve);
                    col = lerp(col, _Color.rgb * 3.0, step(n - _Dissolve, 0.06));
                }

                col = lerp(col, float3(1, 1, 1), _Flash);
                return fixed4(saturate(col), saturate(alpha) * _Fade);
            }
            ENDCG
        }
    }
}
