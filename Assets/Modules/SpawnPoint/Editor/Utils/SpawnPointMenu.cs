using SpawnPoint.Runtime;
using UnityEditor;
using UnityEngine;

namespace SpawnPoint.Editor
{
    public static class SpawnPointMenu
    {
        [MenuItem("GameObject/Spawn Point/Spawn Point Hub", false, 10)]
        public static void CreateSpawnPointHub(MenuCommand menuCommand)
        {
            // Create a custom game object
            GameObject go = new GameObject("SpawnPointHub", typeof(SpawnPointHub));

            // Ensure it gets reparented if this was a context click (otherwise it goes to root)
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);

            // Register the creation in the undo system
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);

            Selection.activeObject = go;
        }
    }
}
