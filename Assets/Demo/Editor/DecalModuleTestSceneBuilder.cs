using DecalMini;
using ModularDemo.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ModularDemo.Editor
{
    public static class DecalModuleTestSceneBuilder
    {
        private const string ScenePath = "Assets/Demo/Scenes/DecalModuleTest.unity";
        private const string AtlasPath = "Assets/Demo/Data/DecalAtlasConfig.asset";

        [MenuItem("Tools/Decal System/Demo/Create Module Test Scene")]
        public static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolder("Assets/Demo", "Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "DecalModuleTest";

            DecalAtlasConfigMini atlas = AssetDatabase.LoadAssetAtPath<DecalAtlasConfigMini>(AtlasPath);
            Texture2D gridTex = GetTexture(atlas, 0);
            Texture2D markTex = GetTexture(atlas, 1);
            Texture2D iconTex = GetTexture(atlas, 2);
            Texture2D portalTex = GetTexture(atlas, 3);
            Texture2D spawnTex = GetTexture(atlas, 4);

            var root = new GameObject("Decal Module Test Scene");
            CreateLighting();
            CreateCamera();

            Material floorMaterial = CreateMaterial("Test Floor", new Color(0.22f, 0.24f, 0.25f));
            Material wallMaterial = CreateMaterial("Test Wall", new Color(0.34f, 0.35f, 0.36f));
            Material accentMaterial = CreateMaterial("Test Accent", new Color(0.2f, 0.36f, 0.5f));
            Material bulletMaterial = CreateMaterial("Test Bullet", new Color(0.9f, 0.72f, 0.28f));

            CreatePrimitive("Main Test Floor", PrimitiveType.Cube, new Vector3(0f, -0.05f, 0f), new Vector3(18f, 0.1f, 14f), floorMaterial, root.transform);
            CreatePrimitive("Static Projector Wall", PrimitiveType.Cube, new Vector3(-6f, 1.5f, 1f), new Vector3(0.2f, 3f, 4f), wallMaterial, root.transform);
            CreatePrimitive("Bullet Impact Wall", PrimitiveType.Cube, new Vector3(6f, 1.5f, 1f), new Vector3(0.2f, 3f, 4f), wallMaterial, root.transform);
            CreatePrimitive("Aura Platform", PrimitiveType.Cylinder, new Vector3(0f, 0.08f, -4f), new Vector3(2.2f, 0.16f, 2.2f), accentMaterial, root.transform);
            CreatePrimitive("Runtime Data Pad", PrimitiveType.Cube, new Vector3(0f, 0.01f, 4.5f), new Vector3(4.5f, 0.03f, 3f), accentMaterial, root.transform);

            CreateLabel("Static Projector", new Vector3(-6f, 3.25f, 1f), root.transform);
            CreateLabel("Aura Component", new Vector3(0f, 1.75f, -4f), root.transform);
            CreateLabel("Footprints", new Vector3(-3.5f, 0.25f, -1.5f), root.transform);
            CreateLabel("Tracks", new Vector3(3.5f, 0.25f, -1.5f), root.transform);
            CreateLabel("Bullet Impact", new Vector3(6f, 3.25f, 1f), root.transform);
            CreateLabel("Runtime Data Burst", new Vector3(0f, 0.35f, 4.5f), root.transform);

            CreateStaticProjector(markTex, root.transform);
            CreateAuraTest(portalTex ?? markTex, root.transform);
            CreateFootprintTest(markTex ?? gridTex, iconTex ?? markTex, root.transform);
            CreateTrackTest(spawnTex ?? gridTex, root.transform);

            GameObject bulletTemplate = CreateBulletTemplate(markTex ?? gridTex, bulletMaterial, root.transform);
            Transform muzzle = CreateMarker("Bullet Muzzle", new Vector3(3.4f, 1.25f, 1f), root.transform);
            Transform target = CreateMarker("Bullet Target", new Vector3(7.1f, 1.25f, 1f), root.transform);
            Transform runtimeCenter = CreateMarker("Runtime Data Center", new Vector3(0f, 0.08f, 4.5f), root.transform);

            var driver = new GameObject("Decal Module Test Driver");
            driver.transform.SetParent(root.transform);
            var sceneDriver = driver.AddComponent<DecalModuleTestSceneDriver>();
            ConfigureDriver(sceneDriver, atlas, bulletTemplate, muzzle, target, runtimeCenter, gridTex ?? markTex);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log($"[DecalModuleTestSceneBuilder] Created {ScenePath}");
        }

        private static void CreateLighting()
        {
            var lightGo = new GameObject("Directional Light");
            lightGo.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
        }

        private static void CreateCamera()
        {
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(0f, 8f, -10f);
            cameraGo.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 45f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 80f;
            cameraGo.AddComponent<AudioListener>();
        }

        private static void CreateStaticProjector(Texture2D texture, Transform parent)
        {
            if (texture == null)
                return;

            var projectorGo = new GameObject("Static Projector - Wall Decal");
            projectorGo.transform.SetParent(parent);
            projectorGo.transform.SetPositionAndRotation(new Vector3(-5.88f, 1.45f, 1f), AlignToSurface(Vector3.right, Vector3.up));
            projectorGo.transform.localScale = new Vector3(1.35f, 1.35f, 0.35f);
            var projector = projectorGo.AddComponent<DecalProjectorMini>();
            projector.decalTexture = texture;
            projector.color = new Color(1f, 0.86f, 0.45f, 1f);
            projector.sortingOrder = 10;
        }

        private static void CreateAuraTest(Texture2D texture, Transform parent)
        {
            if (texture == null)
                return;

            var auraGo = new GameObject("Aura Component Probe");
            auraGo.transform.SetParent(parent);
            auraGo.transform.SetPositionAndRotation(new Vector3(0f, 0.16f, -4f), AlignToSurface(Vector3.up, Vector3.forward));
            auraGo.transform.localScale = new Vector3(2.4f, 2.4f, 0.45f);
            var aura = auraGo.AddComponent<DecalAuraComponent>();
            aura.auraModule.auraTexture = texture;
            aura.auraModule.layerR.color = new Color(0.2f, 0.7f, 1f, 0.95f);
            aura.auraModule.layerG.color = new Color(0.4f, 1f, 0.55f, 0.75f);
            aura.auraModule.layerB.color = new Color(1f, 0.55f, 0.25f, 0.65f);
            aura.auraModule.layerR.rotationSpeed = 15f;
            aura.auraModule.layerG.rotationSpeed = -8f;
            aura.auraModule.layerB.pulseSpeed = 1.5f;
            aura.lockRotation = true;
            aura.autoSnapToSocket = false;
        }

        private static void CreateFootprintTest(Texture2D leftTexture, Texture2D rightTexture, Transform parent)
        {
            var walker = CreatePrimitive("Footprint Walker", PrimitiveType.Capsule, new Vector3(-3.5f, 0.55f, -1.5f), new Vector3(0.35f, 0.55f, 0.35f), CreateMaterial("Footprint Walker", new Color(0.45f, 0.72f, 0.92f)), parent);
            var mover = walker.AddComponent<DecalModuleTestMover>();
            ConfigureMover(mover, Vector3.forward, 4.5f, 1.2f);

            var footprint = walker.AddComponent<DecalFootprintComponent>();
            footprint.triggerMode = DecalFootprintComponent.TriggerMode.Distance;
            footprint.minStepDistance = 0.55f;
            footprint.footprintModule = new DecalFootprintModule
            {
                mode = FootprintMode.Step,
                leftFootTex = leftTexture,
                rightFootTex = rightTexture != null ? rightTexture : leftTexture,
                footprintSize = 0.38f,
                stepSideOffset = 0.18f,
                lifeTime = 12f,
                tintColor = new Color(0.75f, 0.95f, 1f, 1f),
                softFade = 0.4f,
                groundLayer = -1,
            };
        }

        private static void CreateTrackTest(Texture2D texture, Transform parent)
        {
            var cart = CreatePrimitive("Track Cart", PrimitiveType.Cube, new Vector3(3.5f, 0.35f, -1.5f), new Vector3(0.65f, 0.35f, 0.85f), CreateMaterial("Track Cart", new Color(0.8f, 0.55f, 0.32f)), parent);
            var mover = cart.AddComponent<DecalModuleTestMover>();
            ConfigureMover(mover, Vector3.forward, 4.5f, 0.95f);

            var footprint = cart.AddComponent<DecalFootprintComponent>();
            footprint.triggerMode = DecalFootprintComponent.TriggerMode.Distance;
            footprint.minStepDistance = 0.25f;
            footprint.footprintModule = new DecalFootprintModule
            {
                mode = FootprintMode.Track,
                trackTexture = texture,
                trackWidth = 0.35f,
                trackLength = 0.45f,
                sampleInterval = 0.22f,
                tilingSize = 0.8f,
                wheelCount = 2,
                wheelSpacing = 0.7f,
                lifeTime = 14f,
                tintColor = new Color(1f, 0.8f, 0.55f, 1f),
                softFade = 0.35f,
                groundLayer = -1,
            };
        }

        private static GameObject CreateBulletTemplate(Texture2D texture, Material material, Transform parent)
        {
            var bullet = new GameObject("Bullet Hitscan Template");
            bullet.transform.SetParent(parent);
            bullet.transform.position = new Vector3(3.4f, 1.25f, 1f);
            bullet.SetActive(false);

            var line = bullet.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = 0.025f;
            line.endWidth = 0.002f;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.SetPosition(0, bullet.transform.position);
            line.SetPosition(1, bullet.transform.position + Vector3.right);

            var impact = bullet.AddComponent<DecalBulletImpactComponent>();
            var impactModule = new DecalImpactModule
            {
                texture = texture,
                color = new Color(1f, 0.85f, 0.35f, 1f),
                sizeRange = new Vector2(0.18f, 0.34f),
                projectionDepth = 0.45f,
                lifetime = 16f,
                softFade = 0.25f,
                sortingOrder = DecalImpactModule.DefaultImpactSortingOrder,
                allowedLayers = -1,
            };
            impact.Configure(
                impactModule,
                DecalBulletImpactComponent.TriggerMode.Scripting,
                -1,
                true,
                false,
                0.2f
            );
            return bullet;
        }

        private static void ConfigureDriver(
            DecalModuleTestSceneDriver driver,
            DecalAtlasConfigMini atlas,
            GameObject bulletTemplate,
            Transform muzzle,
            Transform target,
            Transform runtimeCenter,
            Texture2D runtimeTexture
        )
        {
            var serialized = new SerializedObject(driver);
            serialized.FindProperty("atlasConfig").objectReferenceValue = atlas;
            serialized.FindProperty("bulletTemplate").objectReferenceValue = bulletTemplate;
            serialized.FindProperty("bulletMuzzle").objectReferenceValue = muzzle;
            serialized.FindProperty("bulletTarget").objectReferenceValue = target;
            serialized.FindProperty("tracerLifetime").floatValue = 0.075f;
            serialized.FindProperty("runtimeTexture").objectReferenceValue = runtimeTexture;
            serialized.FindProperty("runtimeAreaCenter").objectReferenceValue = runtimeCenter;
            serialized.FindProperty("runtimeAreaSize").vector2Value = new Vector2(4f, 2.6f);
            serialized.FindProperty("baseSpreadAngle").floatValue = 0.35f;
            serialized.FindProperty("recoilPerShot").floatValue = 0.45f;
            serialized.FindProperty("maxRecoilAngle").floatValue = 3.5f;
            serialized.FindProperty("recoilRecoverySpeed").floatValue = 4.5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureMover(DecalModuleTestMover mover, Vector3 axis, float distance, float speed)
        {
            var serialized = new SerializedObject(mover);
            serialized.FindProperty("localAxis").vector3Value = axis;
            serialized.FindProperty("distance").floatValue = distance;
            serialized.FindProperty("speed").floatValue = speed;
            serialized.FindProperty("rotateAlongPath").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreatePrimitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = position;
            go.transform.localScale = scale;
            if (material != null)
                go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private static Transform CreateMarker(string name, Vector3 position, Transform parent)
        {
            var marker = new GameObject(name);
            marker.transform.SetParent(parent);
            marker.transform.position = position;
            return marker.transform;
        }

        private static void CreateLabel(string text, Vector3 position, Transform parent)
        {
            var label = new GameObject("Label - " + text);
            label.transform.SetParent(parent);
            label.transform.position = position;
            label.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
            var mesh = label.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.fontSize = 32;
            mesh.characterSize = 0.08f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = new Color(0.85f, 0.9f, 1f, 1f);
        }

        private static Material CreateMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            var material = new Material(shader)
            {
                name = name,
                color = color,
            };
            return material;
        }

        private static Quaternion AlignToSurface(Vector3 normal, Vector3 tangent)
        {
            return Quaternion.LookRotation(-normal.normalized, tangent.normalized);
        }

        private static Texture2D GetTexture(DecalAtlasConfigMini atlas, int index)
        {
            if (atlas == null || atlas.slices == null || atlas.slices.Count == 0)
                return null;

            int safeIndex = Mathf.Clamp(index, 0, atlas.slices.Count - 1);
            return atlas.slices[safeIndex].albedoMap;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
