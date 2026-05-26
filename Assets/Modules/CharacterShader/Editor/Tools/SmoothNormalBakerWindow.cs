using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CharacterShader.Editor.Tools
{
    /// <summary>
    /// A professional-grade tool to bake Area-Weighted Smooth Normals into Vertex Color RGB.
    /// Preserves existing Alpha channels for outline width masking.
    /// Supports high-poly meshes, SkinnedMeshRenderers, and direct Prefab Asset modifications.
    /// </summary>
    public class SmoothNormalBakerWindow : EditorWindow
    {
        private GameObject _targetObject;
        private bool _autoReplace = true;
        private string _customOutputFolder = "";

        [MenuItem("Window/Character Shader/Smooth Normal Baker")]
        public static void ShowWindow()
        {
            var window = GetWindow<SmoothNormalBakerWindow>("Smooth Normal Baker");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("AAA Smooth Normal Baker", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Bakes area-weighted smooth normals into Vertex Color RGB.\n" +
                "Preserves Vertex Color Alpha (used for Outline Masking).", MessageType.Info);

            EditorGUILayout.Space(10);
            
            _targetObject = (GameObject)EditorGUILayout.ObjectField("Target GameObject", _targetObject, typeof(GameObject), true);
            
            EditorGUILayout.Space(5);
            _autoReplace = EditorGUILayout.Toggle(new GUIContent("Auto Replace References", "Automatically swap the MeshFilter/SkinnedMeshRenderer meshes with the baked ones."), _autoReplace);
            
            EditorGUILayout.Space(5);
            _customOutputFolder = EditorGUILayout.TextField(new GUIContent("Custom Output Folder", "Leave empty to save next to the original mesh."), _customOutputFolder);

            EditorGUILayout.Space(15);

            EditorGUI.BeginDisabledGroup(_targetObject == null);
            if (GUILayout.Button("Bake Smooth Normals", GUILayout.Height(40)))
            {
                ExecuteBake();
            }
            EditorGUI.EndDisabledGroup();
        }

        private void ExecuteBake()
        {
            if (_targetObject == null) return;

            string assetPath = AssetDatabase.GetAssetPath(_targetObject);
            bool isPrefabAsset = !string.IsNullOrEmpty(assetPath) && PrefabUtility.IsPartOfPrefabAsset(_targetObject);

            Dictionary<Mesh, Mesh> processedMeshes = new Dictionary<Mesh, Mesh>();

            try
            {
                if (isPrefabAsset && _autoReplace)
                {
                    // Modify the prefab asset directly
                    using (var scope = new PrefabUtility.EditPrefabContentsScope(assetPath))
                    {
                        ProcessGameObjectTree(scope.prefabContentsRoot, processedMeshes);
                    }
                    Debug.Log($"<color=#3FB950><b>[SmoothNormalBaker]</b></color> Successfully baked and updated Prefab Asset: {assetPath}");
                }
                else
                {
                    // Modify scene instance or just bake without replacing
                    ProcessGameObjectTree(_targetObject, processedMeshes);
                    Debug.Log($"<color=#3FB950><b>[SmoothNormalBaker]</b></color> Successfully baked meshes for GameObject: {_targetObject.name}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"<color=#ff4d4d><b>[SmoothNormalBaker]</b></color> Bake failed: {e.Message}\n{e.StackTrace}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        private void ProcessGameObjectTree(GameObject root, Dictionary<Mesh, Mesh> processedMeshes)
        {
            var meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
            var skinnedRenderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            int totalTasks = meshFilters.Length + skinnedRenderers.Length;
            int currentTask = 0;

            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh == null) continue;
                
                EditorUtility.DisplayProgressBar("Baking Smooth Normals", $"Processing {mf.name}...", (float)currentTask / totalTasks);
                
                Mesh newMesh = GetOrBakeMesh(mf.sharedMesh, processedMeshes);
                if (_autoReplace && newMesh != null)
                {
                    Undo.RecordObject(mf, "Auto Replace Baked Mesh");
                    mf.sharedMesh = newMesh;
                    EditorUtility.SetDirty(mf);
                }
                currentTask++;
            }

            foreach (var smr in skinnedRenderers)
            {
                if (smr.sharedMesh == null) continue;

                EditorUtility.DisplayProgressBar("Baking Smooth Normals", $"Processing {smr.name}...", (float)currentTask / totalTasks);
                
                Mesh newMesh = GetOrBakeMesh(smr.sharedMesh, processedMeshes);
                if (_autoReplace && newMesh != null)
                {
                    Undo.RecordObject(smr, "Auto Replace Baked Mesh");
                    smr.sharedMesh = newMesh;
                    EditorUtility.SetDirty(smr);
                }
                currentTask++;
            }
        }

        private Mesh GetOrBakeMesh(Mesh originalMesh, Dictionary<Mesh, Mesh> processedMeshes)
        {
            if (processedMeshes.TryGetValue(originalMesh, out Mesh cachedMesh))
            {
                return cachedMesh;
            }

            Mesh bakedMesh = BakeSmoothNormalMesh(originalMesh);
            if (bakedMesh == null) return null;

            string originalPath = AssetDatabase.GetAssetPath(originalMesh);
            string dir = string.IsNullOrEmpty(_customOutputFolder) 
                ? (string.IsNullOrEmpty(originalPath) ? "Assets" : Path.GetDirectoryName(originalPath))
                : _customOutputFolder;
            
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string safeName = string.IsNullOrEmpty(originalMesh.name) ? "BakedMesh" : originalMesh.name;
            string newPath = Path.Combine(dir, $"{safeName}_Smoothed.asset").Replace("\\", "/");
            newPath = AssetDatabase.GenerateUniqueAssetPath(newPath);

            AssetDatabase.CreateAsset(bakedMesh, newPath);
            processedMeshes.Add(originalMesh, bakedMesh);

            return bakedMesh;
        }

        private Mesh BakeSmoothNormalMesh(Mesh originalMesh)
        {
            Mesh newMesh = Instantiate(originalMesh);
            newMesh.name = originalMesh.name + "_Smoothed";

            Vector3[] vertices = newMesh.vertices;
            Vector3[] normals = newMesh.normals;
            int[] triangles = newMesh.triangles;
            Color[] colors = newMesh.colors;

            if (colors == null || colors.Length != vertices.Length)
            {
                colors = new Color[vertices.Length];
                for (int i = 0; i < colors.Length; i++)
                    colors[i] = Color.white; // Default alpha 1.0
            }

            // High precision spatial hashing dictionary
            Dictionary<Vector3Int, Vector3> normalHash = new Dictionary<Vector3Int, Vector3>();
            float scaleMultiplier = 10000f; // 0.1mm precision

            // 1. Accumulate Area-Weighted Normals
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int i0 = triangles[i];
                int i1 = triangles[i + 1];
                int i2 = triangles[i + 2];

                Vector3 v0 = vertices[i0];
                Vector3 v1 = vertices[i1];
                Vector3 v2 = vertices[i2];

                // Face normal magnitude is proportional to triangle area (Cross Product)
                Vector3 edge1 = v1 - v0;
                Vector3 edge2 = v2 - v0;
                Vector3 faceNormal = Vector3.Cross(edge1, edge2);

                Vector3Int key0 = Vector3ToInt(v0, scaleMultiplier);
                Vector3Int key1 = Vector3ToInt(v1, scaleMultiplier);
                Vector3Int key2 = Vector3ToInt(v2, scaleMultiplier);

                AddToHash(normalHash, key0, faceNormal);
                AddToHash(normalHash, key1, faceNormal);
                AddToHash(normalHash, key2, faceNormal);
            }

            // 2. Normalize and Apply to Vertex Colors
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3Int key = Vector3ToInt(vertices[i], scaleMultiplier);
                
                if (normalHash.TryGetValue(key, out Vector3 accumulatedNormal))
                {
                    // Fallback to original normal if face normal accumulation failed (e.g., degenerate triangles)
                    if (accumulatedNormal.sqrMagnitude < 0.0001f)
                    {
                        accumulatedNormal = normals[i];
                    }
                    
                    Vector3 smoothNormal = accumulatedNormal.normalized;
                    
                    // Remap from [-1, 1] to [0, 1]
                    float r = (smoothNormal.x * 0.5f) + 0.5f;
                    float g = (smoothNormal.y * 0.5f) + 0.5f;
                    float b = (smoothNormal.z * 0.5f) + 0.5f;

                    // Preserve original Alpha!
                    float a = colors[i].a;

                    colors[i] = new Color(r, g, b, a);
                }
            }

            newMesh.colors = colors;
            return newMesh;
        }

        private Vector3Int Vector3ToInt(Vector3 v, float multiplier)
        {
            return new Vector3Int(
                Mathf.RoundToInt(v.x * multiplier),
                Mathf.RoundToInt(v.y * multiplier),
                Mathf.RoundToInt(v.z * multiplier)
            );
        }

        private void AddToHash(Dictionary<Vector3Int, Vector3> dict, Vector3Int key, Vector3 value)
        {
            if (dict.TryGetValue(key, out Vector3 current))
            {
                dict[key] = current + value;
            }
            else
            {
                dict.Add(key, value);
            }
        }
    }
}
