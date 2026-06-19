using System.IO;
using CharacterShader.Runtime;
using UnityEditor;
using UnityEngine;

namespace CharacterShader.Editor
{
    public class MatCapArrayGeneratorWindow : EditorWindow
    {
        private CharacterMaterialProfile _profile;
        private MatCapArrayConfig _config;
        private Texture2DArray _memoryArray;

        public static void ShowWindow(MatCapArrayConfig config)
        {
            ShowWindow(null, config);
        }

        public static void ShowWindow(CharacterMaterialProfile profile, MatCapArrayConfig config)
        {
            var window = GetWindow<MatCapArrayGeneratorWindow>("MatCap Array Generator");
            window._profile = profile;
            window._config = config;
            window.minSize = new Vector2(400, 550);
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
                DrawAuditBox(false, "Configuration Missing", "No MatCapArrayConfig is assigned. Please open this window from the Character NPR Material Inspector.");
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
            titleStyle.normal.textColor = new Color(0.9f, 0.4f, 0.7f); // Pinkish for MatCap
            GUILayout.Label("MATCAP ARRAY GENERATOR", titleStyle);
            
            var subtitleStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter
            };
            subtitleStyle.normal.textColor = Color.gray;
            GUILayout.Label("Real-time MatCap Texture Array Editor", subtitleStyle);
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
            _config.resolution = EditorGUILayout.IntSlider("Array Resolution", _config.resolution, 32, 2048);

            GUILayout.Space(15);
            DrawLine();
            GUILayout.Space(10);

            // --- SECTION: TEXTURES ---
            GUILayout.Label("MatCap Textures (Material ID 0-7)", sectionHeaderStyle);
            GUILayout.Space(5);

            string[] labels = GetSlotLabels();

            if (_config.matcaps == null || _config.matcaps.Length != 8)
            {
                _config.matcaps = new Texture2D[8];
            }

            for (int i = 0; i < 8; i++)
            {
                _config.matcaps[i] = (Texture2D)EditorGUILayout.ObjectField(labels[i], _config.matcaps[i], typeof(Texture2D), false);
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
            if (GUILayout.Button(" BAKE & SAVE MATCAP ARRAY ", buttonStyle))
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
            if (_config == null || _config.matcaps == null || _config.matcaps.Length != 8) return;

            int res = _config.resolution;
            if (_memoryArray == null || _memoryArray.width != res || _memoryArray.height != res)
            {
                if (_memoryArray != null) DestroyImmediate(_memoryArray);
                _memoryArray = new Texture2DArray(res, res, 8, TextureFormat.RGBA32, true, true);
                _memoryArray.wrapMode = TextureWrapMode.Clamp;
                _memoryArray.filterMode = FilterMode.Bilinear;
            }

            RenderTexture rt = RenderTexture.GetTemporary(res, res, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            
            for (int i = 0; i < 8; i++)
            {
                Texture2D src = _config.matcaps[i];
                if (src != null)
                {
                    Graphics.Blit(src, rt);
                }
                else
                {
                    RenderTexture.active = rt;
                    GL.Clear(true, true, Color.black);
                }

                Texture2D temp = new Texture2D(res, res, TextureFormat.RGBA32, false, true);
                RenderTexture.active = rt;
                temp.ReadPixels(new Rect(0, 0, res, res), 0, 0);
                temp.Apply();
                
                _memoryArray.SetPixels32(temp.GetPixels32(), i, 0);
                DestroyImmediate(temp);
            }
            
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            _memoryArray.Apply(true); // generate mipmaps
        }

        private void ApplyToPreviewMaterial()
        {
            if (_config != null && _config.previewMaterial != null && _memoryArray != null)
            {
                _config.previewMaterial.SetTexture("_MatCapArray", _memoryArray);
                _config.previewMaterial.SetFloat("_UseMatCapArray", 1.0f);
                _config.previewMaterial.EnableKeyword("_USEMATCAPARRAY_ON");
            }
        }

        private void BakeAndSaveAsset()
        {
            if (_config == null) return;

            GenerateMemoryArray();
            
            string configPath = AssetDatabase.GetAssetPath(_config);
            if (string.IsNullOrEmpty(configPath))
            {
                EditorUtility.DisplayDialog("Error", "Please save the MatCapArrayConfig asset inside the project first.", "OK");
                return;
            }

            string directory = Path.GetDirectoryName(configPath);
            string baseName = Path.GetFileNameWithoutExtension(configPath);
            if (baseName.EndsWith("_MatCapConfig"))
            {
                baseName = baseName.Substring(0, baseName.Length - "_MatCapConfig".Length);
            }
            
            string savePath = $"{directory}/{baseName}_MatCapArray.asset".Replace("\\", "/");

            Texture2DArray savedArray = AssetDatabase.LoadAssetAtPath<Texture2DArray>(savePath);
            if (savedArray != null)
            {
                EditorUtility.CopySerialized(_memoryArray, savedArray);
                savedArray.name = Path.GetFileNameWithoutExtension(savePath);
            }
            else
            {
                Texture2DArray clone = new Texture2DArray(_memoryArray.width, _memoryArray.height, _memoryArray.depth, _memoryArray.format, true, true);
                EditorUtility.CopySerialized(_memoryArray, clone);
                clone.name = Path.GetFileNameWithoutExtension(savePath);
                AssetDatabase.CreateAsset(clone, savePath);
                savedArray = clone;
            }

            AssetDatabase.SaveAssets();

            if (_config.previewMaterial != null)
            {
                Undo.RecordObject(_config.previewMaterial, "Apply Baked MatCap Array");
                _config.previewMaterial.SetTexture("_MatCapArray", savedArray);
                _config.previewMaterial.SetFloat("_UseMatCapArray", 1.0f);
                _config.previewMaterial.EnableKeyword("_USEMATCAPARRAY_ON");
                EditorUtility.SetDirty(_config.previewMaterial);
            }

            if (_profile != null)
            {
                Undo.RecordObject(_profile, "Apply Baked MatCap Array To Profile");
                _profile.SetMatCapArrayConfig(_config);
                _profile.SetMatCapArray(savedArray);
                if (_config.previewMaterial != null)
                {
                    _profile.ApplyTo(_config.previewMaterial);
                    EditorUtility.SetDirty(_config.previewMaterial);
                }
                EditorUtility.SetDirty(_profile);
                AssetDatabase.SaveAssets();
            }
            
            Debug.Log($"[MatCapArrayConfig] Successfully baked matcap array to: {savePath}");
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
