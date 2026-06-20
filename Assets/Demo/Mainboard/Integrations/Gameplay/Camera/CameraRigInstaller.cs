using System;
using System.Threading;
using CameraSystem.Runtime;
using Cysharp.Threading.Tasks;
using Mainboard.Runtime;
using UniRx;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mainboard.Runtime.Integrations
{
    public interface ICameraRigRuntime
    {
        Transform Player { get; }
        Camera MainCamera { get; }
        Camera UICamera { get; }
    }

    public interface ICameraSystemRuntime
    {
        bool HasCameraManager { get; }
        Camera MainCamera { get; }
        Camera UICamera { get; }
        void RegisterPlayer(Transform player);
        void UnregisterPlayer(Transform player);
        void RegisterUICamera(Camera uiCamera);
    }

    [CreateAssetMenu(fileName = "CameraRigFeature", menuName = "Demo/Mainboard/Features/Camera Rig")]
    public sealed class CameraRigInstaller : MainboardInstaller
    {
        [SerializeField] private string cameraViewKey = "Assets/Demo/Art/Prefabs/CameraView.prefab";
        [SerializeField] private string uiCameraKey = "";

        public override IGameFeature CreateFeature()
        {
            return new CameraRigFeature(cameraViewKey, uiCameraKey);
        }

        private sealed class CameraRigFeature :
            IGameFeature,
            IFeatureInstaller,
            ILevelUnloadHandler,
            IFeatureShutdown
        {
            private readonly string _cameraViewKey;
            private readonly string _uiCameraKey;
            private readonly CompositeDisposable _disposables = new CompositeDisposable();
            private MainboardContext _context;
            private AssetHandle<GameObject> _cameraHandle;
            private AssetHandle<GameObject> _uiCameraHandle;
            private Transform _registeredPlayer;
            private ICameraRigRuntime _runtime;
            private ICameraSystemRuntime _cameraSystem;

            public CameraRigFeature(string cameraViewKey, string uiCameraKey)
            {
                _cameraViewKey = cameraViewKey;
                _uiCameraKey = uiCameraKey;
            }

            public string Name => "Camera Rig";

            public UniTask InstallAsync(MainboardContext context, CancellationToken cancellationToken)
            {
                _context = context;
                _cameraSystem = new CameraManagerAdapter();
                context.RegisterService(_cameraSystem);
                context.Signals
                    .Receive<PlayerSpawnedSignal>()
                    .Subscribe(signal => BindToPlayerAsync(signal, context.CancellationToken).Forget())
                    .AddTo(_disposables);
                return UniTask.CompletedTask;
            }

            public UniTask OnLevelUnloadingAsync(LevelContext level, CancellationToken cancellationToken)
            {
                if (_registeredPlayer != null)
                {
                    _cameraSystem?.UnregisterPlayer(_registeredPlayer);
                    _registeredPlayer = null;
                }

                _runtime = null;
                return UniTask.CompletedTask;
            }

            public UniTask ShutdownAsync(MainboardContext context, CancellationToken cancellationToken)
            {
                if (_registeredPlayer != null)
                {
                    _cameraSystem?.UnregisterPlayer(_registeredPlayer);
                    _registeredPlayer = null;
                }

                _runtime = null;
                context.Services.Unregister<ICameraSystemRuntime>();
                _cameraSystem = null;
                _cameraHandle?.Dispose();
                _cameraHandle = null;
                _uiCameraHandle?.Dispose();
                _uiCameraHandle = null;
                _disposables.Dispose();
                return UniTask.CompletedTask;
            }

            private async UniTaskVoid BindToPlayerAsync(
                PlayerSpawnedSignal signal,
                CancellationToken cancellationToken
            )
            {
                try
                {
                    var player = signal.Player != null ? signal.Player.transform : null;
                    await EnsureCameraRigAsync(cancellationToken);
                    if (player != null)
                    {
                        _registeredPlayer = player;
                        _cameraSystem.RegisterPlayer(player);
                    }

                    _runtime = new CameraRigRuntime(
                        player,
                        _cameraSystem.MainCamera,
                        _cameraSystem.UICamera
                    );
                    signal.Scope?.RegisterService<ICameraRigRuntime>(_runtime);
                    _context.Signals.Publish(
                        new CameraRigReadySignal(_cameraSystem.MainCamera, _cameraSystem.UICamera)
                    );
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            private async UniTask EnsureCameraRigAsync(CancellationToken cancellationToken)
            {
                if (!_cameraSystem.HasCameraManager && !string.IsNullOrWhiteSpace(_cameraViewKey))
                {
                    var assetService = AssetServiceBootstrap.Ensure(_context);
                    _cameraHandle = await assetService.LoadAsync<GameObject>(
                        _cameraViewKey,
                        cancellationToken
                    );
                    cancellationToken.ThrowIfCancellationRequested();
                    Object.Instantiate(_cameraHandle.Asset);
                }

                var activeUICamera = _cameraSystem.UICamera;

                if (activeUICamera == null && !string.IsNullOrWhiteSpace(_uiCameraKey))
                {
                    var assetService = AssetServiceBootstrap.Ensure(_context);
                    _uiCameraHandle = await assetService.LoadAsync<GameObject>(
                        _uiCameraKey,
                        cancellationToken
                    );
                    cancellationToken.ThrowIfCancellationRequested();

                    var uiCameraObject = Object.Instantiate(_uiCameraHandle.Asset);
                    if (uiCameraObject.transform.parent != null)
                        uiCameraObject.transform.SetParent(null, true);

                    Object.DontDestroyOnLoad(uiCameraObject);

                    if (uiCameraObject.GetComponent<UICameraRegisterHook>() == null)
                        uiCameraObject.AddComponent<UICameraRegisterHook>();

                    activeUICamera = uiCameraObject.GetComponent<Camera>();
                    if (activeUICamera != null)
                        _cameraSystem.RegisterUICamera(activeUICamera);
                }
            }
        }

        private sealed class CameraRigRuntime : ICameraRigRuntime
        {
            public CameraRigRuntime(Transform player, Camera mainCamera, Camera uiCamera)
            {
                Player = player;
                MainCamera = mainCamera;
                UICamera = uiCamera;
            }

            public Transform Player { get; }
            public Camera MainCamera { get; }
            public Camera UICamera { get; }
        }

        private sealed class CameraManagerAdapter : ICameraSystemRuntime
        {
            public bool HasCameraManager => CameraManager.Instance != null;
            public Camera MainCamera => Camera.main;
            public Camera UICamera => CameraManager.CameraUI;

            public void RegisterPlayer(Transform player)
            {
                CameraManager.RegisterPlayer(player);
            }

            public void UnregisterPlayer(Transform player)
            {
                CameraManager.UnregisterPlayer(player);
            }

            public void RegisterUICamera(Camera uiCamera)
            {
                CameraManager.RegisterCameraUI(uiCamera);
            }
        }
    }
}
