using InteractionSystem.Runtime;
using UnityEditor;
using UnityEngine;

namespace InteractionSystem.Editor.Inspectors
{
    [CustomEditor(typeof(ProximityInteractor))]
    [CanEditMultipleObjects]
    public sealed class ProximityInteractorEditor : UnityEditor.Editor
    {
        private SerializedProperty _radiusProperty;
        private SerializedProperty _detectionOffsetProperty;
        private SerializedProperty _targetLayersProperty;
        private SerializedProperty _scanFrequencyProperty;
        private SerializedProperty _hitCapacityProperty;
        private SerializedProperty _autoTriggerProperty;
        private SerializedProperty _warnWhenHitBufferIsFullProperty;

        private void OnEnable()
        {
            _radiusProperty = serializedObject.FindProperty("_radius");
            _detectionOffsetProperty = serializedObject.FindProperty("_detectionOffset");
            _targetLayersProperty = serializedObject.FindProperty("_targetLayers");
            _scanFrequencyProperty = serializedObject.FindProperty("_scanFrequency");
            _hitCapacityProperty = serializedObject.FindProperty("_hitCapacity");
            _autoTriggerProperty = serializedObject.FindProperty("_autoTrigger");
            _warnWhenHitBufferIsFullProperty = serializedObject.FindProperty("_warnWhenHitBufferIsFull");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawStatus();
            EditorGUILayout.Space(6f);
            DrawDetectionFields();
            EditorGUILayout.Space(4f);
            DrawRuntimeFields();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawStatus()
        {
            if (targets.Length != 1)
            {
                EditorGUILayout.HelpBox("Select one interactor to inspect runtime target state.", MessageType.Info);
                return;
            }

            var interactor = target as ProximityInteractor;
            if (interactor == null)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Current Target", DescribeTarget(interactor.CurrentInteractable));
                EditorGUILayout.LabelField("Has Target", interactor.HasTarget ? "Yes" : "No");
                EditorGUILayout.LabelField("Hit Buffer", interactor.IsHitBufferFull ? "Full" : "Ready");

                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Scan"))
                        {
                            interactor.Scan();
                        }

                        if (GUILayout.Button("Interact Current"))
                        {
                            interactor.InteractCurrent();
                        }
                    }
                }
            }
        }

        private void DrawDetectionFields()
        {
            EditorGUILayout.LabelField("Detection", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_radiusProperty);
            EditorGUILayout.PropertyField(_detectionOffsetProperty);
            EditorGUILayout.PropertyField(_targetLayersProperty);
            EditorGUILayout.PropertyField(_scanFrequencyProperty);
            EditorGUILayout.PropertyField(_hitCapacityProperty);
        }

        private void DrawRuntimeFields()
        {
            EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_autoTriggerProperty);
            EditorGUILayout.PropertyField(_warnWhenHitBufferIsFullProperty);
        }

        private static string DescribeTarget(IInteractable target)
        {
            if (target == null)
            {
                return "<none>";
            }

            if (target is Component component)
            {
                return component.gameObject.name;
            }

            return target.GetType().Name;
        }
    }
}
