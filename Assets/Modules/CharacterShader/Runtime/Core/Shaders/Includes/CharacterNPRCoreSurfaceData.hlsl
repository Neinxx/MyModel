#ifndef CHARACTER_NPR_CORE_SURFACE_DATA_INCLUDED
#define CHARACTER_NPR_CORE_SURFACE_DATA_INCLUDED

struct CharacterCoreSurfaceData
{
    half4 albedoAlpha;
    half3 normalWS;
    half3 viewDirWS;
    half profileID;
    half4 mask0;
    half4 mask1;
    half4 profile0;
    half4 profile1;
};

half3 GetCharacterCoreNormalWS(Varyings input)
{
    half3 normalWS = normalize(input.normalWS);

    #if !defined(_NORMALMAP)
        return normalWS;
    #endif

    half tangentSign = input.tangentWS.w;
    half3 bitangentWS = cross(normalWS, input.tangentWS.xyz) * tangentSign;
    half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
    return normalize(TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangentWS, normalWS)));
}

void InitializeCharacterCoreSurfaceData(Varyings input, out CharacterCoreSurfaceData surfaceData)
{
    surfaceData.albedoAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

    #if defined(_ALPHATEST_ON)
        clip(surfaceData.albedoAlpha.a - _Cutoff);
    #endif

    #if defined(_USEMASKMAPS_ON)
        surfaceData.mask0 = SAMPLE_TEXTURE2D(_Mask0, sampler_Mask0, input.uv);
        surfaceData.mask1 = SAMPLE_TEXTURE2D(_Mask1, sampler_Mask1, input.uv);
    #else
        surfaceData.mask0 = half4(0.0h, 1.0h, _Smoothness, _Metallic);
        surfaceData.mask1 = half4(0.5h, 0.5h, 1.0h, 1.0h);
    #endif

    surfaceData.profileID = clamp(round(surfaceData.mask0.r * 7.0h), 0.0h, 7.0h);

    surfaceData.profile0 = half4(0.0h, 0.0h, 1.0h, 1.0h);
    surfaceData.profile1 = half4(0.5h, 1.0h, 0.0h, 1.0h);

    surfaceData.normalWS = GetCharacterCoreNormalWS(input);
    surfaceData.viewDirWS = normalize(input.viewDirWS);
}

#endif
