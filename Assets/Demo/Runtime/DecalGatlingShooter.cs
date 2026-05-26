using UnityEngine;
using DecalMini;

namespace ModularDemo.Runtime
{
    /// <summary>
    /// 加特林射击系统 (Demo Component)
    /// 模拟高速射击并在表面留下贴花。
    /// </summary>
    public class DecalGatlingShooter : MonoBehaviour
    {
        [Header("Bullet Settings")]
        public Texture2D bulletDecal;
        public float bulletSize = 0.2f;
        public float shootRate = 0.1f;
        public float bulletLifeTime = 5f;
        
        [Header("VFX")]
        public Color bulletColor = Color.white;

        public IDecalRuntime DecalRuntime { get; set; } = DecalMini.DecalRuntime.Shared;

        private float _nextShootTime;

        private void Update()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed && Time.time > _nextShootTime)
            {
                Shoot();
                _nextShootTime = Time.time + shootRate;
            }
        }

        public void Shoot()
        {
            Ray ray = new Ray(transform.position, transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                // 计算贴花旋转（对齐法线）
                Quaternion rot = Quaternion.LookRotation(-hit.normal);
                
                // 注入内核：高性能 0-Component 产生方式
                DecalRuntime.SpawnRuntimeDecal(
                    hit.point + hit.normal * 0.01f, 
                    rot, 
                    Vector3.one * bulletSize, 
                    bulletDecal, 
                    bulletLifeTime, 
                    bulletColor
                );
            }
        }
    }
}
