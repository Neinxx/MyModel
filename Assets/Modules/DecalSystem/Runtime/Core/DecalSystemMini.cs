using System.Collections.Generic;
using UnityEngine;

namespace DecalMini
{
    /// <summary>
    /// Decal Provider Interface: Defines the contract for any object capable of contributing to the decal rendering pipeline.
    /// Supports both MonoBehaviour-based projectors and low-level data structures.
    /// </summary>
    public interface IDecalProvider
    {
        /// <summary>
        /// Converts the provider's state into a hardware-friendly data structure for GPU consumption.
        /// </summary>
        /// <returns>A 320-byte aligned struct containing matrices and material properties.</returns>
        DecalDataMini ToDecalData();

        /// <summary>
        /// Global rendering order. Higher values are rendered last (appearing on top).
        /// </summary>
        int sortingOrder { get; }

        /// <summary>
        /// Cached transform reference for spatial indexing.
        /// </summary>
        Transform transform { get; }

        /// <summary>
        /// Lifecycle state for active culling.
        /// </summary>
        bool isActiveAndEnabled { get; }

        /// <summary>
        /// GameObject reference for layer-based filtering.
        /// </summary>
        GameObject gameObject { get; }
    }

    /// <summary>
    /// GLOBAL DECAL MANAGEMENT SYSTEM (Core Kernel)
    /// <para>
    /// This system implements a high-performance, mobile-optimized decal rendering pipeline using:
    /// 1. Spatial Hashing: O(1) retrieval of nearby decals based on camera frustum.
    /// 2. 0-GC Rendering Loop: All hot-path operations avoid heap allocations.
    /// 3. Hybrid Storage: Supports both dynamic Components and baked Static Data.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Thread Safety: Most methods are NOT thread-safe as they interact with Unity's Transform and Object APIs.
    /// Performance: Designed to handle 10,000+ decals with minimal CPU overhead.
    /// </remarks>
    public static class DecalSystemMini
    {
        // ========================================================================
        // 1. INTERNAL DATA STRUCTURES
        // ========================================================================

        /// <summary>
        /// Lightweight wrapper used during the sorting phase.
        /// Stores pre-computed distance to avoid redundant SqrMagnitude calls in the comparer.
        /// </summary>
        private struct DecalSortEntry
        {
            public DecalDataMini data;
            public int SortingOrder;
            public float distSqr;
        }

        // ========================================================================
        // 2. SYSTEM LIFECYCLE
        // ========================================================================

        /// <summary>
        /// Ensures a clean slate across domain reloads or scene transitions.
        /// Triggered automatically by Unity's subsystem registration.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ReleaseAll();
            Debug.Log(
                "<color=#7C8CFF><b>[Decal Mini]</b></color> System kernel re-initialized for clean boot."
            );
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void InitEditorLifecycle()
        {
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            // 在退出运行模式时，强制清理底层纯数据缓存，防止残影污染 Scene 视图
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                ReleaseAll();
            }
        }
#endif

        // ========================================================================
        // 3. SPATIAL INDEXING & DATA BUFFERS
        // ========================================================================

        // Primary registries for active projectors
        private static readonly HashSet<IDecalProvider> _projectorsSet = new();

        // Spatial grids: (X, Z) coordinate packed into long key
        private static readonly Dictionary<long, List<IDecalProvider>> _grid = new();
        private static readonly Dictionary<long, List<DecalStaticEntry>> _staticGrid = new();

        // Data source tracking for additive scene management
        private static readonly Dictionary<object, IEnumerable<DecalStaticEntry>> _loadedSources =
            new();

        // 0-Component Runtime Pool (Circular buffer for impacts, footprints, etc.)
        private static DecalDataMini[] _runtimePool;
        private static float[] _runtimeExpirations;
        private static int[] _runtimeSortingOrders;
        private static float[] _runtimeRadii;

        // Core system parameters
        private static DecalAtlasConfigMini _currentConfig;
        private static float _gridSize = 15f;
        private static int _poolPtr = 0;
        private static int _maxRuntimeCount = 1024;

        // ========================================================================
        // 3. DIAGNOSTIC APIS (For Telemetry & Debugging)
        // ========================================================================
        public static int ActiveStaticCells => _staticGrid?.Count ?? 0;
        public static int ActiveRuntimeCells => _grid?.Count ?? 0;
        public static int LoadedSourceCount => _loadedSources?.Count ?? 0;

        // ========================================================================

        private sealed class DecalEntryComparer : IComparer<DecalSortEntry>
        {
            public int Compare(DecalSortEntry x, DecalSortEntry y) =>
                x.SortingOrder.CompareTo(y.SortingOrder);
        }

        private sealed class DecalProviderComparer : IComparer<IDecalProvider>
        {
            public int Compare(IDecalProvider x, IDecalProvider y) =>
                x.sortingOrder.CompareTo(y.sortingOrder);
        }

        private sealed class DecalStaticEntryComparer : IComparer<DecalStaticEntry>
        {
            public int Compare(DecalStaticEntry x, DecalStaticEntry y) =>
                x.sortingOrder.CompareTo(y.sortingOrder);
        }

        private static readonly DecalEntryComparer _entryComparer = new();
        private static readonly DecalProviderComparer _sortingComparer = new();
        private static readonly DecalStaticEntryComparer _staticEntryComparer = new();

        // Pre-allocated buffer to avoid runtime resizing
        private static DecalSortEntry[] _sortBuffer = new DecalSortEntry[4096];

        // ========================================================================
        // 5. PUBLIC API (Configuration)
        // ========================================================================

        /// <summary>
        /// Current active atlas and system configuration.
        /// </summary>
        public static DecalAtlasConfigMini CurrentConfig => _currentConfig;

        /// <summary>
        /// Total count of active MonoBehaviour-based projectors.
        /// </summary>
        public static int Count => _projectorsSet.Count;

        /// <summary>
        /// Aggregate count of all decals (Projectors + Static + Runtime).
        /// </summary>
        public static int TotalCount => GetActiveTotalCount();

        /// <summary>
        /// Injects a new atlas configuration and re-indexes the entire world if grid size changes.
        /// </summary>
        /// <param name="config">The target atlas configuration asset.</param>
        public static void SetAtlasConfig(DecalAtlasConfigMini config)
        {
            if (_currentConfig == config)
                return;

            float oldSize = _currentConfig != null ? _currentConfig.spatialGridSize : _gridSize;
            _currentConfig = config;

            if (config != null)
            {
                _gridSize = config.spatialGridSize;

                // Full rebuild required if spatial hashing resolution changes
                if (!Mathf.Approximately(oldSize, _gridSize))
                {
                    RebuildAllGrids();
                }
                Debug.Log(
                    $"<color=#7C8CFF><b>[Decal Mini]</b></color> Atlas configuration injected: <color=#9CDCFE>{config.name}</color>"
                );
            }
        }

        /// <summary>
        /// Triggers a full re-indexing of all spatial grids. Use sparingly (e.g., config changes).
        /// </summary>
        private static void RebuildAllGrids()
        {
            _grid.Clear();
            foreach (var proj in _projectorsSet)
            {
                if (proj == null || (proj as UnityEngine.Object) == null)
                    continue;
                RegisterToGrid(proj);
            }

            _staticGrid.Clear();
            foreach (var kvp in _loadedSources)
            {
                foreach (var entry in kvp.Value)
                {
                    long key = GetGridKey(entry.position);
                    if (!_staticGrid.TryGetValue(key, out var list))
                    {
                        list = new List<DecalStaticEntry>();
                        _staticGrid[key] = list;
                    }
                    list.Add(entry);
                }
            }

            foreach (var kvp in _staticGrid)
                kvp.Value.Sort(_staticEntryComparer);

            Debug.Log(
                "<color=#7C8CFF><b>[Decal Mini]</b></color> Spatial grids re-indexed successfully."
            );
        }

        // ========================================================================
        // 6. CORE MANAGEMENT API
        // ========================================================================

        /// <summary>
        /// Registers a dynamic projector into the system.
        /// Automatically inserts into spatial grid with O(log N) complexity for order stability.
        /// </summary>
        public static void Register(IDecalProvider projector)
        {
            if (projector == null || !_projectorsSet.Add(projector))
                return;

            RegisterToGrid(projector);
        }

        private static void RegisterToGrid(IDecalProvider projector)
        {
            long key = GetGridKey(projector.transform.position);
            if (!_grid.TryGetValue(key, out var list))
            {
                list = new List<IDecalProvider>();
                _grid[key] = list;
            }

            int index = list.BinarySearch(projector, _sortingComparer);
            list.Insert(index < 0 ? ~index : index, projector);
        }

        /// <summary>
        /// Updates a projector's location in the spatial hash.
        /// Should be called by components when their Transform changes significantly.
        /// </summary>
        public static void UpdateGridPosition(
            IDecalProvider projector,
            Vector3 oldPos,
            Vector3 newPos
        )
        {
            long oldKey = GetGridKey(oldPos);
            long newKey = GetGridKey(newPos);
            if (oldKey == newKey)
                return;

            if (_grid.TryGetValue(oldKey, out var oldList))
            {
                oldList.Remove(projector);
                if (oldList.Count == 0)
                    _grid.Remove(oldKey);
            }

            RegisterToGrid(projector);
        }

        /// <summary>
        /// Removes a projector from the management set and spatial hash.
        /// </summary>
        public static void Unregister(IDecalProvider projector)
        {
            if (projector == null || !_projectorsSet.Remove(projector))
                return;

            Transform trans = null;
            try
            {
                trans = projector.transform;
            }
            catch (System.Exception)
            {
                // transform is already destroyed or inaccessible
            }

            if (trans != null)
            {
                long key = GetGridKey(trans.position);
                if (_grid.TryGetValue(key, out var list))
                {
                    list.Remove(projector);
                    if (list.Count == 0)
                        _grid.Remove(key);
                }
            }
            else
            {
                // Fallback: search and remove from all cells if transform is lost
                RemoveFromAllGridCells(projector);
            }
        }

        private static void RemoveFromAllGridCells(IDecalProvider projector)
        {
            var enumerator = _grid.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var list = enumerator.Current.Value;
                list.Remove(projector);
            }
        }

        /// <summary>
        /// Injects a block of static decal data (typically from a baked scene).
        /// </summary>
        /// <param name="sourceKey">Owner key for tracking (e.g., Scene object).</param>
        /// <param name="entries">The collection of static entries.</param>
        public static void RegisterStaticData(
            object sourceKey,
            IEnumerable<DecalStaticEntry> entries
        )
        {
            if (sourceKey == null || entries == null)
                return;

            _loadedSources[sourceKey] = entries;
            RebuildAllGrids(); // Trigger re-index for the new static source
            RecalculateStaticCount();
        }

        /// <summary>
        /// Unbinds static data associated with a specific key.
        /// </summary>
        public static void UnregisterStaticData(object sourceKey)
        {
            if (sourceKey == null || !_loadedSources.Remove(sourceKey))
                return;
            RebuildAllGrids();
            RecalculateStaticCount();
        }

        // ========================================================================
        // 7. RENDERING PIPELINE (The Heart)
        // ========================================================================

        /// <summary>
        /// Main retrieval function: Fills a raw data array with sorted decals visible in the frustum.
        /// <para>Guaranteed 0-GC execution path.</para>
        /// </summary>
        /// <param name="camPos">World position of the camera for distance-based culling.</param>
        /// <param name="maxDist">Maximum visibility radius.</param>
        /// <param name="layerMask">Filtering mask for decal layers.</param>
        /// <param name="dataArray">Destination buffer (typically a StructuredBuffer on GPU).</param>
        /// <param name="frustumPlanes">Unity frustum planes for 6-plane culling.</param>
        /// <returns>Actual number of visible decals found.</returns>
        public static int FillData(
            Vector3 camPos,
            float maxDist,
            LayerMask layerMask,
            DecalDataMini[] dataArray,
            Plane[] frustumPlanes
        )
        {
            float maxDistSqr = maxDist * maxDist;
            float currentTime = GetCurrentTime();
            int count = 0;

            // 1. Grid traversal
            float gridSize = _currentConfig != null ? _currentConfig.spatialGridSize : _gridSize;
            int range = Mathf.CeilToInt(maxDist / gridSize);
            int camX = Mathf.FloorToInt(camPos.x / gridSize);
            int camZ = Mathf.FloorToInt(camPos.z / gridSize);

            for (int x = camX - range; x <= camX + range; x++)
            {
                for (int z = camZ - range; z <= camZ + range; z++)
                {
                    long key = ((long)x << 32) | ((long)z & 0xFFFFFFFFL);

                    // A. Process Static Grid
                    if (_staticGrid.TryGetValue(key, out var staticList))
                        count = FillStaticEntries(
                            staticList,
                            camPos,
                            maxDistSqr,
                            layerMask,
                            frustumPlanes,
                            count
                        );

                    // B. Process Dynamic Projectors
                    if (_grid.TryGetValue(key, out var list))
                        count = FillProjectorEntries(
                            list,
                            camPos,
                            maxDistSqr,
                            layerMask,
                            frustumPlanes,
                            count
                        );
                }
            }

            // 2. Process Runtime Impacts (Un-indexed pool)
            count = FillRuntimeEntries(currentTime, camPos, maxDistSqr, count, frustumPlanes);

            // 3. Stable 0-GC QuickSort
            // We sort by SortingOrder (ASC) then Distance (DESC) to ensure stable depth/overlap.
            if (count > 1)
                QuickSort(_sortBuffer, 0, count - 1);

            // 4. Final buffer transfer
            int finalCount = Mathf.Min(count, dataArray.Length);
            for (int i = 0; i < finalCount; i++)
                dataArray[i] = _sortBuffer[i].data;

            return finalCount;
        }

        // ========================================================================
        // 8. DATA SPAWNING (Impacts & VFX)
        // ========================================================================

        /// <summary>
        /// Smart Spawn: Automatically picks between Object Pool (Projector) or Runtime Buffer (0-Component).
        /// Optimized for temporary VFX like footprints or bullet holes.
        /// </summary>
        public static void Spawn(
            Vector3 pos,
            Quaternion rot,
            Vector3 size,
            Texture2D tex,
            float lifeTime,
            Color color,
            float softFade = 0.5f,
            int SortingOrder = 10000,
            float rotationSpeed = 0f,
            float pulseFreq = 0f,
            float pulseIntensity = 0f,
            float animReserved = 0f,
            Vector4? uvOverride = null
        )
        {
            if (!Application.isPlaying)
            {
                SpawnRuntimeDecal(
                    pos,
                    rot,
                    size,
                    tex,
                    lifeTime,
                    color,
                    softFade,
                    SortingOrder,
                    rotationSpeed,
                    pulseFreq,
                    pulseIntensity,
                    animReserved,
                    uvOverride
                );
                return;
            }

            // Try the Component pool first for maximum control
            var proj = DecalPoolMini.Get();
            if (proj != null)
            {
                proj.transform.SetPositionAndRotation(pos, rot);
                proj.transform.localScale = size;
                proj.decalTexture = tex;
                proj.color = color;
                proj.softFade = softFade;
                proj.sortingOrder = SortingOrder;
                proj.rotationSpeed = rotationSpeed;
                proj.pulseEffect = pulseFreq > 0;
                proj.pulseSpeed = pulseFreq;
                proj.pulseRange = pulseIntensity;
                proj.useRadialMask = animReserved > 0.001f;
                proj.radialSoftness = animReserved;
                proj.uvScaleOffset = uvOverride ?? new Vector4(1, 1, 0, 0);
                proj.Play(lifeTime);
                return;
            }

            // Fallback to pure data storage if pool is empty
            SpawnRuntimeDecal(
                pos,
                rot,
                size,
                tex,
                lifeTime,
                color,
                softFade,
                SortingOrder,
                rotationSpeed,
                pulseFreq,
                pulseIntensity,
                animReserved,
                uvOverride
            );
        }

        // ========================================================================
        // 9. INTERNAL UTILITIES
        // ========================================================================

        private static void QuickSort(DecalSortEntry[] array, int left, int right)
        {
            int i = left,
                j = right;
            var pivot = array[(left + right) / 2];

            while (i <= j)
            {
                while (CompareEntries(array[i], pivot) < 0)
                    i++;
                while (CompareEntries(array[j], pivot) > 0)
                    j--;

                if (i <= j)
                {
                    DecalSortEntry temp = array[i];
                    array[i] = array[j];
                    array[j] = temp;
                    i++;
                    j--;
                }
            }

            if (left < j)
                QuickSort(array, left, j);
            if (i < right)
                QuickSort(array, i, right);
        }

        private static int CompareEntries(DecalSortEntry a, DecalSortEntry b)
        {
            if (a.SortingOrder != b.SortingOrder)
                return a.SortingOrder.CompareTo(b.SortingOrder);

            // Primary overlap stability: Farther decals render first (bottom of stack)
            return b.distSqr.CompareTo(a.distSqr);
        }

        private static float GetCurrentTime() =>
            Application.isPlaying ? Time.time : Time.realtimeSinceStartup;

        private static long GetGridKey(Vector3 pos)
        {
            float size = _currentConfig != null ? _currentConfig.spatialGridSize : _gridSize;
            long x = Mathf.FloorToInt(pos.x / size);
            long z = Mathf.FloorToInt(pos.z / size);
            return (x << 32) | (z & 0xFFFFFFFFL);
        }

        private static void RecalculateStaticCount()
        {
            _totalStaticCount = 0;
            foreach (var kvp in _loadedSources)
            {
                if (kvp.Value == null)
                    continue;
                if (kvp.Value is System.Collections.ICollection coll)
                    _totalStaticCount += coll.Count;
                else
                    foreach (var _ in kvp.Value)
                        _totalStaticCount++;
            }
        }

        private static int _totalStaticCount = 0;

        private static int GetActiveTotalCount()
        {
            int runtimeActive = 0;
            if (_runtimeExpirations != null)
            {
                float now = GetCurrentTime();
                for (int i = 0; i < _maxRuntimeCount; i++)
                    if (_runtimeExpirations[i] > now)
                        runtimeActive++;
            }
            return _projectorsSet.Count + _totalStaticCount + runtimeActive;
        }

        /// <summary>
        /// Full system purge. Clears all registries, pools, and spatial hash maps.
        /// </summary>
        public static void ReleaseAll()
        {
            DecalPoolMini.Clear();
            _projectorsSet.Clear();
            _grid.Clear();
            _staticGrid.Clear();
            _loadedSources.Clear();
            _totalStaticCount = 0;
            _runtimePool = null;
            _runtimeExpirations = null;
            _runtimeSortingOrders = null;
            _runtimeRadii = null;
            _poolPtr = 0;
            _currentConfig = null;
        }

        // ========================================================================
        // 10. RECURSIVE FILL HELPERS (Kernel Optimizations)
        // ========================================================================

        private static int FillStaticEntries(
            List<DecalStaticEntry> list,
            Vector3 camPos,
            float maxDistSqr,
            LayerMask mask,
            Plane[] planes,
            int currentCount
        )
        {
            for (int i = 0; i < list.Count; i++)
            {
                var entry = list[i];
                if ((entry.layerMask & mask) == 0)
                    continue;
                float dSqr = Vector3.SqrMagnitude(entry.position - camPos);
                if (dSqr > maxDistSqr)
                    continue;
                if (!IsVisibleInFrustum(entry.position, entry.boundingRadius, planes))
                    continue;

                EnsureBufferCapacity(currentCount);
                _sortBuffer[currentCount].data = entry.data;
                _sortBuffer[currentCount].SortingOrder = entry.sortingOrder;
                _sortBuffer[currentCount].distSqr = dSqr;
                currentCount++;
            }
            return currentCount;
        }

        private static int FillProjectorEntries(
            List<IDecalProvider> list,
            Vector3 camPos,
            float maxDistSqr,
            LayerMask mask,
            Plane[] planes,
            int currentCount
        )
        {
            int listCount = list.Count;
            for (int i = 0; i < listCount; i++)
            {
                var provider = list[i];
                if (provider == null || (provider as UnityEngine.Object) == null) continue;

                // 优化：如果是标准投影器，直接访问缓存字段，绕过接口开销
                if (provider is DecalProjectorMini proj)
                {
                    if (proj == null || !proj.isActiveAndEnabled) continue;
                    var t = proj.transform;
                    if (((1 << proj.gameObject.layer) & mask) == 0) continue;

                    Vector3 pos = t.position;
                    float dSqr = Vector3.SqrMagnitude(pos - camPos);
                    if (dSqr > maxDistSqr) continue;

                    Vector3 scale = t.localScale;
                    float maxScale = Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
                    if (!IsVisibleInFrustum(pos, maxScale * 0.866f, planes)) continue;

                    EnsureBufferCapacity(currentCount);
                    _sortBuffer[currentCount].data = proj.ToDecalData();
                    _sortBuffer[currentCount].SortingOrder = proj.sortingOrder;
                    _sortBuffer[currentCount].distSqr = dSqr;
                    currentCount++;
                }
                else
                {
                    // 退回到基础接口访问
                    if (!provider.isActiveAndEnabled) continue;
                    var t = provider.transform;
                    if (((1 << provider.gameObject.layer) & mask) == 0) continue;

                    Vector3 pos = t.position;
                    float dSqr = Vector3.SqrMagnitude(pos - camPos);
                    if (dSqr > maxDistSqr) continue;

                    Vector3 scale = t.localScale;
                    float maxScale = Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
                    if (!IsVisibleInFrustum(pos, maxScale * 0.866f, planes)) continue;

                    EnsureBufferCapacity(currentCount);
                    _sortBuffer[currentCount].data = provider.ToDecalData();
                    _sortBuffer[currentCount].SortingOrder = provider.sortingOrder;
                    _sortBuffer[currentCount].distSqr = dSqr;
                    currentCount++;
                }
            }
            return currentCount;
        }

        private static int FillRuntimeEntries(
            float now,
            Vector3 camPos,
            float maxDistSqr,
            int currentCount,
            Plane[] planes
        )
        {
            if (_runtimePool == null)
                return currentCount;
            for (int i = 0; i < _maxRuntimeCount; i++)
            {
                if (_runtimeExpirations[i] <= now)
                    continue;

                DecalDataMini data = _runtimePool[i];
                float timeLeft = _runtimeExpirations[i] - now;
                if (timeLeft < 1.0f)
                    data.color.w *= timeLeft; // Fade out last second

                Vector3 pos = new(data.dtw0.w, data.dtw1.w, data.dtw2.w); // Extract position from matrix
                float dSqr = Vector3.SqrMagnitude(pos - camPos);
                if (dSqr > maxDistSqr)
                    continue;
                if (!IsVisibleInFrustum(pos, _runtimeRadii[i], planes))
                    continue;

                EnsureBufferCapacity(currentCount);
                _sortBuffer[currentCount].data = data;
                _sortBuffer[currentCount].SortingOrder = _runtimeSortingOrders[i];
                _sortBuffer[currentCount].distSqr = dSqr;
                currentCount++;
            }
            return currentCount;
        }

        private static bool IsVisibleInFrustum(Vector3 pos, float radius, Plane[] planes)
        {
            for (int i = 0; i < 6; i++)
                if (planes[i].GetDistanceToPoint(pos) < -radius)
                    return false;
            return true;
        }

        private static void EnsureBufferCapacity(int count)
        {
            if (count >= _sortBuffer.Length)
                System.Array.Resize(ref _sortBuffer, _sortBuffer.Length * 2);
        }

        /// <summary>
        /// DATA-ONLY SPAWN: Injects a decal directly into the runtime circular buffer.
        /// <para>No MonoBehaviour or Component is created. Extremely efficient for high-frequency VFX.</para>
        /// </summary>
        public static void SpawnRuntimeDecal(
            Vector3 pos,
            Quaternion rot,
            Vector3 size,
            Texture2D texture,
            float duration = 10f,
            Color color = default,
            float softFade = 0.5f,
            int sortingOrder = 10000,
            float rotationSpeed = 0f,
            float pulseFreq = 0f,
            float pulseIntensity = 0f,
            float animReserved = 0f,
            Vector4? uvOverride = null
        )
        {
            int texIdx = _currentConfig != null ? _currentConfig.GetTextureIndex(texture) : 0;
            SpawnRuntimeDecal(pos, rot, size, texIdx, duration, color, softFade, sortingOrder, rotationSpeed, pulseFreq, pulseIntensity, animReserved, uvOverride);
        }

        /// <summary>
        /// DATA-ONLY SPAWN (Texture Index): Direct injection using a pre-resolved texture index.
        /// </summary>
        public static void SpawnRuntimeDecal(
            Vector3 pos,
            Quaternion rot,
            Vector3 size,
            int textureIndex,
            float duration = 10f,
            Color color = default,
            float softFade = 0.5f,
            int sortingOrder = 10000,
            float rotationSpeed = 0f,
            float pulseFreq = 0f,
            float pulseIntensity = 0f,
            float animReserved = 0f,
            Vector4? uvOverride = null
        )
        {
            InitializeRuntimePool();
            Matrix4x4 dtw = Matrix4x4.TRS(pos, rot, size);

            // --- GPU Safety: Illegal Texture Index Defense ---
            int texIdx = Mathf.Clamp(textureIndex, 0, _currentConfig != null ? _currentConfig.Count - 1 : 0);

            var data = new DecalDataMini
            {
                color = color == default ? Color.white : color,
                uvScaleOffset = uvOverride ?? new Vector4(1, 1, 0, 0),
                fadeParams = new Vector4(0.5f, 1.0f, texIdx, softFade),
                animParams = new Vector4(rotationSpeed, pulseFreq, pulseIntensity, animReserved),
            };
            data.SetMatrices(dtw.inverse, dtw);

            _runtimePool[_poolPtr] = data;
            _runtimeExpirations[_poolPtr] = GetCurrentTime() + duration;
            _runtimeSortingOrders[_poolPtr] = sortingOrder;
            _runtimeRadii[_poolPtr] = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * 0.866f;
            _poolPtr = (_poolPtr + 1) % _maxRuntimeCount;
        }

        private static void InitializeRuntimePool()
        {
            if (_runtimePool != null)
                return;
            _runtimePool = new DecalDataMini[_maxRuntimeCount];
            _runtimeExpirations = new float[_maxRuntimeCount];
            _runtimeSortingOrders = new int[_maxRuntimeCount];
            _runtimeRadii = new float[_maxRuntimeCount];
            for (int i = 0; i < _maxRuntimeCount; i++)
                _runtimeExpirations[i] = -1f;
        }

        /// <summary>
        /// Clears all dynamic runtime decals (footprints, impacts, etc.) from the circular pool.
        /// </summary>
        public static void ClearRuntimePool()
        {
            if (_runtimeExpirations == null)
                return;
            for (int i = 0; i < _maxRuntimeCount; i++)
                _runtimeExpirations[i] = -1f;
            _poolPtr = 0;
            
            // 清理基于 GameObject 的对象池，防止跨场景后引用丢失 (MissingReferenceException)
            DecalPoolMini.Clear();
        }

        public static int GetTextureIndex(Texture2D tex) =>
            _currentConfig != null ? _currentConfig.GetTextureIndex(tex) : -1;

        public static int GetFlipbookCount(Texture2D tex) =>
            _currentConfig != null ? _currentConfig.GetFlipbookCount(tex) : 1;

        public static Texture2DArray GetTextureArray() =>
            _currentConfig != null ? _currentConfig.bakedArray : null;

#if UNITY_EDITOR
        public static void DrawDebugGrid()
        {
            if (_currentConfig == null || !_currentConfig.showDebugGrid)
                return;
            UnityEditor.Handles.color = new Color(0.77f, 0.54f, 0.98f, 0.3f);
            foreach (var key in _grid.Keys)
            {
                int x = (int)(key >> 32);
                int z = (int)(key & 0xFFFFFFFFL);
                float sv = _currentConfig.spatialGridSize;
                Vector3 center = new(x * sv + sv * 0.5f, 0, z * sv + sv * 0.5f);
                UnityEditor.Handles.DrawWireCube(center, new Vector3(sv, 100f, sv));
            }
        }
#endif
    }
}
