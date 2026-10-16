// Кадр камеры ровно таким, каким он виден на экране (для карты глубины комнаты):
// точка экрана (uv 0..1) → точка видео — то же преобразование, что в GyroRig.SampleLuma
// (фон растянут с обрезкой краёв и повёрнут на угол сенсора). Вне видео — чёрное.
Shader "GhostWard/ViewCrop"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
        _Size ("Canvas size", Vector) = (1, 1, 0, 0)
        _Img ("Image size", Vector) = (1, 1, 0, 0)
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
            float4 _Size, _Img;
            float _Angle, _Mirror;

            fixed4 frag(v2f_img i) : SV_Target
            {
                float a = radians(_Angle);
                float c = cos(a), s = sin(a);
                float2 l = (i.uv - 0.5) * _Size.xy;
                float2 r = float2(l.x * c - l.y * s, l.x * s + l.y * c);
                float2 q = r / _Img.xy + 0.5;
                if (_Mirror > 0.5) q.y = 1.0 - q.y;
                if (q.x < 0.0 || q.x > 1.0 || q.y < 0.0 || q.y > 1.0) return 0;
                return tex2D(_MainTex, q);
            }
            ENDCG
        }
    }
}
