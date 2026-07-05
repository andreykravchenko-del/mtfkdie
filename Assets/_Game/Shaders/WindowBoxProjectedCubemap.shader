// Box-projected cubemap для вида в окно с параллаксом от движения головы.
// Вешается на плоскость-«стекло» окна. Луч из камеры через пиксель окна
// пересекается с боксом-объёмом двора, и по точке пересечения сэмплится кубмапа.
// При смещении камеры (головы игрока) ближние стены бокса «съезжают» сильнее
// дальних — получается честный параллакс без геометрии за окном.
Shader "Custom/WindowBoxProjectedCubemap"
{
    Properties
    {
        [NoScaleOffset] _Cubemap ("Cubemap двора", Cube) = "" {}
        _Tint ("Оттенок", Color) = (1,1,1,1)
        _Exposure ("Экспозиция", Range(0,4)) = 1
        _YRotation ("Поворот кубмапы по Y (град)", Range(-180,180)) = 0
        // Размер бокса-объёма двора в мировых единицах (метрах).
        _BoxSize ("Размер бокса (XYZ)", Vector) = (30, 15, 30, 0)
        // Смещение центра бокса относительно объекта-окна.
        _BoxOffset ("Смещение центра бокса", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "WindowCubemap"
            // Стекло смотрит на игрока с одной стороны; Off — чтобы не зависеть
            // от того, как повёрнута плоскость окна в сцене.
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURECUBE(_Cubemap);
            SAMPLER(sampler_Cubemap);

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float  _Exposure;
                float  _YRotation;
                float4 _BoxSize;
                float4 _BoxOffset;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vp = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = vp.positionCS;
                OUT.positionWS  = vp.positionWS;
                return OUT;
            }

            // Пересечение луча (origin, dir) с AABB-боксом. Возвращает направление
            // от центра бокса к точке выхода луча — им и сэмплим кубмапу.
            float3 BoxProject (float3 origin, float3 dir, float3 boxCenter, float3 boxExtents)
            {
                float3 boxMin = boxCenter - boxExtents;
                float3 boxMax = boxCenter + boxExtents;
                float3 t1 = (boxMin - origin) / dir;
                float3 t2 = (boxMax - origin) / dir;
                float3 tMax = max(t1, t2);
                float  dist = min(min(tMax.x, tMax.y), tMax.z);
                float3 hit  = origin + dir * dist;
                return hit - boxCenter;
            }

            // Поворот направления вокруг оси Y — выравнивание «фронта» кубмапы с окном.
            float3 RotateY (float3 v, float degrees)
            {
                float rad = radians(degrees);
                float s = sin(rad);
                float c = cos(rad);
                return float3(c * v.x + s * v.z, v.y, -s * v.x + c * v.z);
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // Центр бокса = позиция объекта-окна в мире + ручное смещение.
                float3 boxCenter  = TransformObjectToWorld(float3(0, 0, 0)) + _BoxOffset.xyz;
                float3 boxExtents = _BoxSize.xyz * 0.5;

                // Луч от камеры через пиксель окна.
                float3 origin = _WorldSpaceCameraPos;
                float3 dir    = normalize(IN.positionWS - _WorldSpaceCameraPos);

                float3 sampleDir = BoxProject(origin, dir, boxCenter, boxExtents);
                sampleDir = RotateY(sampleDir, _YRotation);

                half3 col = SAMPLE_TEXTURECUBE(_Cubemap, sampler_Cubemap, sampleDir).rgb;
                col *= _Tint.rgb * _Exposure;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
