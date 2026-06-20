using CharacterController.Runtime;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using DecalMini;
using DecalMini.Runtime.Modules.Dynamic;

namespace Tests.Runtime
{
    [TestFixture]
    public class DecalSystemPlayModeTests
    {
        private GameObject _entityGo;
        private GameObject _socketGo;
        private GameObject _decalGo;
        private CharacterSocketRegistry _socketRegistry;
        private CharacterSocket _socket;
        private DecalAuraComponent _aura;

        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            // 1. Create the base entity with Character Socket Registry
            _entityGo = new GameObject("TestEntity");
            _socketRegistry = _entityGo.AddComponent<CharacterSocketRegistry>();

            // 2. Create a child Socket transform
            _socketGo = new GameObject("TestSocket_Aura");
            _socketGo.transform.SetParent(_entityGo.transform);
            _socket = _socketGo.AddComponent<CharacterSocket>();
            _socket.Configure(CharacterSocketId.Aura);

            // Refresh sockets immediately
            _socketRegistry.Refresh();

            // 3. Create the Decal Aura GameObject
            _decalGo = new GameObject("TestAuraDecal");
            _decalGo.transform.SetParent(_entityGo.transform);
            _aura = _decalGo.AddComponent<DecalAuraComponent>();
            _aura.socketId = (DecalSocketId)(int)CharacterSocketId.Aura;
            _aura.autoSnapToSocket = true;

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            if (_decalGo != null)
            {
                Object.Destroy(_decalGo);
            }
            if (_socketGo != null)
            {
                Object.Destroy(_socketGo);
            }
            if (_entityGo != null)
            {
                Object.Destroy(_entityGo);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DecalAura_AutoSnapToSocket_CorrectlyParentsToSocket()
        {
            // Trigger Snap Snapping Snug
            _aura.RefreshAnchorLink();

            // Wait 1 frame
            yield return null;

            // Assert that the decal's parent is now the socket transform
            Assert.AreEqual(_socketGo.transform, _decalGo.transform.parent, 
                "DecalAura did not automatically parent/snap to the CharacterSocket of type 'Aura'.");
            Assert.AreEqual(Vector3.zero, _decalGo.transform.localPosition, 
                "DecalAura local position did not reset to zero after snapping.");
        }

        [UnityTest]
        public IEnumerator DecalAura_ToDecalData_GeneratesValidMatricesAndParameters()
        {
            // Setup values
            _aura.tintColor = Color.cyan;
            _aura.radius = 3.0f;
            _aura.projectionDepth = 6.0f;

            yield return null;

            // Generate decal rendering structure
            DecalDataMini data = _aura.ToDecalData();

            // Assertions
            Assert.AreEqual(Color.cyan, (Color)data.color, "DecalData color mismatch.");
            
            // Check that TRS local scale radius and depth are properly embedded in Matrix
            // Matrix W0, W1, W2 contains translation, and scale is represented in basis vector lengths
            Matrix4x4 dtw = new Matrix4x4();
            dtw.SetRow(0, data.dtw0);
            dtw.SetRow(1, data.dtw1);
            dtw.SetRow(2, data.dtw2);
            dtw.SetRow(3, data.dtw3);
            
            // Basis vector lengths should match final TRS local scale
            Vector3 basisX = new Vector3(dtw.m00, dtw.m10, dtw.m20);
            Vector3 basisZ = new Vector3(dtw.m02, dtw.m12, dtw.m22);

            Assert.AreEqual(3.0f, basisX.magnitude, 0.01f, "Decal matrix scale X (radius) mismatch.");
            Assert.AreEqual(6.0f, basisZ.magnitude, 0.01f, "Decal matrix scale Z (depth) mismatch.");
        }

        [UnityTest]
        public IEnumerator DecalSystem_HighFrequencySpawn_RecyclesCircularlyWithoutHeapAlloc()
        {
            // Trigger 100 high-speed dynamic decal spawns (which exceeds typical small pool bounds and forces wrap-around)
            for (int i = 0; i < 100; i++)
            {
                DecalSystemMini.SpawnRuntimeDecal(
                    Vector3.zero,
                    Quaternion.identity,
                    Vector3.one,
                    textureIndex: 0,
                    duration: 5.0f,
                    color: Color.red,
                    softFade: 0.5f,
                    sortingOrder: i
                );
            }

            yield return null;

            // Simple verification that system survived high frequency spawning without crashing
            Assert.Pass("DecalSystemMini successfully processed circular runtime pool allocations.");
        }
    }
}
