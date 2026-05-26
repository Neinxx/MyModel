#ifndef CHARACTER_NPR_FORWARD_PASS_INCLUDED
#define CHARACTER_NPR_FORWARD_PASS_INCLUDED

#include "CharacterNPRInput.hlsl"
#include "CharacterNPRLighting.hlsl"

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

half4 Frag(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    CharacterSurfaceData surfaceData;
    InitializeCharacterSurfaceData(input, surfaceData);

    half3 color = CalculateCharacterNPRLighting(input, surfaceData);

    half postMask = saturate(surfaceData.mask1.a * _PostProcessMaskStrength);
    return half4(color, lerp(1.0h, postMask, _PostProcessMaskStrength));
}

#endif // CHARACTER_NPR_FORWARD_PASS_INCLUDED
