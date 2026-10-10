// Процедурный силуэт призрака: жёсткий контур, светящиеся глаза, «дыхание» хвоста,
// пиксельное растворение при изгнании. Текстуры не нужны.
Shader "GhostWard/Ghost"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.2, 0.25, 1)
        _Fade ("Fade", Range(0, 1)) = 1
        _Shape ("Shape", Float) = 0
        _Flash ("Flash", Range(0, 1)) = 0
        _Dissolve ("Dissolve", Range(0, 1)) = 0
        _Seed ("Seed", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Fade;
                float _Shape;
                float _Flash;
                float _Dissolve;
                float _Seed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                return o;
            }

            float hash21(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

            float sdEllipse(float2 p, float2 c, float2 r) { return (length((p - c) / r) - 1.0) * min(r.x, r.y); }

            // маска с ровным, но жёстким краем шириной в один пиксель
            float fill(float d) { float w = max(fwidth(d), 1e-4); return saturate(0.5 - d / w); }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y + _Seed * 13.0;
                float2 uv = i.uv;
                bool jagged = _Shape > 1.5;
                bool round = _Shape > 0.5 && !jagged;

                // колыхание; у полтергейста — ступенчатые рывки
                uv.x += sin(uv.y * 12.0 + t * 3.0) * 0.015;
                if (jagged) uv += (float2(hash21(floor(t * 8.0)), hash21(floor(t * 8.0) + 3.1)) - 0.5) * 0.02;

                float2 p = uv - 0.5;

                float headR = round ? 0.33 : 0.27;
                float bodyW = round ? 0.22 : 0.27;
                float2 headC = float2(0.0, round ? 0.08 : 0.12);
                float bottom = round ? -0.22 : -0.34;
                float wave = jagged
                    ? bottom + 0.08 * abs(frac(p.x * 6.0 + t * 0.4) * 2.0 - 1.0)
                    : bottom + (round ? 0.06 : 0.04) * sin(p.x * 30.0 + t * 5.0);

                float dHead = length(p - headC) - headR;
                float dBody = max(abs(p.x) - bodyW, max(p.y - headC.y, wave - p.y));
                float d = min(dHead, dBody);
                if (!round && !jagged)
                {
                    // острый капюшон
                    float dHood = max(abs(p.x) * 1.9 + p.y - 0.49, 0.2 - p.y);
                    d = min(d, dHood);
                }

                float inside = fill(d);
                float rim = inside * saturate((d + 0.022) / max(fwidth(d), 1e-4));

                // глаза и рот
                float eyes, cores;
                if (round)
                {
                    // один большой светящийся глаз с вертикальным зрачком
                    float slit = 0.02 + 0.015 * sin(t * 2.0);
                    float iris = fill(sdEllipse(p, headC + float2(0, 0.03), float2(0.12, 0.12)));
                    float pupil = fill(sdEllipse(p, headC + float2(sin(t * 0.7) * 0.04, 0.03), float2(slit, 0.09)));
                    eyes = iris * pupil;
                    cores = iris * (1.0 - pupil);
                }
                else
                {
                    float2 eo = float2(0.1, headC.y + 0.01);
                    float2 er = jagged ? float2(0.05, 0.05) : float2(0.065, 0.045);
                    eyes = max(fill(sdEllipse(p, float2(-eo.x, eo.y), er)), fill(sdEllipse(p, float2(eo.x, eo.y), er)));
                    float2 cr = er * 0.45;
                    cores = max(fill(sdEllipse(p, float2(-eo.x, eo.y), cr)), fill(sdEllipse(p, float2(eo.x, eo.y), cr)));
                }
                float mouthOpen = 0.06 + 0.04 * sin(t * 2.3);
                float mouth = fill(sdEllipse(p, float2(0.0, headC.y - 0.13), float2(0.05, mouthOpen)));

                // цвет тела: бледный оттенок с диагональной штриховкой
                float3 tint = lerp(float3(0.92, 0.95, 1.0), _Color.rgb, 0.35);
                float hatch = step(0.5, frac((p.x + p.y) * 38.0)) * 0.07;
                float3 col = tint - hatch;
                float alpha = inside * lerp(0.35, 0.9, saturate((p.y - bottom) * 2.2));

                col = lerp(col, _Color.rgb * 1.6, rim);
                alpha = max(alpha, rim);

                float3 eyeCol = float3(0.02, 0.0, 0.04);
                float holes = max(eyes, mouth) * inside;
                col = lerp(col, eyeCol, holes);
                alpha = lerp(alpha, 1.0, holes);
                col = lerp(col, _Color.rgb * 2.5, cores * inside);

                // мягкое свечение снаружи силуэта
                float glow = exp(-max(d, 0.0) * 22.0) * (1.0 - inside) * 0.45;
                col = lerp(col, _Color.rgb, glow / max(alpha + glow, 1e-3));
                alpha += glow;

                // пиксельное растворение
                float n = hash21(floor(i.uv * 28.0) + _Seed);
                if (_Dissolve > 0.0)
                {
                    clip(n - _Dissolve);
                    float edge = step(n - _Dissolve, 0.08);
                    col = lerp(col, _Color.rgb * 3.0, edge);
                }

                col = lerp(col, float3(1, 1, 1), _Flash);
                return half4(col, saturate(alpha) * _Fade);
            }
            ENDHLSL
        }
    }
}
