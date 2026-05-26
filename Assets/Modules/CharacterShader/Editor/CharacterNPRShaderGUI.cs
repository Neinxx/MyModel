using CharacterShader.Runtime;
using UnityEditor;
using UnityEngine;

namespace CharacterShader.Editor
{
    /// <summary>
    /// A modern, clean, and elegant Shader GUI for Character NPR.
    /// Uses native collapsible foldout headers, contextual property display, and logical grouping.
    /// </summary>
    public sealed class CharacterNPRShaderGUI : ShaderGUI
    {
        // ── Labels ──
        private static readonly GUIContent BaseMapLabel = new GUIContent("Base Map", "Albedo (RGB) and Opacity (A)");
        private static readonly GUIContent NormalMapLabel = new GUIContent("Normal Map", "Tangent space normal map");
        private static readonly GUIContent Mask0Label = new GUIContent("Mask 0", "R: MaterialID, G: AO, B: Smoothness, A: Metallic");
        private static readonly GUIContent Mask1Label = new GUIContent("Mask 1", "R: RampBias, G: ShadowSoftness, B: MatCap, A: Rim/PostMask");
        private static readonly GUIContent RampArrayLabel = new GUIContent("Ramp Array", "Texture2DArray for light ramps");
        private static readonly GUIContent MatCapArrayLabel = new GUIContent("MatCap Array", "Texture2DArray for MatCaps");

        // ── Foldout States ──
        private bool _surfaceExpanded = true;
        private bool _masksExpanded = true;
        private bool _shadowExpanded = false;
        private bool _responseExpanded = false;
        private bool _rimExpanded = false;
        private bool _hairExpanded = false;
        private bool _pbrExpanded = false;
        private bool _silkExpanded = false;
        private bool _faceExpanded = false;
        private bool _advancedExpanded = false;
        private bool _outlineExpanded = false;
        private bool _isInitialized = false;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            Material material = materialEditor.target as Material;

            if (!_isInitialized)
            {
                CharacterMaterialProfile profile = CharacterShaderProfileBindingStore.instance.GetProfile(material);
                if (profile != null) profile.ApplyTo(material);
                _isInitialized = true;
            }

            EditorGUI.BeginChangeCheck();

            DrawProfileBinding(materialEditor, material);

            EditorGUILayout.Space(5);

            DrawSurface(materialEditor, properties);
            DrawMaskAndArrays(materialEditor, properties);
            DrawShadow(materialEditor, properties);
            DrawResponse(materialEditor, properties);
            DrawRimAndPost(materialEditor, properties);
            DrawHairSpecular(materialEditor, properties);
            DrawTunifiedPBR(materialEditor, properties);
            DrawSilkStocking(materialEditor, properties);
            DrawFaceSDF(materialEditor, properties);
            DrawOutline(materialEditor, properties);
            DrawRendering(materialEditor, properties);

            if (EditorGUI.EndChangeCheck())
            {
                CharacterMaterialProfile profile = CharacterShaderProfileBindingStore.instance.GetProfile(material);
                if (profile != null) profile.ApplyTo(material);
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // Profile Binding
        // ════════════════════════════════════════════════════════════════════════
        private void DrawProfileBinding(MaterialEditor materialEditor, Material material)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                CharacterMaterialProfile current = material != null
                    ? CharacterShaderProfileBindingStore.instance.GetProfile(material)
                    : null;

                EditorGUILayout.LabelField("Physical Material Profile", EditorStyles.miniBoldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    var next = (CharacterMaterialProfile)EditorGUILayout.ObjectField(current, typeof(CharacterMaterialProfile), false);
                    if (EditorGUI.EndChangeCheck() && material != null)
                    {
                        CharacterShaderProfileBindingStore.instance.SetProfile(material, next);
                    }

                    if (next != null)
                    {
                        if (GUILayout.Button("Sync", EditorStyles.miniButton, GUILayout.Width(45)))
                        {
                            ApplyProfile(materialEditor, next);
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("Create", EditorStyles.miniButton, GUILayout.Width(50)))
                        {
                            CreateProfile(material);
                        }
                    }
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // Surface & Albedo
        // ════════════════════════════════════════════════════════════════════════
        private void DrawSurface(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            _surfaceExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(_surfaceExpanded, "Surface & Albedo");
            if (_surfaceExpanded)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.Space(2);
                    MaterialProperty baseMap = Find(properties, "_BaseMap");
                    MaterialProperty baseColor = Find(properties, "_BaseColor");
                    MaterialProperty normalMap = Find(properties, "_BumpMap");
                    MaterialProperty normalScale = Find(properties, "_BumpScale");

                    materialEditor.TexturePropertySingleLine(BaseMapLabel, baseMap, baseColor);
                    
                    if (baseMap.textureValue != null)
                    {
                        materialEditor.TextureScaleOffsetProperty(baseMap);
                    }

                    EditorGUILayout.Space(2);
                    materialEditor.TexturePropertySingleLine(NormalMapLabel, normalMap, normalMap.textureValue != null ? normalScale : null);

                    EditorGUILayout.Space(2);
                    materialEditor.ShaderProperty(Find(properties, "_Metallic"), "Fallback Metallic");
                    materialEditor.ShaderProperty(Find(properties, "_Smoothness"), "Fallback Smoothness");
                    EditorGUILayout.Space(4);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ════════════════════════════════════════════════════════════════════════
        // Masks & Arrays
        // ════════════════════════════════════════════════════════════════════════
        private void DrawMaskAndArrays(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            _masksExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(_masksExpanded, "Masks & Arrays");
            if (_masksExpanded)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.Space(2);

                    // Mask Maps
                    MaterialProperty useMaskMaps = Find(properties, "_UseMaskMaps");
                    materialEditor.ShaderProperty(useMaskMaps, "Use Mask Maps");
                    if (useMaskMaps.floatValue > 0.5f)
                    {
                        EditorGUI.indentLevel++;
                        materialEditor.TexturePropertySingleLine(Mask0Label, Find(properties, "_Mask0"));
                        materialEditor.TexturePropertySingleLine(Mask1Label, Find(properties, "_Mask1"));
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.Space(2);

                    // Ramp Array
                    MaterialProperty useRampArray = Find(properties, "_UseRampArray");
                    materialEditor.ShaderProperty(useRampArray, "Use Ramp Array");
                    if (useRampArray.floatValue > 0.5f)
                    {
                        EditorGUI.indentLevel++;
                        materialEditor.TexturePropertySingleLine(RampArrayLabel, Find(properties, "_RampArray"));
                        
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(EditorGUI.indentLevel * 15);
                        if (GUILayout.Button("Create / Edit Ramp Config", EditorStyles.miniButton, GUILayout.Width(180)))
                        {
                            OpenOrCreateRampConfig((Material)materialEditor.target);
                        }
                        EditorGUILayout.EndHorizontal();
                        
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.Space(2);

                    // MatCap Array
                    MaterialProperty useMatCapArray = Find(properties, "_UseMatCapArray");
                    materialEditor.ShaderProperty(useMatCapArray, "Use MatCap Array");
                    if (useMatCapArray.floatValue > 0.5f)
                    {
                        EditorGUI.indentLevel++;
                        materialEditor.TexturePropertySingleLine(MatCapArrayLabel, Find(properties, "_MatCapArray"));
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.Space(4);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ════════════════════════════════════════════════════════════════════════
        // Shadow Response
        // ════════════════════════════════════════════════════════════════════════
        private void DrawShadow(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            _shadowExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(_shadowExpanded, "Shadow Response");
            if (_shadowExpanded)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.Space(2);
                    materialEditor.ShaderProperty(Find(properties, "_UnifiedShadowColor"), "Unified Shadow Color");
                    materialEditor.ShaderProperty(Find(properties, "_InnerShadowThreshold"), "Inner Threshold");
                    materialEditor.ShaderProperty(Find(properties, "_InnerShadowSoftness"), "Inner Softness");
                    materialEditor.ShaderProperty(Find(properties, "_RampBiasStrength"), "Ramp Bias Strength");
                    materialEditor.ShaderProperty(Find(properties, "_OuterShadowStrength"), "Outer Shadow Strength");
                    materialEditor.ShaderProperty(Find(properties, "_ShadowOverlapCancel"), "Overlap Cancel");
                    materialEditor.ShaderProperty(Find(properties, "_ReceiveMainLight"), "Main Light Color");
                    materialEditor.ShaderProperty(Find(properties, "_ReceiveSH"), "Receive Ambient SH");
                    EditorGUILayout.Space(4);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ════════════════════════════════════════════════════════════════════════
        // Material Response
        // ════════════════════════════════════════════════════════════════════════
        private void DrawResponse(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            _responseExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(_responseExpanded, "Material Response (AO & MatCap)");
            if (_responseExpanded)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.Space(2);
                    materialEditor.ShaderProperty(Find(properties, "_AOIntensity"), "AO Intensity");
                    materialEditor.ShaderProperty(Find(properties, "_MatCapGlobalStrength"), "MatCap Strength");
                    materialEditor.ShaderProperty(Find(properties, "_MetalMatCapBoost"), "Metal MatCap Boost");
                    materialEditor.ShaderProperty(Find(properties, "_MetalShadowLift"), "Metal Shadow Lift");
                    EditorGUILayout.Space(4);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ════════════════════════════════════════════════════════════════════════
        // Rim & Post Mask
        // ════════════════════════════════════════════════════════════════════════
        private void DrawRimAndPost(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            _rimExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(_rimExpanded, "Rim & Post Processing");
            if (_rimExpanded)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.Space(2);
                    materialEditor.ShaderProperty(Find(properties, "_RimColor"), "Light Rim Color (Front)");
                    materialEditor.ShaderProperty(Find(properties, "_DarkRimColor"), "Dark Rim Color (Contour/Back)");
                    materialEditor.ShaderProperty(Find(properties, "_RimWidth"), "Rim Width");
                    materialEditor.ShaderProperty(Find(properties, "_RimSoftness"), "Rim Softness");
                    materialEditor.ShaderProperty(Find(properties, "_RimIntensity"), "Rim Intensity");
                    materialEditor.ShaderProperty(Find(properties, "_RimLightAlign"), "Light Alignment (-1 Back, 1 Front)");
                    materialEditor.ShaderProperty(Find(properties, "_RimShadowMasking"), "Outer Shadow Masking (0=Keep, 1=Hide)");
                    materialEditor.ShaderProperty(Find(properties, "_PostProcessMaskStrength"), "Post Mask Output");
                    EditorGUILayout.Space(4);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ════════════════════════════════════════════════════════════════════════
        // Hair Anisotropic Specular
        // ════════════════════════════════════════════════════════════════════════
        private void DrawHairSpecular(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            _hairExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(_hairExpanded, "Hair Anisotropic Specular");
            if (_hairExpanded)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.Space(2);
                    MaterialProperty useAniso = Find(properties, "_UseAnisoHair");
                    materialEditor.ShaderProperty(useAniso, "Enable Hair Specular");
                    
                    if (useAniso.floatValue > 0.5f)
                    {
                        EditorGUI.indentLevel++;
                        materialEditor.TexturePropertySingleLine(new GUIContent("Hair Shift (R) Mask (G)"), Find(properties, "_HairAnisoMap"));
                        materialEditor.ShaderProperty(Find(properties, "_HairSpecColor"), "Specular Color");
                        materialEditor.ShaderProperty(Find(properties, "_HairSpecShift"), "Global Shift (Angel Ring Height)");
                        materialEditor.ShaderProperty(Find(properties, "_HairSpecSpread"), "Specular Spread");
                        materialEditor.ShaderProperty(Find(properties, "_HairSpecSoftness"), "Specular Softness");
                        materialEditor.ShaderProperty(Find(properties, "_HairSpecIntensity"), "Specular Intensity");
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.Space(4);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ════════════════════════════════════════════════════════════════════════
        // Tunified PBR
        // ════════════════════════════════════════════════════════════════════════
        private void DrawTunifiedPBR(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            _pbrExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(_pbrExpanded, "Tunified PBR (Metals & Leather)");
            if (_pbrExpanded)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.Space(2);
                    MaterialProperty usePbr = Find(properties, "_UseTunifiedPBR");
                    materialEditor.ShaderProperty(usePbr, "Enable Tunified PBR");
                    
                    if (usePbr.floatValue > 0.5f)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.HelpBox("PBR module is driven by Mask 0! Ensure your material ID mask has Alpha (Metallic) and Blue (Smoothness) painted for metallic/leather areas.", MessageType.Info);
                        materialEditor.ShaderProperty(Find(properties, "_PBRSpecularStrength"), "Specular Strength");
                        materialEditor.ShaderProperty(Find(properties, "_PBReflectionStrength"), "IBL Reflection Strength");
                        materialEditor.ShaderProperty(Find(properties, "_PBRStylizedThreshold"), "Stylized Threshold (0=Real, 1=Anime)");
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.Space(4);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ════════════════════════════════════════════════════════════════════════
        // Stockings & Silk
        // ════════════════════════════════════════════════════════════════════════
        private void DrawSilkStocking(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            _silkExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(_silkExpanded, "Stockings & Silk (Denier)");
            if (_silkExpanded)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.Space(2);
                    MaterialProperty useSilk = Find(properties, "_UseSilk");
                    materialEditor.ShaderProperty(useSilk, "Enable Stocking/Silk");
                    
                    if (useSilk.floatValue > 0.5f)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.HelpBox("Silk module requires Material ID 5 (Silk)! Ensure stockings are painted with ID 5 in Mask0.r (value ~0.71).", MessageType.Info);
                        materialEditor.ShaderProperty(Find(properties, "_SilkSkinColor"), "Underlying Skin Color");
                        materialEditor.ShaderProperty(Find(properties, "_SilkLightColor"), "Silk Center Color (Light)");
                        materialEditor.ShaderProperty(Find(properties, "_SilkDarkColor"), "Silk Edge Color (Dark)");
                        materialEditor.ShaderProperty(Find(properties, "_SilkTransparency"), "Transparency (Denier)");
                        materialEditor.ShaderProperty(Find(properties, "_SilkFresnelPower"), "Edge Thickness Power");
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.Space(4);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ════════════════════════════════════════════════════════════════════════
        // SDF Face Shadow
        // ════════════════════════════════════════════════════════════════════════
        private void DrawFaceSDF(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            _faceExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(_faceExpanded, "SDF Face Shadow");
            if (_faceExpanded)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.Space(2);
                    MaterialProperty useSDF = Find(properties, "_UseFaceSDF");
                    materialEditor.ShaderProperty(useSDF, "Enable SDF Face");
                    
                    if (useSDF.floatValue > 0.5f)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.HelpBox("Requires CharacterFaceSDFController.cs attached to the character's Head Bone to pass orientation vectors!", MessageType.Warning);
                        materialEditor.TexturePropertySingleLine(new GUIContent("Face SDF Map (R: Threshold)"), Find(properties, "_FaceSDFMap"));
                        materialEditor.ShaderProperty(Find(properties, "_FaceShadowOffset"), "Shadow Offset");
                        materialEditor.ShaderProperty(Find(properties, "_FaceShadowSoftness"), "Shadow Softness");
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.Space(4);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ════════════════════════════════════════════════════════════════════════
        // Stylized Outline
        // ════════════════════════════════════════════════════════════════════════
        private void DrawOutline(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            _outlineExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(_outlineExpanded, "Stylized Outline");
            if (_outlineExpanded)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.Space(2);
                    materialEditor.ShaderProperty(Find(properties, "_UseSmoothNormal"), "Use Vertex Color RGB as Smooth Normal");
                    materialEditor.ShaderProperty(Find(properties, "_UseOutlineMask"), "Use Vertex Color A as Width Mask");
                    
                    EditorGUILayout.Space(2);
                    materialEditor.ShaderProperty(Find(properties, "_OutlineColor"), "Outline Tint Color");
                    materialEditor.ShaderProperty(Find(properties, "_OutlineTintMix"), "BaseMap Tint Mix (0=Solid, 1=Tinted)");
                    
                    EditorGUILayout.Space(2);
                    materialEditor.ShaderProperty(Find(properties, "_OutlineWidth"), "Base Outline Width");
                    materialEditor.ShaderProperty(Find(properties, "_OutlineWidthScale"), "Global Distance Scale");
                    
                    EditorGUILayout.Space(2);
                    materialEditor.ShaderProperty(Find(properties, "_OutlineMinDist"), "Min Camera Distance (Clamp)");
                    materialEditor.ShaderProperty(Find(properties, "_OutlineMaxDist"), "Max Camera Distance (Clamp)");
                    
                    EditorGUILayout.Space(2);
                    materialEditor.ShaderProperty(Find(properties, "_OutlineZOffset"), "Outline Z-Offset (Anti-Acne)");
                    EditorGUILayout.Space(4);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ════════════════════════════════════════════════════════════════════════
        // Advanced Rendering
        // ════════════════════════════════════════════════════════════════════════
        private void DrawRendering(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            _advancedExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(_advancedExpanded, "Advanced Rendering");
            if (_advancedExpanded)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.Space(2);
                    materialEditor.ShaderProperty(Find(properties, "_Cull"), "Cull Mode");
                    
                    MaterialProperty alphaClip = Find(properties, "_AlphaClip");
                    materialEditor.ShaderProperty(alphaClip, "Alpha Clip");
                    if (alphaClip.floatValue > 0.5f)
                    {
                        EditorGUI.indentLevel++;
                        materialEditor.ShaderProperty(Find(properties, "_Cutoff"), "Alpha Cutoff");
                        EditorGUI.indentLevel--;
                    }

                    MaterialProperty stencilRef = Find(properties, "_StencilRef");
                    if (stencilRef != null)
                    {
                        materialEditor.ShaderProperty(stencilRef, "Stencil Ref (PostProcess Tag)");
                    }
                    
                    EditorGUILayout.Space(2);
                    materialEditor.EnableInstancingField();
                    materialEditor.RenderQueueField();
                    EditorGUILayout.Space(4);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ════════════════════════════════════════════════════════════════════════
        // Helper Methods
        // ════════════════════════════════════════════════════════════════════════
        private static MaterialProperty Find(MaterialProperty[] properties, string name)
        {
            return FindProperty(name, properties, false);
        }

        private static void ApplyProfile(MaterialEditor materialEditor, CharacterMaterialProfile profile)
        {
            foreach (Object target in materialEditor.targets)
            {
                if (target is not Material targetMaterial) continue;

                Undo.RecordObject(targetMaterial, "Apply Character Material Profile");
                profile.ApplyTo(targetMaterial);
                CharacterShaderProfileBindingStore.instance.SetProfile(targetMaterial, profile);
                EditorUtility.SetDirty(targetMaterial);
            }
            AssetDatabase.SaveAssets();
        }

        private static void CreateProfile(Material material)
        {
            string folder = "Assets";
            if (material != null)
            {
                string materialPath = AssetDatabase.GetAssetPath(material);
                if (!string.IsNullOrEmpty(materialPath))
                {
                    folder = System.IO.Path.GetDirectoryName(materialPath)?.Replace('\\', '/') ?? folder;
                }
            }

            string path = EditorUtility.SaveFilePanelInProject(
                "Create Character Material Profile",
                material != null ? $"{material.name}_Profile.asset" : "CharacterMaterialProfile.asset",
                "asset",
                "Choose where to save the profile asset.",
                folder);

            if (string.IsNullOrEmpty(path)) return;

            CharacterMaterialProfile profile = ScriptableObject.CreateInstance<CharacterMaterialProfile>();
            if (material != null)
            {
                profile.PullFrom(material);
            }

            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();

            if (material != null)
            {
                CharacterShaderProfileBindingStore.instance.SetProfile(material, profile);
            }

            Selection.activeObject = profile;
        }

        private void OpenOrCreateRampConfig(Material material)
        {
            if (material == null) return;
            string materialPath = AssetDatabase.GetAssetPath(material);
            if (string.IsNullOrEmpty(materialPath)) return;

            string directory = System.IO.Path.GetDirectoryName(materialPath);
            string configPath = $"{directory}/{material.name}_RampConfig.asset".Replace("\\", "/");

            RampArrayConfig config = AssetDatabase.LoadAssetAtPath<RampArrayConfig>(configPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<RampArrayConfig>();
                config.previewMaterial = material;
                AssetDatabase.CreateAsset(config, configPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"Created new RampArrayConfig at {configPath}");
            }
            else
            {
                if (config.previewMaterial != material)
                {
                    config.previewMaterial = material;
                    EditorUtility.SetDirty(config);
                    AssetDatabase.SaveAssets();
                }
            }

            RampArrayGeneratorWindow.ShowWindow(config);
        }
    }
}
