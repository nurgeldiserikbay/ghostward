// Процедурные силуэты сущностей: жёсткий контур, светящиеся глаза, пиксельное растворение.
// _Shape: 0 призрак, 1 тень, 2 полтергейст, 3 албасты. Текстуры не нужны.
Shader "GhostWard/Entity"
{
    Properties
    {
        _Body ("Body", Color) = (0.8, 0.85, 0.95, 1)
        _Color ("Accent", Color) = (0.5, 0.7, 1, 1)
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
                half4 _Body;
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
            float fill(float d) { float w = max(fwidth(d), 1e-4); return saturate(0.5 - d / w); }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y + _Seed * 13.0;
                float2 uv = i.uv;
                int shape = (int)round(_Shape);

                uv.x += sin(uv.y * 12.0 + t * 3.0) * (shape == 1 ? 0.025 : 0.012);
                if (shape == 2) uv += (float2(hash21(floor(t * 8.0)), hash21(floor(t * 8.0) + 3.1)) - 0.5) * 0.02;
                float2 p = uv - 0.5;

                float d, bottom;
                float eyes = 0, cores = 0, mouth = 0, hair = 0;
                float opacity = 0.85;

                if (shape == 0)
                {
                    // призрак: саван с острым капюшоном
                    float2 hc = float2(0.0, 0.12);
                    bottom = -0.34;
                    float wave = bottom + 0.04 * sin(p.x * 30.0 + t * 5.0);
                    float dHead = length(p - hc) - 0.27;
                    float dBody = max(abs(p.x) - 0.27, max(p.y - hc.y, wave - p.y));
                    float dHood = max(abs(p.x) * 1.9 + p.y - 0.49, 0.2 - p.y);
                    d = min(min(dHead, dBody), dHood);
                    float2 er = float2(0.065, 0.045);
                    eyes = max(fill(sdEllipse(p, float2(-0.1, 0.13), er)), fill(sdEllipse(p, float2(0.1, 0.13), er)));
                    cores = max(fill(sdEllipse(p, float2(-0.1, 0.13), er * 0.4)), fill(sdEllipse(p, float2(0.1, 0.13), er * 0.4)));
                    mouth = fill(sdEllipse(p, float2(0.0, -0.01), float2(0.05, 0.06 + 0.04 * sin(t * 2.3))));
                }
                else if (shape == 1)
                {
                    // тень: высокая фигура без черт, тающая книзу
                    bottom = -0.46;
                    float dHead = length(p - float2(0.0, 0.3)) - 0.1;
                    float w = lerp(0.07, 0.22, saturate((0.21 - p.y) / 0.12));
                    float wave = bottom + 0.06 * sin(p.x * 22.0 + t * 3.0);
                    float dBody = max(abs(p.x) - w, max(p.y - 0.23, wave - p.y));
                    d = min(dHead, dBody);
                    cores = max(fill(sdEllipse(p, float2(-0.035, 0.31), float2(0.02, 0.011))),
                                fill(sdEllipse(p, float2(0.035, 0.31), float2(0.02, 0.011))));
                    opacity = 0.96;
                }
                else if (shape == 2)
                {
                    // полтергейст: рваный клубок
                    float2 hc = float2(0.0, 0.12);
                    bottom = -0.34;
                    float wave = bottom + 0.08 * abs(frac(p.x * 6.0 + t * 0.4) * 2.0 - 1.0);
                    d = min(length(p - hc) - 0.27, max(abs(p.x) - 0.27, max(p.y - hc.y, wave - p.y)));
                    float2 er = float2(0.05, 0.05);
                    eyes = max(fill(sdEllipse(p, float2(-0.1, 0.13), er)), fill(sdEllipse(p, float2(0.1, 0.13), er)));
                    cores = max(fill(sdEllipse(p, float2(-0.1, 0.13), er * 0.45)), fill(sdEllipse(p, float2(0.1, 0.13), er * 0.45)));
                    mouth = fill(sdEllipse(p, float2(0.0, -0.01), float2(0.08, 0.05)));
                }
                else
                {
                    // албасты: женщина в длинном платье, волосы закрывают лицо, видна щель с одним глазом
                    bottom = -0.47;
                    float dHead = sdEllipse(p, float2(0.0, 0.3), float2(0.09, 0.115));
                    float hw = 0.15 + 0.02 * sin(p.y * 35.0 + t * 2.0);
                    float dHair = max(abs(p.x) - hw, max(p.y - 0.4, -0.05 - p.y));
                    float dw = lerp(0.09, 0.21, saturate((0.12 - p.y) / 0.55));
                    float wave = bottom + 0.04 * abs(frac(p.x * 9.0) * 2.0 - 1.0);
                    float dDress = max(abs(p.x) - dw, max(p.y - 0.15, wave - p.y));
                    d = min(min(dHead, dHair), dDress);
                    float gap = fill(sdEllipse(p, float2(0.025, 0.29), float2(0.03, 0.07)));
                    hair = saturate(fill(dHair) + fill(dHead)) * (1.0 - gap);
                    cores = fill(sdEllipse(p, float2(0.028, 0.3), float2(0.016, 0.011)));
                }

                float inside = fill(d);
                float rim = inside * saturate((d + 0.018) / max(fwidth(d), 1e-4));

                float3 col = _Body.rgb;
                float light = dot(_Body.rgb, float3(0.33, 0.33, 0.33));
                col -= step(0.5, frac((p.x + p.y) * 38.0)) * 0.07 * step(0.3, light);
                col = lerp(col, float3(0.02, 0.02, 0.025), hair);

                float alpha = inside * lerp(0.35, opacity, saturate((p.y - bottom) * 2.2));
                col = lerp(col, _Color.rgb * 1.2, rim * (shape == 1 ? 0.35 : 1.0));
                alpha = max(alpha, rim * (shape == 1 ? 0.5 : 1.0));

                float holes = max(eyes, mouth) * inside;
                col = lerp(col, float3(0.02, 0.0, 0.03), holes);
                alpha = lerp(alpha, 1.0, holes);
                col = lerp(col, _Color.rgb * 2.5, cores * inside);
                alpha = max(alpha, cores * inside);

                float glow = exp(-max(d, 0.0) * 22.0) * (1.0 - inside) * (shape == 1 ? 0.15 : 0.35);
                col = lerp(col, _Color.rgb, glow / max(alpha + glow, 1e-3));
                alpha += glow;

                float n = hash21(floor(i.uv * 28.0) + _Seed);
                if (_Dissolve > 0.0)
                {
                    clip(n - _Dissolve);
                    col = lerp(col, _Color.rgb * 3.0, step(n - _Dissolve, 0.08));
                }

                col = lerp(col, float3(1, 1, 1), _Flash);
                return half4(col, saturate(alpha) * _Fade);
            }
            ENDHLSL
        }
    }
}
