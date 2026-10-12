// Выпрямление кадра камеры для нейросети: поворот на _Angle (кратно 90°) и отражение по вертикали —
// так же, как фон на экране разворачивает видео. Обратное преобразование — в Feed.shader.
Shader "GhostWard/Upright"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
        _Angle ("Angle", Float) = 0
        _Mirror ("Mirror", Float) = 0
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Angle, _Mirror;

            fixed4 frag(v2f_img i) : SV_Target
            {
                float a = radians(_Angle);
                float c = cos(a), s = sin(a);
                float2 d = i.uv - 0.5;
                float2 q = 0.5 + float2(d.x * c - d.y * s, d.x * s + d.y * c);
                if (_Mirror > 0.5) q.y = 1.0 - q.y;
                return tex2D(_MainTex, q);
            }
            ENDCG
        }
    }
}
