using CharacterController.Runtime;
using UnityEngine;
using PlayerState.Runtime;
using DecalMini;
using DecalMini.Runtime.Modules.Dynamic;

namespace ModularDemo.Runtime
{
    [CreateAssetMenu(fileName = "DemoAura", menuName = "Modular Demo/Aura Feature")]
    public class DemoAuraFeature : DemoDecalFeature
    {
        [Header("Profile Configuration")]
        public DecalAuraModule auraModule = new();

        [Header("Transform")]
        public float radius = 2.0f;
        public float projectionDepth = 1.5f;

        [Header("Character Socket")]
        public CharacterSocketId socketId = CharacterSocketId.Aura;
        public bool autoSnapToSocket = true;

        [Header("Orientation & Ground Alignment")]
        public bool lockRotation = true;
        public bool stickToGround = true;
        public LayerMask groundLayer = -1;
        public float heightOffset = 0.05f;
        public float raycastDistance = 5.0f;

        public override void Apply(GameObject target)
        {
            var socketRegistry = ResolveSocketRegistry(target);
            Transform mountPoint = target.transform;
            if (socketRegistry != null && socketRegistry.TryGet(socketId, out var socket))
                mountPoint = socket;

            // 🌟 极致 O(1) 零内存分配重用逻辑 🌟
            // 优先查找 mountPoint 上是否已存在现有的 DecalAuraComponent 组件。
            // 如果存在，我们直接覆写配置参数，完全避免了 Destroy 和 AddComponent 的 GC 与 CPU 开销！
            var aura = mountPoint.GetComponent<DecalAuraComponent>();
            if (aura == null)
            {
                aura = mountPoint.gameObject.AddComponent<DecalAuraComponent>();
            }

            // 深度克隆数据，防止 ScriptableObject 数据在运行时被意外污染
            aura.auraModule = DeepCopyModule(auraModule);
            aura.radius = radius;
            aura.projectionDepth = projectionDepth;
            aura.socketId = socketId;
            aura.autoSnapToSocket = autoSnapToSocket;
            aura.lockRotation = lockRotation;
            aura.stickToGround = stickToGround;
            aura.groundLayer = groundLayer;
            aura.heightOffset = heightOffset;
            aura.raycastDistance = raycastDistance;

            // 强制重新物理对齐并标记为 Dirty，使新参数瞬间同步上传至 GPU
            aura.RefreshAnchorLink();
        }

        public override void Remove(GameObject target)
        {
            var root = ResolveSocketRegistry(target)?.gameObject ?? target;
            var auras = root.GetComponentsInChildren<DecalAuraComponent>(true);
            foreach (var aura in auras)
            {
                if (Application.isPlaying)
                    Destroy(aura);
                else
                    DestroyImmediate(aura);
            }
        }

        private static CharacterSocketRegistry ResolveSocketRegistry(GameObject target)
        {
            if (target == null)
                return null;

            var registry = target.GetComponentInParent<CharacterSocketRegistry>();
            if (registry != null)
                return registry;

            return target.GetComponentInChildren<CharacterSocketRegistry>(true);
        }

        /// <summary>
        /// 深拷贝 DecalAuraModule，防止 SO 资产数据被运行时引用污染。
        /// </summary>
        private static DecalAuraModule DeepCopyModule(DecalAuraModule src)
        {
            if (src == null)
                return new DecalAuraModule();

            var copy = new DecalAuraModule
            {
                auraTexture = src.auraTexture,
                softFade = src.softFade,
                pulseIntensity = src.pulseIntensity,
                layerR = CopyLayer(src.layerR),
                layerG = CopyLayer(src.layerG),
                layerB = CopyLayer(src.layerB),
                layerA = CopyLayer(src.layerA)
            };

            return copy;
        }

        private static DecalAuraLayer CopyLayer(DecalAuraLayer src)
        {
            if (src == null)
                return new DecalAuraLayer();

            return new DecalAuraLayer
            {
                name = src.name,
                active = src.active,
                color = src.color,
                scale = src.scale,
                rotationSpeed = src.rotationSpeed,
                pulseSpeed = src.pulseSpeed
            };
        }
    }
}
