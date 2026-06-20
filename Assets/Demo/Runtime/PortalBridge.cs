using UnityEngine;
using PortalSystem.Runtime;
using PlayerState.Runtime;
using Mainboard.Runtime;
using Cysharp.Threading.Tasks;

namespace ModularDemo.Runtime
{
    [RequireComponent(typeof(PortalHub))]
    public class PortalBridge : MonoBehaviour
    {
        public PlayerStateSO playerState;
        [SerializeField] private GameMainboard mainboard;
        [SerializeField] private bool logRuntimeStatus = true;
        private PortalHub _hub;

        private void Awake()
        {
            if (mainboard == null)
                mainboard = FindFirstObjectByType<GameMainboard>();

            _hub = GetComponent<PortalHub>();
            _hub.OnPortalTriggeredAction += HandlePortalTrigger;
        }

        private void OnDestroy()
        {
            if (_hub != null) _hub.OnPortalTriggeredAction -= HandlePortalTrigger;
        }

        private void HandlePortalTrigger(string targetLevel, string spawnPointID)
        {
            HandlePortalTriggerAsync(targetLevel, spawnPointID).Forget();
        }

        private async UniTaskVoid HandlePortalTriggerAsync(string targetLevel, string spawnPointID)
        {
            LogRuntimeStatus("Before transition", targetLevel, spawnPointID);

            // 1. 保存位置信息到 PlayerState (如果需要)
            if (playerState != null)
            {
                playerState.lastSpawnPointID = spawnPointID;
                playerState.lastPosition = Vector3.zero; // 既然是传送，就不保留具体坐标
            }

            // 2. 执行加载
            if (mainboard != null && mainboard.Context != null)
            {
                if (!mainboard.Context.TryResolve<IWorldNavigator>(out var navigator))
                {
                    Debug.LogError(
                        $"[PortalBridge] World navigator is not available. {BuildRuntimeStatus()}"
                    );
                    return;
                }

                var result = await navigator.LoadLevelAsync(
                    targetLevel,
                    mainboard.GetCancellationTokenOnDestroy()
                );
                if (!result.Success)
                {
                    Debug.LogError(
                        $"[PortalBridge] Failed to load level '{targetLevel}': {result.Error}. {BuildRuntimeStatus()}"
                    );
                    return;
                }

                LogRuntimeStatus("After transition", targetLevel, spawnPointID);
            }
            else
            {
                Debug.LogError("[PortalBridge] Mainboard is not available.");
            }
        }

        private void LogRuntimeStatus(string label, string targetLevel, string spawnPointID)
        {
            if (!logRuntimeStatus)
                return;

            Debug.Log(
                $"[PortalBridge] {label}: TargetLevel='{targetLevel}', SpawnPoint='{spawnPointID}'. {BuildRuntimeStatus()}",
                this
            );
        }

        private string BuildRuntimeStatus()
        {
            string portalStatus = _hub != null ? _hub.DescribeRuntimeStatus() : "PortalHub=<missing>";
            string mainboardStatus =
                mainboard != null ? mainboard.DescribeRuntimeStatus() : "Mainboard=<missing>";
            return $"{portalStatus}; {mainboardStatus}";
        }
    }
}
