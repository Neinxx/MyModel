using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using UISystem.Runtime;

namespace UISystem.Tests
{
    public class MockUIView : UIView
    {
        public bool HasOpened { get; private set; } = false;
        public bool HasClosed { get; private set; } = false;

        public static MockUIView Create(GameObject go, string id, bool hideOnAwake)
        {
            var view = go.AddComponent<MockUIView>();
            view.viewID = id;
            view.hideOnAwake = hideOnAwake;
            view.fadeDuration = 0f; // Disable fade for instant testing
            return view;
        }

        protected override void Awake()
        {
            base.Awake();
        }

        public override void Open()
        {
            // Bypass coroutine for synchronous tests
            canvasGroup = GetComponent<CanvasGroup>();
            canvasGroup.alpha = 1.0f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            isVisible = true;
            gameObject.SetActive(true);
            HasOpened = true;
            OnBeforeOpen?.Invoke();
        }

        public override void Close()
        {
            // Bypass coroutine for synchronous tests
            canvasGroup = GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0.0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            isVisible = false;
            gameObject.SetActive(false);
            HasClosed = true;
            OnAfterClose?.Invoke();
        }
    }

    [TestFixture]
    public class UISystemTests
    {
        private List<GameObject> _spawnedObjects;

        [SetUp]
        public void SetUp()
        {
            _spawnedObjects = new List<GameObject>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawnedObjects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            _spawnedObjects.Clear();

            // Reset singleton reference
            var instanceField = typeof(UIManager).GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (instanceField != null)
            {
                instanceField.SetValue(null, null);
            }
        }

        [Test]
        public void UIManager_SingletonAndRegistration_CorrectlyRegistersViews()
        {
            // 1. Setup
            var managerGo = new GameObject("UIManager");
            _spawnedObjects.Add(managerGo);
            
            // Create child views representing the UI hierarchy
            var childGo1 = new GameObject("ViewA");
            childGo1.transform.SetParent(managerGo.transform);
            childGo1.AddComponent<CanvasGroup>();
            var viewA = MockUIView.Create(childGo1, "ViewA_ID", true);

            var childGo2 = new GameObject("ViewB");
            childGo2.transform.SetParent(managerGo.transform);
            childGo2.AddComponent<CanvasGroup>();
            var viewB = MockUIView.Create(childGo2, "ViewB_ID", true);

            // Add manager component
            var manager = managerGo.AddComponent<UIManager>();

            // 2. Execute
            manager.SendMessage("Awake", null, SendMessageOptions.DontRequireReceiver);

            // 3. Verify: Singleton and registration
            Assert.AreEqual(manager, UIManager.Instance, "UIManager failed to set Singleton reference");
            Assert.AreEqual(viewA, manager.GetView<MockUIView>("ViewA_ID"), "ViewA was not registered in UIManager");
            Assert.AreEqual(viewB, manager.GetView<MockUIView>("ViewB_ID"), "ViewB was not registered in UIManager");
        }

        [Test]
        public void UIManager_OpenAndCloseView_CorrectlyUpdatesLifecycleAndHistory()
        {
            // 1. Setup
            var managerGo = new GameObject("UIManager");
            _spawnedObjects.Add(managerGo);
            var manager = managerGo.AddComponent<UIManager>();
            manager.SendMessage("Awake", null, SendMessageOptions.DontRequireReceiver);

            var viewGo = new GameObject("ViewTarget");
            viewGo.transform.SetParent(managerGo.transform);
            viewGo.AddComponent<CanvasGroup>();
            var view = MockUIView.Create(viewGo, "TargetID", true);
            view.SendMessage("Awake", null, SendMessageOptions.DontRequireReceiver);

            manager.RegisterView(view);

            // 2. Execute: Open View
            manager.OpenView("TargetID", addToHistory: true);

            // 3. Verify Open State
            Assert.IsTrue(view.HasOpened, "MockUIView.Open was not called by UIManager");
            
            var historyField = typeof(UIManager).GetField("_history", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var historyStack = (Stack<UIView>)historyField.GetValue(manager);
            Assert.AreEqual(1, historyStack.Count, "Opened view was not pushed to navigation history stack");
            Assert.AreEqual(view, historyStack.Peek(), "Wrong view at the top of history stack");

            // 4. Execute: Close Top View
            manager.CloseTopView();

            // 5. Verify Close State
            Assert.IsTrue(view.HasClosed, "MockUIView.Close was not called when closing top view");
            Assert.AreEqual(0, historyStack.Count, "Closed view was not popped from history stack");
        }
    }
}
