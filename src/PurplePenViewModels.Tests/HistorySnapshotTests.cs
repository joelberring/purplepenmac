using NUnit.Framework;
using PurplePen;
using PurplePen.MapModel;
using System;
using System.IO;
using System.Text.Json;

namespace PurplePenViewModels.Tests
{
    /// <summary>Tests event-local history snapshot semantics.</summary>
    [TestFixture]
    public class HistorySnapshotTests
    {
        [Test]
        public void CreateReadsTheCurrentEventPayload()
        {
            EventDB eventDB = new EventDB(new UndoMgr(20));
            HistorySnapshotStore store = new HistorySnapshotStore(eventDB);

            HistorySnapshot snapshot = store.Create(eventDB, "Initial");

            Assert.That(snapshot.EventPayload, Is.EqualTo(eventDB.SaveToString()));
            Assert.That(snapshot.Hash, Is.EqualTo(HistorySnapshot.ComputeHash(snapshot.EventPayload)));
            Assert.That(snapshot.PayloadHash, Is.EqualTo(snapshot.Hash));
            Assert.That(snapshot.SnapshotId, Is.Not.Empty);
        }

        [Test]
        public void SaveManifest_ReplacesExistingFileWithoutLeavingTemporaryFiles()
        {
            EventDB eventDB = new EventDB(new UndoMgr(20));
            HistorySnapshotStore store = new HistorySnapshotStore(eventDB);
            store.Create(eventDB, "Initial");
            string manifest = Path.Combine(Path.GetTempPath(), "purplepen-history-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(manifest, "previous contents");
            try {
                store.SaveManifest(manifest);
                HistorySnapshotStore restored = HistorySnapshotStore.LoadManifest(manifest);

                Assert.That(restored.Snapshots, Has.Count.EqualTo(1));
                Assert.That(Directory.GetFiles(Path.GetDirectoryName(manifest)!, Path.GetFileName(manifest) + ".tmp-*").Length, Is.EqualTo(0));
            }
            finally {
                File.Delete(manifest);
            }
        }

        [Test]
        public void StoreKeepsSnapshotsAndChainsParents()
        {
            EventDB eventDB = new EventDB(new UndoMgr(20));
            HistorySnapshotStore store = new HistorySnapshotStore(eventDB);

            HistorySnapshot first = store.Create(eventDB, "One");
            HistorySnapshot second = store.Create(eventDB, "Two");

            Assert.That(store.Snapshots, Has.Count.EqualTo(2));
            Assert.That(second.ParentHash, Is.EqualTo(first.Hash));
            Assert.That(second.ParentSnapshotId, Is.EqualTo(first.SnapshotId));
        }

        [Test]
        public void ManifestRoundTripRestoresSnapshotsAndCurrentPayload()
        {
            EventDB eventDB = new EventDB(new UndoMgr(20));
            HistorySnapshotStore source = new HistorySnapshotStore(eventDB);
            HistorySnapshot first = source.Create(eventDB, "One", "First comment");
            HistorySnapshot second = source.Create(eventDB, "Two", "Second comment");
            string path = Path.Combine(Path.GetTempPath(), "purplepen-history-" + Guid.NewGuid().ToString("N") + ".json");

            try {
                source.SaveManifest(path);
                HistorySnapshotStore restored = HistorySnapshotStore.LoadManifest(path);

                Assert.That(restored.Snapshots, Has.Count.EqualTo(2));
                Assert.That(restored.Snapshots[0].Hash, Is.EqualTo(first.Hash));
                Assert.That(restored.Snapshots[0].SnapshotId, Is.EqualTo(first.SnapshotId));
                Assert.That(restored.Snapshots[0].Comment, Is.EqualTo("First comment"));
                Assert.That(restored.Snapshots[1].ParentHash, Is.EqualTo(first.Hash));
                Assert.That(restored.Snapshots[1].CreatedUtc, Is.EqualTo(second.CreatedUtc));
                Assert.That(restored.CurrentPayload, Is.EqualTo(second.EventPayload));
            }
            finally {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void IdenticalPayloadsHaveDistinctSnapshotIdsAndAnUnambiguousParentChain()
        {
            EventDB eventDB = new EventDB(new UndoMgr(20));
            HistorySnapshotStore store = new HistorySnapshotStore(eventDB);

            HistorySnapshot first = store.Create(eventDB, "One");
            HistorySnapshot second = store.Create(eventDB, "Two");

            Assert.That(second.Hash, Is.EqualTo(first.Hash));
            Assert.That(second.SnapshotId, Is.Not.EqualTo(first.SnapshotId));
            Assert.That(second.ParentSnapshotId, Is.EqualTo(first.SnapshotId));
            Assert.That(store.Find(second.SnapshotId), Is.SameAs(second));
            Assert.Throws<ArgumentException>(() => store.Find(first.Hash));
        }

        [Test]
        public void DiffIncludesEventClassesAndRouteChoiceCandidates()
        {
            string firstPayload = "<event-db><event-class id=\"1\" name=\"H21\" /><route-choice-candidate id=\"2\" /></event-db>";
            string secondPayload = "<event-db><event-class id=\"1\" name=\"D21\" /><event-class id=\"3\" name=\"H45\" /><route-choice-candidate id=\"4\" /></event-db>";
            HistorySnapshot first = new HistorySnapshot("One", "", DateTime.UtcNow, HistorySnapshot.ComputeHash(firstPayload), "", firstPayload);
            HistorySnapshot second = new HistorySnapshot("Two", "", DateTime.UtcNow, HistorySnapshot.ComputeHash(secondPayload), first.Hash, secondPayload);

            HistorySnapshotDiff diff = HistorySnapshotStore.Compare(first, second);

            Assert.That(diff.Changed["event-classes"], Is.EqualTo(1));
            Assert.That(diff.Added["event-classes"], Is.EqualTo(1));
            Assert.That(diff.Removed["route-choice-candidates"], Is.EqualTo(1));
            Assert.That(diff.Added["route-choice-candidates"], Is.EqualTo(1));
        }

        [Test]
        public void ManifestRejectsTamperedPayloadHash()
        {
            EventDB eventDB = new EventDB(new UndoMgr(20));
            HistorySnapshotStore source = new HistorySnapshotStore(eventDB);
            source.Create(eventDB, "One");
            string path = Path.Combine(Path.GetTempPath(), "purplepen-history-corrupt-" + Guid.NewGuid().ToString("N") + ".json");

            try {
                source.SaveManifest(path);
                using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(path))) {
                    string json = document.RootElement.GetRawText().Replace("\"Hash\":\"", "\"Hash\":\"BAD");
                    File.WriteAllText(path, json);
                }

                Assert.Throws<InvalidDataException>(() => HistorySnapshotStore.LoadManifest(path));
            }
            finally {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
