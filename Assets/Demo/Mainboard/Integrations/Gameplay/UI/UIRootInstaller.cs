using System.Threading;
using Cysharp.Threading.Tasks;
using Mainboard.Runtime;
using UniRx;
using UISystem.Runtime;
using UnityEngine;

namespace Mainboard.Runtime.Integrations
{
    public interface IUIRuntime
    {
        UIManager Manager { get; }
        Canvas Canvas { get; }
        void OpenView(string viewID, bool addToHistory = true);
        void BindCanvasToCamera(Camera uiCamera, float planeDistance);
    }

    [CreateAssetMenu(fileName = "UIRootFeature", menuName = "Demo/Mainboard/Features/UI Root")]
    public sealed class UIRootInstaller : MainboardInstaller
    {
        [SerializeField] private string uiRootKey = "Assets/Demo/Art/Prefabs/UIRoot.prefab";
        [SerializeField] private bool dontDestroyOnLoad = true;

        public override IGameFeature CreateFeature()
        {
            return new UIRootFeature(uiRootKey, dontDestroyOnLoad);
        }

        private sealed class UIRootFeature : IGameFeature, IFeatureInstaller, IFeatureBoot, IFeatureShutdown
        {
            private readonly string _uiRootKey;
            private readonly bool _dontDestroyOnLoad;
            private readonly CompositeDisposable _disposables = new CompositeDisposable();
            private AssetHandle<GameObject> _handle;
            private GameObject _root;
            private IUIRuntime _runtime;
            private MainboardContext _context;

            public UIRootFeature(string uiRootKey, bool dontDestroyOnLoad)
            {
                _uiRootKey = uiRootKey;
                _dontDestroyOnLoad = dontDestroyOnLoad;
            }

            public string Name => "UI Root";

            public UniTask InstallAsync(MainboardContext context, CancellationToken cancellationToken)
            {
                _context = context;
                AssetServiceBootstrap.Ensure(context);
                context.Signals
                    .Receive<WorldStartUIRequestedSignal>()
                    .Subscribe(signal => OpenRequestedView(signal.ViewID))
                    .AddTo(_disposables);
                return UniTask.CompletedTask;
            }

            public async UniTask BootAsync(
                MainboardContext context,
                CancellationToken cancellationToken
            )
            {
                if (string.IsNullOrWhiteSpace(_uiRootKey))
                    return;

                var assetService = AssetServiceBootstrap.Ensure(context);
                _handle = await assetService.LoadAsync<GameObject>(_uiRootKey, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                _root = Object.Instantiate(_handle.Asset);
                _root.name = _handle.Asset.name;

                if (_dontDestroyOnLoad)
                    Object.DontDestroyOnLoad(_root);

                var manager = _root.GetComponent<UIManager>()
                    ?? _root.GetComponentInChildren<UIManager>(true);

                if (manager == null)
                    Debug.LogError("[Mainboard.UI] UI root prefab does not contain a UIManager.");

                _runtime = new UIRuntime(manager);
                context.RegisterService<IUIRuntime>(_runtime);
                context.Signals.Publish(new UIRootReadySignal(_root));
            }

            public UniTask ShutdownAsync(MainboardContext context, CancellationToken cancellationToken)
            {
                if (_root != null)
                {
                    Object.Destroy(_root);
                    _root = null;
                }

                _handle?.Dispose();
                _handle = null;
                context.Services.Unregister<IUIRuntime>();
                _runtime = null;
                _disposables.Dispose();
                return UniTask.CompletedTask;
            }

            private void OpenRequestedView(string viewID)
            {
                if (string.IsNullOrWhiteSpace(viewID))
                    return;

                if (_runtime != null)
                {
                    _runtime.OpenView(viewID);
                    return;
                }

                if (_context != null && _context.TryResolve<IUIRuntime>(out var runtime))
                    runtime.OpenView(viewID);
            }
        }

        private sealed class UIRuntime : IUIRuntime
        {
            public UIRuntime(UIManager manager)
            {
                Manager = manager;
                Canvas = ResolveCanvas(manager);
            }

            public UIManager Manager { get; }
            public Canvas Canvas { get; }

            public void OpenView(string viewID, bool addToHistory = true)
            {
                if (string.IsNullOrWhiteSpace(viewID))
                    return;

                Manager?.OpenView(viewID, addToHistory);
            }

            public void BindCanvasToCamera(Camera uiCamera, float planeDistance)
            {
                if (Canvas == null)
                    return;

                Canvas.renderMode = RenderMode.ScreenSpaceCamera;
                if (uiCamera != null)
                {
                    Canvas.worldCamera = uiCamera;
                    Canvas.planeDistance = planeDistance;
                }
            }

            private static Canvas ResolveCanvas(UIManager manager)
            {
                if (manager == null)
                    return null;

                return manager.GetComponent<Canvas>()
                    ?? manager.GetComponentInParent<Canvas>()
                    ?? manager.GetComponentInChildren<Canvas>(true);
            }
        }
    }
}
