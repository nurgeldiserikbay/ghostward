// Сущность по фотореалистичной картинке (Resources/Entities/<Kind>.png).
// Картинка — фигура с прозрачным фоном (или на чёрном: тогда вырезаем чёрное, _Key = 1).
// В мире (_World = 1) фигура «снимается» той же камерой, что и комната (LookCommon): тот же вид,
// свет и зерно; качается и дышит, голова иногда дёргается, ступни уходят в тень, края мягкие.
// В интерфейсе (дневник, скример) — та же картинка без окраски под камеру.
// Параметры, общие с процедурным шейдером: _Fade, _Flash, _Dissolve, _Flip, _Locked и трафарет UI.
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
        _World ("In world", Float) = 0
        _Dim ("Distance dim", Range(0, 1)) = 1
        _Crop ("Figure box in picture (x, y, w, h)", Vector) = (0, 0, 1, 1)

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
            #include "LookCommon.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float _Fade, _Flash, _Dissolve, _Seed, _Flip, _Locked, _Key, _World, _Dim;
            float4 _Crop;   // в мире квад обтягивает саму фигуру: ступни — ровно по нижнему краю

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 screen : TEXCOORD1; float3 world : TEXCOORD2; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.screen = ComputeScreenPos(o.pos);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float hash11(float x) { return frac(sin(x * 127.1) * 43758.5453); }
            float hash21(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

            // цвет и непрозрачность картинки в точке: прозрачность из PNG и вырезка чёрного фона
            float4 Sample(float2 uv)
            {
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) return 0;
                if (_World > 0.5) uv = _Crop.xy + uv * _Crop.zw;
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
                float h = _Flip > 0.5 ? 1.0 - uv.y : uv.y;        // высота по фигуре: 0 — ступни, 1 — макушка

                // «дыхание» и дрожь: фигура чуть тянется и вздрагивает
                uv.y = 0.5 + (uv.y - 0.5) * (1.0 + 0.008 * sin(t * 1.7));
                uv.x += (hash11(tf + _Seed) - 0.5) * 0.004;

                if (_World > 0.5)
                {
                    // медленное качание: ноги стоят, верх гнётся
                    uv.x -= sin(t * 0.8 + _Seed) * 0.014 * h * h;
                    // голова иногда резко дёргается в сторону — неестественно, как в хоррорах
                    float twitch = step(0.965, hash11(floor(t * 5.0) + _Seed * 3.0));
                    uv.x += twitch * (hash11(floor(t * 5.0) + 7.0) - 0.5) * 0.05 * smoothstep(0.72, 0.85, h);
                }

                // сбой: строки рвутся в стороны, каналы расходятся
                float glitch = step(0.93, hash11(tf * 0.37 + _Seed));
                float tear = (hash21(float2(floor(uv.y * 40.0), tf)) - 0.5) * 0.06 * glitch;
                uv.x += tear;
                float split = 0.012 * glitch;

                float4 c = Sample(uv);
                float r = Sample(uv + float2(split, 0)).r;
                float b = Sample(uv - float2(split, 0)).b;
                float3 col = float3(r, c.g, b);
                float alpha = c.a;

                if (_World > 0.5)
                {
                    // мягкий край: усредняем прозрачность с соседями — фигура не «вырезана ножницами»
                    float2 o = _MainTex_TexelSize.xy * 2.5;
                    float soft = (Sample(uv + float2(o.x, 0)).a + Sample(uv - float2(o.x, 0)).a
                                + Sample(uv + float2(0, o.y)).a + Sample(uv - float2(0, o.y)).a) * 0.25;
                    alpha = min(alpha, soft * 1.15);
                    // ступни чуть уходят в тень пола (квад обрезан по фигуре — ступни у самого края)
                    alpha *= smoothstep(0.0, 0.03, h);
                    float2 sp = i.screen.xy / max(i.screen.w, 1e-4);
                    col = GW_Grade(col, sp, _Dim);
                    alpha *= GW_Occlusion(i.world);   // за настоящим столом или косяком — не видна
                }
                else
                {
                    // в интерфейсе — лёгкая холодная тонировка и плёночное зерно по фигуре
                    float lum = dot(col, float3(0.3, 0.59, 0.11));
                    col = lerp(col, lum.xxx, 0.15);
                    col *= lerp(float3(1.0, 1.0, 1.0), float3(0.9, 0.96, 1.08), 0.5);
                    col += (hash21(i.uv * 400.0 + tf) - 0.5) * 0.035 * step(0.5, alpha);
                }

                // мерцание
                col *= 0.93 + 0.07 * hash11(tf * 1.3 + _Seed);

                // края квада гасим, чтобы не было рамки; низ (ступни) в мире — своим затуханием выше
                float edgeY = _World > 0.5 ? 1.0 - h : min(i.uv.y, 1.0 - i.uv.y);
                // в мире квад обтягивает фигуру (поля 3%) — край гасим узко, чтобы не съесть макушку
                float edge = saturate(min(min(i.uv.x, 1.0 - i.uv.x), edgeY) * (_World > 0.5 ? 40.0 : 12.0));
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
