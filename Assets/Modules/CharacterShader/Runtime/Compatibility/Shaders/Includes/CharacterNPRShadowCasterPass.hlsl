#ifndef CHARACTER_NPR_SHADOW_CASTER_PASS_INCLUDED
#define CHARACTER_NPR_SHADOW_CASTER_PASS_INCLUDED

#include "CharacterNPRInput.hlsl"
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

    // 1. You can add Vertex Animation logic here in the future
    float3 positionOS = input.positionOS.xyz;
    
    float3 positionWS = TransformObjectToWorld(positionOS);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

    // 2. Fetch Light Direction depending on light type
    #if _CASTING_PUNCTUAL_LIGHT_SHADOW
        float3 lightDirectionWS = normalize(_LightPosition.xyz - positionWS);
    #else
        float3 lightDirectionWS = _LightDirection;
    #endif

    // 3. Apply standard URP Shadow Bias to avoid shadow acne
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

    // 4. Clamping the shadow clip space to avoid disappearing shadows near camera
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

    // Perform Alpha Clipping for Shadows (e.g. Hair, Tattered Clothes)
    #if defined(_ALPHATEST_ON)
        half4 baseColor = _BaseColor;
        half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a;
        clip(alpha * baseColor.a - _Cutoff);
    #endif

    return 0;
}

#endif // CHARACTER_NPR_SHADOW_CASTER_PASS_INCLUDED
