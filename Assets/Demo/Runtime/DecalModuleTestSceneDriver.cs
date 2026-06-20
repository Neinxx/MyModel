using DecalMini;
using UnityEngine;

namespace ModularDemo.Runtime
{
    public sealed class DecalModuleTestSceneDriver : MonoBehaviour
    {
        [Header("System")]
        [SerializeField] private DecalAtlasConfigMini atlasConfig;

        [Header("Bullet Impact")]
        [SerializeField] private GameObject bulletTemplate;
        [SerializeField] private Transform bulletMuzzle;
        [SerializeField] private Transform bulletTarget;
        [SerializeField] private float bulletRange = 80f;
        [SerializeField] private float bulletInterval = 0.45f;
        [SerializeField] private float tracerLifetime = 0.075f;
        [SerializeField] private float baseSpreadAngle = 0.35f;
        [SerializeField] private float recoilPerShot = 0.45f;
        [SerializeField] private float maxRecoilAngle = 3.5f;
        [SerializeField] private float recoilRecoverySpeed = 4.5f;

        [Header("Runtime Data Decals")]
        [SerializeField] private Texture2D runtimeTexture;
        [SerializeField] private Transform runtimeAreaCenter;
        [SerializeField] private Vector2 runtimeAreaSize = new(4f, 3f);
        [SerializeField] private float runtimeSpawnInterval = 0.35f;

        private float _nextBulletTime;
        private float _nextRuntimeDecalTime;
        private float _recoilAngle;

        private void Awake()
        {
            if (atlasConfig != null)
                DecalSystemMini.SetAtlasConfig(atlasConfig);

            if (bulletTemplate != null)
                bulletTemplate.SetActive(false);
        }

        private void Update()
        {
            RecoverBulletRecoil();
            TickBulletImpactTest();
            TickRuntimeDataTest();
        }

        private void RecoverBulletRecoil()
        {
            _recoilAngle = Mathf.MoveTowards(_recoilAngle, 0f, recoilRecoverySpeed * Time.deltaTime);
        }

        private void TickBulletImpactTest()
        {
            if (bulletTemplate == null || bulletMuzzle == null || bulletTarget == null)
                return;
            if (Time.time < _nextBulletTime)
                return;

            _nextBulletTime = Time.time + Mathf.Max(0.05f, bulletInterval);
            Vector3 direction = CalculateBulletDirection((bulletTarget.position - bulletMuzzle.position).normalized);
            var bullet = Instantiate(bulletTemplate, bulletMuzzle.position, Quaternion.LookRotation(direction));
            bullet.name = "Runtime Hitscan Bullet Probe";
            bullet.SetActive(true);

            Vector3 endPosition = bulletMuzzle.position + direction * Mathf.Max(0.01f, bulletRange);
            if (bullet.TryGetComponent<DecalBulletImpactComponent>(out var impact))
            {
                if (impact.FireHitscan(bulletMuzzle.position, direction, bulletRange, out RaycastHit hit))
                    endPosition = hit.point;
            }

            DrawTracer(bullet, bulletMuzzle.position, endPosition);
            _recoilAngle = Mathf.Min(maxRecoilAngle, _recoilAngle + Mathf.Max(0f, recoilPerShot));
            Destroy(bullet, Mathf.Max(0.01f, tracerLifetime));
        }

        private Vector3 CalculateBulletDirection(Vector3 baseDirection)
        {
            if (baseDirection.sqrMagnitude <= 0.0001f)
                return transform.forward;

            Vector3 direction = baseDirection.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, direction);
            if (right.sqrMagnitude <= 0.0001f)
                right = Vector3.Cross(Vector3.forward, direction);
            right.Normalize();

            Vector3 up = Vector3.Cross(direction, right).normalized;
            float spreadAngle = Mathf.Max(0f, baseSpreadAngle + _recoilAngle);
            Vector2 spread = Random.insideUnitCircle * spreadAngle;

            Quaternion recoilRotation = Quaternion.AngleAxis(-_recoilAngle, right);
            Quaternion spreadRotation =
                Quaternion.AngleAxis(spread.x, up) *
                Quaternion.AngleAxis(-spread.y, right);

            return (spreadRotation * recoilRotation * direction).normalized;
        }

        private static void DrawTracer(GameObject bullet, Vector3 start, Vector3 end)
        {
            if (!bullet.TryGetComponent<LineRenderer>(out var line))
            {
                line = bullet.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.startWidth = 0.025f;
                line.endWidth = 0.002f;
                line.numCapVertices = 2;
                if (bullet.TryGetComponent<MeshRenderer>(out var renderer))
                {
                    line.sharedMaterial = renderer.sharedMaterial;
                    renderer.enabled = false;
                }
            }

            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }

        private void TickRuntimeDataTest()
        {
            if (runtimeTexture == null || runtimeAreaCenter == null)
                return;
            if (Time.time < _nextRuntimeDecalTime)
                return;

            _nextRuntimeDecalTime = Time.time + Mathf.Max(0.05f, runtimeSpawnInterval);
            Vector3 randomOffset = new(
                Random.Range(-runtimeAreaSize.x * 0.5f, runtimeAreaSize.x * 0.5f),
                0f,
                Random.Range(-runtimeAreaSize.y * 0.5f, runtimeAreaSize.y * 0.5f)
            );

            Quaternion rotation = Quaternion.LookRotation(-Vector3.up, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward);
            DecalSystemMini.SpawnRuntimeDecal(
                runtimeAreaCenter.position + randomOffset + Vector3.up * 0.01f,
                rotation,
                Vector3.one * Random.Range(0.25f, 0.55f),
                runtimeTexture,
                8f,
                new Color(0.85f, 1f, 1f, 0.9f),
                0.35f,
                12000
            );
        }
    }
}
