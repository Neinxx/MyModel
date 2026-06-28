#ifndef CHARACTER_NPR_CORE_OUTLINE_PASS_INCLUDED
#define CHARACTER_NPR_CORE_OUTLINE_PASS_INCLUDED

#include "CharacterNPRCoreInput.hlsl"

struct OutlineAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 color : COLOR;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct OutlineVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

OutlineVaryings OutlinePassVertex(OutlineAttributes input)
{
    OutlineVaryings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    #if defined(_USE_SMOOTH_NORMAL)
        float3 normalWS = TransformObjectToWorldNormal(input.color.rgb * 2.0 - 1.0);
    #else
        float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    #endif

    float4 positionCS = TransformWorldToHClip(positionWS);
    float widthMask = 1.0;
    #if defined(_USE_OUTLINE_MASK)
        widthMask = input.color.a;
    #endif

    float3 normalCS = TransformWorldToHClipDir(normalWS);
    float2 expandDir = normalize(normalCS.xy);
    float width = _OutlineWidth * 0.002 * widthMask * _OutlineWidthScale;
    float clampedDepth = clamp(positionCS.w, _OutlineMinDist, _OutlineMaxDist);
    float fovCorrection = 1.0 / UNITY_MATRIX_P[1][1];
    float2 screenRatio = float2(_ScreenParams.y / _ScreenParams.x, 1.0);
    positionCS.xy += expandDir * screenRatio * width * clampedDepth * fovCorrection;

    #if UNITY_REVERSED_Z
        positionCS.z -= _OutlineZOffset * positionCS.w;
    #else
        positionCS.z += _OutlineZOffset * positionCS.w;
    #endif

    output.positionCS = positionCS;
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    return output;
}

half4 OutlinePassFragment(OutlineVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
    #if defined(_ALPHATEST_ON)
        clip(baseMap.a * _BaseColor.a - _Cutoff);
    #endif
    half3 darkenedBase = baseMap.rgb * _BaseColor.rgb * _OutlineColor.rgb * 0.5h;
    return half4(lerp(_OutlineColor.rgb, darkenedBase, _OutlineTintMix), 1.0h);
}

#endif
