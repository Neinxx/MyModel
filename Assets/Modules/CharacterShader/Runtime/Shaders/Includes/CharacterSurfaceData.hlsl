#ifndef CHARACTER_NPR_SURFACE_DATA_INCLUDED
#define CHARACTER_NPR_SURFACE_DATA_INCLUDED

// Struct to pass parsed masking and profile data
struct CharacterSurfaceData
{
    half4 albedoAlpha;
    half3 normalWS;
    half3 viewDirWS;
    half3 tangentWS; // Added to support anisotropic specular
    
    half profileID;  // The rounded Material ID
    
    half4 mask0; // R MaterialID, G AO, B Smoothness, A Metallic
    half4 mask1; // R RampBias, G ShadowSoftness, B MatCap, A Rim/Post Mask
    
    half4 profile0; // RampSlice, MatCapSlice, MatCapStrength, RimStrength
    half4 profile1; // ShadowHardness, BiasScale, MetalLift, AOIntensity
};

// Calculates world space normal with Normal Map and Scale
half3 GetCharacterNormalWS(Varyings input)
{
    half3 normalWS = normalize(input.normalWS);
    half4 tangentWS = input.tangentWS;
    half tangentSign = tangentWS.w;
    half3 bitangentWS = cross(normalWS, tangentWS.xyz) * tangentSign;
    half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
    return normalize(TransformTangentToWorld(normalTS, half3x3(tangentWS.xyz, bitangentWS, normalWS)));
}

void InitializeCharacterSurfaceData(Varyings input, out CharacterSurfaceData surfaceData)
{
    surfaceData.albedoAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
    
    #if defined(_ALPHATEST_ON)
        clip(surfaceData.albedoAlpha.a - _Cutoff);
    #endif

    if (_UseMaskMaps > 0.5h)
    {
        surfaceData.mask0 = SAMPLE_TEXTURE2D(_Mask0, sampler_Mask0, input.uv);
        surfaceData.mask1 = SAMPLE_TEXTURE2D(_Mask1, sampler_Mask1, input.uv);
    }
    else
    {
        surfaceData.mask0 = half4(0.0h, 1.0h, _Smoothness, _Metallic);
        surfaceData.mask1 = half4(0.5h, 0.5h, 1.0h, 1.0h);
    }

    // Material ID mapped to 0-7 and clamped strictly to prevent array bounds corruption
    surfaceData.profileID = clamp(round(surfaceData.mask0.r * 7.0h), 0.0h, 7.0h);
    
    if (_UseRampArray > 0.5h || _UseMatCapArray > 0.5h)
    {
        // Decode Profile Data dynamically from global uniforms array based on Profile ID
        // (Assuming _MatProfile0 and _MatProfile1 arrays are populated by script)
        int id = (int)surfaceData.profileID;
        surfaceData.profile0 = GetProfile0(id);
        surfaceData.profile1 = GetProfile1(id);
    }
    else
    {
        surfaceData.profile0 = half4(0, 0, 1, 1);
        surfaceData.profile1 = half4(0.5, 1, 0, 1);
    }

    surfaceData.normalWS = GetCharacterNormalWS(input);
    surfaceData.viewDirWS = normalize(input.viewDirWS);
    surfaceData.tangentWS = input.tangentWS.xyz; // Need this for anisotropic lighting
}

#endif // CHARACTER_NPR_SURFACE_DATA_INCLUDED
