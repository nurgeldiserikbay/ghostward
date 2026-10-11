// Видео с камеры в режиме гироскопа: приглушённое и холодное («тёмная камера»),
// иногда — серо-чёрный ночной режим с зерном и строками. _Glitch — помехи рядом с сущностью.
Shader "GhostWard/Feed"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "black" {}
        _Mode ("Night vision", Range(0, 1)) = 0
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
            float _Mode, _Glitch;

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

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y;
                float2 sp = i.screen.xy / max(i.screen.w, 1e-4);   // 0..1 по экрану
                float2 px = sp * _ScreenParams.xy;

                // помехи: строки экрана рвутся и смещаются
                float row = floor(sp.y * 60.0);
                float tear = step(1.0 - _Glitch * 0.6, hash21(float2(row, floor(t * 24.0))));
                float2 uv = i.uv + float2(tear * (hash21(float2(row, t)) - 0.5) * 0.08 * _Glitch, 0.0);

                float3 c = tex2D(_MainTex, uv).rgb;
                float lum = dot(c, float3(0.299, 0.587, 0.114));
                float grain = hash21(floor(px * 0.5) + frac(t * 7.0) * 113.0) - 0.5;

                // тёмная камера: почти без цвета, холодный оттенок, придавленные тени
                float3 dark = lerp(lum.xxx, c, 0.3) * float3(0.78, 0.88, 1.0);
                dark = pow(dark, 1.5) * 0.75 + grain * 0.05;

                // ночной режим: серо-чёрная картинка, вытянутая яркость, крупное зерно, строки
                float nv = pow(saturate(lum * 1.7), 0.85) + grain * 0.28;
                nv *= 0.82 + 0.18 * step(0.5, frac(px.y * 0.25));
                nv *= 0.9 + 0.1 * sin(t * 50.0);
                float3 night = nv.xxx * float3(0.86, 0.9, 0.86);

                float3 col = lerp(dark, night, _Mode);
                col = lerp(col, (grain + 0.5).xxx, _Glitch * 0.35 * step(0.6, hash21(float2(row, floor(t * 30.0)))));

                // виньетка
                float2 v = sp - 0.5;
                col *= saturate(1.15 - dot(v, v) * 1.8);
                return fixed4(col, 1.0) * i.color;
            }
            ENDCG
        }
    }
}
