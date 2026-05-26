#ifndef CHARACTER_NPR_DEPTH_ONLY_PASS_INCLUDED
#define CHARACTER_NPR_DEPTH_ONLY_PASS_INCLUDED

#include "CharacterNPRInput.hlsl"

struct DepthVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

DepthVaryings DepthOnlyVertex(Attributes input)
{
    DepthVaryings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    output.positionCS = TransformWorldToHClip(positionWS);
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

    return output;
}

half4 DepthOnlyFragment(DepthVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);

    #if defined(_ALPHATEST_ON)
        half4 baseColor = _BaseColor;
        half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a;
        clip(alpha * baseColor.a - _Cutoff);
    #endif

    return 0;
}

#endif // CHARACTER_NPR_DEPTH_ONLY_PASS_INCLUDED
