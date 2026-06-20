using System.Collections.Generic;
using System.Threading;
using CharacterController.Runtime;
using Cysharp.Threading.Tasks;
using Mainboard.Runtime;
using PlayerState.Runtime;
using SpawnPoint.Runtime;
using UnityEngine;
using WorldSceneModule.Runtime;
using Object = UnityEngine.Object;

namespace Mainboard.Runtime.Integrations
{
    public interface IPlayerRuntime
    {
        GameObject GameObject { get; }
        Transform Transform { get; }
    }

    public interface ISpawnPointService
    {
        bool TryGetByID(string id, out ISpawnPoint spawnPoint);
        bool TryGetFirst(out ISpawnPoint spawnPoint);
    }

    [CreateAssetMenu(fileName = "PlayerFeature", menuName = "Demo/Mainboard/Features/Player")]
    public sealed class PlayerLevelInstaller : MainboardInstaller
    {
        [Header("Player")]
        [SerializeField] private PlayerStateSO playerState;
        [SerializeField] private string playerPrefabKey =
            "Assets/Demo/Art/Prefabs/Character_logic.prefab";
        [SerializeField] private string visualPrefabKey =
            "Assets/Demo/Art/Prefabs/Anbu_art.prefab";
        [SerializeField] private string defaultSpawnPointID = "Start";
        [SerializeField] private bool ensureCharacterMotor = true;
        [SerializeField] private bool warnIfCharacterBrainMissing = true;

        public override IGameFeature CreateFeature()
        {
            return new PlayerFeature(
                playerState,
                playerPrefabKey,
                visualPrefabKey,
                defaultSpawnPointID,
                ensureCharacterMotor,
                warnIfCharacterBrainMissing
            );
        }

        private sealed class PlayerFeature :
            IGameFeature,
            IFeatureInstaller,
            ILevelLoadHandler,
            ILevelUnloadHandler,
            IFeatureShutdown
        {
            private readonly PlayerStateSO _playerState;
            private readonly string _playerPrefabKey;
            private readonly string _visualPrefabKey;
            private readonly string _defaultSpawnPointID;
            private readonly bool _ensureCharacterMotor;
            private readonly bool _warnIfCharacterBrainMissing;
            private MainboardContext _context;
            private AssetHandle<GameObject> _playerHandle;
            private AssetHandle<GameObject> _visualHandle;
            private Transform _spawnedPlayerTransform;
            private GameObject _spawnedVisual;
            private IPlayerRuntime _playerRuntime;

            public PlayerFeature(
                PlayerStateSO playerState,
                string playerPrefabKey,
                string visualPrefabKey,
                string defaultSpawnPointID,
                bool ensureCharacterMotor,
                bool warnIfCharacterBrainMissing
            )
            {
                _playerState = playerState;
                _playerPrefabKey = playerPrefabKey;
                _visualPrefabKey = visualPrefabKey;
                _defaultSpawnPointID = defaultSpawnPointID;
                _ensureCharacterMotor = ensureCharacterMotor;
                _warnIfCharacterBrainMissing = warnIfCharacterBrainMissing;
            }

            public string Name => "Player";

            public UniTask InstallAsync(MainboardContext context, CancellationToken cancellationToken)
            {
                _context = context;
                return UniTask.CompletedTask;
            }

            public async UniTask OnLevelLoadedAsync(
                LevelContext level,
                CancellationToken cancellationToken
            )
            {
                if (string.IsNullOrWhiteSpace(_playerPrefabKey))
                {
                    Debug.LogError("[Mainboard.Player] Player prefab key is empty.");
                    return;
                }

                var assetService = AssetServiceBootstrap.Ensure(_context);
                _playerHandle = await assetService.LoadAsync<GameObject>(
                    _playerPrefabKey,
                    cancellationToken
                );
                cancellationToken.ThrowIfCancellationRequested();

                var spawnPointService = SceneSpawnPointService.Create(level);
                level.Scope?.RegisterService<ISpawnPointService>(spawnPointService);

                var spawnPose = ResolveSpawnPose(spawnPointService);
                var player = Object.Instantiate(
                    _playerHandle.Asset,
                    spawnPose.position,
                    spawnPose.rotation
                );
                _spawnedPlayerTransform = player.transform;

                if (_ensureCharacterMotor && !player.TryGetComponent<CharacterMotor>(out _))
                    player.AddComponent<CharacterMotor>();

                await AttachVisualAsync(player, cancellationToken);
                BindPlayerStateReceivers(player);
                WarnIfCharacterBrainMissing(player);

                _playerRuntime = new PlayerRuntime(player);
                level.Scope?.RegisterService<IPlayerRuntime>(_playerRuntime);
                level.TryGetConfig<LevelConfig>(out var levelConfig);
                _context.Signals.Publish(new PlayerSpawnedSignal(player, levelConfig, level.Scope));
            }

            public UniTask OnLevelUnloadingAsync(LevelContext level, CancellationToken cancellationToken)
            {
                ReleasePlayer();
                return UniTask.CompletedTask;
            }

            public UniTask ShutdownAsync(MainboardContext context, CancellationToken cancellationToken)
            {
                ReleasePlayer();
                return UniTask.CompletedTask;
            }

            private (Vector3 position, Quaternion rotation) ResolveSpawnPose(
                ISpawnPointService spawnPointService
            )
            {
                var spawnID = _defaultSpawnPointID;
                if (_playerState != null && !string.IsNullOrEmpty(_playerState.lastSpawnPointID))
                    spawnID = _playerState.lastSpawnPointID;

                ISpawnPoint spawnPoint = null;
                if (
                    spawnPointService == null
                    || !spawnPointService.TryGetByID(spawnID, out spawnPoint)
                )
                    spawnPointService?.TryGetFirst(out spawnPoint);

                return spawnPoint != null
                    ? (spawnPoint.Position, spawnPoint.Rotation)
                    : (Vector3.zero, Quaternion.identity);
            }

            private void BindPlayerStateReceivers(GameObject player)
            {
                if (_playerState == null)
                    return;

                var receivers = player.GetComponentsInChildren<IPlayerStateReceiver>(true);
                foreach (var receiver in receivers)
                    receiver.BindPlayerState(_playerState);
            }

            private void WarnIfCharacterBrainMissing(GameObject player)
            {
                if (!_warnIfCharacterBrainMissing)
                    return;

                if (player.GetComponent<CharacterBrain>() != null)
                    return;

                Debug.LogWarning(
                    "[Mainboard.Player] Spawned player has no CharacterBrain. Add a concrete brain component to the player prefab."
                );
            }

            private void ReleasePlayer()
            {
                if (_spawnedPlayerTransform != null)
                {
                    Object.Destroy(_spawnedPlayerTransform.gameObject);
                    _spawnedPlayerTransform = null;
                }

                _spawnedVisual = null;
                _playerRuntime = null;
                _playerHandle?.Dispose();
                _playerHandle = null;
                _visualHandle?.Dispose();
                _visualHandle = null;
            }

            private async UniTask AttachVisualAsync(GameObject player, CancellationToken cancellationToken)
            {
                if (player == null || string.IsNullOrWhiteSpace(_visualPrefabKey))
                    return;

                var assetService = AssetServiceBootstrap.Ensure(_context);
                _visualHandle = await assetService.LoadAsync<GameObject>(
                    _visualPrefabKey,
                    cancellationToken
                );
                cancellationToken.ThrowIfCancellationRequested();

                if (_visualHandle?.Asset == null)
                    return;

                var visualRoot = ResolveVisualRoot(player.transform);
                if (visualRoot == null)
                    return;

                _spawnedVisual = Object.Instantiate(_visualHandle.Asset, visualRoot);
                _spawnedVisual.transform.localPosition = Vector3.zero;
                _spawnedVisual.transform.localRotation = Quaternion.identity;
                _spawnedVisual.transform.localScale = Vector3.one;

                var receiver = player.GetComponent<ICharacterAnimatorReceiver>();
                var animator = _spawnedVisual.GetComponentInChildren<CharacterAnimator>(true);
                if (receiver != null && animator != null)
                    receiver.BindAnimator(animator);
            }

            private Transform ResolveVisualRoot(Transform player)
            {
                var registry = player.GetComponentInChildren<CharacterSocketRegistry>(true);
                if (registry != null && registry.TryGet(CharacterSocketId.VisualRoot, out var visualRoot))
                    return visualRoot;

                Debug.LogError(
                    $"[Mainboard.Player] Player '{player.name}' is missing CharacterSocketId.VisualRoot. " +
                    "Visual attachment requires CharacterSocketRegistry protocol."
                );
                return null;
            }
        }

        private sealed class PlayerRuntime : IPlayerRuntime
        {
            public PlayerRuntime(GameObject gameObject)
            {
                GameObject = gameObject;
                Transform = gameObject != null ? gameObject.transform : null;
            }

            public GameObject GameObject { get; }
            public Transform Transform { get; }
        }

        private sealed class SceneSpawnPointService : ISpawnPointService
        {
            private readonly List<ISpawnPoint> _spawnPoints;
            private readonly Dictionary<string, ISpawnPoint> _byID;

            private SceneSpawnPointService(List<ISpawnPoint> spawnPoints)
            {
                _spawnPoints = spawnPoints;
                _byID = new Dictionary<string, ISpawnPoint>();

                foreach (var spawnPoint in _spawnPoints)
                {
                    if (spawnPoint == null || string.IsNullOrWhiteSpace(spawnPoint.ID))
                        continue;

                    _byID[spawnPoint.ID] = spawnPoint;
                }
            }

            public static SceneSpawnPointService Create(LevelContext level)
            {
                var spawnPoints = new List<ISpawnPoint>();
                if (level.Scene.IsValid())
                {
                    foreach (var root in level.Scene.GetRootGameObjects())
                    {
                        var hubs = root.GetComponentsInChildren<SpawnPointHub>(true);
                        foreach (var hub in hubs)
                        {
                            if (hub != null)
                                spawnPoints.Add(hub);
                        }
                    }
                }

                if (spawnPoints.Count == 0)
                {
                    foreach (var hub in SpawnPointHub.AllHubs)
                    {
                        if (hub != null)
                            spawnPoints.Add(hub);
                    }
                }

                return new SceneSpawnPointService(spawnPoints);
            }

            public bool TryGetByID(string id, out ISpawnPoint spawnPoint)
            {
                if (!string.IsNullOrWhiteSpace(id) && _byID.TryGetValue(id, out spawnPoint))
                    return true;

                spawnPoint = null;
                return false;
            }

            public bool TryGetFirst(out ISpawnPoint spawnPoint)
            {
                foreach (var candidate in _spawnPoints)
                {
                    if (candidate == null || candidate.IsBlocked)
                        continue;

                    spawnPoint = candidate;
                    return true;
                }

                spawnPoint = null;
                return false;
            }
        }
    }
}
