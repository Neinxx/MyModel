Shader "Universal Render Pipeline/Character/NPR Array"
{
    Properties
    {
        [Header(Surface)]
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Range(0, 2)) = 1
        _Metallic("Fallback Metallic", Range(0, 1)) = 0
        _Smoothness("Fallback Smoothness", Range(0, 1)) = 0.55

        [Header(Mask Packing)]
        [Toggle(_USEMASKMAPS_ON)] _UseMaskMaps("Use Mask Maps", Float) = 0
        _Mask0("Mask 0: R MaterialID, G AO, B Smoothness, A Metallic", 2D) = "white" {}
        _Mask1("Mask 1: R RampBias, G ShadowSoftness, B MatCap, A Rim Post Mask", 2D) = "white" {}

        [Header(Face SDF Shadow)]
        [Toggle(_USE_FACE_SDF)] _UseFaceSDF("Enable SDF Face Shadow", Float) = 0
        [NoScaleOffset] _FaceSDFMap("Face SDF Map (R)", 2D) = "white" {}
        _FaceShadowOffset("Shadow Offset", Range(-1, 1)) = 0.0
        _FaceShadowSoftness("Shadow Softness", Range(0, 0.5)) = 0.05
        
        [Header(Ramp And MatCap Arrays)]
        [Toggle(_USERAMPARRAY_ON)] _UseRampArray("Use Ramp Texture2DArray", Float) = 0
        _RampArray("Ramp Array", 2DArray) = "" {}
        [Toggle(_USEMATCAPARRAY_ON)] _UseMatCapArray("Use MatCap Texture2DArray", Float) = 0
        _MatCapArray("MatCap Array", 2DArray) = "" {}

        [Header(Shadow Model)]
        _UnifiedShadowColor("Unified Inner/Outer Shadow Color", Color) = (0.62, 0.68, 0.86, 1)
        _InnerShadowThreshold("Inner Shadow Threshold", Range(0, 1)) = 0.48
        _InnerShadowSoftness("Inner Shadow Softness", Range(0.001, 0.5)) = 0.08
        _RampBiasStrength("Ramp Bias Strength", Range(0, 1)) = 0.18
        _OuterShadowStrength("Outer Shadow Strength", Range(0, 1)) = 0.75
        _AdditionalShadowStrength("Additional Shadow Strength", Range(0, 1)) = 0.65
        _ShadowOverlapCancel("Inner/Outer Overlap Cancel", Range(0, 1)) = 0.65
        _ReceiveMainLight("Receive Main Light Color", Range(0, 1)) = 0.35
        _ReceiveSH("Receive Ambient SH (Global Illumination)", Range(0, 1)) = 0.5

        [Header(Material Response)]
        _AOIntensity("AO Intensity", Range(0, 1)) = 0.75
        _MatCapGlobalStrength("MatCap Global Strength", Range(0, 3)) = 1
        _MetalMatCapBoost("Metal MatCap Boost", Range(0, 4)) = 1.6
        _MetalShadowLift("Metal Shadow Lift", Range(0, 1)) = 0.2

        [Header(Rim And Post Mask)]
        _RimColor("Light Rim Color", Color) = (0.55, 0.82, 1, 1)
        _DarkRimColor("Dark Rim Color (Contour)", Color) = (0.2, 0.3, 0.5, 1)
        _RimWidth("Rim Width", Range(0, 1)) = 0.5
        _RimSoftness("Rim Softness", Range(0.001, 0.5)) = 0.05
        _RimIntensity("Rim Intensity", Range(0, 5)) = 1.0
        _RimLightAlign("Rim Light Alignment (-1 Back, 1 Front)", Range(-1, 1)) = -0.5
        _RimShadowMasking("Outer Shadow Masking", Range(0, 1)) = 0.5
        _PostProcessMaskStrength("Post Process Mask Strength", Range(0, 1)) = 0.6

        [Header(Tunified PBR)]
        [Toggle(_USE_TUNIFIED_PBR)] _UseTunifiedPBR("Enable Tunified PBR", Float) = 0
        _PBRSpecularStrength("PBR Specular Strength", Range(0, 5)) = 1.0
        _PBReflectionStrength("Environment Reflection Strength", Range(0, 5)) = 1.0
        _PBRStylizedThreshold("Stylized Threshold", Range(0, 0.999)) = 0.5
        
        [Header(Stocking and Silk)]
        [Toggle(_USE_SILK)] _UseSilk("Enable Stocking/Silk", Float) = 0
        _SilkSkinColor("Underlying Skin Color", Color) = (1.0, 0.8, 0.7, 1.0)
        _SilkDarkColor("Silk Edge Color (Dark)", Color) = (0.1, 0.1, 0.15, 1.0)
        _SilkLightColor("Silk Center Color (Light)", Color) = (0.3, 0.3, 0.35, 1.0)
        _SilkTransparency("Transparency (Denier)", Range(0, 1)) = 0.5
        _SilkFresnelPower("Edge Thickness Power", Range(0.1, 5.0)) = 3.0
        
        [Header(Hair Anisotropic Specular)]
        [Toggle(_USE_ANISO_HAIR)] _UseAnisoHair("Enable Hair Specular", Float) = 0
        _HairAnisoMap("Hair Shift (R) / Spec Mask (G)", 2D) = "gray" {}
        _HairSpecColor("Hair Specular Color", Color) = (1, 1, 1, 1)
        _HairSpecShift("Global Shift", Range(-1, 1)) = 0
        _HairSpecSpread("Specular Spread", Range(0.01, 1.0)) = 0.5
        _HairSpecSoftness("Specular Softness", Range(0.001, 0.5)) = 0.05
        _HairSpecIntensity("Specular Intensity", Range(0, 5)) = 1.0

        [Header(Stylized Outline)]
        [Toggle(_USE_SMOOTH_NORMAL)] _UseSmoothNormal("Use Vertex Color RGB as Smooth Normal", Float) = 0
        [Toggle(_USE_OUTLINE_MASK)] _UseOutlineMask("Use Vertex Color A as Width Mask", Float) = 0
        _OutlineColor("Outline Tint Color", Color) = (0.1, 0.1, 0.1, 1)
        _OutlineTintMix("BaseMap Tint Mix", Range(0, 1)) = 0.5
        _OutlineWidth("Outline Width", Range(0, 10)) = 2.0
        _OutlineZOffset("Outline Z-Offset", Range(0, 1)) = 0.001
        
        [Header(Outline Distance Fade)]
        _OutlineMinDist("Min Camera Distance", Float) = 2.0
        _OutlineMaxDist("Max Camera Distance", Float) = 20.0
        _OutlineWidthScale("Distance Scale Factor", Range(0.1, 5.0)) = 1.0

        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clip", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        _StencilRef("Stencil Ref", Float) = 0
        
        // Hidden Properties required for SRP Batcher CBUFFER matching
        [HideInInspector] _MatProfile0_0("MatProfile0_0", Vector) = (0,0,0,0)
        [HideInInspector] _MatProfile0_1("MatProfile0_1", Vector) = (0,0,0,0)
        [HideInInspector] _MatProfile0_2("MatProfile0_2", Vector) = (0,0,0,0)
        [HideInInspector] _MatProfile0_3("MatProfile0_3", Vector) = (0,0,0,0)
        [HideInInspector] _MatProfile0_4("MatProfile0_4", Vector) = (0,0,0,0)
        [HideInInspector] _MatProfile0_5("MatProfile0_5", Vector) = (0,0,0,0)
        [HideInInspector] _MatProfile0_6("MatProfile0_6", Vector) = (0,0,0,0)
        [HideInInspector] _MatProfile0_7("MatProfile0_7", Vector) = (0,0,0,0)
        
        [HideInInspector] _MatProfile1_0("MatProfile1_0", Vector) = (0,0,0,0)
        [HideInInspector] _MatProfile1_1("MatProfile1_1", Vector) = (0,0,0,0)
        [HideInInspector] _MatProfile1_2("MatProfile1_2", Vector) = (0,0,0,0)
        [HideInInspector] _MatProfile1_3("MatProfile1_3", Vector) = (0,0,0,0)
        [HideInInspector] _MatProfile1_4("MatProfile1_4", Vector) = (0,0,0,0)
        [HideInInspector] _MatProfile1_5("MatProfile1_5", Vector) = (0,0,0,0)
        [HideInInspector] _MatProfile1_6("MatProfile1_6", Vector) = (0,0,0,0)
        [HideInInspector] _MatProfile1_7("MatProfile1_7", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardNPR"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            Blend One Zero

            Stencil
            {
                Ref [_StencilRef]
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma require 2darray
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Includes/CharacterNPRForwardPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5

            // Material Keywords
            #pragma shader_feature_local _ALPHATEST_ON

            // Universal Pipeline keywords
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Includes/CharacterNPRShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "StylizedOutline"
            Tags { "LightMode" = "StylizedOutline" }

            Cull Front
            ZWrite On
            ZTest LEqual
            Blend Off

            HLSLPROGRAM
            #pragma target 3.5

            #pragma shader_feature_local _USE_SMOOTH_NORMAL
            #pragma shader_feature_local _USE_OUTLINE_MASK
            #pragma shader_feature_local _ALPHATEST_ON

            #pragma vertex OutlinePassVertex
            #pragma fragment OutlinePassFragment

            #include "Includes/CharacterNPROutlinePass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "CharacterMask"
            Tags { "LightMode" = "CharacterMask" }

            ZWrite On
            ZTest LEqual
            ColorMask RGBA
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            #pragma vertex MaskPassVertex
            #pragma fragment MaskPassFragment

            #include "Includes/CharacterNPRMaskPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5

            #pragma shader_feature_local _ALPHATEST_ON

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Includes/CharacterNPRDepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "CharacterShader.Editor.CharacterNPRShaderGUI"
    FallBack Off
}
