# StylizedProvencal Art Assets

This folder is organized as a pure art asset package.

## Structure

- `Animations`: animation clips and controllers shipped with the art pack.
- `Materials/Master`: authored materials.
- `Materials/ModelImports`: model-import generated materials.
- `Models`: source mesh assets.
- `Prefabs`: reusable art prefabs.
- `Preview/Scenes`: preview or vendor sample scenes.
- `Shaders/ShaderGraphs`: shader graphs and shader subgraphs.
- `Terrain/TerrainLayers`: terrain layer assets.
- `Textures`: texture maps.

## Rules

- Keep gameplay scripts and module code outside this folder.
- Keep demo logic outside this folder.
- Keep preview scenes under `Preview`.
- Move Unity assets with their `.meta` files to preserve references.

## Notes
- Preview scenes use external lighting settings with baked/realtime GI disabled; they are inspection scenes, not production bake scenes.
- Model importers calculate tangents in Unity instead of importing missing source tangents/binormals, keeping imports warning-light and consistent.
- Vegetation prefabs are kept non-static and grass model importers do not generate lightmap UVs, so preview lighting does not feed foliage into GI precompute.
