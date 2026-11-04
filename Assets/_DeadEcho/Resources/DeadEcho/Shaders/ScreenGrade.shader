// Dead Echo · Ajuste de imagen a pantalla completa (Built-in, OnRenderImage).
// Brillo, contraste, saturacion, tinte, filtro daltonico (matriz 3x3) y grano de pelicula.
// Esta en Resources para que se incluya en la build (lo carga ScreenGrade.cs).
Shader "Hidden/DeadEcho/ScreenGrade"
{
    Properties
    {
        _MainTex ("Imagen", 2D) = "white" {}
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
            float _Brightness;   // multiplicador (1 = sin cambio)
            float _Gamma;        // exponente (1 = sin cambio)
            float _Saturation;   // 0 = blanco y negro, 1 = original
            float _Contrast;     // 1 = sin cambio
            float _Grain;        // amplitud del ruido
            float _GrainSeed;    // cambia cada fotograma de grano
            float4 _Tint;
            float4 _Row0;        // filas de la matriz de color (daltonismo)
            float4 _Row1;
            float4 _Row2;

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float4 src = tex2D(_MainTex, i.uv);
                float3 col = src.rgb * _Brightness;
                col = pow(max(col, 0.0001), _Gamma);

                float luma = dot(col, float3(0.299, 0.587, 0.114));
                col = lerp(luma.xxx, col, _Saturation);
                col = (col - 0.5) * _Contrast + 0.5;
                col *= _Tint.rgb;

                col = float3(dot(_Row0.xyz, col), dot(_Row1.xyz, col), dot(_Row2.xyz, col));

                float n = Hash(floor(i.uv * _ScreenParams.xy) + _GrainSeed);
                col += (n - 0.5) * _Grain;
                return float4(saturate(col), src.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
