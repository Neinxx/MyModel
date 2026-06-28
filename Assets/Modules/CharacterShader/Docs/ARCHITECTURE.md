# CharacterShader Architecture

CharacterShader is a character rendering module, not a gameplay module. Keep it small, portable, and easy to strip.

## Module Slices

- Core: `Universal Render Pipeline/Character/NPR Core`, base NPR surface, 2D ramp, 2D matcap, shadow, rim, alpha clip, outline, and mask output.
- Outline: outline shader pass and URP renderer feature.
- Face: face SDF sampling and the runtime controller that writes head vectors.
- Authoring: inspectors, material profiles, texture array bakers, and smooth normal tools.
- Experimental: specialized looks such as silk, hair anisotropic highlights, tunified PBR, and material detail layers until a production character proves they are needed.
- Compatibility: `Universal Render Pipeline/Character/NPR Array`, the existing full shader kept for old materials and demos while Core becomes the default.

## Rules

- Runtime code must not reference UnityEditor.
- Editor tools must live under Editor and compile through CharacterShader.Editor.
- Demo assets may assemble CharacterShader features, but the module must not depend on Demo content.
- The core shader should stay boring: stable properties, low variant pressure, and predictable mobile cost.
- Specialized looks should be opt-in, easy to remove, and documented before they graduate into Core.

## Current State

`CharacterNPRCore` is the preferred default for new materials. It intentionally uses ordinary 2D ramp and matcap textures; texture arrays stay in the compatibility shader. `CharacterNPRArray` is feature-complete but too broad for a foundation module, so keep it as a compatibility shader for old materials and demos.
