#ifndef CHARACTER_NPR_INPUT_INCLUDED
#define CHARACTER_NPR_INPUT_INCLUDED

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
TEXTURE2D(_MaterialDetailMap); SAMPLER(sampler_MaterialDetailMap);
TEXTURE2D(_HairAnisoMap); SAMPLER(sampler_HairAnisoMap);
TEXTURE2D(_FaceSDFMap); SAMPLER(sampler_FaceSDFMap);
#if defined(_USERAMPARRAY_ON)
    TEXTURE2D_ARRAY(_RampArray); SAMPLER(sampler_RampArray);
#endif
#if defined(_USEMATCAPARRAY_ON)
    TEXTURE2D_ARRAY(_MatCapArray); SAMPLER(sampler_MatCapArray);
#endif

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _MaterialDetailMap_ST;
    float4 _HairAnisoMap_ST;
    half4 _BaseColor;
    half _UseNormalMap;
    half _BumpScale;
    half _Metallic;
    half _Smoothness;
    half _UseMaskMaps;
    half _UseMaterialDetail;
    half _UseRampArray;
    half _UseMatCapArray;
    half _UseFaceSDF;
    half _UseAnisoHair;
    half _UseTunifiedPBR;
    half _UseSilk;

    // Material Detail Layers
    half4 _SkinSSSColor;
    half _SkinSSSStrength;
    half _SkinBlushStrength;
    half _SkinShadowSoftness;
    half4 _LeatherWearColor;
    half _LeatherWearStrength;
    half _LeatherSpecStrength;
    half4 _FabricShadowColor;
    half _FabricDirectionStrength;
    half _FabricWeaveScale;
    half _FabricWeaveStrength;
    half _MetalFacetStrength;
    half _MetalEdgeStrength;
    half4 _RubberSpecColor;
    half _RubberSpecStrength;
    half _RubberSpecHardness;
    half4 _ClearcoatColor;
    half _ClearcoatStrength;
    half _GlassThicknessStrength;
    
    // Face SDF
    half4 _HeadForwardWS;
    half4 _HeadRightWS;
    half _FaceShadowOffset;
    half _FaceShadowSoftness;
    half _FaceShadowStrength;
    half _FaceSDFMirrorStrength;
    half _FaceSDFMaterialID;
    half _FaceSDFMaterialTolerance;
    
    // Stockings & Silk
    half4 _SilkSkinColor;
    half4 _SilkDarkColor;
    half4 _SilkLightColor;
    half _SilkTransparency;
    half _SilkFresnelPower;
    
    // Tunified PBR
    half _PBRSpecularStrength;
    half _PBReflectionStrength;
    half _PBRStylizedThreshold;
    
    // Hair Aniso
    half4 _HairSpecColor;
    half4 _HairSpecSecondaryColor;
    half _HairSpecShift;
    half _HairSpecSecondaryShift;
    half _HairSpecSpread;
    half _HairSpecSecondarySpread;
    half _HairSpecSoftness;
    half _HairSpecIntensity;
    half _HairSpecSecondaryIntensity;
    half _HairSpecViewWeight;
    half _HairSpecShapePower;

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
    
    // Outline
    half4 _OutlineColor;
    half _OutlineTintMix;
    half _OutlineWidth;
    half _OutlineZOffset;
    half _OutlineMinDist;
    half _OutlineMaxDist;
    half _OutlineWidthScale;
    
    // Profiles (Unrolled for SRP Batcher Compatibility)
    half4 _MatProfile0_0; half4 _MatProfile0_1; half4 _MatProfile0_2; half4 _MatProfile0_3;
    half4 _MatProfile0_4; half4 _MatProfile0_5; half4 _MatProfile0_6; half4 _MatProfile0_7;
    
    half4 _MatProfile1_0; half4 _MatProfile1_1; half4 _MatProfile1_2; half4 _MatProfile1_3;
    half4 _MatProfile1_4; half4 _MatProfile1_5; half4 _MatProfile1_6; half4 _MatProfile1_7;
    
    half _Cull;
    half _AlphaClip;
    half _Cutoff;
    half _UseSmoothNormal;
    half _UseOutlineMask;
    half _StencilRef;
    half _DebugMode;
CBUFFER_END

half4 GetProfile0(half id)
{
    half4 result = _MatProfile0_0;
    result = lerp(result, _MatProfile0_1, step(0.5h, id));
    result = lerp(result, _MatProfile0_2, step(1.5h, id));
    result = lerp(result, _MatProfile0_3, step(2.5h, id));
    result = lerp(result, _MatProfile0_4, step(3.5h, id));
    result = lerp(result, _MatProfile0_5, step(4.5h, id));
    result = lerp(result, _MatProfile0_6, step(5.5h, id));
    result = lerp(result, _MatProfile0_7, step(6.5h, id));
    return result;
}

half4 GetProfile1(half id)
{
    half4 result = _MatProfile1_0;
    result = lerp(result, _MatProfile1_1, step(0.5h, id));
    result = lerp(result, _MatProfile1_2, step(1.5h, id));
    result = lerp(result, _MatProfile1_3, step(2.5h, id));
    result = lerp(result, _MatProfile1_4, step(3.5h, id));
    result = lerp(result, _MatProfile1_5, step(4.5h, id));
    result = lerp(result, _MatProfile1_6, step(5.5h, id));
    result = lerp(result, _MatProfile1_7, step(6.5h, id));
    return result;
}

#endif // CHARACTER_NPR_INPUT_INCLUDED
