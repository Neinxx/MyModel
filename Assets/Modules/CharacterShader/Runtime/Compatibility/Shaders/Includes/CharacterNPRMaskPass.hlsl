#ifndef CHARACTER_NPR_MASK_PASS_INCLUDED
#define CHARACTER_NPR_MASK_PASS_INCLUDED

#include "CharacterNPRInput.hlsl"

Varyings MaskPassVertex(Attributes input)
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
    output.shadowCoord = half4(0, 0, 0, 0);
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.fogFactor = 0;
    
    return output;
}

half4 MaskPassFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half alpha = 1.0h;
    #if defined(_ALPHATEST_ON)
        alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
        clip(alpha - _Cutoff);
    #endif

    half rimPostMask = 1.0h;
    #if defined(_USEMASKMAPS_ON)
        rimPostMask = SAMPLE_TEXTURE2D(_Mask1, sampler_Mask1, input.uv).a;
    #endif

    half postMask = saturate(rimPostMask * _PostProcessMaskStrength);
    return half4(1.0h, 1.0h, 1.0h, lerp(1.0h, postMask, _PostProcessMaskStrength));
}

#endif // CHARACTER_NPR_MASK_PASS_INCLUDED
