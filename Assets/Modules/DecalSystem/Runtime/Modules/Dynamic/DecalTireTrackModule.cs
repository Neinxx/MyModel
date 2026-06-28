using System;
using System.Collections.Generic;
using UnityEngine;

namespace DecalMini
{
    [Serializable]
    public class DecalTireTrackModule
    {
        public const int DefaultTrackSortingOrder = 9000;

        [Header("Visual")]
        [HideInInspector]
        public Texture2D texture;
        public Material material;
        public Color color = Color.white;
        public float trackWidth = 0.32f;
        public float textureRepeatMeters = 1.2f;
        public int sortingOrder = DefaultTrackSortingOrder;

        [Header("Sampling")]
        [Range(1, 4)] public int wheelCount = 2;
        public float wheelSpacing = 1.45f;
        public float sampleDistance = 0.32f;
        public float minSpeed = 0.08f;
        public LayerMask groundLayer = -1;
        public float raycastDistance = 1.2f;
        public float normalOffset = 0.012f;

        [Header("Lifetime")]
        public float lifetime = 10f;
        public float fadeDuration = 2f;
        [Range(16, 512)] public int maxPointsPerWheel = 192;

        private readonly List<TrackPoint>[] _points =
        {
            new(),
            new(),
            new(),
            new()
        };

        private Vector3 _lastEmitterPosition;
        private bool _hasEmitterPosition;

        public void ResetRuntime()
        {
            for (int i = 0; i < _points.Length; i++)
                _points[i].Clear();

            _lastEmitterPosition = Vector3.zero;
            _hasEmitterPosition = false;
        }

        public bool Tick(
            Transform emitter,
            Transform[] wheelAnchors,
            DecalTireTrackRenderer renderer,
            float deltaTime
        )
        {
            if (emitter == null || renderer == null || texture == null)
                return false;

            bool changed = RemoveExpiredPoints(Time.time);

            float speed = EstimateSpeed(emitter.position, deltaTime);
            if (speed >= Mathf.Max(0.001f, minSpeed))
            {
                int activeWheelCount = GetActiveWheelCount(wheelAnchors);
                for (int wheelIndex = 0; wheelIndex < activeWheelCount; wheelIndex++)
                {
                    if (TrySampleWheel(emitter, wheelAnchors, wheelIndex, out TrackPoint point))
                        changed |= AppendPoint(wheelIndex, point);
                }
            }

            renderer.SetMaterial(material, texture, color, sortingOrder);
            renderer.UpdateMesh(
                _points,
                GetActiveWheelCount(wheelAnchors),
                color,
                Mathf.Max(0.001f, trackWidth),
                Mathf.Max(0.001f, textureRepeatMeters),
                Time.time,
                lifetime,
                fadeDuration
            );
            return changed;
        }

        public void DrawGizmos(Transform emitter, Transform[] wheelAnchors)
        {
            if (emitter == null)
                return;

            Color previous = Gizmos.color;
            Gizmos.color = DecalGizmoUtility.ChampagneGold;

            int activeWheelCount = GetActiveWheelCount(wheelAnchors);
            for (int i = 0; i < activeWheelCount; i++)
            {
                Vector3 position = GetWheelOrigin(emitter, wheelAnchors, i);
                Vector3 right = GetEmitterRight(emitter);
                float halfWidth = Mathf.Max(0.01f, trackWidth) * 0.5f;
                Gizmos.DrawLine(position - right * halfWidth, position + right * halfWidth);
                Gizmos.DrawWireSphere(position, 0.045f);
            }

            Gizmos.color = previous;
        }

        private bool TrySampleWheel(
            Transform emitter,
            Transform[] wheelAnchors,
            int wheelIndex,
            out TrackPoint point
        )
        {
            point = default;
            Vector3 origin = GetWheelOrigin(emitter, wheelAnchors, wheelIndex) + Vector3.up * 0.35f;

            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, Mathf.Max(0.01f, raycastDistance), groundLayer))
                return false;

            Vector3 forward = Vector3.ProjectOnPlane(emitter.forward, hit.normal);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.ProjectOnPlane(emitter.up, hit.normal);
            if (forward.sqrMagnitude < 0.0001f)
                return false;

            forward.Normalize();
            Vector3 right = Vector3.Cross(hit.normal, forward).normalized;
            if (right.sqrMagnitude < 0.0001f)
                return false;

            point = new TrackPoint
            {
                position = hit.point + hit.normal * Mathf.Max(0f, normalOffset),
                normal = hit.normal,
                right = right,
                createdAt = Time.time,
                distance = 0f
            };

            return true;
        }

        private bool AppendPoint(int wheelIndex, TrackPoint point)
        {
            List<TrackPoint> wheelPoints = _points[Mathf.Clamp(wheelIndex, 0, _points.Length - 1)];
            if (wheelPoints.Count > 0)
            {
                TrackPoint lastPoint = wheelPoints[wheelPoints.Count - 1];
                float distance = Vector3.Distance(point.position, lastPoint.position);
                if (distance < Mathf.Max(0.01f, sampleDistance))
                    return false;

                point.distance = lastPoint.distance + distance;
            }

            wheelPoints.Add(point);

            int maxCount = Mathf.Clamp(maxPointsPerWheel, 2, 512);
            while (wheelPoints.Count > maxCount)
                wheelPoints.RemoveAt(0);

            return true;
        }

        private bool RemoveExpiredPoints(float now)
        {
            float safeLifetime = Mathf.Max(0.01f, lifetime);
            bool changed = false;

            for (int wheelIndex = 0; wheelIndex < _points.Length; wheelIndex++)
            {
                List<TrackPoint> wheelPoints = _points[wheelIndex];
                int removeCount = 0;

                while (removeCount < wheelPoints.Count && now - wheelPoints[removeCount].createdAt >= safeLifetime)
                    removeCount++;

                if (removeCount <= 0)
                    continue;

                wheelPoints.RemoveRange(0, removeCount);
                changed = true;
            }

            return changed;
        }

        private int GetActiveWheelCount(Transform[] wheelAnchors)
        {
            if (wheelAnchors != null && wheelAnchors.Length > 0)
                return Mathf.Clamp(wheelAnchors.Length, 1, 4);

            return Mathf.Clamp(wheelCount, 1, 4);
        }

        private Vector3 GetWheelOrigin(Transform emitter, Transform[] wheelAnchors, int wheelIndex)
        {
            if (wheelAnchors != null && wheelIndex < wheelAnchors.Length && wheelAnchors[wheelIndex] != null)
                return wheelAnchors[wheelIndex].position;

            int activeWheelCount = Mathf.Clamp(wheelCount, 1, 4);
            float offset = activeWheelCount > 1
                ? (wheelIndex / (float)(activeWheelCount - 1) - 0.5f) * wheelSpacing
                : 0f;

            return emitter.position + GetEmitterRight(emitter) * offset;
        }

        private Vector3 GetEmitterRight(Transform emitter)
        {
            Vector3 right = emitter.right;
            if (right.sqrMagnitude < 0.0001f)
                right = Vector3.right;

            return right.normalized;
        }

        private float EstimateSpeed(Vector3 emitterPosition, float deltaTime)
        {
            if (!_hasEmitterPosition)
            {
                _lastEmitterPosition = emitterPosition;
                _hasEmitterPosition = true;
                return 0f;
            }

            float speed = Vector3.Distance(emitterPosition, _lastEmitterPosition) / Mathf.Max(0.0001f, deltaTime);
            _lastEmitterPosition = emitterPosition;
            return speed;
        }

        public struct TrackPoint
        {
            public Vector3 position;
            public Vector3 normal;
            public Vector3 right;
            public float createdAt;
            public float distance;
        }
    }
}
