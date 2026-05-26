# Character NPR Array Shader

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

- Texture arrays require GLES3, Metal, or Vulkan. Keep fallback mode available for unsupported targets.
- Prefer one shared character material template and override profiles per character.
- Keep mask textures compressed, but avoid compression formats that destroy ID channels.
- Put the character on the `Character` layer so `CharacterPostProcessFeature` can capture it separately.
