using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UISystem.Runtime;
using DecalMini;

namespace ModularDemo.Runtime
{
    /// <summary>
    /// 运行态贴花空间遥测及交互调试面板 (Decal Spatial Telemetry & Interactive Debug View)
    /// 继承自 UIView，享受 UISystem 统一生命周期管理，并支持 F12 全局热键唤起。
    /// </summary>
    public class DecalDebugView : UIView
    {
        [Header("Telemetry Metrics Elements")]
        public Text totalCountText;
        public Text activeProjectorsText;
        public Text gridCellsText;

        [Header("Scroll List Elements")]
        public ScrollRect scrollRect;
        public RectTransform listContent;

        [Header("Item Template")]
        public GameObject itemTemplate; // 滚动列表中的单个贴花条目模板

        private readonly List<DecalProjectorMini> _scannedDecals = new();
        private readonly List<GameObject> _spawnedItems = new();
        private readonly List<Texture2D> _decalTexturesPool = new();
        private Coroutine _refreshCoroutine;
        private IDecalRuntime _decalRuntime = DecalRuntime.Shared;

        protected override void Awake()
        {
            base.Awake();

            // 隐藏模板，仅在克隆时激活
            if (itemTemplate != null)
            {
                itemTemplate.SetActive(false);
            }

            // 提前扫描已有贴花以建立贴图池，为测试 Spawn 提供优质多样的数据支持
            CollectTexturePool();
        }

        protected virtual void Start()
        {
            // 自动绑定 Footer 控制台按钮，避免在编辑器生成脚本中进行繁杂的 UnityEvent 序列化操作，极其鲁棒
            Button[] buttons = GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                if (btn.gameObject.name == "SpawnBtn")
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(SpawnTestDecal);
                }
                else if (btn.gameObject.name == "ClearBtn")
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(ClearAllDynamicDecals);
                }
                else if (btn.gameObject.name == "CleanInjectionsBtn")
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(ForceCleanInjections);
                }
            }
        }

        private void OnEnable()
        {
            // 启动每秒低耗静默自动遥测刷新协程
            _refreshCoroutine = StartCoroutine(AutoRefreshRoutine());
        }

        private void OnDisable()
        {
            if (_refreshCoroutine != null)
            {
                StopCoroutine(_refreshCoroutine);
                _refreshCoroutine = null;
            }
        }



        private IEnumerator AutoRefreshRoutine()
        {
            while (true)
            {
                RefreshStats();
                RefreshList();
                yield return new WaitForSecondsRealtime(1.0f);
            }
        }

        /// <summary>
        /// 收集场景已有的贴花贴图，构筑缓冲池
        /// </summary>
        private void CollectTexturePool()
        {
            _decalTexturesPool.Clear();
            var projectors = Object.FindObjectsByType<DecalProjectorMini>(FindObjectsSortMode.None);
            foreach (var p in projectors)
            {
                if (p != null && p.decalTexture != null && !_decalTexturesPool.Contains(p.decalTexture))
                {
                    _decalTexturesPool.Add(p.decalTexture);
                }
            }
        }

        /// <summary>
        /// 刷新遥测仪表盘
        /// </summary>
        public void RefreshStats()
        {
            if (totalCountText != null)
                totalCountText.text = $"Total Decals: {_decalRuntime.TotalCount}";
            if (activeProjectorsText != null)
                activeProjectorsText.text = $"Active Projectors: {_decalRuntime.ProjectorCount}";
            if (gridCellsText != null)
                gridCellsText.text = $"Spatial Cells (Active): {_decalRuntime.ActiveRuntimeCells}";
        }

        /// <summary>
        /// 刷新滚动贴花实例列表
        /// </summary>
        public void RefreshList()
        {
            // 1. 清理旧实例节点
            foreach (var item in _spawnedItems)
            {
                if (item != null) Destroy(item);
            }
            _spawnedItems.Clear();

            if (itemTemplate == null || listContent == null) return;

            // 2. 重新扫描场景中所有的贴花投影器
            _scannedDecals.Clear();
            var projectors = Object.FindObjectsByType<DecalProjectorMini>(FindObjectsSortMode.None);
            _scannedDecals.AddRange(projectors);

            // 3. 动态实例化列表元素
            for (int i = 0; i < _scannedDecals.Count; i++)
            {
                var decal = _scannedDecals[i];
                if (decal == null) continue;

                GameObject itemObj = Instantiate(itemTemplate, listContent);
                itemObj.SetActive(true);
                _spawnedItems.Add(itemObj);

                // A. 填充信息文本
                Text label = itemObj.transform.Find("Label")?.GetComponent<Text>();
                if (label != null)
                {
                    string texName = decal.decalTexture != null ? decal.decalTexture.name : "None";
                    label.text = $"#{i + 1:D2} - {decal.gameObject.name} ({texName})";
                }

                Text posLabel = itemObj.transform.Find("PosLabel")?.GetComponent<Text>();
                if (posLabel != null)
                {
                    Vector3 pos = decal.transform.position;
                    posLabel.text = $"Pos: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})";
                }

                // B. 绑定按钮事件：精准瞬移 Teleport
                Button tpBtn = itemObj.transform.Find("TeleportBtn")?.GetComponent<Button>();
                if (tpBtn != null)
                {
                    tpBtn.onClick.RemoveAllListeners();
                    tpBtn.onClick.AddListener(() => TeleportToDecal(decal));
                }

                // C. 绑定按钮事件：脉冲闪烁 Flash
                Button flashBtn = itemObj.transform.Find("FlashBtn")?.GetComponent<Button>();
                if (flashBtn != null)
                {
                    flashBtn.onClick.RemoveAllListeners();
                    flashBtn.onClick.AddListener(() => FlashDecal(decal));
                }

                // D. 绑定按钮事件：销毁 Destroy
                Button destroyBtn = itemObj.transform.Find("DestroyBtn")?.GetComponent<Button>();
                if (destroyBtn != null)
                {
                    destroyBtn.onClick.RemoveAllListeners();
                    destroyBtn.onClick.AddListener(() =>
                    {
                        if (decal != null)
                        {
                            Destroy(decal.gameObject);
                        }
                        // 微秒级延迟后自更新
                        Invoke(nameof(ForceRefreshTelemetry), 0.05f);
                    });
                }
            }
        }

        private void ForceRefreshTelemetry()
        {
            RefreshStats();
            RefreshList();
        }

        /// <summary>
        /// 将主摄像机/玩家完美瞬移对齐到指定贴花
        /// </summary>
        private void TeleportToDecal(DecalProjectorMini decal)
        {
            if (decal == null) return;

            // 1. 优先移动并对齐主摄像机
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                // 摄像机稍微拉退一点并在上方， LookAt 朝向贴花中心
                Vector3 camTargetPos = decal.transform.position + decal.transform.forward * 2.5f + Vector3.up * 1.0f;
                mainCam.transform.position = camTargetPos;
                mainCam.transform.LookAt(decal.transform.position);
                Debug.Log($"<color=#BC8CFF><b>[DecalDebug]</b></color> Teleported MainCamera to Decal: {decal.name}");
            }

            // 2. 尝试寻找玩家，同步将其移到贴花后方
            var player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                // 如果没有 Tag，尝试模糊搜索带有 CharacterController 的根物体
                var cc = Object.FindFirstObjectByType<UnityEngine.CharacterController>();
                if (cc != null) player = cc.gameObject;
            }

            if (player != null)
            {
                var cc = player.GetComponent<UnityEngine.CharacterController>();
                if (cc != null) cc.enabled = false; // 暂时禁用物理刚性组件

                player.transform.position = decal.transform.position + decal.transform.forward * 1.5f + Vector3.up * 0.1f;

                if (cc != null) cc.enabled = true;
                Debug.Log($"<color=#BC8CFF><b>[DecalDebug]</b></color> Teleported Player character to Decal: {decal.name}");
            }
        }

        /// <summary>
        /// 触发贴花色彩脉冲闪烁高亮
        /// </summary>
        private void FlashDecal(DecalProjectorMini decal)
        {
            if (decal == null || !decal.isActiveAndEnabled) return;
            StartCoroutine(FlashRoutine(decal));
        }

        private IEnumerator FlashRoutine(DecalProjectorMini decal)
        {
            Color originalColor = decal.color;
            int originalSorting = decal.sortingOrder;

            // 强行提高渲染排序，以防深度冲突或被遮挡
            decal.sortingOrder = 999;

            for (int i = 0; i < 4; i++)
            {
                if (decal == null) yield break;
                decal.color = new Color(1.0f, 0.2f, 0.2f, 1.0f); // 鲜红色脉冲
                yield return new WaitForSecondsRealtime(0.15f);

                if (decal == null) yield break;
                decal.color = new Color(1.0f, 1.0f, 1.0f, 1.0f); // 白色高亮脉冲
                yield return new WaitForSecondsRealtime(0.15f);
            }

            if (decal != null)
            {
                decal.color = originalColor;
                decal.sortingOrder = originalSorting;
            }
        }

        /// <summary>
        /// 一键在相机正前方生成测试用贴花
        /// </summary>
        public void SpawnTestDecal()
        {
            GameObject testObj = new GameObject("Dynamic_Debug_Decal");
            var decal = testObj.AddComponent<DecalProjectorMini>();

            // 1. 给它随机赋一个场景已有的贴花贴图，建立关联
            if (_decalTexturesPool.Count > 0)
            {
                decal.decalTexture = _decalTexturesPool[Random.Range(0, _decalTexturesPool.Count)];
            }

            // 2. 赋予个性参数，以供调试
            decal.color = new Color(Random.value, Random.value, Random.value, 1f);
            decal.sortingOrder = Random.Range(1, 10);
            decal.rotationSpeed = Random.Range(0f, 40f); // 随机转速测试更新
            if (Random.value > 0.5f)
            {
                decal.pulseEffect = true;
                decal.pulseSpeed = Random.Range(1f, 4f);
            }

            // 3. 部署位置
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                testObj.transform.position = mainCam.transform.position + mainCam.transform.forward * 3.5f;
                testObj.transform.rotation = Quaternion.LookRotation(-mainCam.transform.forward);
            }
            else
            {
                testObj.transform.position = new Vector3(Random.Range(-5f, 5f), 0.5f, Random.Range(-5f, 5f));
            }

            // 4. 重建列表
            CollectTexturePool(); // 随时扩充
            ForceRefreshTelemetry();
            Debug.Log($"<color=#BC8CFF><b>[DecalDebug]</b></color> Dynamically spawned a dynamic decal at position {testObj.transform.position}");
        }

        /// <summary>
        /// 一键销毁场景里所有的动态贴花
        /// </summary>
        public void ClearAllDynamicDecals()
        {
            var projectors = Object.FindObjectsByType<DecalProjectorMini>(FindObjectsSortMode.None);
            int count = 0;
            foreach (var p in projectors)
            {
                if (p != null)
                {
                    Destroy(p.gameObject);
                    count++;
                }
            }
            Debug.Log($"<color=#BC8CFF><b>[DecalDebug]</b></color> Cleaned up {count} active decal projector game objects.");
            ForceRefreshTelemetry();
        }

        /// <summary>
        /// 手动一键强力还原 Shader 注入设置（编辑器下可用，真机包自动安全防御隔离）
        /// </summary>
        public void ForceCleanInjections()
        {
#if UNITY_EDITOR
            // 采用反射解耦，防御因 asmdef 跨 Runtime-Editor 依赖产生的打包报错
            System.Type preprocessorType = System.Type.GetType("SmartBuild.Editor.SmartBuildPreprocessor, SmartBuild.Editor");
            if (preprocessorType != null)
            {
                var method = preprocessorType.GetMethod("ForceRestore", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (method != null)
                {
                    int count = (int)method.Invoke(null, null);
                    Debug.Log($"<color=#BC8CFF><b>[DecalDebug]</b></color> Reflectively executed ForceRestore! Removed {count} shader(s) from GraphicsSettings.");
                    return;
                }
            }
            Debug.LogWarning("[DecalDebug] SmartBuildPreprocessor or ForceRestore method could not be resolved via Reflection.");
#else
            Debug.LogWarning("[DecalDebug] Clean Injections is an editor-only utility and is disabled in standalone builds.");
#endif
        }
    }

    /// <summary>
    /// 全局不受视图激活状态影响的 F12 快捷键控制器 (Persistent F12 Shortcut Manager)
    /// 挂载在始终激活的 UIManager Canvas 上，确保 100% 稳定的开关响应。
    /// </summary>
    public class DecalDebugHotkeyManager : MonoBehaviour
    {
        private void Update()
        {
            bool f12Pressed = false;
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f12Key.wasPressedThisFrame)
            {
                f12Pressed = true;
            }
#endif
            if (!f12Pressed)
            {
                try
                {
                    if (Input.GetKeyDown(KeyCode.F12))
                    {
                        f12Pressed = true;
                    }
                }
                catch (System.InvalidOperationException)
                {
                    // 在纯新输入系统模式下，调用 Input.GetKeyDown 会抛出 InvalidOperationException 异常，安全防御捕获
                }
            }

            if (f12Pressed)
            {
                var manager = GetComponentInParent<UIManager>();
                var view = manager != null ? manager.GetView<DecalDebugView>("DecalDebugView") : null;
                if (view != null)
                {
                    if (view.IsVisible)
                    {
                        view.Close();
                        Debug.Log("<color=#BC8CFF><b>[DecalDebug]</b></color> Detected F12: Closing Debug Panel.");
                    }
                    else
                    {
                        view.Open();
                        Debug.Log("<color=#BC8CFF><b>[DecalDebug]</b></color> Detected F12: Opening Debug Panel.");
                    }
                }
            }
        }
    }
}
