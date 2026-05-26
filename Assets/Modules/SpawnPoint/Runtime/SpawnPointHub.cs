using System.Collections.Generic;
using UnityEngine;

namespace SpawnPoint.Runtime
{
    /// <summary>
    /// 工业级生成中枢（Spawn Point Hub）
    /// 实现了 ISpawnPoint 接口，采用高性能注册表模式
    /// </summary>
    [ExecuteAlways]
    [SelectionBase]
    [DisallowMultipleComponent]
    [AddComponentMenu("Spawn Point/Spawn Point Hub")]
    public sealed class SpawnPointHub : MonoBehaviour, ISpawnPoint
    {
        [Header("Identity")]
        [SerializeField]
        [Tooltip(
            "The unique string identifier for this spawn point. Can be used for direct lookup via SpawnPointHub.GetByID()."
        )]
        private string _hubID = "Default";

        [SerializeField]
        [Tooltip(
            "Defines which team or faction this spawn point belongs to. 0 is typically the Player or Neutral team."
        )]
        private int _teamIndex = 0;

        [SerializeField]
        [Range(0, 1000)]
        [Tooltip(
            "Determines the spawn order. Higher priority points are chosen first when multiple points are available for a team."
        )]
        private int _priority = 100;

        [Header("Status")]
        [SerializeField]
        [Tooltip(
            "If checked, this spawn point will be excluded from the 'best point' search logic. Useful for dynamic hazards or occupied spots."
        )]
        private bool _isBlocked = false;

        // --- ISpawnPoint Implementation ---
        public string ID => _hubID;
        public int TeamIndex => _teamIndex;
        public int Priority => _priority;
        public bool IsBlocked => _isBlocked;
        public Transform SpawnTransform => transform;
        public Vector3 Position => transform.position;
        public Quaternion Rotation => transform.rotation;

        // --- Global Registry (Industrial Logic) ---
        private static readonly HashSet<SpawnPointHub> _allHubs = new HashSet<SpawnPointHub>();
        private static readonly Dictionary<string, SpawnPointHub> _idLookup =
            new Dictionary<string, SpawnPointHub>();

        /// <summary>
        /// 获取所有在线的生成点（只读暴露，防止外部篡改）
        /// </summary>
        public static IEnumerable<SpawnPointHub> AllHubs => _allHubs;

        private void OnEnable()
        {
            if (!_allHubs.Contains(this))
            {
                _allHubs.Add(this);
                // 自动处理 ID 冲突告警
                if (_idLookup.ContainsKey(_hubID))
                {
                    Debug.LogWarning(
                        $"[SpawnPoint] Duplicate ID detected: {_hubID} on {gameObject.name}. This will overwrite the lookup entry!"
                    );
                }
                _idLookup[_hubID] = this;
            }
        }

        private void OnDisable()
        {
            _allHubs.Remove(this);
            if (_idLookup.TryGetValue(_hubID, out var existing) && existing == this)
            {
                _idLookup.Remove(_hubID);
            }
        }

        /// <summary>
        /// O(1) 性能获取生成点
        /// </summary>
        public static SpawnPointHub GetByID(string id)
        {
            return _idLookup.TryGetValue(id, out var hub) ? hub : null;
        }

        /// <summary>
        /// 获取符合条件的最佳生成点（按优先级排序）
        /// </summary>
        public static List<SpawnPointHub> GetOrderedHubsForTeam(
            int team,
            bool includeBlocked = false
        )
        {
            var results = new List<SpawnPointHub>();
            foreach (var hub in _allHubs)
            {
                if (hub.TeamIndex == team && (includeBlocked || !hub.IsBlocked))
                {
                    results.Add(hub);
                }
            }
            results.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            return results;
        }

        #region Editor Visualization
        private void OnDrawGizmos()
        {
            // 绘制专业版图标（向上偏移 0.8 米）
            Gizmos.DrawIcon(transform.position + Vector3.up * 0.8f, "icon_Spawn_1.png", true);

            // 绘制底座圆盘
            Color themeColor = GetTeamColor(_teamIndex);
            Gizmos.color = new Color(themeColor.r, themeColor.g, themeColor.b, 0.3f);
            DrawDisc(transform.position + Vector3.up * 0.02f, 0.5f);

            // 绘制朝向箭头
            Gizmos.color = new Color(themeColor.r, themeColor.g, themeColor.b, 1.0f);
            Vector3 forward = transform.forward;
            Vector3 pos = transform.position + Vector3.up * 0.05f;
            Gizmos.DrawRay(pos, forward * 0.7f);

            // 绘制箭头尖端
            Vector3 right = transform.right;
            Vector3 arrowTip = pos + forward * 0.7f;
            Gizmos.DrawLine(arrowTip, arrowTip - forward * 0.15f + right * 0.1f);
            Gizmos.DrawLine(arrowTip, arrowTip - forward * 0.15f - right * 0.1f);
        }

        private Color GetTeamColor(int team)
        {
            return team switch
            {
                0 => new Color(0.2f, 0.8f, 1f), // Neutral/Player
                1 => Color.red, // Enemy Team A
                2 => Color.green, // Enemy Team B
                _ => Color.white,
            };
        }

        private void DrawDisc(Vector3 center, float radius)
        {
            const int segments = 32;
            float step = 360f / segments;
            Vector3 last = center + new Vector3(radius, 0, 0);
            for (int i = 1; i <= segments; i++)
            {
                float rad = i * step * Mathf.Deg2Rad;
                Vector3 next =
                    center + new Vector3(Mathf.Cos(rad) * radius, 0, Mathf.Sin(rad) * radius);
                Gizmos.DrawLine(last, next);
                last = next;
            }
        }
        #endregion
    }
}
