#ifndef CHARACTER_NPR_LIGHTING_INCLUDED
#define CHARACTER_NPR_LIGHTING_INCLUDED

#include "CharacterNPRInput.hlsl"
#include "CharacterSurfaceData.hlsl"
#include "CharacterNPRSilk.hlsl"
#include "CharacterNPRFaceSDF.hlsl"
#include "CharacterNPRTunifiedPBR.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

half3 CalculateCharacterNPRLighting(Varyings input, CharacterSurfaceData surface)
{
    Light mainLight = GetMainLight(input.shadowCoord);
    
    // --- Dominant Light Tracking for Specular and Rim ---
    Light dominantLight = mainLight;
    // We assume main light has infinite distance attenuation (1.0)
    half maxIlluminance = max(0.001h, length(mainLight.color) * mainLight.shadowAttenuation);

    // 0. Stockings & Silk (Modify Albedo before lighting based on ID 5)
    if (_UseSilk > 0.5h)
    {
        half isSilk = saturate(1.0h - abs(surface.profileID - 5.0h) * 10.0h);
        if (isSilk > 0.0h)
        {
            surface.albedoAlpha.rgb = ApplySilkAlbedo(
                surface.albedoAlpha.rgb,
                surface.normalWS,
                surface.viewDirWS,
                _SilkSkinColor,
                _SilkDarkColor,
                _SilkLightColor,
                _SilkTransparency,
                _SilkFresnelPower
            );
        }
    }

    // 1. Ramp & Inner Shadow (Main Light)
    half ndl = saturate(dot(surface.normalWS, mainLight.direction) * 0.5h + 0.5h);
    half rampBias = (surface.mask1.r - 0.5h) * _RampBiasStrength * surface.profile1.y;
    half rampU = saturate(ndl + rampBias);

    half localSoftness = max(0.001h, _InnerShadowSoftness * lerp(1.5h, 0.35h, saturate(surface.mask1.g)) / max(0.25h, surface.profile1.x));
    half proceduralRamp = smoothstep(_InnerShadowThreshold - localSoftness, _InnerShadowThreshold + localSoftness, rampU);
    half rampSlice = surface.profile0.x;
    half rampLight = proceduralRamp;
    
    if (_UseRampArray > 0.5h)
    {
        rampLight = SAMPLE_TEXTURE2D_ARRAY(_RampArray, sampler_RampArray, float2(rampU, 0.5), (int)round(rampSlice)).r;
    }
    
    // Override Ramp with Face SDF if enabled
    if (_UseFaceSDF > 0.5h)
    {
        rampLight = CalculateFaceSDFShadow(
            input.uv, 
            mainLight.direction, 
            _HeadForwardWS, 
            _HeadRightWS, 
            _FaceShadowOffset, 
            _FaceShadowSoftness
        );
    }

    half innerShadow = 1.0h - rampLight;
    
    // 2. Outer/Cast Shadows and Diffuse lighting (Main + Additional Lights)
    half mainOuterShadow = (1.0h - mainLight.shadowAttenuation) * _OuterShadowStrength;
    
    half additionalOuterShadow = 0.0h;
    half3 additionalLightDiffuse = half3(0, 0, 0);
    
    #if defined(_ADDITIONAL_LIGHTS)
    uint additionalLightCount = GetAdditionalLightsCount();
    [loop]
    for (uint lightIndex = 0u; lightIndex < additionalLightCount; lightIndex++)
    {
        Light additionalLight = GetAdditionalLight(lightIndex, input.positionWS);
        
        // Shadow Contribution
        half projectedShadow = (1.0h - additionalLight.shadowAttenuation) * additionalLight.distanceAttenuation;
        additionalOuterShadow = max(additionalOuterShadow, projectedShadow);
        
        // --- Stylized NPR Illuminance (Ramp) for Additional Lights ---
        half addNdl = saturate(dot(surface.normalWS, additionalLight.direction) * 0.5h + 0.5h);
        half addRampU = saturate(addNdl + rampBias);
        half addRampLight = smoothstep(_InnerShadowThreshold - localSoftness, _InnerShadowThreshold + localSoftness, addRampU);
        
        half addIntensity = additionalLight.distanceAttenuation * additionalLight.shadowAttenuation;
        additionalLightDiffuse += additionalLight.color * (addRampLight * addIntensity);

        // --- Dominant Light Tracking ---
        // Find the most influential light to drive Rim and Hair Specular
        half currentIlluminance = length(additionalLight.color) * addIntensity;
        if (currentIlluminance > maxIlluminance)
        {
            maxIlluminance = currentIlluminance;
            dominantLight = additionalLight;
        }
    }
    #endif
    
    half outerShadow = saturate(mainOuterShadow + additionalOuterShadow * _AdditionalShadowStrength);
    
    // 3. Shadow Mixing
    half mixedShadow = saturate(innerShadow + outerShadow - innerShadow * outerShadow * _ShadowOverlapCancel);
    half metalLift = saturate(_MetalShadowLift + surface.profile1.z) * surface.mask0.a;
    mixedShadow = lerp(mixedShadow, mixedShadow * (1.0h - metalLift), surface.mask0.a);

    // 4. Base Lighting Application
    half3 directLightTint = lerp(half3(1.0h, 1.0h, 1.0h), mainLight.color, _ReceiveMainLight);
    half3 litBase = surface.albedoAlpha.rgb * directLightTint;
    
    // Add additional lights diffuse illumination
    litBase += surface.albedoAlpha.rgb * additionalLightDiffuse * _ReceiveMainLight;
    
    half3 shadowed = litBase * _UnifiedShadowColor.rgb;

    // Apply Spherical Harmonics (Ambient Light / Light Probes / GI)
    half3 ambientSH = SampleSH(surface.normalWS) * _ReceiveSH;
    litBase += surface.albedoAlpha.rgb * ambientSH;
    shadowed += surface.albedoAlpha.rgb * ambientSH * lerp(half3(1.0h, 1.0h, 1.0h), _UnifiedShadowColor.rgb, 0.5h);
    
    half3 color = lerp(litBase, shadowed, mixedShadow);

    // 5. Ambient Occlusion
    half ao = lerp(1.0h, saturate(surface.mask0.g), saturate(_AOIntensity * surface.profile1.w));
    color *= ao;

    // 6. MatCap (Using Reflection Vector instead of just View Space Normal)
    half3 reflectWS = reflect(-surface.viewDirWS, surface.normalWS);
    half3 reflectVS = mul((float3x3)UNITY_MATRIX_V, reflectWS);
    half2 matcapUV = reflectVS.xy * 0.5h + 0.5h;
    
    half matcapSlice = surface.profile0.y;
    half3 matcap = half3(0.0h, 0.0h, 0.0h);
    
    if (_UseMatCapArray > 0.5h)
    {
        matcap = SAMPLE_TEXTURE2D_ARRAY(_MatCapArray, sampler_MatCapArray, matcapUV, (int)round(matcapSlice)).rgb;
    }
    
    half matcapStrength = surface.mask1.b * surface.profile0.z * _MatCapGlobalStrength;
    matcapStrength *= lerp(0.55h, 1.25h, surface.mask0.b);
    matcapStrength *= lerp(1.0h, _MetalMatCapBoost, surface.mask0.a);
    color += matcap * matcapStrength;

    // 7. Rim Light (AAA Anime Style) - Driven by Dominant Light
    half3 shiftedViewDir = normalize(surface.viewDirWS - dominantLight.direction * _RimLightAlign);
    
    half NdotVShift = saturate(dot(surface.normalWS, shiftedViewDir));
    half fresnel = 1.0h - NdotVShift;
    
    half rimThreshold = 1.0h - _RimWidth * surface.profile0.w;
    half rimLight = smoothstep(rimThreshold - _RimSoftness, rimThreshold + _RimSoftness, fresnel);
    
    half NdotL_Rim = saturate(dot(surface.normalWS, dominantLight.direction) * 0.5h + 0.5h);
    half weightFront = saturate(_RimLightAlign);
    half weightBack = saturate(-_RimLightAlign);
    half weightUniform = 1.0h - abs(_RimLightAlign);
    half directionalMask = (NdotL_Rim * weightFront) + ((1.0h - NdotL_Rim) * weightBack) + weightUniform;
    
    // Shadow Suppression uses Dominant Light attenuation
    half domAtten = dominantLight.shadowAttenuation * dominantLight.distanceAttenuation;
    half shadowMask = lerp(1.0h, saturate(domAtten), _RimShadowMasking);
    
    rimLight *= directionalMask * shadowMask * surface.mask1.a * _RimIntensity;
    
    half3 currentRimColor = lerp(_DarkRimColor.rgb, _RimColor.rgb, NdotL_Rim);
    color += currentRimColor * rimLight * lerp(half3(1.0h, 1.0h, 1.0h), dominantLight.color, _ReceiveMainLight);

    // 8. Hair Anisotropic Specular (Angel Ring) - Driven by Dominant Light
    if (_UseAnisoHair > 0.5h)
    {
        half isHair = saturate(1.0h - abs(surface.profileID - 1.0h) * 10.0h);
        
        if (isHair > 0.0h)
        {
            half2 anisoUV = input.uv * _HairAnisoMap_ST.xy + _HairAnisoMap_ST.zw;
            half4 anisoSample = SAMPLE_TEXTURE2D(_HairAnisoMap, sampler_HairAnisoMap, anisoUV);
            
            half anisoShift = (anisoSample.r * 2.0h - 1.0h) + _HairSpecShift;
            half anisoMask = anisoSample.g;
            
            half3 offsetTangent = normalize(surface.tangentWS.xyz + surface.normalWS * anisoShift);
            
            half3 H = normalize(surface.viewDirWS + dominantLight.direction);
            half TdotH = dot(offsetTangent, H);
            half anisoHighlight = 1.0h - abs(TdotH);
            
            half thresh = 1.0h - _HairSpecSpread;
            half hairSpec = smoothstep(thresh - _HairSpecSoftness, thresh + _HairSpecSoftness, anisoHighlight);
            
            hairSpec *= anisoMask * shadowMask * _HairSpecIntensity * isHair;
            
            color += _HairSpecColor.rgb * hairSpec * dominantLight.color;
        }
    }

    // 9. Tunified PBR (Stylized Metallic & Specular)
    if (_UseTunifiedPBR > 0.5h)
    {
        half3 pbrContribution = CalculateTunifiedPBR(
            surface.mask0.a, // Metallic
            surface.mask0.b, // Smoothness
            surface.normalWS,
            surface.viewDirWS,
            input.positionWS,
            dominantLight, // Use dominant light for PBR specular as well!
            _PBRSpecularStrength,
            _PBReflectionStrength,
            _PBRStylizedThreshold
        );
        
        color += pbrContribution;
    }

    // 10. Fog
    color = MixFog(color, input.fogFactor);

    return color;
}

#endif // CHARACTER_NPR_LIGHTING_INCLUDED
