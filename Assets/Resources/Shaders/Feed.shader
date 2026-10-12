// Видео с камеры в режиме гироскопа. Вид задаётся настройкой (_Style) и эпизодами ночного режима (_Alt, _Mode):
// 0 — обычный, 1 — тёмный, 2 — зелёное ночное видение, 3 — ИК-камера наблюдения, 4 — VHS.
// _Glitch — помехи у логова и рядом с сущностью.
Shader "GhostWard/Feed"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "black" {}
        _Style ("Style", Float) = 1
        _Alt ("Episode style", Float) = 2
        _Mode ("Episode blend", Range(0, 1)) = 0
        _Glitch ("Glitch", Range(0, 1)) = 0
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
                    return (pow(d, 1.5) * 0.75 + grain * 0.05) * vig;
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
                    float nv = pow(saturate(lum * 1.8), 0.8) + bloom + grain * 0.18;
                    nv *= 0.92 + 0.08 * sin(t * 40.0);
                    float3 green = float3(0.32, 1.0, 0.62) * nv + float3(0.0, 0.05, 0.03);
                    return green * saturate(1.25 - dot(v, v) * 2.6);
                }

                if (style == 3)
                {
                    // ИК-камера наблюдения: серая, контрастная, строки, крупное зерно
                    float ir = saturate((lum - 0.06) * 1.6);
                    ir = pow(ir, 0.85) + grain * 0.22;
                    ir *= 0.86 + 0.14 * step(0.5, frac(px.y * 0.5));
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
                    float3 c0 = float3(0.02, 0.0, 0.08), c1 = float3(0.1, 0.05, 0.6), c2 = float3(0.6, 0.05, 0.6);
                    float3 c3 = float3(0.95, 0.2, 0.1), c4 = float3(1.0, 0.75, 0.1), c5 = float3(1.0, 1.0, 0.85);
                    float3 th = h < 0.2 ? lerp(c0, c1, h / 0.2)
                              : h < 0.4 ? lerp(c1, c2, (h - 0.2) / 0.2)
                              : h < 0.6 ? lerp(c2, c3, (h - 0.4) / 0.2)
                              : h < 0.8 ? lerp(c3, c4, (h - 0.6) / 0.2)
                              : lerp(c4, c5, (h - 0.8) / 0.2);
                    return th * saturate(1.2 - dot(v, v) * 1.5);
                }

                // VHS: расслоение цвета, полоса трекинга, приглушённые цвета
                float band = 1.0 - smoothstep(0.0, 0.04, abs(frac(sp.y * 0.6 - t * 0.07) - 0.5));
                float2 j = float2((hash21(float2(floor(sp.y * 200.0), floor(t * 30.0))) - 0.5) * 0.02 * band, 0.0);
                float r = tex2D(_MainTex, uv + j + float2(0.004, 0)).r;
                float g = tex2D(_MainTex, uv + j).g;
                float b = tex2D(_MainTex, uv + j - float2(0.004, 0)).b;
                float3 vhs = lerp(luma(float3(r, g, b)).xxx, float3(r, g, b), 0.65);
                vhs = vhs * 0.9 + band * (grain + 0.3) * 0.35 + grain * 0.06;
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

                col = lerp(col, (grain + 0.5).xxx, _Glitch * 0.35 * step(0.6, hash21(float2(row, floor(t * 30.0)))));
                return fixed4(col, 1.0) * i.color;
            }
            ENDCG
        }
    }
}
