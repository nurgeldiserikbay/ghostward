// Сущность в мире должна сниматься той же «камерой», что и комната: тот же вид (ночное видение,
// тепловизор, наблюдение…), тот же свет и то же зерно. Иначе она выглядит наклейкой поверх видео.
// Глобальные значения выставляет Game.UpdateLook; на картинки в интерфейсе (_World = 0) не действует.
#ifndef GHOSTWARD_LOOK_COMMON
#define GHOSTWARD_LOOK_COMMON

float _GW_Style, _GW_Alt, _GW_Mode;   // вид камеры, вид эпизода, доля эпизода 0..1
float _GW_Light;                       // освещённость комнаты 0..1 (по осмотру)

float GW_Hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

float3 GW_LookOne(int style, float3 c)
{
    float l = dot(c, float3(0.299, 0.587, 0.114));
    if (style == 0) return c;
    if (style == 1) return lerp(l.xxx, c, 0.35) * float3(0.78, 0.88, 1.0) * 0.85;             // тёмная
    if (style == 2) return float3(0.32, 1.0, 0.62) * pow(saturate(l * 1.8), 0.8) + 0.02;     // ночное видение
    if (style == 6) return float3(0.86, 1.0, 0.9) * saturate(pow(l * 2.3, 0.75));            // ИК-съёмка
    if (style == 3) return (saturate(l * 1.4)).xxx * float3(0.9, 0.95, 0.95);                 // наблюдение
    if (style == 5) return lerp(float3(0.02, 0.0, 0.08), float3(0.12, 0.06, 0.55), saturate(l * 0.7)); // тепловизор: холодная
    return lerp(l.xxx, c, 0.65) * 0.95;                                                         // VHS
}

// screenUv — 0..1 по экрану (для зерна в пикселях экрана, как у видео); dim — затемнение по расстоянию
float3 GW_Grade(float3 c, float2 screenUv, float dim)
{
    int style = (int)round(_GW_Style);
    // свет комнаты: в тёмной комнате фигура тусклее (кроме приборов, которые сами «видят» в темноте)
    bool device = style == 2 || style == 6 || style == 3 || style == 5;
    float light = device ? 1.0 : lerp(0.5, 1.0, saturate(_GW_Light * 2.5));
    c *= light * dim;
    float3 a = GW_LookOne(style, c);
    if (_GW_Mode > 0.001) a = lerp(a, GW_LookOne((int)round(_GW_Alt), c), _GW_Mode);
    // то же зерно, что у кадра камеры: по пикселям экрана, 2×2, меняется 7 раз в секунду
    float2 px = floor(screenUv * _ScreenParams.xy * 0.5);
    float grain = GW_Hash(px + frac(_Time.y * 7.0) * 113.0) - 0.5;
    a += grain * (style == 0 ? 0.03 : 0.07);
    return a;
}

#endif
