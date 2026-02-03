Shader "Custom/VolumetricClouds"
{
    Properties
    {
        _Steps ("Ray Steps", Int) = 64
        _StepSize ("Step Size", Float) = 0.5

        _LightSteps ("Light Steps", Int) = 8
        _LightStepSize ("Light Step Size", Float) = 1.0
        _LightDir ("Light Direction", Vector) = (0,1,0,0)

        _Density ("Density", Float) = 1.2
        _Absorption ("Light Absorption", Float) = 1.0
        _Darkness ("Shadow Darkness", Range(0,1)) = 0.25

        _NoiseScale ("Noise Scale", Float) = 0.01
        _DetailScale ("Detail Scale", Float) = 0.05
        _Coverage ("Coverage", Range(0,1)) = 0.45
        _Octaves ("FBM Octaves", Int) = 3

        _Storminess ("Storminess", Range(0,1)) = 0.7
        _CloudBase ("Cloud Base Height", Float) = 200.0
        _CloudTop ("Cloud Top Height", Float) = 900.0
        _Anvil ("Anvil Strength", Range(0,1)) = 0.6
        _Turbulence ("Turbulence", Float) = 0.8
        _StormTint ("Storm Tint", Color) = (0.75,0.78,0.85,1)

        _WindDir ("Wind Direction", Vector) = (1,0,0,0)
        _WindSpeed ("Wind Speed", Float) = 10.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On
        Cull Front

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            int _Steps;
            float _StepSize;

            int _LightSteps;
            float _LightStepSize;
            float3 _LightDir;

            float _Density;
            float _Absorption;
            float _Darkness;

            float _NoiseScale;
            float _DetailScale;
            float _Coverage;
            float _Octaves;

            float _Storminess;
            float _CloudBase;
            float _CloudTop;
            float _Anvil;
            float _Turbulence;
            float4 _StormTint;

            float3 _WindDir;
            float _WindSpeed;
            float _TimeValue;

            float3 _LightningPosWS;
            float _LightningIntensity;
            float _LightningRadius;

            #define MAX_STEPS 128
            #define MAX_LIGHT_STEPS 16

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f output;
                output.pos = UnityObjectToClipPos(v.vertex);
                output.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return output;
            }

            // basic hash, converts 3D pos into psuedo-random float [0,1]
            float hash(float3 p)
            {
                // dot product + sine to randomise
                return frac(sin(dot(p, float3(12.9898,78.233,37.719))) * 43758.5453);
            }

            // noise, lerps rand vals across 3D grid
            float noise(float3 p)
            {
                float3 cellId = floor(p);
                float3 cellUv = frac(p);

                // hash for each of the 8 cube corners
                float n000 = hash(cellId + float3(0, 0, 0));
                float n100 = hash(cellId + float3(1, 0, 0));
                float n010 = hash(cellId + float3(0, 1, 0));
                float n110 = hash(cellId + float3(1, 1, 0));
                float n001 = hash(cellId + float3(0, 0, 1));
                float n101 = hash(cellId + float3(1, 0, 1));
                float n011 = hash(cellId + float3(0, 1, 1));
                float n111 = hash(cellId + float3(1, 1, 1));

                // smoothstep interpolation weights for each axis
                float3 u = cellUv * cellUv * (3 - 2 * cellUv);

                // trilinear interpolation for the corners
                return lerp(
                    lerp(lerp(n000, n100, u.x), lerp(n010, n110, u.x), u.y),
                    lerp(lerp(n001, n101, u.x), lerp(n011, n111, u.x), u.y),
                    u.z
                );
            }

            // fractal brownian noise or fractal noise for short
            // combines multiple octaves of noise at increasing frequency and decreasing amplitude
            float fbm(float3 p) // give pos
            {
                float value = 0;
                float amplitude = 0.5;

                for (int octaveIndex = 0; octaveIndex < _Octaves; octaveIndex++) // num of octaves
                {
                    value += noise(p) * amplitude; // add weighted noise
                    p *= 2; // double it
                    amplitude *= 0.5; // reduce amplitude
                }

                return value;
            }

            // height mask for storm clouds (base->top, flatter top)
            float HeightMask(float3 positionWS)
            {
                float height01 = saturate((positionWS.y - _CloudBase) / max(1e-3, (_CloudTop - _CloudBase)));

                // fade in at base, fade out near top
                float baseFade = smoothstep(0.0, 0.15, height01);
                float topFade  = 1.0 - smoothstep(0.75, 1.0, height01);

                // flatten into anvil near top
                float anvil = lerp(1.0, smoothstep(0.55, 0.9, height01), _Anvil);

                return baseFade * topFade * anvil;
            }

            // main cloud density function
            float SampleDensity(float3 positionWS)
            {
                // wind offset in world space
                float3 windOffsetWS = normalize(_WindDir) * _TimeValue * _WindSpeed;
                float3 movedPositionWS = positionWS + windOffsetWS;

                // height shaping (storm layers)
                float heightMask = HeightMask(positionWS);
                if (heightMask <= 0.001) { return 0; }

                // large cloud shapes
                float3 macroPosition = movedPositionWS * (_NoiseScale * 0.25);
                float macroNoise = fbm(macroPosition);

                // erosion detail
                float3 detailPosition = movedPositionWS * (_DetailScale * 0.5);
                float detailNoise = fbm(detailPosition);

                // extra breakup/turbulence (stormy ragged edges)
                float turbulenceNoise = fbm(movedPositionWS * (_DetailScale * 1.25)) * _Turbulence;

                // storminess pushes density up and increases contrast
                float density = macroNoise - detailNoise * lerp(0.30, 0.55, _Storminess);
                density += turbulenceNoise * lerp(0.05, 0.20, _Storminess);

                // coverage shaping (more overcast in storm)
                float stormCoverage = lerp(_Coverage, _Coverage * 0.7, _Storminess);
                density = saturate((density - stormCoverage) * lerp(2.0, 4.0, _Storminess));

                return density * _Density * heightMask;
            }

            // cheaper density for shadowing
            float SampleShadowDensity(float3 positionWS)
            {
                return fbm(positionWS * (_NoiseScale * 0.25));
            }

            // extract object scale from transform matrix
            float3 GetObjectScale()
            {
                return float3(
                    length(unity_ObjectToWorld._m00_m10_m20),
                    length(unity_ObjectToWorld._m01_m11_m21),
                    length(unity_ObjectToWorld._m02_m12_m22)
                );
            }

            // ray vs axis-aligned box intersection
            bool RayBoxOS(float3 rayOriginOS, float3 rayDirectionOS, out float tEnter, out float tExit)
            {
                float3 boxMin = float3(-0.5, -0.5, -0.5);
                float3 boxMax = float3( 0.5,  0.5,  0.5);

                float3 inv = 1.0 / rayDirectionOS;
                float3 t0 = (boxMin - rayOriginOS) * inv;
                float3 t1 = (boxMax - rayOriginOS) * inv;

                float3 tMin = min(t0, t1);
                float3 tMax = max(t0, t1);

                tEnter = max(max(tMin.x, tMin.y), tMin.z);
                tExit  = min(min(tMax.x, tMax.y), tMax.z);

                return tExit > max(tEnter, 0.0);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // world-space ray (based on the actual surface pixel)
                float3 rayOriginWS = _WorldSpaceCameraPos;
                float3 rayDirectionWS = normalize(i.worldPos - _WorldSpaceCameraPos);
                // transform ray into object space (handles rotation + scale correctly)
                float3 rayOriginOS = mul(unity_WorldToObject, float4(rayOriginWS, 1)).xyz;
                float3 rayDirectionOS = normalize(mul((float3x3)unity_WorldToObject, rayDirectionWS));

                float rayEnterDistance, rayExitDistance;
                if (!RayBoxOS(rayOriginOS, rayDirectionOS,
                            rayEnterDistance, rayExitDistance))
                    { discard; }

                float rayDistance = max(rayEnterDistance, 0);

                float stepSizeOS = (rayExitDistance - rayDistance) / max(1, _Steps);
                stepSizeOS = max(stepSizeOS, 0.0001);

                float transmittance = 1.0;
                float finalLight = 0;

                // raymarch through volume
                for (int stepIndex = 0; stepIndex < MAX_STEPS && stepIndex < _Steps; stepIndex++)
                {
                    if (rayDistance > rayExitDistance || transmittance < 0.01) { break; }

                    float3 samplePositionOS = rayOriginOS + rayDirectionOS * rayDistance;
                    float3 samplePositionWS = mul(unity_ObjectToWorld, float4(samplePositionOS, 1)).xyz;

                    float densitySample = SampleDensity(samplePositionWS);

                    // skip empty space
                    if (densitySample < 0.02)
                    {
                        rayDistance += _StepSize * 2.0;
                        continue;
                    }

                    float transmittanceAtSample = transmittance;
                    
                    float lightDensity = 0;
                    float3 lightRayPositionWS = samplePositionWS;

                    // shadow ray
                    for (int lightStepIndex = 0; lightStepIndex < MAX_LIGHT_STEPS && lightStepIndex < _LightSteps; lightStepIndex++)
                    {
                        lightRayPositionWS += normalize(_LightDir) * _LightStepSize; // step toward light
                        lightDensity += SampleShadowDensity(lightRayPositionWS);
                    }

                    float shadow = lerp(_Darkness, 1.0, exp(-lightDensity * _Absorption));

                    // storm lighting, darker interiors and less uniform brightness
                    float stormDarken = lerp(1.0, 0.65, _Storminess);
                    finalLight += densitySample * transmittanceAtSample * shadow * stormDarken;                    

                    // lightning glow (light inside cloud from lightning)
                    float distanceToStrike = distance(samplePositionWS, _LightningPosWS);
                    float lightningAtten = saturate(1.0 - (distanceToStrike / max(1e-3, _LightningRadius)));
                    lightningAtten *= lightningAtten;

                    finalLight += densitySample * transmittanceAtSample * lightningAtten * (_LightningIntensity * 8.0);

                    float stormAbsorb = lerp(_Absorption, _Absorption * 1.6, _Storminess);
                    transmittance *= exp(-densitySample * stormAbsorb);                    

                    rayDistance += _StepSize;
                }

                float3 col = (finalLight * _StormTint.rgb);
                return float4(col, 1.0 - transmittance);
            }
            ENDHLSL
        }
    }
}
