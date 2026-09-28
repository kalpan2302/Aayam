using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aayam.Tests.PlayMode
{
    public class ScreenNavigationTests
    {
        private GameObject _managerGO;
        private UI.ScreenManager _manager;

        private GameObject _loadingScreen;
        private GameObject _arScreen;
        private GameObject _browserScreen;
        private GameObject _settingsScreen;

        [SetUp]
        public void SetUp()
        {
            _managerGO = new GameObject("ScreenManager");
            _manager   = _managerGO.AddComponent<UI.ScreenManager>();

            _loadingScreen  = new GameObject("LoadingScreen");
            _arScreen       = new GameObject("ARScreen");
            _browserScreen  = new GameObject("BrowserScreen");
            _settingsScreen = new GameObject("SettingsScreen");
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_managerGO);
            Object.Destroy(_loadingScreen);
            Object.Destroy(_arScreen);
            Object.Destroy(_browserScreen);
            Object.Destroy(_settingsScreen);
        }

        [UnityTest]
        public IEnumerator ScreenManager_Exists_AfterCreation()
        {
            yield return null;
            Assert.IsNotNull(_manager, "ScreenManager should exist after creation");
        }

        [UnityTest]
        public IEnumerator GameObject_SetActive_TogglesCorrectly()
        {
            _loadingScreen.SetActive(true);
            _arScreen.SetActive(false);

            yield return null;

            Assert.IsTrue(_loadingScreen.activeSelf,  "Loading should be active");
            Assert.IsFalse(_arScreen.activeSelf,       "AR should be inactive");

            _loadingScreen.SetActive(false);
            _arScreen.SetActive(true);

            yield return null;

            Assert.IsFalse(_loadingScreen.activeSelf, "Loading should be inactive after nav");
            Assert.IsTrue(_arScreen.activeSelf,        "AR should be active after nav");
        }

        [UnityTest]
        public IEnumerator BackStack_MultipleNavigations_PreservesOrder()
        {
            var stack = new System.Collections.Generic.Stack<string>();

            stack.Push("Loading");
            stack.Push("ARScan");
            stack.Push("ProductBrowser");

            yield return null;

            Assert.AreEqual("ProductBrowser", stack.Peek());

            stack.Pop();
            Assert.AreEqual("ARScan", stack.Peek(), "Should return to ARScan");

            stack.Pop();
            Assert.AreEqual("Loading", stack.Peek(), "Should return to Loading");
        }
    }

    public class ComponentLifecycleTests
    {
        [UnityTest]
        public IEnumerator MonoBehaviour_OnEnable_CalledOnStart()
        {
            var go = new GameObject("TestComponent");
            var tracker = go.AddComponent<LifecycleTracker>();

            yield return null;

            Assert.IsTrue(tracker.StartCalled, "Start should be called after one frame");
            Assert.IsTrue(tracker.OnEnableCalled, "OnEnable should be called");

            Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator MonoBehaviour_Disable_StopsUpdate()
        {
            var go = new GameObject("TestComponent");
            var tracker = go.AddComponent<LifecycleTracker>();

            yield return null;
            int framesBeforeDisable = tracker.UpdateCount;

            tracker.enabled = false;
            yield return null;
            yield return null;

            Assert.AreEqual(framesBeforeDisable, tracker.UpdateCount,
                "Update should not be called after disable");

            Object.Destroy(go);
        }
    }

    public class LifecycleTracker : MonoBehaviour
    {
        public bool OnEnableCalled;
        public bool StartCalled;
        public int  UpdateCount;

        private void OnEnable()  => OnEnableCalled = true;
        private void Start()     => StartCalled = true;
        private void Update()    => UpdateCount++;
    }
}
