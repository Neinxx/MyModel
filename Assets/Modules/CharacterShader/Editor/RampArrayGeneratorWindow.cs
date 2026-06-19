using System.IO;
using CharacterShader.Runtime;
using UnityEditor;
using UnityEngine;

namespace CharacterShader.Editor
{
    public class RampArrayGeneratorWindow : EditorWindow
    {
        private CharacterMaterialProfile _profile;
        private RampArrayConfig _config;
        private Texture2DArray _memoryArray;

        public static void ShowWindow(RampArrayConfig config)
        {
            ShowWindow(null, config);
        }

        public static void ShowWindow(CharacterMaterialProfile profile, RampArrayConfig config)
        {
            var window = GetWindow<RampArrayGeneratorWindow>("Ramp Array Generator");
            window._profile = profile;
            window._config = config;
            window.minSize = new Vector2(400, 500);
            window.Show();
            window.GenerateMemoryArray();
            window.ApplyToPreviewMaterial();
        }

        private void OnEnable()
        {
            if (_config != null)
            {
                GenerateMemoryArray();
            }
        }

        private void OnDisable()
        {
            if (_memoryArray != null)
            {
                DestroyImmediate(_memoryArray);
            }
        }

        private void OnGUI()
        {
            if (_config == null)
            {
                DrawAuditBox(false, "Configuration Missing", "No RampArrayConfig is assigned. Please open this window from the Character NPR Material Inspector.");
                return;
            }

            EditorGUI.BeginChangeCheck();

            GUILayout.BeginHorizontal();
            GUILayout.Space(15); // Left margin
            GUILayout.BeginVertical();

            // --- HEADER ---
            GUILayout.Space(15);
            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 22,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = new Color(0.24f, 0.72f, 1.00f);
            GUILayout.Label("RAMP ARRAY GENERATOR", titleStyle);
            
            var subtitleStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter
            };
            subtitleStyle.normal.textColor = Color.gray;
            GUILayout.Label("Real-time Light Threshold & Shadow Terminator Editor", subtitleStyle);
            GUILayout.Space(15);
            DrawLine();

            // --- SECTION: TARGET CONFIG ---
            GUILayout.Space(10);
            var sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                margin = new RectOffset(0, 0, 10, 5)
            };
            GUILayout.Label("Target & Resolution", sectionHeaderStyle);

            _config.previewMaterial = (Material)EditorGUILayout.ObjectField("Preview Material", _config.previewMaterial, typeof(Material), false);
            _config.resolution = EditorGUILayout.IntSlider("Ramp Resolution", _config.resolution, 32, 1024);

            GUILayout.Space(15);
            DrawLine();
            GUILayout.Space(10);

            // --- SECTION: GRADIENTS ---
            GUILayout.Label("Ramp Gradients (Material ID 0-7)", sectionHeaderStyle);
            GUILayout.Space(5);

            string[] labels = GetSlotLabels();

            if (_config.ramps == null || _config.ramps.Length != 8)
            {
                _config.ramps = new Gradient[8];
                for (int i = 0; i < 8; i++) _config.ramps[i] = new Gradient();
            }

            for (int i = 0; i < 8; i++)
            {
                _config.ramps[i] = EditorGUILayout.GradientField(labels[i], _config.ramps[i]);
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_config);
                GenerateMemoryArray();
                ApplyToPreviewMaterial();
            }

            GUILayout.FlexibleSpace();
            DrawLine();
            GUILayout.Space(5);

            // --- CTA BUTTON ---
            var buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                fixedHeight = 40
            };
            
            GUI.backgroundColor = new Color(0.25f, 0.72f, 0.31f); // Emerald Green
            if (GUILayout.Button(" BAKE & SAVE RAMP ARRAY ", buttonStyle))
            {
                BakeAndSaveAsset();
            }
            GUI.backgroundColor = Color.white; // Reset background
            
            GUILayout.Space(15);

            GUILayout.EndVertical();
            GUILayout.Space(15); // Right margin
            GUILayout.EndHorizontal();
        }

        private void DrawLine()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            rect.height = 1;
            EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f, 0.5f));
            GUILayout.Space(5);
        }

        private void DrawAuditBox(bool passed, string title, string message)
        {
            Color bgColor = passed ? new Color(0.18f, 0.39f, 0.22f) : new Color(0.53f, 0.21f, 0.21f);
            Color textColor = passed ? new Color(0.60f, 1.00f, 0.67f) : new Color(1.00f, 0.67f, 0.67f);

            var boxStyle = new GUIStyle(GUI.skin.box)
            {
                margin = new RectOffset(5, 5, 5, 5),
                padding = new RectOffset(10, 10, 8, 8)
            };

            GUI.backgroundColor = bgColor;
            GUILayout.BeginVertical(boxStyle);
            GUI.backgroundColor = Color.white;

            var headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.normal.textColor = textColor;
            GUILayout.Label($"{(passed ? "✔" : "✘")}  {title}", headerStyle);

            var descStyle = new GUIStyle(EditorStyles.wordWrappedLabel) { fontSize = 11 };
            descStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
            GUILayout.Label(message, descStyle);

            GUILayout.EndVertical();
        }

        private void GenerateMemoryArray()
        {
            if (_config == null || _config.ramps == null || _config.ramps.Length != 8) return;

            int res = _config.resolution;
            if (_memoryArray == null || _memoryArray.width != res || _memoryArray.height != 2)
            {
                if (_memoryArray != null) DestroyImmediate(_memoryArray);
                _memoryArray = new Texture2DArray(res, 2, 8, TextureFormat.RGBA32, false, true);
                _memoryArray.wrapMode = TextureWrapMode.Clamp;
                _memoryArray.filterMode = FilterMode.Bilinear;
            }

            for (int i = 0; i < 8; i++)
            {
                Color32[] pixels = new Color32[res * 2];
                Gradient gradient = _config.ramps[i] ?? new Gradient();
                
                for (int x = 0; x < res; x++)
                {
                    float t = (float)x / (res - 1);
                    Color32 c32 = gradient.Evaluate(t); 
                    pixels[x] = c32;
                    pixels[x + res] = c32;
                }
                
                _memoryArray.SetPixels32(pixels, i, 0);
            }
            
            _memoryArray.Apply(false);
        }

        private void ApplyToPreviewMaterial()
        {
            if (_config != null && _config.previewMaterial != null && _memoryArray != null)
            {
                _config.previewMaterial.SetTexture("_RampArray", _memoryArray);
                _config.previewMaterial.SetFloat("_UseRampArray", 1.0f);
                _config.previewMaterial.EnableKeyword("_USERAMPARRAY_ON");
            }
        }

        private void BakeAndSaveAsset()
        {
            if (_config == null) return;

            GenerateMemoryArray();
            
            string configPath = AssetDatabase.GetAssetPath(_config);
            if (string.IsNullOrEmpty(configPath))
            {
                EditorUtility.DisplayDialog("Error", "Please save the RampArrayConfig asset inside the project first.", "OK");
                return;
            }

            string directory = Path.GetDirectoryName(configPath);
            string baseName = Path.GetFileNameWithoutExtension(configPath);
            if (baseName.EndsWith("_RampConfig"))
            {
                baseName = baseName.Substring(0, baseName.Length - "_RampConfig".Length);
            }
            
            string savePath = $"{directory}/{baseName}_RampArray.asset".Replace("\\", "/");

            Texture2DArray savedArray = AssetDatabase.LoadAssetAtPath<Texture2DArray>(savePath);
            if (savedArray != null)
            {
                EditorUtility.CopySerialized(_memoryArray, savedArray);
                savedArray.name = Path.GetFileNameWithoutExtension(savePath);
            }
            else
            {
                Texture2DArray clone = new Texture2DArray(_memoryArray.width, _memoryArray.height, _memoryArray.depth, _memoryArray.format, false, true);
                EditorUtility.CopySerialized(_memoryArray, clone);
                clone.name = Path.GetFileNameWithoutExtension(savePath);
                AssetDatabase.CreateAsset(clone, savePath);
                savedArray = clone;
            }

            AssetDatabase.SaveAssets();

            if (_config.previewMaterial != null)
            {
                Undo.RecordObject(_config.previewMaterial, "Apply Baked Ramp Array");
                _config.previewMaterial.SetTexture("_RampArray", savedArray);
                _config.previewMaterial.SetFloat("_UseRampArray", 1.0f);
                _config.previewMaterial.EnableKeyword("_USERAMPARRAY_ON");
                EditorUtility.SetDirty(_config.previewMaterial);
            }

            if (_profile != null)
            {
                Undo.RecordObject(_profile, "Apply Baked Ramp Array To Profile");
                _profile.SetRampArrayConfig(_config);
                _profile.SetRampArray(savedArray);
                if (_config.previewMaterial != null)
                {
                    _profile.ApplyTo(_config.previewMaterial);
                    EditorUtility.SetDirty(_config.previewMaterial);
                }
                EditorUtility.SetDirty(_profile);
                AssetDatabase.SaveAssets();
            }
            
            Debug.Log($"[RampArrayConfig] Successfully baked ramp array to: {savePath}");
            EditorGUIUtility.PingObject(savedArray);
        }

        private string[] GetSlotLabels()
        {
            if (_profile != null)
            {
                return _profile.GetSlotDisplayNames();
            }

            string[] labels = new string[CharacterMaterialProfile.SlotCount];
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i] = CharacterMaterialProfile.GetDefaultSlotDisplayName(i);
            }

            return labels;
        }
    }
}
