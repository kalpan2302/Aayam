using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Aayam.Core.Bootstrap;

namespace Aayam.Tests.EditMode
{
    [TestFixture]
    public class DeviceIdentityTests
    {
        private string _filePath;
        private string _backupPath;

        [SetUp]
        public void SetUp()
        {
            _filePath = Path.Combine(Application.persistentDataPath, "device_identity.json");
            _backupPath = _filePath + ".bak";
            if (File.Exists(_filePath)) File.Move(_filePath, _backupPath);
            ClearCachedUuid();
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_filePath)) File.Delete(_filePath);
            if (File.Exists(_backupPath)) File.Move(_backupPath, _filePath);
            ClearCachedUuid();
        }

        [Test]
        public void GetOrCreateDeviceUuid_SameProcess_ReturnsSameValue()
        {
            string first = DeviceIdentity.GetOrCreateDeviceUuid();
            string second = DeviceIdentity.GetOrCreateDeviceUuid();

            Assert.AreEqual(first, second);
        }

        [Test]
        public void GetOrCreateDeviceUuid_PersistsAcrossSimulatedRestart()
        {
            string first = DeviceIdentity.GetOrCreateDeviceUuid();

            ClearCachedUuid(); // simulate a fresh launch re-reading from disk
            string second = DeviceIdentity.GetOrCreateDeviceUuid();

            Assert.AreEqual(first, second);
            Assert.IsTrue(File.Exists(_filePath));
        }

        [Test]
        public void GetOrCreateDeviceUuid_IsAValidGuid()
        {
            string uuid = DeviceIdentity.GetOrCreateDeviceUuid();

            Assert.DoesNotThrow(() => System.Guid.Parse(uuid));
        }

        private static void ClearCachedUuid()
        {
            typeof(DeviceIdentity)
                .GetField("_cachedUuid", BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, null);
        }
    }
}
