#ifndef CHARACTER_NPR_CORE_INPUT_INCLUDED
#define CHARACTER_NPR_CORE_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    half4 tangentWS : TEXCOORD3;
    half3 viewDirWS : TEXCOORD4;
    float4 shadowCoord : TEXCOORD5;
    half fogFactor : TEXCOORD6;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
TEXTURE2D(_Mask0); SAMPLER(sampler_Mask0);
TEXTURE2D(_Mask1); SAMPLER(sampler_Mask1);
TEXTURE2D(_RampMap); SAMPLER(sampler_RampMap);
TEXTURE2D(_MatCapMap); SAMPLER(sampler_MatCapMap);

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half _UseNormalMap;
    half _BumpScale;
    half _Metallic;
    half _Smoothness;
    half _UseMaskMaps;
    half _UseRampMap;
    half _UseMatCapMap;

    half4 _UnifiedShadowColor;
    half _InnerShadowThreshold;
    half _InnerShadowSoftness;
    half _RampBiasStrength;
    half _OuterShadowStrength;
    half _AdditionalShadowStrength;
    half _ShadowOverlapCancel;
    half _ReceiveMainLight;
    half _ReceiveSH;
    half _AOIntensity;
    half _MatCapGlobalStrength;
    half _MetalMatCapBoost;
    half _MetalShadowLift;
    half4 _RimColor;
    half4 _DarkRimColor;
    half _RimWidth;
    half _RimSoftness;
    half _RimIntensity;
    half _RimLightAlign;
    half _RimShadowMasking;
    half _PostProcessMaskStrength;

    half4 _OutlineColor;
    half _OutlineTintMix;
    half _OutlineWidth;
    half _OutlineZOffset;
    half _OutlineMinDist;
    half _OutlineMaxDist;
    half _OutlineWidthScale;



    half _Cull;
    half _AlphaClip;
    half _Cutoff;
    half _UseSmoothNormal;
    half _UseOutlineMask;
    half _StencilRef;
    half _DebugMode;
CBUFFER_END

#endif
