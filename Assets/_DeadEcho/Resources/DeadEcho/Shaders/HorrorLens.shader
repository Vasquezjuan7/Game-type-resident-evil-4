// Dead Echo · Lente de terror (Built-in, OnRenderImage). La usa HorrorLens.cs en la camara del juego.
// 1) Niebla de altura: espesa a ras de suelo, se aclara al subir, con jirones que se mueven con el viento
//    y que "recogen" la luz de antorchas y linterna (dispersion).
// 2) Aberracion cromatica en los bordes, viñeta, tono frio en sombras / calido en luces,
//    y desaturacion + latido de los bordes cuando sube el miedo.
// 3) Destello de los rayos (ilumina la niebla).
Shader "Hidden/DeadEcho/HorrorLens"
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
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            // Rayos de las 4 esquinas del frustum (mundo, hasta el plano lejano)
            float4 _RayBL, _RayBR, _RayTL, _RayTR;
            float4 _CamPos;

            float _FogOn;
            float4 _FogColor;
            float _FogDensity;   // densidad a la altura base
            float _FogFalloff;   // cuanto se aclara al subir (1/m)
            float _FogBase;      // altura (mundo) del suelo de la niebla
            float _FogMax;       // opacidad maxima
            float _FogScatter;   // cuanto tiñe la luz de la escena a la niebla
            float _SkyDistance;
            float4 _Wind;        // xy = direccion*velocidad, z = escala de los jirones, w = fuerza
            float _Flash;        // destello de rayo 0..1

            float _CA;           // aberracion cromatica
            float _Vignette;
            float _VignetteRadius;
            float _Desat;
            float _Pulse;
            float4 _ShadowTint;
            float4 _HighTint;

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash(i), b = Hash(i + float2(1, 0)), c = Hash(i + float2(0, 1)), d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 p)
            {
                return Noise(p) * 0.55 + Noise(p * 2.07 + 13.1) * 0.3 + Noise(p * 4.13 - 7.7) * 0.15;
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                float2 duv = uv;
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0) duv.y = 1.0 - duv.y;
                #endif

                // --- Aberracion cromatica (mas fuerte hacia los bordes) ---
                float2 fromCenter = uv - 0.5;
                float2 off = fromCenter * dot(fromCenter, fromCenter) * _CA * 4.0;
                float3 col;
                col.r = tex2D(_MainTex, uv - off).r;
                col.g = tex2D(_MainTex, uv).g;
                col.b = tex2D(_MainTex, uv + off).b;

                // --- Niebla de altura ---
                if (_FogOn > 0.5)
                {
                    float raw = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, duv);
                    float lin = Linear01Depth(raw);
                    float3 ray = lerp(lerp(_RayBL.xyz, _RayBR.xyz, duv.x), lerp(_RayTL.xyz, _RayTR.xyz, duv.x), duv.y);
                    float3 dir = normalize(ray);
                    float dist = lin >= 0.9999 ? _SkyDistance : length(ray * lin);
                    float3 world = _CamPos.xyz + dir * dist;

                    float dy = world.y - _CamPos.y;
                    float fdy = _FogFalloff * dy;
                    float shape = abs(fdy) > 0.001 ? (1.0 - exp(-fdy)) / fdy : 1.0;
                    float baseD = _FogDensity * exp(-_FogFalloff * (_CamPos.y - _FogBase));
                    float amount = baseD * dist * shape;

                    // Jirones: ruido en un punto cercano del rayo (la niebla que rodea a Leon se mueve)
                    float3 probe = _CamPos.xyz + dir * min(dist, 22.0);
                    float2 wp = probe.xz * _Wind.z + _Time.y * _Wind.xy;
                    float wisps = Fbm(wp) * 0.65 + Fbm(wp * 0.37 - _Time.y * _Wind.xy * 0.6) * 0.35;
                    amount *= lerp(1.0, wisps * 2.0, _Wind.w);

                    float fog = min(1.0 - exp(-max(amount, 0.0)), _FogMax);
                    float3 fogCol = _FogColor.rgb * (1.0 + _Flash * 5.0) + col * _FogScatter;
                    col = lerp(col, fogCol, fog);
                }

                // Destello del rayo sobre toda la imagen
                col += _Flash * float3(0.55, 0.6, 0.75) * 0.18;

                // --- Tono: sombras frias, luces calidas ---
                float luma = dot(col, float3(0.299, 0.587, 0.114));
                col *= lerp(_ShadowTint.rgb, _HighTint.rgb, smoothstep(0.02, 0.55, luma));

                // --- Miedo: menos color ---
                luma = dot(col, float3(0.299, 0.587, 0.114));
                col = lerp(col, luma.xxx, saturate(_Desat));

                // --- Viñeta (los bordes laten con el miedo) ---
                float2 v = fromCenter * float2(_ScreenParams.x / _ScreenParams.y, 1.0);
                float r = length(v);
                float rad = _VignetteRadius - _Pulse * 0.08;
                float vig = smoothstep(rad, rad - 0.55, r);
                col *= lerp(1.0, vig, saturate(_Vignette));

                return float4(saturate(col), 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
