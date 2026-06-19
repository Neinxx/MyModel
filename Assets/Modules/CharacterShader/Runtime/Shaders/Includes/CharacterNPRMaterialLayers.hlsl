#ifndef CHARACTER_NPR_MATERIAL_LAYERS_INCLUDED
#define CHARACTER_NPR_MATERIAL_LAYERS_INCLUDED

#include "CharacterNPRInput.hlsl"
#include "CharacterSurfaceData.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

half IsMaterialID(half profileID, half targetID)
{
    return saturate(1.0h - abs(profileID - targetID) * 10.0h);
}

half ToonSpecular(half3 normalWS, half3 viewDirWS, half3 lightDirWS, half smoothness, half shape, half softness)
{
    half3 halfDir = normalize(viewDirWS + lightDirWS);
    half nh = saturate(dot(normalWS, halfDir));
    half threshold = lerp(0.92h, 0.55h, saturate(smoothness));
    threshold = saturate(threshold - shape * 0.25h);
    return smoothstep(threshold - softness, threshold + softness, nh);
}

half3 ApplyCharacterMaterialLayers(
    half3 color,
    CharacterSurfaceData surface,
    Light dominantLight,
    half mixedShadow,
    half rimLight,
    half2 uv)
{
    #if !defined(_USE_MATERIAL_DETAIL)
        return color;
    #endif

    half skin = IsMaterialID(surface.profileID, 0.0h);
    half hair = IsMaterialID(surface.profileID, 1.0h);
    half fabric = IsMaterialID(surface.profileID, 2.0h);
    half metal = IsMaterialID(surface.profileID, 3.0h);
    half leather = IsMaterialID(surface.profileID, 4.0h);
    half transparentGlass = IsMaterialID(surface.profileID, 6.0h);
    half rubberPlastic = IsMaterialID(surface.profileID, 7.0h);

    half ndl = saturate(dot(surface.normalWS, dominantLight.direction) * 0.5h + 0.5h);
    half ndv = saturate(dot(surface.normalWS, surface.viewDirWS));
    half backScatter = saturate(1.0h - ndl) * saturate(1.0h - ndv * 0.65h);
    half detailR = surface.detail.r;
    half detailG = surface.detail.g;
    half detailB = surface.detail.b;
    half detailA = surface.detail.a;
    half edge = pow(saturate(1.0h - ndv), 2.0h);

    // Skin: soft subsurface tint, painted blush/nose/ear accents, and gentler shadow rolloff.
    half skinSSS = skin * detailR * _SkinSSSStrength;
    color += _SkinSSSColor.rgb * skinSSS * lerp(0.35h, 1.0h, backScatter);
    color = lerp(color, color * lerp(half3(1.0h, 1.0h, 1.0h), _SkinSSSColor.rgb, _SkinBlushStrength), skin * detailR);
    half3 softenedSkinShadow = lerp(color, surface.albedoAlpha.rgb * lerp(half3(1.0h, 1.0h, 1.0h), _SkinSSSColor.rgb, 0.18h), mixedShadow);
    color = lerp(color, softenedSkinShadow, skin * _SkinShadowSoftness * mixedShadow);

    // Fabric: direction/detail channel cools the shadow and adds stylized woven value bands.
    half fabricDetail = fabric * detailG;
    color = lerp(color, color * _FabricShadowColor.rgb, fabricDetail * mixedShadow * _FabricDirectionStrength);
    half weaveA = abs(frac(uv.x * _FabricWeaveScale) - 0.5h) * 2.0h;
    half weaveB = abs(frac(uv.y * _FabricWeaveScale * 0.73h) - 0.5h) * 2.0h;
    half weave = lerp(weaveA, weaveB, detailG);
    half weaveValue = lerp(1.0h - _FabricWeaveStrength * 0.08h, 1.0h + _FabricWeaveStrength * 0.08h, weave);
    color *= lerp(1.0h, weaveValue * lerp(0.92h, 1.08h, detailB), fabric);

    // Leather: warm worn edges plus a compact oily highlight.
    half leatherWear = leather * saturate(detailG + edge * 0.5h) * _LeatherWearStrength;
    color = lerp(color, _LeatherWearColor.rgb, leatherWear * rimLight);
    half leatherSpec = ToonSpecular(surface.normalWS, surface.viewDirWS, dominantLight.direction, surface.mask0.b, detailB, 0.035h);
    color += _LeatherWearColor.rgb * leatherSpec * leather * max(0.15h, detailB) * _LeatherSpecStrength * dominantLight.color;

    // Metal: stronger stylized reflection on painted shape regions, plus view-edge sparkle.
    half metalClear = metal * max(detailB, detailA);
    half metalSpec = ToonSpecular(surface.normalWS, surface.viewDirWS, dominantLight.direction, surface.mask0.b, detailB, 0.025h);
    half facet = smoothstep(0.35h, 0.85h, detailG) * _MetalFacetStrength;
    color += _ClearcoatColor.rgb * metalSpec * metalClear * _ClearcoatStrength * (1.0h + facet) * dominantLight.color;
    color += _ClearcoatColor.rgb * edge * metal * detailA * _MetalEdgeStrength;

    // Rubber/plastic: larger, harder cartoon spec blocks.
    half rubberSoftness = lerp(0.11h, 0.018h, _RubberSpecHardness);
    half rubberSpec = ToonSpecular(surface.normalWS, surface.viewDirWS, dominantLight.direction, max(surface.mask0.b, 0.55h), detailB, rubberSoftness);
    color += _RubberSpecColor.rgb * rubberSpec * rubberPlastic * max(0.25h, detailB) * _RubberSpecStrength * dominantLight.color;

    // Transparent/effect: keep opaque sorting, but give it a glassy edge and internal tint.
    half glassMask = transparentGlass * max(detailA, surface.mask1.b);
    half glassFresnel = pow(saturate(1.0h - ndv), 3.0h);
    color = lerp(color, color * _ClearcoatColor.rgb, glassMask * detailR * _GlassThicknessStrength);
    color += _ClearcoatColor.rgb * glassFresnel * glassMask * _ClearcoatStrength;

    // Hair gets a subtle low-frequency tint from detail A without replacing the dedicated hair spec layer.
    color *= lerp(1.0h, lerp(0.94h, 1.08h, detailA), hair * 0.35h);

    return color;
}

#endif // CHARACTER_NPR_MATERIAL_LAYERS_INCLUDED
