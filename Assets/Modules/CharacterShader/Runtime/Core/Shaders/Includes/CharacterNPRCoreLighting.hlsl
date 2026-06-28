#ifndef CHARACTER_NPR_CORE_LIGHTING_INCLUDED
#define CHARACTER_NPR_CORE_LIGHTING_INCLUDED

#include "CharacterNPRCoreInput.hlsl"
#include "CharacterNPRCoreSurfaceData.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

half3 CalculateCharacterNPRCoreLighting(Varyings input, CharacterCoreSurfaceData surface)
{
    Light mainLight = GetMainLight(input.shadowCoord);
    Light dominantLight = mainLight;
    half maxIlluminance = max(0.001h, length(mainLight.color) * mainLight.shadowAttenuation);

    half ndl = saturate(dot(surface.normalWS, mainLight.direction) * 0.5h + 0.5h);
    half rampBias = (surface.mask1.r - 0.5h) * _RampBiasStrength * surface.profile1.y;
    half rampU = saturate(ndl + rampBias);
    half localSoftness = max(0.001h, _InnerShadowSoftness * lerp(1.5h, 0.35h, saturate(surface.mask1.g)) / max(0.25h, surface.profile1.x));
    half rampLight = smoothstep(_InnerShadowThreshold - localSoftness, _InnerShadowThreshold + localSoftness, rampU);

    #if defined(_USERAMPMAP_ON)
        rampLight = SAMPLE_TEXTURE2D(_RampMap, sampler_RampMap, float2(rampU, 0.5h)).r;
    #endif

    half innerShadow = 1.0h - rampLight;
    half mainOuterShadow = (1.0h - mainLight.shadowAttenuation) * _OuterShadowStrength;
    half additionalOuterShadow = 0.0h;
    half3 additionalLightDiffuse = half3(0.0h, 0.0h, 0.0h);

    #if defined(_ADDITIONAL_LIGHTS)
    uint additionalLightCount = GetAdditionalLightsCount();
    [loop]
    for (uint lightIndex = 0u; lightIndex < additionalLightCount; lightIndex++)
    {
        Light additionalLight = GetAdditionalLight(lightIndex, input.positionWS);
        half addIntensity = additionalLight.distanceAttenuation * additionalLight.shadowAttenuation;
        half projectedShadow = (1.0h - additionalLight.shadowAttenuation) * additionalLight.distanceAttenuation;
        additionalOuterShadow = max(additionalOuterShadow, projectedShadow);

        half addNdl = saturate(dot(surface.normalWS, additionalLight.direction) * 0.5h + 0.5h);
        half addRampU = saturate(addNdl + rampBias);
        half addRampLight = smoothstep(_InnerShadowThreshold - localSoftness, _InnerShadowThreshold + localSoftness, addRampU);
        additionalLightDiffuse += additionalLight.color * (addRampLight * addIntensity);

        half currentIlluminance = length(additionalLight.color) * addIntensity;
        if (currentIlluminance > maxIlluminance)
        {
            maxIlluminance = currentIlluminance;
            dominantLight = additionalLight;
        }
    }
    #endif

    half outerShadow = saturate(mainOuterShadow + additionalOuterShadow * _AdditionalShadowStrength);
    half mixedShadow = saturate(innerShadow + outerShadow - innerShadow * outerShadow * _ShadowOverlapCancel);
    half metalLift = saturate(_MetalShadowLift + surface.profile1.z) * surface.mask0.a;
    mixedShadow = lerp(mixedShadow, mixedShadow * (1.0h - metalLift), surface.mask0.a);

    half3 directLightTint = lerp(half3(1.0h, 1.0h, 1.0h), mainLight.color, _ReceiveMainLight);
    half3 litBase = surface.albedoAlpha.rgb * directLightTint;
    litBase += surface.albedoAlpha.rgb * additionalLightDiffuse * _ReceiveMainLight;

    half3 shadowed = litBase * _UnifiedShadowColor.rgb;
    half3 ambientSH = SampleSH(surface.normalWS) * _ReceiveSH;
    litBase += surface.albedoAlpha.rgb * ambientSH;
    shadowed += surface.albedoAlpha.rgb * ambientSH * lerp(half3(1.0h, 1.0h, 1.0h), _UnifiedShadowColor.rgb, 0.5h);

    half3 color = lerp(litBase, shadowed, mixedShadow);
    half ao = lerp(1.0h, saturate(surface.mask0.g), saturate(_AOIntensity * surface.profile1.w));
    color *= ao;

    half3 reflectWS = reflect(-surface.viewDirWS, surface.normalWS);
    half3 reflectVS = mul((float3x3)UNITY_MATRIX_V, reflectWS);
    half2 matcapUV = reflectVS.xy * 0.5h + 0.5h;
    half3 matcap = half3(0.0h, 0.0h, 0.0h);

    #if defined(_USEMATCAPMAP_ON)
        matcap = SAMPLE_TEXTURE2D(_MatCapMap, sampler_MatCapMap, matcapUV).rgb;
    #endif

    half matcapStrength = surface.mask1.b * surface.profile0.z * _MatCapGlobalStrength;
    matcapStrength *= lerp(0.55h, 1.25h, surface.mask0.b);
    matcapStrength *= lerp(1.0h, _MetalMatCapBoost, surface.mask0.a);
    color += matcap * matcapStrength;

    half3 shiftedViewDir = normalize(surface.viewDirWS - dominantLight.direction * _RimLightAlign);
    half fresnel = 1.0h - saturate(dot(surface.normalWS, shiftedViewDir));
    half rimThreshold = 1.0h - _RimWidth * surface.profile0.w;
    half rimLight = smoothstep(rimThreshold - _RimSoftness, rimThreshold + _RimSoftness, fresnel);

    half ndlRim = saturate(dot(surface.normalWS, dominantLight.direction) * 0.5h + 0.5h);
    half directionalMask = ndlRim * saturate(_RimLightAlign) + (1.0h - ndlRim) * saturate(-_RimLightAlign) + (1.0h - abs(_RimLightAlign));
    half domAtten = dominantLight.shadowAttenuation * dominantLight.distanceAttenuation;
    half shadowMask = lerp(1.0h, saturate(domAtten), _RimShadowMasking);
    rimLight *= directionalMask * shadowMask * surface.mask1.a * _RimIntensity;

    half3 rimColor = lerp(_DarkRimColor.rgb, _RimColor.rgb, ndlRim);
    color += rimColor * rimLight * lerp(half3(1.0h, 1.0h, 1.0h), dominantLight.color, _ReceiveMainLight);

    return MixFog(color, input.fogFactor);
}

#endif
