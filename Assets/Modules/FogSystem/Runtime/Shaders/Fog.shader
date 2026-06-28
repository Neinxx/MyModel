Shader "Hidden/FogSystem/Fog"
{
    Properties
    {
        // Driven by RendererFeature
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "FogPass"
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // Packed parameters to minimize constant buffer slots
            // Distance Fog: x = density, y = start distance, z = end distance, w = exponential (1) vs linear (0)
            float4 _DistanceFogParams;
            // Height Fog: x = base height, y = height density (k), z = base density (rho_0), w = unused
            float4 _HeightFogParams;
            // Skybox Fog: x = virtual distance, y = intensity multiplier, z = affect skybox (1/0), w = unused
            float4 _SkyboxFogParams;

            // Gradient LUT
            TEXTURE2D(_FogGradientTex);
            SAMPLER(sampler_FogGradientTex);

            float4x4 _InvVP;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                
                // Sample scene color
                half4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                
                // Sample scene depth
                float rawDepth = SampleSceneDepth(uv);
                
                #if UNITY_REVERSED_Z
                    float deviceDepth = rawDepth;
                #else
                    float deviceDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif

                // Reconstruct world space position
                float3 positionWS = ComputeWorldSpacePosition(uv, deviceDepth, _InvVP);

                // Compute view vector and radial distance
                float3 viewDir = positionWS - _WorldSpaceCameraPos.xyz;
                float dist = length(viewDir);
                
                #if UNITY_REVERSED_Z
                    bool isSkybox = (rawDepth < 1e-7);
                #else
                    bool isSkybox = (rawDepth > 1.0 - 1e-7);
                #endif

                float affectSkybox = _SkyboxFogParams.z;
                if (isSkybox && affectSkybox < 0.5)
                {
                    return sceneColor;
                }

                // Normalize view direction safely
                viewDir = SafeNormalize(viewDir);

                if (isSkybox)
                {
                    dist = _SkyboxFogParams.x;
                    positionWS = _WorldSpaceCameraPos.xyz + viewDir * dist;
                }

                // --- 1. Distance Fog ---
                float fogDistanceFactor = 0.0;
                float distanceDensity = _DistanceFogParams.x;
                float startDist = _DistanceFogParams.y;
                float endDist = _DistanceFogParams.z;
                float isExponential = _DistanceFogParams.w;

                float activeDist = max(0.0, dist - startDist);

                if (isExponential > 0.5)
                {
                    fogDistanceFactor = 1.0 - exp(-activeDist * distanceDensity);
                }
                else
                {
                    float range = max(0.001, endDist - startDist);
                    fogDistanceFactor = saturate(activeDist / range);
                }

                // --- 2. Height Fog (Analytical Ray Integral) ---
                float k = _HeightFogParams.y; // Height density
                float rho_0 = _HeightFogParams.z; // Base density
                float heightBase = _HeightFogParams.x; // Height base
                
                float rho_C = rho_0 * exp(-k * (_WorldSpaceCameraPos.y - heightBase));
                float heightDifference = viewDir.y * dist;
                
                // Epsilon protection against division-by-zero
                float epsilon = 1e-5;
                float divVal = abs(heightDifference) > epsilon ? heightDifference : (heightDifference >= 0.0 ? epsilon : -epsilon);
                float heightIntegralFactor = (1.0 - exp(-k * heightDifference)) / (k * divVal);
                
                float heightOpticalDepth = rho_C * dist * heightIntegralFactor;
                float fogHeightFactor = saturate(1.0 - exp(-heightOpticalDepth));

                // --- 3. Combined Fog Factor (Transmission-Rate Based) ---
                float fogFactor = saturate(1.0 - (1.0 - fogDistanceFactor) * (1.0 - fogHeightFactor));

                float finalFogFactor = fogFactor;
                if (isSkybox)
                {
                    finalFogFactor = saturate(fogFactor * _SkyboxFogParams.y);
                }

                // --- 4. Sample Gradient LUT ---
                half4 fogColor = SAMPLE_TEXTURE2D(_FogGradientTex, sampler_FogGradientTex, float2(finalFogFactor, 0.5));

                // --- 5. Blend Color ---
                half3 finalColor = lerp(sceneColor.rgb, fogColor.rgb, finalFogFactor * fogColor.a);

                return half4(finalColor, sceneColor.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
