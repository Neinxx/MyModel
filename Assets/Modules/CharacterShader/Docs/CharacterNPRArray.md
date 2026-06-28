# Character NPR Array Shader


## Shader Choices

Use `Universal Render Pipeline/Character/NPR Core` for new character materials by default. It keeps the foundation small: base color, normal, masks, 2D ramp, 2D matcap, shadow, rim, outline, depth, shadow caster, and character mask output.

Use `Universal Render Pipeline/Character/NPR Array` only when an old material needs the full compatibility surface, including texture arrays, Face SDF, material detail layers, silk, tunified PBR, and hair anisotropic highlights.

## Module Layout

The module is split by responsibility:

- `Runtime/Core`: compatibility NPR shader surface and shared HLSL inputs.
- `Runtime/Face`: face SDF runtime controller and face-specific HLSL.
- `Runtime/Outline`: URP outline feature and outline pass HLSL.
- `Runtime/Experimental`: optional specialized material looks that should not become core until production characters need them.
- `Editor`: authoring tools, material inspectors, texture array bakers, and profile bindings.

`CharacterNPRArray` remains the compatibility shader while the module is simplified into smaller production surfaces.

This module is the character-body shading layer for the ZZZ-like pipeline.
The existing `CharacterPostProcess` module should stay responsible for screen-space character effects; this shader is responsible for material ID routing, ramp shading, matcap response, inner shadow, outer projected shadow, AO, smoothness, and metallic response.

## Shader

Use:

```text
Universal Render Pipeline/Character/NPR Array
```

The shader is designed to work in two stages:

1. Prototype mode: keep `Use Mask Maps`, `Use Ramp Texture2DArray`, and `Use MatCap Texture2DArray` disabled. The material still renders with procedural toon lighting.
2. Production mode: enable the mask maps and texture arrays after the art textures are packed.

## Mask Layout

`Mask0` stores stable material properties.

| Channel | Meaning | Notes |
| --- | --- | --- |
| R | Material ID | Encoded as `id / 7`, supporting IDs 0-7. |
| G | AO | White means no occlusion, darker means stronger occlusion. |
| B | Smoothness | Used to strengthen matcap and highlight response. |
| A | Metallic | Used to boost matcap and lift shadow darkness on metals. |

`Mask1` stores lighting-control and effect masks.

| Channel | Meaning | Notes |
| --- | --- | --- |
| R | Ramp bias | `0.5` is neutral. Lower/higher shifts the inner shadow boundary. |
| G | Local shadow hardness | Higher is harder, lower is softer. |
| B | MatCap/spec mask | Controls material-specific matcap contribution. |
| A | Rim/post-process mask | Used by rim and exported as alpha for character post-process capture. |

## Material ID Profiles

Edit material IDs through a `CharacterMaterialProfile` asset instead of editing shader vectors by hand.

Create one from:

```text
Assets/Create/Character Shader/Material Profile
```

When a material uses `Universal Render Pipeline/Character/NPR Array`, its material inspector shows a `Character Material Profile` box. Assign a profile and click `Apply Profile`. The editor writes the profile values into the shader vectors, so runtime rendering remains a plain material with no extra lookup cost.

The profile binding itself is editor-only and stored under:

```text
ProjectSettings/CharacterShaderProfileBindings.asset
```

Each material ID still maps to two shader vectors internally.

The profile inspector is designed for artists:

- Left side: choose material ID 0-7. `Mask0.R` should be painted as `ID / 7`.
- Identity: name the region and choose a material type preset, such as Skin, Hair, Fabric, or Metal.
- Texture Array Slices: choose which ramp and matcap slice this region uses.
- Shadow Style: tune shadow hardness, shadow-position sensitivity, dark-area lift, and AO influence.
- Highlight Feel: tune matcap highlight and rim-light acceptance.
- Shader Writeback: read-only preview of the vectors written into the material.

`Apply Type Preset` resets the selected ID card to an art-friendly starting point while keeping the rest of the profile untouched.

`MatProfile0_ID`:

```text
x = ramp array slice
y = matcap array slice
z = matcap strength
w = rim strength
```

`MatProfile1_ID`:

```text
x = shadow hardness multiplier
y = ramp bias scale
z = metal shadow lift
w = AO strength
```

This keeps the mask texture cheap while letting each material category behave differently.

## Inner And Outer Shadow

The shader treats both shadow types as one art-directed shadow color:

```text
UnifiedShadowColor
```

Inner shadow comes from the NDotL ramp. Outer shadow comes from URP main-light shadow attenuation. They are merged with `Inner/Outer Overlap Cancel`, which prevents stacked shadows from becoming dirty or over-dark.

Recommended tuning:

| Goal | Parameter |
| --- | --- |
| Softer anime face/body shadow | Increase `Inner Shadow Softness` or paint lower `Mask1.G`. |
| Harder clothing/metal terminator | Increase profile `ShadowHardness` or paint higher `Mask1.G`. |
| Keep cast shadows visible but clean | Raise `Outer Shadow Strength`, then tune `Inner/Outer Overlap Cancel`. |
| Avoid black metal | Increase `Metal Shadow Lift` or profile metal lift. |

## Texture2DArray Setup

Editor tools:

```text
Tools/Character Shader/Create Default NPR Material
Tools/Character Shader/Build Texture2DArray From Selection
```

When building an array, select the source textures in the Project window. The tool sorts by asset path before writing slices, so name files with a clear prefix such as `00_Skin`, `01_Hair`, `02_Fabric`.

Ramp array:

- Width axis is light response from dark to bright.
- Height can be 1 pixel or a small strip.
- Slice index is selected by material ID profile.

MatCap array:

- Each slice is a material family response, such as skin, hair, fabric, leather, metal, plastic, glass, effect.
- Keep mobile slices compact. 256 or 512 is usually enough for character matcap.

## Mobile Notes

- Texture arrays require GLES3, Metal, or Vulkan. Keep `Use Ramp Texture2DArray` and `Use MatCap Texture2DArray` disabled for fallback materials on unsupported targets.
- Prefer one shared character material template and override profiles per character.
- Keep mask textures compressed, but avoid compression formats that destroy ID channels.
- Put the character on the `Character` layer so `CharacterPostProcessFeature` can capture it separately.

## Runtime Binding Notes

`CharacterFaceSDFController` writes `_HeadForwardWS` and `_HeadRightWS` through `MaterialPropertyBlock` per renderer. This keeps SDF face shadows correct when multiple characters are visible at the same time.

The material inspector and array generator tools keep shader keywords in sync with their float toggles. If you set these toggles from custom scripts, also enable or disable the matching local keyword on the material.

## Single Source Of Truth

`CharacterMaterialProfile` is the source asset for character material style data. Materials are treated as runtime caches that receive profile vectors, baked texture arrays, and shader keywords.

The material inspector, Ramp Array Generator, and MatCap Array Generator all work through the bound profile:

- Assign or create a `CharacterMaterialProfile` in the material inspector.
- Use `Create / Edit Ramp Config` or `Create / Edit MatCap Config` from that material inspector.
- The generated config assets are stored next to the profile and referenced by the profile.
- When an array is baked, the resulting `Texture2DArray` is written back to the profile and applied to the preview material.

This keeps material ID profile data, ramp slices, matcap slices, source configs, and baked arrays aligned across editor tools.

Material ID display names in the material inspector, Ramp Array Generator, MatCap Array Generator, and profile inspector all come from `CharacterMaterialProfile.Slots`. Rename a slot in the profile when artists need a project-specific label such as `Face Skin`, `Hair Front`, or `Coat Metal`.

## ZZZ-Style Control Notes

Face SDF now blends only onto the selected `Face Material ID`, so enabling face shadows no longer overrides the body ramp. `Mirror SDF by Light Side` samples the same SDF texture with flipped X when the light comes from the opposite side, which lets a single face SDF texture cover left/right lighting for symmetric faces.

Hair anisotropic highlights use `_HairAnisoMap` as a packed control map:

| Channel | Meaning |
| --- | --- |
| R | Tangent shift / highlight height |
| G | Primary highlight mask |
| B | Secondary highlight mask |
| A | Highlight shape mask |

Use the material `Debug Mode` to inspect production data directly on the character. The most useful modes during authoring are `MaterialID`, `Mask0`, `Mask1`, `Ramp`, `FaceSDF`, `HairMask`, and `PostMask`.
`Ramp` displays the actual ramp light result, including `Texture2DArray` sampling when `Use Ramp Array` is enabled.

## Material Detail Layers

Enable `Use Material Detail Map` when a character needs more ZZZ-style material separation without splitting into many shaders.

`MaterialDetailMap` is packed as:

| Channel | Meaning |
| --- | --- |
| R | Skin SSS/blush/translucency or glass internal tint |
| G | Leather edge wear, fabric weave direction blend, or metal facet regions |
| B | Local stylized specular shape or cloth value variation |
| A | Clearcoat/glass edge feel, metal edge intensity, or low-frequency hair variation |

Current material ID behavior:

| ID | Profile | Extra response |
| --- | --- | --- |
| 0 | Skin | SSS tint, nose/cheek/ear color accents, softer half-shadow feel |
| 2 | Fabric | Shadow color variation, UV-space weave bands, broad woven-detail value shift |
| 3 | Metal | Painted facet regions, stronger clearcoat/spec shape, view-edge highlight |
| 4 | Leather | Warm worn edges and compact oily highlight |
| 6 | Glass | Opaque-pipeline glassy edge highlight, internal tint, thickness feel |
| 7 | Effect | Rubber/plastic-style hard blocky toon highlight |

Transparent or semi-transparent parts still need a separate material/pass if they require true sorting or alpha blending. The detail layer only gives opaque characters a glassy response while preserving the existing character mask and post-process path.
