using UnityEngine;
using WorldSceneModule.Runtime;
using Object = UnityEngine.Object;

namespace Mainboard.Runtime
{
    internal static class WorldRuntimeResolver
    {
        public static WorldRuntimeHandle Resolve(MainboardContext context, WorldRuntimeConfig config)
        {
            if (config == null)
                return new WorldRuntimeHandle(null, null);

            var driver = config.sceneDriverOverride != null
                ? ResolveConfiguredDriver(context, config.sceneDriverOverride, out var owned)
                : ResolveAvailableDriver(context, config.createSceneDriverIfMissing, out owned);

            if (driver != null && config.registry != null)
                driver.Registry = config.registry;

            return new WorldRuntimeHandle(driver, owned);
        }

        private static WorldSceneDriver ResolveAvailableDriver(
            MainboardContext context,
            bool createIfMissing,
            out WorldSceneDriver owned
        )
        {
            owned = null;

            var local = context.Board.GetComponent<WorldSceneDriver>();
            if (local != null)
                return local;

            var sceneDriver = Object.FindFirstObjectByType<WorldSceneDriver>();
            if (sceneDriver != null)
                return sceneDriver;

            if (!createIfMissing)
                return null;

            owned = context.Board.gameObject.AddComponent<WorldSceneDriver>();
            return owned;
        }

        private static WorldSceneDriver ResolveConfiguredDriver(
            MainboardContext context,
            WorldSceneDriver configured,
            out WorldSceneDriver owned
        )
        {
            owned = null;

            if (configured.gameObject.scene.IsValid() && configured.gameObject.activeInHierarchy)
                return configured;

            if (!Application.isPlaying)
                return configured;

            var instance = Object.Instantiate(configured.gameObject);
            instance.name = configured.gameObject.name;
            if (instance.transform.parent != null)
                instance.transform.SetParent(null, true);
            if (!instance.activeSelf)
                instance.SetActive(true);

            owned = instance.GetComponent<WorldSceneDriver>();
            if (owned == null)
                owned = instance.AddComponent<WorldSceneDriver>();

            Debug.Log(
                $"[Mainboard] Instantiated runtime WorldSceneDriver from '{configured.name}'.",
                context.Board
            );
            return owned;
        }
    }
}
