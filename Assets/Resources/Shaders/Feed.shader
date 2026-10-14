// Видео с камеры в режиме гироскопа. Вид задаётся настройкой (_Style) и эпизодами ночного режима (_Alt, _Mode):
// 0 — обычный, 1 — тёмный, 2 — зелёное ночное видение, 3 — ИК-камера наблюдения, 4 — VHS, 5 — тепловизор,
// 6 — ночная ИК-съёмка любительской камерой (серо-зелёная, пятно ИК-подсветки в центре).
// В тепловизоре маска нейросети (_SegTex: R — человек, G — животное) делает живых горячими;
// _ColdPerson — «одержимость»: человек холодный, как сущность.
// _Glitch — помехи у логова и рядом с сущностью.
// _Possess 0..1 — «одержимость» в любом виде камеры: по силуэту человека (маска _SegTex) ползут тёмные вены,
// тело темнеет, по краю — тёмно-красный ореол; на лице (_EyeA/_EyeB/_Mouth от поиска лиц) глаза заливает
// чернотой с красными точками, от глаз расходятся вены, рот темнеет и растягивается.
Shader "GhostWard/Feed"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "black" {}
        _Style ("Style", Float) = 1
        _Alt ("Episode style", Float) = 2
        _Mode ("Episode blend", Range(0, 1)) = 0
        _Glitch ("Glitch", Range(0, 1)) = 0
        _SegTex ("Segmentation", 2D) = "black" {}
        _SegOn ("Segmentation on", Float) = 0
        _SegAngle ("Segmentation angle", Float) = 0
        _SegMirror ("Segmentation mirror", Float) = 0
        _ColdPerson ("Cold person", Float) = 0
        _Possess ("Possession", Range(0, 1)) = 0
        _FaceOn ("Face found", Float) = 0
        _EyeA ("Eye A", Vector) = (0, 0, 0, 0)
        _EyeB ("Eye B", Vector) = (0, 0, 0, 0)
        _Mouth ("Mouth", Vector) = (0, 0, 0, 0)
        _FaceSize ("Face size", Float) = 0.2
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "CanUseSpriteAtlas" = "False" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Style, _Alt, _Mode, _Glitch;
            sampler2D _SegTex;
            float _SegOn, _SegAngle, _SegMirror, _ColdPerson;
            float _Possess, _FaceOn, _FaceSize;
            float4 _EyeA, _EyeB, _Mouth;

            // координата кадра камеры → координата выпрямленного кадра, на котором считалась маска
            float2 segUv(float2 t)
            {
                float2 q = t;
                if (_SegMirror > 0.5) q.y = 1.0 - q.y;
                float a = radians(-_SegAngle);
                float c = cos(a), s = sin(a);
                float2 d = q - 0.5;
                return 0.5 + float2(d.x * c - d.y * s, d.x * s + d.y * c);
            }

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; float4 screen : TEXCOORD1; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                o.screen = ComputeScreenPos(o.pos);
                return o;
            }

            float hash21(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }
            float luma(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }

            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i), b = hash21(i + float2(1, 0)), c = hash21(i + float2(0, 1)), d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // тонкие ветвящиеся вены: «гребни» шума двух масштабов
            float veins(float2 p)
            {
                float a = pow(1.0 - abs(vnoise(p) * 2.0 - 1.0), 7.0);
                float b = pow(1.0 - abs(vnoise(p * 2.3 + 17.0) * 2.0 - 1.0), 9.0);
                return saturate(a + b * 0.7);
            }

            // одержимость: всё в координатах выпрямленного кадра (q), как маска и точки лица
            float3 possess(float3 col, float2 uv, float t)
            {
                float2 q = segUv(uv);
                float m = tex2D(_SegTex, q).r;
                float person = smoothstep(0.3, 0.7, m);
                float rim = smoothstep(0.08, 0.3, m) * (1.0 - smoothstep(0.45, 0.75, m));
                float k = _Possess;
                // кожа мертвеет: уходит цвет, серо-синяя бледность, темнее; по телу — тонкие тёмные вены
                float l = luma(col);
                float3 dead = lerp(l.xxx, col, 0.25) * float3(0.78, 0.86, 0.95) * 0.7;
                col = lerp(col, dead, person * k);
                float v = veins(q * 22.0 + float2(0.0, t * 0.03));
                col = lerp(col, float3(0.02, 0.015, 0.03), person * k * v * 0.75);
                // вокруг — тёмная дымка, чуть дрожит
                col *= 1.0 - rim * k * (0.45 + 0.15 * sin(t * 7.0));

                if (_FaceOn > 0.5)
                {
                    float r = max(_FaceSize * 0.1, 0.006);
                    float dA = length(q - _EyeA.xy) / r, dB = length(q - _EyeB.xy) / r;
                    float d = min(dA, dB);
                    float2 c = dA < dB ? _EyeA.xy : _EyeB.xy;
                    // глаза — чёрные, с красной точкой-зрачком
                    col = lerp(col, float3(0.0, 0.0, 0.0), saturate(1.7 - d * 1.2) * k);
                    col += float3(1.0, 0.12, 0.08) * saturate(1.0 - d * 3.5) * k * (0.7 + 0.3 * sin(t * 13.0));
                    // вены от глаз — лучами
                    float ang = atan2(q.y - c.y, q.x - c.x);
                    float rays = pow(abs(sin(ang * 7.0 + vnoise(q * 40.0) * 2.0)), 22.0);
                    float around = saturate(1.0 - abs(d - 2.0) / 1.1);
                    col = lerp(col, float3(0.015, 0.012, 0.02), rays * around * k * 0.8);
                    // рот — тёмная растянутая щель
                    float2 dm = (q - _Mouth.xy) / float2(r * 3.4, r * 0.9);
                    col = lerp(col, float3(0.0, 0.0, 0.0), saturate(1.4 - length(dm)) * k);
                }
                return col;
            }

            float3 look(int style, float2 uv, float2 sp, float2 px, float t, float grain)
            {
                float3 c = tex2D(_MainTex, uv).rgb;
                float lum = luma(c);
                float2 v = sp - 0.5;
                float vig = saturate(1.15 - dot(v, v) * 1.8);

                if (style == 0) return c;

                if (style == 1)
                {
                    // тёмная камера: почти без цвета, холодный оттенок, придавленные тени
                    float3 d = lerp(lum.xxx, c, 0.3) * float3(0.78, 0.88, 1.0);
                    // в тенях — цветной шум матрицы, как у телефона ночью
                    float shadow = 1.0 - saturate(lum * 3.0);
                    float3 chroma = float3(hash21(px * 0.25 + t), hash21(px * 0.25 + t + 3.1), hash21(px * 0.25 + t + 7.7)) - 0.5;
                    return (pow(d, 1.5) * 0.75 + grain * 0.05 + chroma * 0.035 * shadow) * vig;
                }

                if (style == 2)
                {
                    // зелёное ночное видение: вытянутая яркость, засветка ярких мест, мягкое зерно
                    float2 o = _MainTex_TexelSize.xy * 6.0;
                    float bloom = 0.0;
                    bloom += luma(tex2D(_MainTex, uv + float2(o.x, 0)).rgb);
                    bloom += luma(tex2D(_MainTex, uv - float2(o.x, 0)).rgb);
                    bloom += luma(tex2D(_MainTex, uv + float2(0, o.y)).rgb);
                    bloom += luma(tex2D(_MainTex, uv - float2(0, o.y)).rgb);
                    bloom = saturate(bloom * 0.25 - 0.65) * 2.5;
                    float soft = (lum * 2.0 + luma(tex2D(_MainTex, uv + o * 0.4).rgb) + luma(tex2D(_MainTex, uv - o * 0.4).rgb)) * 0.25;
                    float gain = 0.94 + 0.06 * sin(t * 1.3) + 0.03 * sin(t * 40.0);          // автоусиление «дышит»
                    float nv = pow(saturate(soft * 1.9), 0.8) * gain + bloom + grain * 0.2;
                    nv += step(0.9985, hash21(floor(px * 0.5) + floor(t * 20.0))) * 0.8;     // искры усилителя
                    nv *= 0.93 + 0.07 * step(0.5, frac(px.x * 0.33)) * step(0.5, frac(px.y * 0.33));   // сетка люминофора
                    float3 green = float3(0.32, 1.0, 0.62) * nv + float3(0.0, 0.05, 0.03);
                    return green * saturate(1.25 - dot(v, v) * 2.6);
                }

                if (style == 3)
                {
                    // ИК-камера наблюдения: серая, контрастная, строки, крупное зерно
                    float2 fe = (uv - 0.5) * (1.0 + dot(v, v) * 0.18) + 0.5;                // «рыбий глаз»
                    float inside = step(0.0, fe.x) * step(fe.x, 1.0) * step(0.0, fe.y) * step(fe.y, 1.0);
                    float ir = saturate((luma(tex2D(_MainTex, saturate(fe)).rgb) - 0.06) * 1.6) * inside;
                    ir = pow(ir, 0.85) + grain * 0.22;
                    ir *= 0.86 + 0.14 * step(0.5, frac(px.y * 0.5 + step(0.5, frac(t * 15.0)) * 0.5));   // чересстрочность
                    return (ir * float3(0.9, 0.95, 0.95)) * vig;
                }

                if (style == 5)
                {
                    // тепловизор: размытая «температура» из яркости → палитра чёрный–синий–фиолетовый–красный–жёлтый
                    float2 o = _MainTex_TexelSize.xy * 5.0;
                    float h = lum * 0.4;
                    h += luma(tex2D(_MainTex, uv + float2(o.x, 0)).rgb) * 0.15;
                    h += luma(tex2D(_MainTex, uv - float2(o.x, 0)).rgb) * 0.15;
                    h += luma(tex2D(_MainTex, uv + float2(0, o.y)).rgb) * 0.15;
                    h += luma(tex2D(_MainTex, uv - float2(0, o.y)).rgb) * 0.15;
                    h = saturate(h * 1.3 + grain * 0.04);
                    // контуры предметов чуть теплее — так видно комнату
                    float gx = luma(tex2D(_MainTex, uv + float2(o.x, 0)).rgb) - luma(tex2D(_MainTex, uv - float2(o.x, 0)).rgb);
                    float gy = luma(tex2D(_MainTex, uv + float2(0, o.y)).rgb) - luma(tex2D(_MainTex, uv - float2(0, o.y)).rgb);
                    h = saturate(h + sqrt(gx * gx + gy * gy) * 0.35);
                    h += (hash21(float2(floor(px.y * 0.5), floor(t * 9.0))) - 0.5) * 0.02;   // полосы сенсора
                    if (_SegOn > 0.5)
                    {
                        // живые — горячие на холодном фоне комнаты; «одержимый» человек — ледяной
                        float2 m = tex2D(_SegTex, segUv(uv)).rg;
                        h *= 0.55;   // комната остывает, чтобы живые выделялись
                        float person = smoothstep(0.25, 0.7, m.r);
                        float animal = smoothstep(0.25, 0.7, m.g);
                        float hot = 0.78 + 0.12 * lum + grain * 0.05;
                        h = lerp(h, _ColdPerson > 0.5 ? 0.06 + 0.05 * lum : hot, person);
                        h = lerp(h, hot - 0.05, animal);
                    }
                    float3 c0 = float3(0.02, 0.0, 0.08), c1 = float3(0.1, 0.05, 0.6), c2 = float3(0.6, 0.05, 0.6);
                    float3 c3 = float3(0.95, 0.2, 0.1), c4 = float3(1.0, 0.75, 0.1), c5 = float3(1.0, 1.0, 0.85);
                    float3 th = h < 0.2 ? lerp(c0, c1, h / 0.2)
                              : h < 0.4 ? lerp(c1, c2, (h - 0.2) / 0.2)
                              : h < 0.6 ? lerp(c2, c3, (h - 0.4) / 0.2)
                              : h < 0.8 ? lerp(c3, c4, (h - 0.6) / 0.2)
                              : lerp(c4, c5, (h - 0.8) / 0.2);
                    return th * saturate(1.2 - dot(v, v) * 1.5);
                }

                if (style == 6)
                {
                    // ночная ИК-съёмка: ИК-подсветка бьёт в центр — близкое пересвечено, края тонут в темноте;
                    // серо-зелёный монохром, глаза и светлые ткани светятся
                    float2 o = _MainTex_TexelSize.xy * 2.0;
                    float l = (lum * 2.0 + luma(tex2D(_MainTex, uv + o).rgb) + luma(tex2D(_MainTex, uv - o).rgb)) * 0.25;
                    float spot = saturate(1.35 - length(v * float2(1.0, 1.25)) * 2.1);
                    float ir = saturate(pow(l * 2.4, 0.7) * (0.25 + spot * 1.1));
                    ir = ir + saturate(ir - 0.8) * 1.5 + grain * 0.16;                       // пересвет и зерно
                    ir *= 0.95 + 0.05 * sin(t * 50.0 + sp.y * 400.0);
                    return ir * float3(0.86, 1.0, 0.9) * saturate(1.3 - dot(v, v) * 2.0);
                }

                // VHS: расслоение цвета, полоса трекинга, приглушённые цвета
                float band = 1.0 - smoothstep(0.0, 0.04, abs(frac(sp.y * 0.6 - t * 0.07) - 0.5));
                float2 j = float2((hash21(float2(floor(sp.y * 200.0), floor(t * 30.0))) - 0.5) * 0.02 * band, 0.0);
                float r = tex2D(_MainTex, uv + j + float2(0.004, 0)).r;
                float g = tex2D(_MainTex, uv + j).g;
                float b = tex2D(_MainTex, uv + j - float2(0.004, 0)).b;
                float3 vhs = lerp(luma(float3(r, g, b)).xxx, float3(r, g, b), 0.65);
                vhs = vhs * 0.9 + band * (grain + 0.3) * 0.35 + grain * 0.06;
                float head = step(sp.y, 0.025);                                            // шум переключения головок
                vhs = lerp(vhs, (hash21(float2(floor(px.y), floor(t * 60.0))) * 0.8).xxx, head * 0.8);
                return vhs * vig;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y;
                float2 sp = i.screen.xy / max(i.screen.w, 1e-4);   // 0..1 по экрану
                float2 px = sp * _ScreenParams.xy;

                // помехи: строки экрана рвутся и смещаются
                float row = floor(sp.y * 60.0);
                float tear = step(1.0 - _Glitch * 0.6, hash21(float2(row, floor(t * 24.0))));
                float2 uv = i.uv + float2(tear * (hash21(float2(row, t)) - 0.5) * 0.08 * _Glitch, 0.0);
                float grain = hash21(floor(px * 0.5) + frac(t * 7.0) * 113.0) - 0.5;

                float3 col = look((int)round(_Style), uv, sp, px, t, grain);
                if (_Mode > 0.001) col = lerp(col, look((int)round(_Alt), uv, sp, px, t, grain), _Mode);
                if (_Possess > 0.001) col = possess(col, uv, t);

                col = lerp(col, (grain + 0.5).xxx, _Glitch * 0.35 * step(0.6, hash21(float2(row, floor(t * 30.0)))));
                return fixed4(col, 1.0) * i.color;
            }
            ENDCG
        }
    }
}
