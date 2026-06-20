using System.Threading;
using Cysharp.Threading.Tasks;
using Mainboard.Runtime;
using UniRx;
using UISystem.Runtime;
using UnityEngine;

namespace Mainboard.Runtime.Integrations
{
    [CreateAssetMenu(fileName = "HudFeature", menuName = "Demo/Mainboard/Features/HUD")]
    public sealed class HudInstaller : MainboardInstaller
    {
        [SerializeField] private string hudViewID = "PlayerHUD";
        [SerializeField] private float uiCanvasPlaneDistance = 2f;

        public override IGameFeature CreateFeature()
        {
            return new HudFeature(hudViewID, uiCanvasPlaneDistance);
        }

        private sealed class HudFeature : IGameFeature, IFeatureInstaller, IFeatureShutdown
        {
            private readonly string _hudViewID;
            private readonly float _uiCanvasPlaneDistance;
            private readonly CompositeDisposable _disposables = new CompositeDisposable();
            private MainboardContext _context;

            public HudFeature(string hudViewID, float uiCanvasPlaneDistance)
            {
                _hudViewID = hudViewID;
                _uiCanvasPlaneDistance = uiCanvasPlaneDistance;
            }

            public string Name => "HUD";

            public UniTask InstallAsync(MainboardContext context, CancellationToken cancellationToken)
            {
                _context = context;
                context.Signals
                    .Receive<CameraRigReadySignal>()
                    .Subscribe(signal => BindCanvasAndOpenHud(signal.UICamera))
                    .AddTo(_disposables);
                return UniTask.CompletedTask;
            }

            public UniTask ShutdownAsync(MainboardContext context, CancellationToken cancellationToken)
            {
                _disposables.Dispose();
                return UniTask.CompletedTask;
            }

            private void BindCanvasAndOpenHud(Camera uiCamera)
            {
                if (_context == null || !_context.TryResolve<IUIRuntime>(out var uiRuntime))
                    return;

                uiRuntime.BindCanvasToCamera(uiCamera, _uiCanvasPlaneDistance);
                uiRuntime.OpenView(_hudViewID, false);
            }
        }
    }
}
