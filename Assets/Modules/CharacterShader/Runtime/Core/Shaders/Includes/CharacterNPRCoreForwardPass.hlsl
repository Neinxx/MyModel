#ifndef CHARACTER_NPR_CORE_FORWARD_PASS_INCLUDED
#define CHARACTER_NPR_CORE_FORWARD_PASS_INCLUDED

#include "CharacterNPRCoreInput.hlsl"
#include "CharacterNPRCoreLighting.hlsl"

Varyings Vert(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs positionInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    output.positionCS = positionInput.positionCS;
    output.positionWS = positionInput.positionWS;
    output.normalWS = normalInput.normalWS;
    output.tangentWS = half4(normalInput.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    output.viewDirWS = GetWorldSpaceNormalizeViewDir(positionInput.positionWS);
    output.shadowCoord = TransformWorldToShadowCoord(positionInput.positionWS);
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.fogFactor = ComputeFogFactor(positionInput.positionCS.z);
    return output;
}

half3 GetCharacterCoreMaterialIDColor(half id)
{
    half3 color = half3(0.95h, 0.35h, 0.28h);
    color = lerp(color, half3(0.95h, 0.78h, 0.28h), step(0.5h, id));
    color = lerp(color, half3(0.35h, 0.85h, 0.35h), step(1.5h, id));
    color = lerp(color, half3(0.20h, 0.75h, 0.95h), step(2.5h, id));
    color = lerp(color, half3(0.40h, 0.45h, 1.00h), step(3.5h, id));
    color = lerp(color, half3(0.80h, 0.35h, 1.00h), step(4.5h, id));
    color = lerp(color, half3(1.00h, 0.45h, 0.75h), step(5.5h, id));
    color = lerp(color, half3(0.85h, 0.85h, 0.85h), step(6.5h, id));
    return color;
}

half3 CalculateCharacterCoreDebugColor(Varyings input, CharacterCoreSurfaceData surface)
{
    half mode = _DebugMode;
    if (mode < 0.5h) return half3(-1.0h, -1.0h, -1.0h);
    if (mode < 1.5h) return GetCharacterCoreMaterialIDColor(surface.profileID);
    if (mode < 2.5h) return surface.mask0.rgb;
    if (mode < 3.5h) return surface.mask1.rgb;
    if (mode < 4.5h)
    {
        Light mainLight = GetMainLight(input.shadowCoord);
        half ndl = saturate(dot(surface.normalWS, mainLight.direction) * 0.5h + 0.5h);
        half rampBias = (surface.mask1.r - 0.5h) * _RampBiasStrength * surface.profile1.y;
        half rampU = saturate(ndl + rampBias);
        half localSoftness = max(0.001h, _InnerShadowSoftness * lerp(1.5h, 0.35h, saturate(surface.mask1.g)) / max(0.25h, surface.profile1.x));
        half rampLight = smoothstep(_InnerShadowThreshold - localSoftness, _InnerShadowThreshold + localSoftness, rampU);
        #if defined(_USERAMPMAP_ON)
            rampLight = SAMPLE_TEXTURE2D(_RampMap, sampler_RampMap, float2(rampU, 0.5h)).r;
        #endif
        return half3(rampLight, rampLight, rampLight);
    }
    if (mode < 5.5h) return surface.normalWS * 0.5h + 0.5h;
    if (mode < 6.5h)
    {
        half3 reflectWS = reflect(-surface.viewDirWS, surface.normalWS);
        half3 reflectVS = mul((float3x3)UNITY_MATRIX_V, reflectWS);
        half2 matcapUV = reflectVS.xy * 0.5h + 0.5h;
        return half3(matcapUV, 0.0h);
    }
    half postMask = saturate(surface.mask1.a * _PostProcessMaskStrength);
    return half3(postMask, postMask, postMask);
}

half4 Frag(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    CharacterCoreSurfaceData surfaceData;
    InitializeCharacterCoreSurfaceData(input, surfaceData);

    half3 debugColor = CalculateCharacterCoreDebugColor(input, surfaceData);
    if (debugColor.r >= 0.0h)
    {
        return half4(debugColor, 1.0h);
    }

    half3 color = CalculateCharacterNPRCoreLighting(input, surfaceData);
    half postMask = saturate(surfaceData.mask1.a * _PostProcessMaskStrength);
    return half4(color, lerp(1.0h, postMask, _PostProcessMaskStrength));
}

#endif
