using CharacterController.Runtime;
using UnityEditor;
using UnityEngine;

namespace CharacterController.Editor
{
    public static class CharacterAnimatorTools
    {
        [MenuItem("CONTEXT/CharacterAnimator/Extract Clips From Animator Controller")]
        private static void ExtractClipsFromAnimator(MenuCommand command)
        {
            var characterAnimator = command.context as CharacterAnimator;
            if (characterAnimator == null)
                return;

            var animator = characterAnimator.GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                Debug.LogWarning("[CharacterAnimator] Animator or Animator Controller is missing.", characterAnimator);
                return;
            }

            var serializedObject = new SerializedObject(characterAnimator);
            int count = 0;
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip == null)
                    continue;

                string lowerName = clip.name.ToLowerInvariant();
                if (lowerName.Contains("idle"))
                    count += TryAssignClip(serializedObject, "idleClip", clip);
                else if (lowerName.Contains("walk"))
                    count += TryAssignClip(serializedObject, "walkClip", clip);
                else if (lowerName.Contains("slowrun") || lowerName.Contains("slow_run") || lowerName.Contains("jog"))
                    count += TryAssignClip(serializedObject, "slowRunClip", clip);
                else if (lowerName.Contains("run") || lowerName.Contains("move") || lowerName.Contains("sprint"))
                    count += TryAssignClip(serializedObject, "runClip", clip);
                else if (lowerName.Contains("jump"))
                    count += TryAssignClip(serializedObject, "jumpClip", clip);
            }

            if (count > 0)
            {
                serializedObject.ApplyModifiedProperties();
                Debug.Log($"[CharacterAnimator] Extracted {count} clips from Animator Controller.", characterAnimator);
            }
            else
            {
                Debug.Log("[CharacterAnimator] No matching clips found. Expected names containing idle, walk, run, or jump.", characterAnimator);
            }
        }

        private static int TryAssignClip(SerializedObject serializedObject, string propertyName, AnimationClip clip)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue != null)
                return 0;

            property.objectReferenceValue = clip;
            return 1;
        }
    }
}
