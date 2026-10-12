// Процедурные силуэты сущностей с жёсткими краями. Текстуры не нужны.
// _Shape — номер силуэта: 0 призрак, 1 тень, 2 полтергейст, 3 албасты,
// 4 двойник, 5 кикимора, 6 упырь, 7 мара. _Seed даёт вариации внешности у разных экземпляров.
// Шейдер работает и на квадах в мире, и на RawImage в интерфейсе (энциклопедия, скример).
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
        _Flip ("Flip", Float) = 0
        _Locked ("Locked", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Body, _Color;
            float _Fade, _Shape, _Flash, _Dissolve, _Seed, _Flip, _Locked;

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
            float sdEllipse(float2 p, float2 c, float2 r) { return (length((p - c) / r) - 1.0) * min(r.x, r.y); }
            float sdCapsule(float2 p, float2 a, float2 b, float r)
            {
                float2 pa = p - a, ba = b - a;
                float h = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * h) - r;
            }
            float sdBox(float2 p, float2 c, float2 h) { float2 d = abs(p - c) - h; return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0); }
            float fill(float d) { float w = max(fwidth(d), 1e-4); return saturate(0.5 - d / w); }

            // зубы внутри эллипса рта: треугольники сверху и снизу
            float teeth(float2 p, float2 c, float2 r, float n, float len)
            {
                float dx = (p.x - c.x) / r.x;
                if (abs(dx) >= 1.0) return 0.0;
                float edge = r.y * sqrt(1.0 - dx * dx);
                float tri = abs(frac((dx * 0.5 + 0.5) * n) * 2.0 - 1.0);
                float l = r.y * len * (1.0 - tri);
                float upper = step(c.y + edge - p.y, l);
                float lower = step(p.y - (c.y - edge), l);
                return saturate(upper + lower);
            }

            // три когтя из точки кисти
            float claws(float2 p, float2 hand, float2 dir, float len)
            {
                float2 side = float2(-dir.y, dir.x);
                float d = 1e5;
                for (int k = -1; k <= 1; k++)
                    d = min(d, sdCapsule(p, hand, hand + (dir + side * k * 0.45) * len, 0.008));
                return d;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y + _Seed * 13.0;
                float tq = floor(t * 8.0) / 8.0;                    // дёрганое покадровое время
                float s1 = hash11(_Seed + 1.0), s2 = hash11(_Seed + 2.0), s3 = hash11(_Seed + 3.0);
                int shape = (int)round(_Shape);

                float2 uv = i.uv;
                if (_Flip > 0.5) uv.y = 1.0 - uv.y;

                // помехи: изредка строки смещаются, фигура подёргивается
                float glitch = step(0.94, hash11(floor(t * 12.0) + _Seed));
                uv.x += glitch * (hash21(float2(floor(uv.y * 24.0), floor(t * 12.0))) - 0.5) * 0.08;
                uv.x += step(0.9, hash11(floor(t * 3.0) + _Seed * 7.0)) * 0.02;
                if (shape != 1 && shape != 4) uv.x += sin(uv.y * 12.0 + tq * 3.0) * 0.01;

                float2 p = uv - 0.5;
                p.x += p.y * (s1 - 0.5) * 0.15;                     // небольшой наклон у каждого своего

                float d = 1e5, bottom = -0.46;
                float holes = 0, cores = 0, toothMask = 0, hair = 0, shadeMask = 0;
                float opacity = 0.88;
                float hs = 0.9 + s2 * 0.2;                          // размер головы

                if (shape == 0)
                {
                    // ПРИЗРАК: высокая фигура в рваном саване, лицо в тени капюшона,
                    // вытянутый рот в беззвучном крике, чёрные подтёки из глаз, длинные руки тянутся вперёд
                    float2 hc = float2(0.0, 0.25);
                    bottom = -0.47;
                    float rag = bottom + 0.07 * abs(frac(p.x * 7.0 + tq * 0.4) * 2.0 - 1.0);
                    float w = lerp(0.11, 0.24, saturate((0.15 - p.y) / 0.6));
                    d = sdEllipse(p, hc + float2(0.0, 0.01), float2(0.13, 0.16) * hs);          // капюшон
                    d = min(d, max(abs(p.x) - w, max(p.y - 0.17, rag - p.y)));                 // саван
                    float sway = sin(tq * 3.0) * 0.025;
                    float2 h1 = float2(-0.27, -0.17 + sway), h2 = float2(0.27, -0.17 - sway);
                    d = min(d, sdCapsule(p, float2(-0.12, 0.1), h1, 0.022));
                    d = min(d, sdCapsule(p, float2(0.12, 0.1), h2, 0.022));
                    d = min(d, claws(p, h1, float2(-0.3, -1.0), 0.06));
                    d = min(d, claws(p, h2, float2(0.3, -1.0), 0.06));
                    // лицо в глубине капюшона
                    float face = fill(sdEllipse(p, hc - float2(0.0, 0.01), float2(0.085, 0.12) * hs));
                    hair = face * 0.55;
                    float2 ex = float2(0.036, hc.y + 0.02);
                    float eyes = max(fill(sdEllipse(p, float2(-ex.x, ex.y), float2(0.02, 0.034))), fill(sdEllipse(p, ex, float2(0.02, 0.034))));
                    float streak = (fill(sdBox(p, float2(-ex.x, ex.y - 0.08), float2(0.005, 0.08))) + fill(sdBox(p, float2(ex.x, ex.y - 0.09), float2(0.005, 0.09)))) * 0.8;
                    float mouth = fill(sdEllipse(p, float2(0.0, hc.y - 0.08), float2(0.022, 0.05 + 0.02 * sin(t * 2.0))));
                    holes = saturate(eyes + streak + mouth);
                    cores = max(fill(sdEllipse(p, float2(-ex.x, ex.y), float2(0.006, 0.006))), fill(sdEllipse(p, ex, float2(0.006, 0.006))));
                    opacity = 0.75;
                }
                else if (shape == 1)
                {
                    // ТЕНЬ: высокий безликий силуэт, руки до колен, когти, рваный край
                    float tilt = sin(tq * 1.3) * 0.02;
                    d = length(p - float2(tilt, 0.34)) - 0.08 * hs;
                    float w = lerp(0.05, 0.15, saturate((0.27 - p.y) / 0.07));
                    d = min(d, max(abs(p.x) - w, max(p.y - 0.28, -0.12 - p.y)));
                    d = min(d, sdCapsule(p, float2(-0.06, -0.12), float2(-0.07, -0.47), 0.04));
                    d = min(d, sdCapsule(p, float2(0.06, -0.12), float2(0.07, -0.47), 0.04));
                    float sway = sin(tq * 2.0) * 0.02;
                    d = min(d, sdCapsule(p, float2(-0.14, 0.23), float2(-0.19 + sway, -0.22), 0.028));
                    d = min(d, sdCapsule(p, float2(0.14, 0.23), float2(0.19 - sway, -0.22), 0.028));
                    d = min(d, claws(p, float2(-0.19 + sway, -0.22), float2(0.0, -1.0), 0.07));
                    d = min(d, claws(p, float2(0.19 - sway, -0.22), float2(0.0, -1.0), 0.07));
                    d += (hash21(floor(uv * 48.0) + floor(t * 8.0)) - 0.5) * 0.012;
                    cores = max(fill(sdEllipse(p, float2(tilt - 0.03, 0.345), float2(0.016, 0.008))),
                                fill(sdEllipse(p, float2(tilt + 0.03, 0.345), float2(0.016, 0.008))));
                    opacity = 0.97;
                }
                else if (shape == 2)
                {
                    // ПОЛТЕРГЕЙСТ: колючий клубок со множеством глаз и пастью
                    float a = atan2(p.y, p.x);
                    d = length(p * float2(1.0, 1.1) - float2(0.0, 0.05)) - 0.29 - 0.045 * sin(a * 9.0 + tq * 6.0);
                    int n = 3 + (int)(s3 * 4.0);
                    for (int k = 0; k < 7; k++)
                    {
                        if (k >= n) break;
                        float2 c = float2(hash11(k + _Seed * 3.1) - 0.5, hash11(k * 1.7 + _Seed) * 0.5 - 0.05) * float2(0.36, 0.6);
                        float r = 0.025 + hash11(k + 9.0 + _Seed) * 0.03;
                        holes = max(holes, fill(sdEllipse(p, c, float2(r, r))));
                        cores = max(cores, fill(sdEllipse(p, c, float2(r, r) * 0.35)));
                    }
                    float2 mc = float2(0.0, -0.1), mr = float2(0.15, 0.05 + 0.02 * sin(t * 3.0));
                    float mouth = fill(sdEllipse(p, mc, mr));
                    toothMask = mouth * teeth(p, mc, mr, 9.0, 0.7);
                    holes = max(holes, mouth);
                }
                else if (shape == 3)
                {
                    // АЛБАСТЫ: длинное платье, волосы закрывают лицо, видна щель с одним глазом, когтистые руки
                    float hw = 0.14 + 0.02 * sin(p.y * 35.0 + t * 2.0);
                    float dHead = sdEllipse(p, float2(0.0, 0.31), float2(0.085, 0.11) * hs);
                    float dHair = min(sdEllipse(p, float2(0.0, 0.32), float2(0.125, 0.12)), max(abs(p.x) - hw, max(p.y - 0.32, -0.06 - p.y)));
                    float dw = lerp(0.08, 0.2, saturate((0.12 - p.y) / 0.55));
                    float wave = -0.47 + 0.04 * abs(frac(p.x * 9.0) * 2.0 - 1.0);
                    d = min(min(dHead, dHair), max(abs(p.x) - dw, max(p.y - 0.16, wave - p.y)));
                    float reach = sin(tq * 2.0) * 0.02;
                    d = min(d, sdCapsule(p, float2(-0.11, 0.13), float2(-0.2, -0.2 + reach), 0.022));
                    d = min(d, sdCapsule(p, float2(0.11, 0.13), float2(0.22, -0.16 - reach), 0.022));
                    d = min(d, claws(p, float2(-0.2, -0.2 + reach), float2(-0.2, -1.0), 0.08));
                    d = min(d, claws(p, float2(0.22, -0.16 - reach), float2(0.2, -1.0), 0.08));
                    float gap = fill(sdEllipse(p, float2(0.025, 0.3), float2(0.028, 0.07)));
                    hair = saturate(fill(dHair) + fill(dHead)) * (1.0 - gap);
                    cores = fill(sdEllipse(p, float2(0.027, 0.305), float2(0.014, 0.009)));
                }
                else if (shape == 4)
                {
                    // ДВОЙНИК: чёрный человеческий силуэт с огромной белой улыбкой
                    d = sdEllipse(p, float2(0.0, 0.31), float2(0.095, 0.115) * hs);
                    d = min(d, sdBox(p, float2(0.0, 0.03), float2(0.13, 0.17)));
                    d = min(d, sdCapsule(p, float2(-0.06, -0.12), float2(-0.065, -0.47), 0.045));
                    d = min(d, sdCapsule(p, float2(0.06, -0.12), float2(0.065, -0.47), 0.045));
                    d = min(d, sdCapsule(p, float2(-0.155, 0.17), float2(-0.17, -0.17), 0.03));
                    d = min(d, sdCapsule(p, float2(0.155, 0.17), float2(0.17, -0.17), 0.03));
                    float2 q = p - float2(0.0, p.x * p.x * 5.0);
                    float2 mc = float2(0.0, 0.255), mr = float2(0.085, 0.022 + 0.008 * sin(t));
                    float grin = fill(sdEllipse(q, mc, mr));
                    toothMask = grin * step(0.18, frac(p.x * 70.0));
                    holes = grin;
                    float2 ec = float2(0.04, 0.325);
                    float eyes = max(fill(sdEllipse(p, -ec * float2(1, -1), float2(0.02, 0.014))), fill(sdEllipse(p, ec, float2(0.02, 0.014))));
                    toothMask = max(toothMask, eyes);
                    holes = max(holes, eyes);
                    opacity = 0.97;
                }
                else if (shape == 5)
                {
                    // КИКИМОРА: тощее паукообразное тельце, длинные суставчатые конечности, огромные глаза
                    d = sdEllipse(p, float2(0.0, -0.05), float2(0.13, 0.1));
                    d = min(d, length(p - float2(0.0, 0.11)) - 0.085 * hs);
                    for (int k = 0; k < 2; k++)
                    {
                        float side = k == 0 ? -1.0 : 1.0;
                        float a1 = sin(tq * 8.0 + k * 2.0) * 0.03, a2 = sin(tq * 8.0 + k * 2.0 + 1.5) * 0.03;
                        float2 b1 = float2(0.09 * side, -0.02), k1 = float2(0.28 * side, 0.14 + a1), f1 = float2(0.38 * side, -0.3);
                        float2 b2 = float2(0.07 * side, -0.1), k2 = float2(0.22 * side, -0.02 + a2), f2 = float2(0.3 * side, -0.44);
                        d = min(d, min(sdCapsule(p, b1, k1, 0.018), sdCapsule(p, k1, f1, 0.014)));
                        d = min(d, min(sdCapsule(p, b2, k2, 0.018), sdCapsule(p, k2, f2, 0.014)));
                    }
                    float dHair = min(sdEllipse(p, float2(0.0, 0.13), float2(0.105, 0.1)), max(abs(p.x) - (0.1 + 0.015 * sin(p.y * 40.0 + t)), max(p.y - 0.13, 0.0 - p.y)));
                    d = min(d, dHair);
                    hair = fill(dHair) * (1.0 - fill(sdBox(p, float2(0.0, 0.12), float2(0.075, 0.05))));
                    float2 ec = float2(0.035, 0.125);
                    float eyes = max(fill(sdEllipse(p, float2(-ec.x, ec.y), float2(0.028, 0.034))), fill(sdEllipse(p, ec, float2(0.028, 0.034))));
                    holes = eyes;
                    cores = max(fill(sdEllipse(p, float2(-ec.x, ec.y), float2(0.012, 0.016))), fill(sdEllipse(p, ec, float2(0.012, 0.016))));
                    float2 mc = float2(0.0, 0.07), mr = float2(0.03, 0.012);
                    float mouth = fill(sdEllipse(p, mc, mr));
                    toothMask = mouth * teeth(p, mc, mr, 5.0, 0.8);
                    holes = max(holes, mouth);
                    bottom = -0.2;
                }
                else if (shape == 6)
                {
                    // УПЫРЬ: сгорбленный мертвец, голова вперёд, разинутая пасть с клыками, рёбра
                    float2 hc = float2(0.05, 0.26);
                    d = sdEllipse(p, hc, float2(0.085, 0.095) * hs);
                    d = min(d, sdCapsule(p, float2(0.0, 0.13), float2(-0.03, -0.13), 0.1));
                    d = min(d, sdCapsule(p, float2(-0.06, -0.15), float2(-0.08, -0.47), 0.04));
                    d = min(d, sdCapsule(p, float2(0.04, -0.15), float2(0.06, -0.47), 0.04));
                    float reach = sin(tq * 4.0) * 0.03;
                    float2 e1 = float2(0.2, 0.0), h1 = float2(0.26, -0.18 + reach);
                    float2 e2 = float2(-0.15, -0.02), h2 = float2(-0.2, -0.24 - reach);
                    d = min(d, min(sdCapsule(p, float2(0.08, 0.1), e1, 0.025), sdCapsule(p, e1, h1, 0.02)));
                    d = min(d, min(sdCapsule(p, float2(-0.09, 0.1), e2, 0.025), sdCapsule(p, e2, h2, 0.02)));
                    d = min(d, claws(p, h1, float2(0.2, -1.0), 0.07));
                    d = min(d, claws(p, h2, float2(-0.2, -1.0), 0.07));
                    float2 mc = hc + float2(0.01, -0.06), mr = float2(0.05, 0.045 + 0.02 * sin(t * 3.0));
                    float mouth = fill(sdEllipse(p, mc, mr));
                    toothMask = mouth * teeth(p, mc, mr, 4.0, 0.9);
                    float2 el = hc + float2(-0.03, 0.02), er = hc + float2(0.035, 0.02);
                    float eyes = max(fill(sdEllipse(p, el, float2(0.022, 0.02))), fill(sdEllipse(p, er, float2(0.022, 0.02))));
                    holes = max(mouth, eyes);
                    cores = max(fill(sdEllipse(p, el, float2(0.009, 0.009))), fill(sdEllipse(p, er, float2(0.009, 0.009))));
                    float ribs = step(0.55, frac(p.y * 30.0)) * fill(sdEllipse(p, float2(-0.015, 0.0), float2(0.065, 0.1)));
                    shadeMask = ribs;
                }
                else
                {
                    // МАРА: сгорбленная старуха в лохмотьях, волосы до пола, длинные костлявые пальцы
                    d = sdEllipse(p, float2(0.0, 0.04), float2(0.2, 0.15));
                    float cw = lerp(0.14, 0.3, saturate((0.05 - p.y) / 0.5));
                    float wave = -0.46 + 0.05 * abs(frac(p.x * 7.0 + 0.3) * 2.0 - 1.0);
                    d = min(d, max(abs(p.x) - cw, max(p.y - 0.05, wave - p.y)));
                    float dHead = sdEllipse(p, float2(0.0, 0.17), float2(0.08, 0.09) * hs);
                    d = min(d, dHead);
                    float dHair = min(sdEllipse(p, float2(0.0, 0.18), float2(0.115, 0.11)), max(abs(p.x) - (0.12 + 0.02 * sin(p.y * 30.0 + t * 1.5)), max(p.y - 0.18, -0.12 - p.y)));
                    d = min(d, dHair);
                    float creep = sin(tq * 1.5) * 0.015;
                    float dF = 1e5;
                    for (int k = 0; k < 2; k++)
                    {
                        float side = k == 0 ? -1.0 : 1.0;
                        float2 hand = float2(0.1 * side, -0.1);
                        for (int f = 0; f < 4; f++)
                        {
                            float2 tip = float2((0.06 + f * 0.04) * side, -0.33 - f * 0.015 + creep);
                            dF = min(dF, sdCapsule(p, hand, tip, 0.009));
                        }
                    }
                    d = min(d, dF);
                    toothMask = fill(dF) * 0.8;                     // костлявые пальцы бледнее плаща
                    float gaps = fill(sdBox(p, float2(0.0, 0.16), float2(0.05, 0.015)));
                    hair = saturate(fill(dHair) + fill(dHead)) * (1.0 - gaps);
                    cores = max(fill(sdEllipse(p, float2(-0.028, 0.16), float2(0.012, 0.008))), fill(sdEllipse(p, float2(0.028, 0.16), float2(0.012, 0.008))));
                }

                float inside = fill(d);
                float rim = inside * saturate((d + 0.014) / max(fwidth(d), 1e-4));

                float3 col = _Body.rgb;
                float light = dot(_Body.rgb, float3(0.33, 0.33, 0.33));
                col -= step(0.5, frac((p.x + p.y) * 40.0)) * 0.06 * step(0.3, light);
                col = lerp(col, col * 0.45, shadeMask);
                col = lerp(col, float3(0.02, 0.02, 0.025), hair);

                float alpha = inside * lerp(0.4, opacity, saturate((p.y - bottom) * 2.2));
                float rimK = shape == 1 || shape == 4 ? 0.5 : 0.6;
                // контур светлее самой сущности — чтобы тёмные (тень, двойник) читались на тёмной комнате
                float3 rimCol = lerp(_Color.rgb, float3(0.72, 0.78, 0.86), 0.4);
                col = lerp(col, rimCol, rim * rimK);
                alpha = max(alpha, rim * rimK);

                holes *= inside;
                col = lerp(col, float3(0.015, 0.0, 0.02), holes);
                alpha = lerp(alpha, 1.0, holes);
                col = lerp(col, float3(0.92, 0.9, 0.82), toothMask * inside);
                col = lerp(col, _Color.rgb * 2.5, cores * inside);
                alpha = max(alpha, cores * inside);

                float edge = saturate(min(min(i.uv.x, 1.0 - i.uv.x), min(i.uv.y, 1.0 - i.uv.y)) * 10.0);
                float glow = exp(-max(d, 0.0) * 30.0) * (1.0 - inside) * (shape == 1 || shape == 4 ? 0.2 : 0.24) * edge;
                col = lerp(col, rimCol, glow / max(alpha + glow, 1e-3));
                alpha += glow;

                // закрытая запись энциклопедии — только чёрный силуэт
                if (_Locked > 0.5) { col = float3(0.13, 0.12, 0.15); alpha = inside * 0.9; }

                float n = hash21(floor(i.uv * 28.0) + _Seed);
                if (_Dissolve > 0.0)
                {
                    clip(n - _Dissolve);
                    col = lerp(col, _Color.rgb * 3.0, step(n - _Dissolve, 0.08));
                }

                col = lerp(col, float3(1, 1, 1), _Flash);
                return fixed4(col, saturate(alpha) * _Fade);
            }
            ENDCG
        }
    }
}
