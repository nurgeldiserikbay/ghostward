// Общая часть шейдеров сущностей (Entity.shader — силуэты 0–19, Entity2.shader — 20–39):
// параметры, вершинный шейдер, примитивы-расстояния и итоговая раскраска.
#ifndef GHOSTWARD_ENTITY_COMMON
#define GHOSTWARD_ENTITY_COMMON

#include "UnityCG.cginc"
#include "LookCommon.cginc"

fixed4 _Body, _Color;
float _Fade, _Shape, _Flash, _Dissolve, _Seed, _Flip, _Locked, _World, _Dim;

struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 screen : TEXCOORD1; };

v2f vert(appdata v)
{
    v2f o;
    o.pos = UnityObjectToClipPos(v.vertex);
    o.uv = v.uv;
    o.screen = ComputeScreenPos(o.pos);
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

// время, вариации экземпляра, помехи и наклон; straight — без «покачивания» (тень, двойник)
void EntityPrelude(v2f i, bool straight, out float t, out float tq, out float s1, out float s2, out float s3,
                   out float2 uv, out float2 p, out float hs)
{
    t = _Time.y + _Seed * 13.0;
    tq = floor(t * 8.0) / 8.0;                    // дёрганое покадровое время
    s1 = hash11(_Seed + 1.0); s2 = hash11(_Seed + 2.0); s3 = hash11(_Seed + 3.0);

    uv = i.uv;
    if (_Flip > 0.5) uv.y = 1.0 - uv.y;

    // помехи: изредка строки смещаются, фигура подёргивается
    float glitch = step(0.94, hash11(floor(t * 12.0) + _Seed));
    uv.x += glitch * (hash21(float2(floor(uv.y * 24.0), floor(t * 12.0))) - 0.5) * 0.08;
    uv.x += step(0.9, hash11(floor(t * 3.0) + _Seed * 7.0)) * 0.02;
    if (!straight) uv.x += sin(uv.y * 12.0 + tq * 3.0) * 0.01;

    p = uv - 0.5;
    p.x += p.y * (s1 - 0.5) * 0.15;               // небольшой наклон у каждого своего
    hs = 0.9 + s2 * 0.2;                          // размер головы
}

// d — расстояние до силуэта; маски: holes — чёрные провалы, cores — светящиеся точки, toothMask — кость/зубы,
// hair — чёрные волосы, shadeMask — затенение; dark — тёмная сущность, контур слабее
fixed4 EntityCompose(v2f i, float2 p, float d, float bottom, float holes, float cores, float toothMask,
                     float hair, float shadeMask, float opacity, bool dark)
{
        float inside = fill(d);
        float rim = inside * saturate((d + 0.014) / max(fwidth(d), 1e-4));

        float3 col = _Body.rgb;
        float light = dot(_Body.rgb, float3(0.33, 0.33, 0.33));
        col -= step(0.5, frac((p.x + p.y) * 40.0)) * 0.06 * step(0.3, light);
        col = lerp(col, col * 0.45, shadeMask);
        col = lerp(col, float3(0.02, 0.02, 0.025), hair);

        float alpha = inside * lerp(0.4, opacity, saturate((p.y - bottom) * 2.2));
        float rimK = dark ? 0.5 : 0.6;
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
        float glow = exp(-max(d, 0.0) * 30.0) * (1.0 - inside) * (dark ? 0.2 : 0.24) * edge;
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

        // в мире — та же «камера», что у комнаты (вид, свет, зерно)
        if (_World > 0.5) col = GW_Grade(col, i.screen.xy / max(i.screen.w, 1e-4), _Dim);

        col = lerp(col, float3(1, 1, 1), _Flash);
        return fixed4(col, saturate(alpha) * _Fade);
}

#endif
