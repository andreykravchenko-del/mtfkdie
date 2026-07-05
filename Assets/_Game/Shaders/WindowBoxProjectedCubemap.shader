// Box-projected cubemap для вида в окно с параллаксом от движения головы.
// Вешается на плоскость-«стекло» окна. Луч из камеры через пиксель окна
// пересекается с боксом-объёмом двора, и по точке пересечения сэмплится кубмапа.
// При смещении камеры (головы игрока) ближние стены бокса «съезжают» сильнее
// дальних — получается честный параллакс без геометрии за окном.
//
// Поверх вида изнутри — слой стекла: Fresnel-отражение окружения (reflection
// probe / скайбокс со стороны игрока), солнечный блик и лёгкий тон стекла.
Shader "Custom/WindowBoxProjectedCubemap"
{
    Properties
    {
        [Header(Interior)]
        [NoScaleOffset] _Cubemap ("Cubemap двора", Cube) = "" {}
        _Tint ("Оттенок", Color) = (1,1,1,1)
        _Exposure ("Экспозиция", Range(0,4)) = 1
        _YRotation ("Поворот кубмапы по Y (град)", Range(-180,180)) = 0
        // Размер бокса-объёма двора в мировых единицах (метрах).
        _BoxSize ("Размер бокса (XYZ)", Vector) = (30, 15, 30, 0)
        // Смещение центра бокса относительно объекта-окна.
        _BoxOffset ("Смещение центра бокса", Vector) = (0, 0, 0, 0)

        [Header(Glass)]
        _GlassTint ("Тон стекла", Color) = (0.85, 0.92, 0.95, 1)
        _ReflectionStrength ("Сила отражений", Range(0,1)) = 0.5
        _FresnelPower ("Резкость Френеля", Range(0.5,8)) = 4
        _Smoothness ("Гладкость (чёткость отражений)", Range(0,1)) = 0.9
        _SpecularStrength ("Сила блика солнца", Range(0,8)) = 2
        _SpecularPower ("Узость блика", Range(4,512)) = 128
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

            // Lighting.hlsl тянет Core + GI (GlossyEnvironmentReflection) + GetMainLight.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURECUBE(_Cubemap);
            SAMPLER(sampler_Cubemap);

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float  _Exposure;
                float  _YRotation;
                float4 _BoxSize;
                float4 _BoxOffset;
                float4 _GlassTint;
                float  _ReflectionStrength;
                float  _FresnelPower;
                float  _Smoothness;
                float  _SpecularStrength;
                float  _SpecularPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vp = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = vp.positionCS;
                OUT.positionWS  = vp.positionWS;
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
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
                // --- Вид изнутри: box-projected cubemap ---
                float3 boxCenter  = TransformObjectToWorld(float3(0, 0, 0)) + _BoxOffset.xyz;
                float3 boxExtents = _BoxSize.xyz * 0.5;

                float3 origin = _WorldSpaceCameraPos;
                float3 dir    = normalize(IN.positionWS - _WorldSpaceCameraPos);

                float3 sampleDir = BoxProject(origin, dir, boxCenter, boxExtents);
                sampleDir = RotateY(sampleDir, _YRotation);

                half3 interior = SAMPLE_TEXTURECUBE(_Cubemap, sampler_Cubemap, sampleDir).rgb;
                interior *= _Tint.rgb * _Exposure;
                // Стекло слегка тонирует то, что видно сквозь него.
                interior *= _GlassTint.rgb;

                // --- Слой стекла: нормаль, взгляд, Френель ---
                float3 V = -dir; // от точки к камере
                float3 N = normalize(IN.normalWS);
                // Cull Off: нормаль тыльной грани смотрит от игрока — разворачиваем к нему.
                N *= sign(dot(N, V));

                float NdotV   = saturate(dot(N, V));
                float fresnel = pow(1.0 - NdotV, _FresnelPower);

                // Отражение окружения со стороны игрока (reflection probe / скайбокс).
                float3 reflectVec = reflect(-V, N);
                half   roughness  = 1.0 - _Smoothness;
                half3  envRefl    = GlossyEnvironmentReflection(reflectVec, roughness, 1.0h);

                // Солнечный блик от главного источника.
                Light mainLight = GetMainLight();
                float  glint    = pow(saturate(dot(reflectVec, mainLight.direction)), _SpecularPower);
                half3  specular = glint * _SpecularStrength * mainLight.color;

                // Смешиваем вид сквозь стекло с отражением по Френелю и добавляем блик.
                half3 col = lerp(interior, envRefl, saturate(fresnel * _ReflectionStrength));
                col += specular;

                return half4(col, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
