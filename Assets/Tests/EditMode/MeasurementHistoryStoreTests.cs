using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Aayam.Models;
using Aayam.Services;

namespace Aayam.Tests.EditMode
{
    [TestFixture]
    public class MeasurementHistoryStoreTests
    {
        private string _filePath;
        private string _backupPath;

        [SetUp]
        public void SetUp()
        {
            _filePath = Path.Combine(Application.persistentDataPath, "measurement_history.json");
            _backupPath = _filePath + ".bak";
            if (File.Exists(_filePath)) File.Move(_filePath, _backupPath);

            MeasurementHistoryStore.ResetInMemoryStateForTests();
            MeasurementHistoryStore.DisableBackendSyncForTests = true;
        }

        [TearDown]
        public void TearDown()
        {
            MeasurementHistoryStore.DisableBackendSyncForTests = false;
            MeasurementHistoryStore.ResetInMemoryStateForTests();

            if (File.Exists(_filePath)) File.Delete(_filePath);
            if (File.Exists(_backupPath)) File.Move(_backupPath, _filePath);
        }

        private static MeasurementHistoryEntry MakeEntry(string doorLocalId, float widthMm)
        {
            return new MeasurementHistoryEntry
            {
                DoorLocalId = doorLocalId,
                TimestampUtc = DateTime.UtcNow.ToString("o"),
                WidthMm = widthMm,
                HeightMm = 2000f,
                WidthConfidence = 0.9f,
                HeightConfidence = 0.9f,
                Source = MeasurementSource.UserAssisted,
            };
        }

        [Test]
        public void SaveMeasurement_WritesToDiskImmediately()
        {
            MeasurementHistoryStore.SaveMeasurement(MakeEntry("door-1", 750f));

            Assert.IsTrue(File.Exists(_filePath));
        }

        [Test]
        public void SaveMeasurement_ThenList_ReturnsSavedEntry()
        {
            MeasurementHistoryStore.SaveMeasurement(MakeEntry("door-1", 750f));

            var all = MeasurementHistoryStore.ListMeasurements();

            Assert.AreEqual(1, all.Count);
            Assert.AreEqual(750f, all[0].WidthMm);
            Assert.AreEqual(MeasurementSource.UserAssisted, all[0].Source);
        }

        [Test]
        public void SaveMeasurement_IsAppendOnly_NotAnOverwrite()
        {
            MeasurementHistoryStore.SaveMeasurement(MakeEntry("door-1", 750f));
            MeasurementHistoryStore.SaveMeasurement(MakeEntry("door-1", 752f));

            var all = MeasurementHistoryStore.ListMeasurements();

            Assert.AreEqual(2, all.Count);
        }

        [Test]
        public void ListMeasurements_ScopesByDoorLocalId()
        {
            MeasurementHistoryStore.SaveMeasurement(MakeEntry("door-1", 750f));
            MeasurementHistoryStore.SaveMeasurement(MakeEntry("door-2", 900f));

            var door1Only = MeasurementHistoryStore.ListMeasurements("door-1");

            Assert.AreEqual(1, door1Only.Count);
            Assert.AreEqual(750f, door1Only[0].WidthMm);
        }

        [Test]
        public void SaveMeasurement_PersistsAcrossSimulatedRestart()
        {
            MeasurementHistoryStore.SaveMeasurement(MakeEntry("door-1", 750f));

            MeasurementHistoryStore.ResetInMemoryStateForTests(); // simulate a fresh launch re-reading from disk

            var all = MeasurementHistoryStore.ListMeasurements();
            Assert.AreEqual(1, all.Count);
            Assert.AreEqual(750f, all[0].WidthMm);
        }
    }
}
