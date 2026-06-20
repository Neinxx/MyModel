using UnityEngine;
using WorldSceneModule.Runtime;

namespace Mainboard.Runtime.Integrations
{
    public readonly struct PlayerSpawnedSignal
    {
        public PlayerSpawnedSignal(GameObject player, LevelConfig level)
        {
            Player = player;
            Level = level;
            Scope = null;
        }

        public PlayerSpawnedSignal(GameObject player, LevelConfig level, LevelScope scope)
        {
            Player = player;
            Level = level;
            Scope = scope;
        }

        public GameObject Player { get; }
        public LevelConfig Level { get; }
        public LevelScope Scope { get; }
    }

    public readonly struct CameraRigReadySignal
    {
        public CameraRigReadySignal(Camera mainCamera, Camera uiCamera)
        {
            MainCamera = mainCamera;
            UICamera = uiCamera;
        }

        public Camera MainCamera { get; }
        public Camera UICamera { get; }
    }

    public readonly struct UIRootReadySignal
    {
        public UIRootReadySignal(GameObject root)
        {
            Root = root;
        }

        public GameObject Root { get; }
    }

    public readonly struct DecalLevelDataLoadedSignal
    {
        public DecalLevelDataLoadedSignal(LevelConfig level)
        {
            Level = level;
        }

        public LevelConfig Level { get; }
    }

}
