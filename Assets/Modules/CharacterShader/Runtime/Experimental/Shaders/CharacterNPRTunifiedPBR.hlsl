#ifndef CHARACTER_NPR_TUNIFIED_PBR_INCLUDED
#define CHARACTER_NPR_TUNIFIED_PBR_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl"

// Calculates a stylised physical specular (GGX) and environment reflection (IBL)
half3 CalculateTunifiedPBR(
    half metallic, 
    half smoothness, 
    half3 normalWS, 
    half3 viewDirWS, 
    half3 positionWS, 
    Light mainLight,
    half pbrSpecularStrength,
    half pbrReflectionStrength,
    half stylizedThreshold)
{
    // If metallic is 0, this module contributes nothing (Data-driven gating)
    if (metallic <= 0.001h)
    {
        return half3(0.0h, 0.0h, 0.0h);
    }

    half perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(smoothness);
    half roughness = max(PerceptualRoughnessToRoughness(perceptualRoughness), HALF_MIN_SQRT);

    // 1. Direct Stylized GGX Specular
    half3 halfDir = SafeNormalize(float3(mainLight.direction) + float3(viewDirWS));
    half NoH = saturate(dot(normalWS, halfDir));
    half NoV = saturate(dot(normalWS, viewDirWS));
    half NoL = saturate(dot(normalWS, mainLight.direction));

    // GGX Normal Distribution
    half d = NoH * NoH * (roughness * roughness - 1.0h) + 1.0h;
    half LoH2 = saturate(dot(mainLight.direction, halfDir));
    half specularTerm = roughness * roughness / ((max(0.1h, LoH2) * d * d) * max(0.1h, NoL) * max(0.1h, NoV));
    
    // Stylized Threshold (Hard cutoff for anime look)
    // Map threshold from 0..1 to an appropriate specular cutoff range
    half specThreshold = 1.0h - stylizedThreshold;
    half animeSpecular = smoothstep(specThreshold - 0.05h, specThreshold + 0.05h, saturate(specularTerm));
    
    // Mask by shadow and normal lighting
    half3 finalSpecular = animeSpecular * mainLight.color * mainLight.distanceAttenuation * mainLight.shadowAttenuation * pbrSpecularStrength;

    // 2. Environment Reflection (IBL)
    half3 reflectVector = reflect(-viewDirWS, normalWS);
    // Use standard URP Glossy Environment Reflection
    half3 indirectReflection = GlossyEnvironmentReflection(reflectVector, positionWS, perceptualRoughness, 1.0h);
    
    // Posterize/Stylize the reflection slightly if threshold is high to avoid realistic noise
    if (stylizedThreshold > 0.5h)
    {
        indirectReflection = floor(indirectReflection * 4.0h) * 0.25h;
    }
    
    half3 finalReflection = indirectReflection * pbrReflectionStrength;

    // Combine and multiply by metallic (Metallic acts as the alpha mask for the PBR layer)
    return (finalSpecular + finalReflection) * metallic;
}

#endif // CHARACTER_NPR_TUNIFIED_PBR_INCLUDED
