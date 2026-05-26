#ifndef CHARACTER_NPR_OUTLINE_PASS_INCLUDED
#define CHARACTER_NPR_OUTLINE_PASS_INCLUDED

#include "CharacterNPRInput.hlsl"

struct OutlineAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 color : COLOR; // rgb: smooth normal, a: width mask
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
    
    // 1. Smooth Normal selection
    #if defined(_USE_SMOOTH_NORMAL)
        float3 smoothNormalOS = input.color.rgb * 2.0 - 1.0;
        float3 normalWS = TransformObjectToWorldNormal(smoothNormalOS);
    #else
        float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    #endif

    float4 positionCS = TransformWorldToHClip(positionWS);

    // 2. Vertex Color Masking
    float widthMask = 1.0;
    #if defined(_USE_OUTLINE_MASK)
        widthMask = input.color.a;
    #endif

    // 3. Screen-space expansion with FOV correction and Distance clamping
    float3 normalCS = TransformWorldToHClipDir(normalWS);
    float2 expandDir = normalize(normalCS.xy);
    
    // Base width
    float width = _OutlineWidth * 0.002 * widthMask * _OutlineWidthScale;
    
    // Distance Clamping (prevents outline from being too thick close up, or disappearing far away)
    // positionCS.w is the distance from camera in projection space
    float clampedDepth = clamp(positionCS.w, _OutlineMinDist, _OutlineMaxDist);
    
    // FOV Correction: UNITY_MATRIX_P[1][1] accounts for the field of view stretching
    float fovCorrection = 1.0 / UNITY_MATRIX_P[1][1];
    
    // Aspect Ratio correction
    float2 screenRatio = float2(_ScreenParams.y / _ScreenParams.x, 1.0);
    
    positionCS.xy += expandDir * screenRatio * width * clampedDepth * fovCorrection;

    // 4. Z-Offset hack (pushing outline into the screen slightly to avoid acne on the model surface)
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

    // BaseMap Tinting (Blend between pure _OutlineColor and darkened BaseMap)
    half3 darkenedBase = baseMap.rgb * _BaseColor.rgb * _OutlineColor.rgb * 0.5; // Multiply tint
    half3 finalColor = lerp(_OutlineColor.rgb, darkenedBase, _OutlineTintMix);

    return half4(finalColor, 1.0h);
}

#endif // CHARACTER_NPR_OUTLINE_PASS_INCLUDED
