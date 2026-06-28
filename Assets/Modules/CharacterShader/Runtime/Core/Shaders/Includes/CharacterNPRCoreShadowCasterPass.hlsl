#ifndef CHARACTER_NPR_CORE_SHADOW_CASTER_PASS_INCLUDED
#define CHARACTER_NPR_CORE_SHADOW_CASTER_PASS_INCLUDED

#include "CharacterNPRCoreInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

float3 _LightDirection;
float3 _LightPosition;

struct ShadowVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

ShadowVaryings ShadowPassVertex(Attributes input)
{
    ShadowVaryings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    #if _CASTING_PUNCTUAL_LIGHT_SHADOW
        float3 lightDirectionWS = normalize(_LightPosition.xyz - positionWS);
    #else
        float3 lightDirectionWS = _LightDirection;
    #endif

    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
    #if UNITY_REVERSED_Z
        positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
    #else
        positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
    #endif

    output.positionCS = positionCS;
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    return output;
}

half4 ShadowPassFragment(ShadowVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    #if defined(_ALPHATEST_ON)
        half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a;
        clip(alpha * _BaseColor.a - _Cutoff);
    #endif
    return 0;
}

#endif
