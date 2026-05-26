using UnityEditor;
using UnityEngine;

namespace DecalMini
{
    /// <summary>
    /// 极致火焰生成工具：一键构建符合移动端性能的 HDR 火焰粒子
    /// </summary>
    public class DecalFireGeneratorMini : EditorWindow
    {
        [MenuItem("Tools/Decal System/Fire Effect Generator")]
        public static void ShowWindow()
        {
            GetWindow<DecalFireGeneratorMini>("Fire Generator");
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            GUILayout.Label("极致火焰粒子生成器", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("点击下方按钮将在场景中创建一个预配置的高性能火焰粒子系统，完美适配火焰足迹。", MessageType.Info);

            if (GUILayout.Button("创建经典火焰 (Classic Fire)", GUILayout.Height(40)))
            {
                CreateFire(false);
            }

            if (GUILayout.Button("创建 HDR 余烬 (HDR Embers)", GUILayout.Height(40)))
            {
                CreateFire(true);
            }
        }

        private void CreateFire(bool isEmbers)
        {
            GameObject go = new GameObject(isEmbers ? "HDR_Embers_Effect" : "Classic_Fire_Effect");
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            
            // 1. 核心模块配置
            var main = ps.main;
            main.duration = 1.0f;
            main.startLifetime = isEmbers ? new ParticleSystem.MinMaxCurve(0.5f, 1.2f) : new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = isEmbers ? new ParticleSystem.MinMaxCurve(0.5f, 1.5f) : new ParticleSystem.MinMaxCurve(2.0f, 4.0f);
            main.startSize = isEmbers ? new ParticleSystem.MinMaxCurve(0.05f, 0.15f) : new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.simulationSpace = ParticleSystemSimulationSpace.World; // 世界空间更自然

            // 2. 发射模块
            var emission = ps.emission;
            emission.rateOverTime = isEmbers ? 50 : 30;

            // 3. 形状模块 (锥形)
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 0.1f;

            // 4. 生命周期颜色 (关键：HDR 效果)
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            if (isEmbers)
            {
                grad.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(new Color(2f, 0.5f, 0f), 0.0f), new GradientColorKey(Color.red, 0.5f), new GradientColorKey(Color.black, 1.0f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 0.7f), new GradientAlphaKey(0.0f, 1.0f) }
                );
            }
            else
            {
                grad.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(Color.yellow, 0.0f), new GradientColorKey(new Color(1f, 0.3f, 0f), 0.4f), new GradientColorKey(Color.black, 1.0f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(1.0f, 0.2f), new GradientAlphaKey(0.0f, 1.0f) }
                );
            }
            colorOverLifetime.color = grad;

            // 5. 生命周期尺寸
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.0f, 0.2f);
            curve.AddKey(0.3f, 1.0f);
            curve.AddKey(1.0f, 0.0f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, curve);

            // 6. 渲染器配置
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            // 尝试使用内置材质 (防止紫屏)
            Material particleMat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
            if (particleMat != null) renderer.material = particleMat;

            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Create Fire Effect");
            Debug.Log($"<color=orange><b>[Fire Generator]</b></color> 火焰特效已生成：{go.name}");
        }
    }
}
