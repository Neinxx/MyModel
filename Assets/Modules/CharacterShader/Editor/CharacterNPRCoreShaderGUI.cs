using UnityEditor;
using UnityEngine;

namespace CharacterShader.Editor
{
    public sealed class CharacterNPRCoreShaderGUI : ShaderGUI
    {
        private static readonly string[] ToggleProperties =
        {
            "_AlphaClip",
            "_UseNormalMap",
            "_UseMaskMaps",
            "_UseRampMap",
            "_UseMatCapMap",
            "_UseSmoothNormal",
            "_UseOutlineMask"
        };

        private static readonly string[] ToggleKeywords =
        {
            "_ALPHATEST_ON",
            "_NORMALMAP",
            "_USEMASKMAPS_ON",
            "_USERAMPMAP_ON",
            "_USEMATCAPMAP_ON",
            "_USE_SMOOTH_NORMAL",
            "_USE_OUTLINE_MASK"
        };

        private bool surfaceExpanded = true;
        private bool masksExpanded = true;
        private bool lightingExpanded = true;
        private bool outlineExpanded;
        private bool renderingExpanded;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            EditorGUI.BeginChangeCheck();

            DrawSurface(materialEditor, properties);
            DrawMasks(materialEditor, properties);
            DrawLighting(materialEditor, properties);
            DrawOutline(materialEditor, properties);
            DrawRendering(materialEditor, properties);

            if (EditorGUI.EndChangeCheck())
            {
                SyncKeywords(materialEditor.targets, properties);
            }
        }

        private void DrawSurface(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            surfaceExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(surfaceExpanded, "Surface");
            if (surfaceExpanded)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    materialEditor.TexturePropertySingleLine(new GUIContent("Base Map"), Find(properties, "_BaseMap"), Find(properties, "_BaseColor"));
                    MaterialProperty baseMap = Find(properties, "_BaseMap");
                    if (baseMap.textureValue != null)
                    {
                        materialEditor.TextureScaleOffsetProperty(baseMap);
                    }

                    MaterialProperty normalToggle = Find(properties, "_UseNormalMap");
                    materialEditor.ShaderProperty(normalToggle, "Use Normal Map");
                    if (normalToggle.floatValue > 0.5f)
                    {
                        materialEditor.TexturePropertySingleLine(new GUIContent("Normal Map"), Find(properties, "_BumpMap"), Find(properties, "_BumpScale"));
                    }

                    materialEditor.ShaderProperty(Find(properties, "_Metallic"), "Fallback Metallic");
                    materialEditor.ShaderProperty(Find(properties, "_Smoothness"), "Fallback Smoothness");
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawMasks(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            masksExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(masksExpanded, "Masks, Ramp, MatCap");
            if (masksExpanded)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    MaterialProperty useMasks = Find(properties, "_UseMaskMaps");
                    materialEditor.ShaderProperty(useMasks, "Use Mask Maps");
                    if (useMasks.floatValue > 0.5f)
                    {
                        materialEditor.TexturePropertySingleLine(new GUIContent("Mask 0"), Find(properties, "_Mask0"));
                        materialEditor.TexturePropertySingleLine(new GUIContent("Mask 1"), Find(properties, "_Mask1"));
                    }

                    MaterialProperty useRamp = Find(properties, "_UseRampMap");
                    materialEditor.ShaderProperty(useRamp, "Use Ramp Map");
                    if (useRamp.floatValue > 0.5f)
                    {
                        materialEditor.TexturePropertySingleLine(new GUIContent("Ramp Map"), Find(properties, "_RampMap"));
                    }

                    MaterialProperty useMatCap = Find(properties, "_UseMatCapMap");
                    materialEditor.ShaderProperty(useMatCap, "Use MatCap Map");
                    if (useMatCap.floatValue > 0.5f)
                    {
                        materialEditor.TexturePropertySingleLine(new GUIContent("MatCap Map"), Find(properties, "_MatCapMap"));
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawLighting(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            lightingExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(lightingExpanded, "Lighting");
            if (lightingExpanded)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    materialEditor.ShaderProperty(Find(properties, "_UnifiedShadowColor"), "Shadow Color");
                    materialEditor.ShaderProperty(Find(properties, "_InnerShadowThreshold"), "Inner Threshold");
                    materialEditor.ShaderProperty(Find(properties, "_InnerShadowSoftness"), "Inner Softness");
                    materialEditor.ShaderProperty(Find(properties, "_RampBiasStrength"), "Ramp Bias");
                    materialEditor.ShaderProperty(Find(properties, "_OuterShadowStrength"), "Outer Shadow");
                    materialEditor.ShaderProperty(Find(properties, "_ReceiveMainLight"), "Main Light Color");
                    materialEditor.ShaderProperty(Find(properties, "_ReceiveSH"), "Ambient SH");
                    materialEditor.ShaderProperty(Find(properties, "_AOIntensity"), "AO");
                    materialEditor.ShaderProperty(Find(properties, "_MatCapGlobalStrength"), "MatCap Strength");
                    materialEditor.ShaderProperty(Find(properties, "_RimColor"), "Rim Color");
                    materialEditor.ShaderProperty(Find(properties, "_DarkRimColor"), "Dark Rim Color");
                    materialEditor.ShaderProperty(Find(properties, "_RimWidth"), "Rim Width");
                    materialEditor.ShaderProperty(Find(properties, "_RimIntensity"), "Rim Intensity");
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawOutline(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            outlineExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(outlineExpanded, "Outline");
            if (outlineExpanded)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    materialEditor.ShaderProperty(Find(properties, "_UseSmoothNormal"), "Use Smooth Normal");
                    materialEditor.ShaderProperty(Find(properties, "_UseOutlineMask"), "Use Width Mask");
                    materialEditor.ShaderProperty(Find(properties, "_OutlineColor"), "Color");
                    materialEditor.ShaderProperty(Find(properties, "_OutlineTintMix"), "Base Tint Mix");
                    materialEditor.ShaderProperty(Find(properties, "_OutlineWidth"), "Width");
                    materialEditor.ShaderProperty(Find(properties, "_OutlineZOffset"), "Z Offset");
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawRendering(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            renderingExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(renderingExpanded, "Rendering");
            if (renderingExpanded)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    materialEditor.ShaderProperty(Find(properties, "_Cull"), "Cull");
                    materialEditor.ShaderProperty(Find(properties, "_AlphaClip"), "Alpha Clip");
                    materialEditor.ShaderProperty(Find(properties, "_Cutoff"), "Cutoff");
                    materialEditor.ShaderProperty(Find(properties, "_StencilRef"), "Stencil Ref");
                    materialEditor.ShaderProperty(Find(properties, "_DebugMode"), "Debug Mode");
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static MaterialProperty Find(MaterialProperty[] properties, string name)
        {
            return FindProperty(name, properties);
        }

        private static void SyncKeywords(Object[] targets, MaterialProperty[] properties)
        {
            foreach (Object target in targets)
            {
                if (!(target is Material material))
                {
                    continue;
                }

                for (int i = 0; i < ToggleProperties.Length; i++)
                {
                    MaterialProperty toggle = FindProperty(ToggleProperties[i], properties, false);
                    if (toggle != null && toggle.floatValue > 0.5f)
                    {
                        material.EnableKeyword(ToggleKeywords[i]);
                    }
                    else
                    {
                        material.DisableKeyword(ToggleKeywords[i]);
                    }
                }
            }
        }
    }
}
