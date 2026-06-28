Shader "Universal Render Pipeline/Character/NPR Core"
{
    Properties
    {
        [Header(Surface)]
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [Toggle(_NORMALMAP)] _UseNormalMap("Use Normal Map", Float) = 0
        _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Range(0, 2)) = 1
        _Metallic("Fallback Metallic", Range(0, 1)) = 0
        _Smoothness("Fallback Smoothness", Range(0, 1)) = 0.55

        [Header(Masks)]
        [Toggle(_USEMASKMAPS_ON)] _UseMaskMaps("Use Mask Maps", Float) = 0
        _Mask0("Mask 0: R MaterialID, G AO, B Smoothness, A Metallic", 2D) = "white" {}
        _Mask1("Mask 1: R RampBias, G ShadowSoftness, B MatCap, A Rim/Post Mask", 2D) = "white" {}

        [Header(Ramp And MatCap)]
        [Toggle(_USERAMPMAP_ON)] _UseRampMap("Use Ramp Map", Float) = 0
        [NoScaleOffset] _RampMap("Ramp Map", 2D) = "white" {}
        [Toggle(_USEMATCAPMAP_ON)] _UseMatCapMap("Use MatCap Map", Float) = 0
        [NoScaleOffset] _MatCapMap("MatCap Map", 2D) = "black" {}

        [Header(Shadow)]
        _UnifiedShadowColor("Unified Shadow Color", Color) = (0.62, 0.68, 0.86, 1)
        _InnerShadowThreshold("Inner Shadow Threshold", Range(0, 1)) = 0.48
        _InnerShadowSoftness("Inner Shadow Softness", Range(0.001, 0.5)) = 0.08
        _RampBiasStrength("Ramp Bias Strength", Range(0, 1)) = 0.18
        _OuterShadowStrength("Outer Shadow Strength", Range(0, 1)) = 0.75
        _AdditionalShadowStrength("Additional Shadow Strength", Range(0, 1)) = 0.65
        _ShadowOverlapCancel("Inner/Outer Overlap Cancel", Range(0, 1)) = 0.65
        _ReceiveMainLight("Receive Main Light Color", Range(0, 1)) = 0.35
        _ReceiveSH("Receive Ambient SH", Range(0, 1)) = 0.5

        [Header(Response)]
        _AOIntensity("AO Intensity", Range(0, 1)) = 0.75
        _MatCapGlobalStrength("MatCap Strength", Range(0, 3)) = 1
        _MetalMatCapBoost("Metal MatCap Boost", Range(0, 4)) = 1.6
        _MetalShadowLift("Metal Shadow Lift", Range(0, 1)) = 0.2

        [Header(Rim)]
        _RimColor("Light Rim Color", Color) = (0.55, 0.82, 1, 1)
        _DarkRimColor("Dark Rim Color", Color) = (0.2, 0.3, 0.5, 1)
        _RimWidth("Rim Width", Range(0, 1)) = 0.5
        _RimSoftness("Rim Softness", Range(0.001, 0.5)) = 0.05
        _RimIntensity("Rim Intensity", Range(0, 5)) = 1.0
        _RimLightAlign("Rim Light Alignment", Range(-1, 1)) = -0.5
        _RimShadowMasking("Outer Shadow Masking", Range(0, 1)) = 0.5
        _PostProcessMaskStrength("Post Mask Output", Range(0, 1)) = 0.6

        [Header(Outline)]
        [Toggle(_USE_SMOOTH_NORMAL)] _UseSmoothNormal("Use Vertex Color RGB as Smooth Normal", Float) = 0
        [Toggle(_USE_OUTLINE_MASK)] _UseOutlineMask("Use Vertex Color A as Width Mask", Float) = 0
        _OutlineColor("Outline Tint Color", Color) = (0.1, 0.1, 0.1, 1)
        _OutlineTintMix("BaseMap Tint Mix", Range(0, 1)) = 0.5
        _OutlineWidth("Outline Width", Range(0, 10)) = 2.0
        _OutlineZOffset("Outline Z-Offset", Range(0, 1)) = 0.001
        _OutlineMinDist("Min Camera Distance", Float) = 2.0
        _OutlineMaxDist("Max Camera Distance", Float) = 20.0
        _OutlineWidthScale("Distance Scale Factor", Range(0.1, 5.0)) = 1.0

        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clip", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        _StencilRef("Stencil Ref", Float) = 0
        _DebugMode("Debug Mode", Float) = 0

    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardNPRCore"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            Blend One Zero
            Stencil { Ref [_StencilRef] Comp Always Pass Replace }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _USEMASKMAPS_ON
            #pragma shader_feature_local _USERAMPMAP_ON
            #pragma shader_feature_local _USEMATCAPMAP_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Includes/CharacterNPRCoreForwardPass.hlsl"
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
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #include "Includes/CharacterNPRCoreShadowCasterPass.hlsl"
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
            #include "Includes/CharacterNPRCoreOutlinePass.hlsl"
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
            #pragma shader_feature_local _USEMASKMAPS_ON
            #pragma multi_compile_instancing
            #pragma vertex MaskPassVertex
            #pragma fragment MaskPassFragment
            #include "Includes/CharacterNPRCoreMaskPass.hlsl"
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
            #include "Includes/CharacterNPRCoreDepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "CharacterShader.Editor.CharacterNPRCoreShaderGUI"
    FallBack Off
}
