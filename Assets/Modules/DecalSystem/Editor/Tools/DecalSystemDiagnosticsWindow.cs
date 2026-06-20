using DecalMini;
using UnityEditor;
using UnityEngine;

namespace DecalMini.Editor
{
    public sealed class DecalSystemDiagnosticsWindow : EditorWindow
    {
        [MenuItem("Tools/Decal System/Diagnostics", false, 901)]
        public static void Open()
        {
            var window = GetWindow<DecalSystemDiagnosticsWindow>("Decal Diagnostics");
            window.minSize = new Vector2(360f, 260f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Decal System Diagnostics", EditorStyles.boldLabel);
            EditorGUILayout.Space(6f);

            DrawReadonly("Config", DecalSystemMini.CurrentConfig != null ? DecalSystemMini.CurrentConfig.name : "(none)");
            DrawReadonly("Projectors", DecalSystemMini.Count.ToString());
            DrawReadonly("Total Decals", DecalSystemMini.TotalCount.ToString());
            DrawReadonly("Runtime Cells", DecalSystemMini.ActiveRuntimeCells.ToString());
            DrawReadonly("Static Cells", DecalSystemMini.ActiveStaticCells.ToString());
            DrawReadonly("Loaded Sources", DecalSystemMini.LoadedSourceCount.ToString());
            DrawReadonly("Last Visible", DecalSystemMini.LastVisibleCount.ToString());
            DrawReadonly("Last Candidates", DecalSystemMini.LastCandidateCount.ToString());
            DrawReadonly("Static Visible", DecalSystemMini.LastStaticVisibleCount.ToString());
            DrawReadonly("Projector Visible", DecalSystemMini.LastProjectorVisibleCount.ToString());
            DrawReadonly("Runtime Visible", DecalSystemMini.LastRuntimeVisibleCount.ToString());
            DrawReadonly("Runtime Pool", DecalSystemMini.RuntimePoolActiveCount + " / " + DecalSystemMini.RuntimePoolCapacity);
            DrawReadonly("Sort Buffer", DecalSystemMini.SortBufferCapacity.ToString());
            DrawReadonly("Verbose Logs", DecalSystemLog.VerboseEnabled ? "On" : "Off");

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Clear Runtime Pool"))
                    DecalSystemMini.ClearRuntimePool();

                if (GUILayout.Button("Release All State"))
                {
                    if (EditorUtility.DisplayDialog("Decal Diagnostics", "Release all decal runtime state? This clears registered projectors, static sources, and runtime decals until systems register again.", "Release", "Cancel"))
                        DecalSystemMini.ReleaseAll();
                }
            }
        }

        private static void DrawReadonly(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(120f));
                EditorGUILayout.SelectableLabel(value, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }
    }
}
