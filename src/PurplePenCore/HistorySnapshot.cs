/* Copyright (c) 2026, Purple Pen contributors. */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;

namespace PurplePen
{
    /// <summary>An immutable, named event revision payload used by history review.</summary>
    public sealed class HistorySnapshot
    {
        public string Name { get; private set; }
        public string Comment { get; private set; }
        public DateTime CreatedUtc { get; private set; }
        /// <summary>Unique snapshot identity. Unlike a payload hash, it remains distinct for identical revisions.</summary>
        public string SnapshotId { get; private set; }
        public string Hash { get; private set; }
        /// <summary>Hash of EventPayload, retained separately from the snapshot identity.</summary>
        public string PayloadHash { get; private set; }
        public string ParentHash { get; private set; }
        public string ParentSnapshotId { get; private set; }
        public string EventPayload { get; private set; }

        /// <summary>Rehydrates a persisted snapshot while preserving its immutable identity and timestamp.</summary>
        [JsonConstructor]
        public HistorySnapshot(string name, string comment, DateTime createdUtc, string hash, string parentHash, string eventPayload,
                               string snapshotId = null, string payloadHash = null, string parentSnapshotId = null)
        {
            if (String.IsNullOrWhiteSpace(name)) throw new ArgumentException("A snapshot name is required.", nameof(name));
            if (String.IsNullOrEmpty(eventPayload)) throw new ArgumentException("A snapshot payload is required.", nameof(eventPayload));
            if (String.IsNullOrWhiteSpace(hash)) throw new ArgumentException("A snapshot hash is required.", nameof(hash));

            Name = name.Trim();
            Comment = comment ?? "";
            CreatedUtc = createdUtc;
            Hash = hash;
            PayloadHash = payloadHash ?? hash;
            ParentHash = parentHash ?? "";
            SnapshotId = String.IsNullOrWhiteSpace(snapshotId) ? Guid.NewGuid().ToString("N") : snapshotId;
            ParentSnapshotId = parentSnapshotId ?? "";
            EventPayload = eventPayload;
        }

        internal HistorySnapshot(string name, string comment, string payload, HistorySnapshot parent)
        {
            if (String.IsNullOrWhiteSpace(name)) throw new ArgumentException("A snapshot name is required.", nameof(name));
            if (String.IsNullOrEmpty(payload)) throw new ArgumentException("A snapshot payload is required.", nameof(payload));
            Name = name.Trim(); Comment = comment ?? ""; EventPayload = payload;
            PayloadHash = ComputeHash(payload); Hash = PayloadHash;
            SnapshotId = Guid.NewGuid().ToString("N");
            ParentHash = parent == null ? "" : parent.Hash;
            ParentSnapshotId = parent == null ? "" : parent.SnapshotId;
            CreatedUtc = DateTime.UtcNow;
        }

        public void Rename(string name) { if (String.IsNullOrWhiteSpace(name)) throw new ArgumentException("A snapshot name is required.", nameof(name)); Name = name.Trim(); }
        public void SetComment(string comment) { Comment = comment ?? ""; }
        public static string ComputeHash(string payload)
        {
            using (SHA256 sha = SHA256.Create()) return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        }
    }

    /// <summary>Object-level changes between two event snapshots.</summary>
    public sealed class HistorySnapshotDiff
    {
        public IReadOnlyDictionary<string, int> Added { get; internal set; }
        public IReadOnlyDictionary<string, int> Removed { get; internal set; }
        public IReadOnlyDictionary<string, int> Changed { get; internal set; }
        public bool IsIdentical { get { return Added.Values.Sum() == 0 && Removed.Values.Sum() == 0 && Changed.Values.Sum() == 0; } }
        public string ToText()
        {
            return String.Join(Environment.NewLine, new[] { "Identical: " + IsIdentical, Format("Added", Added), Format("Removed", Removed), Format("Changed", Changed) });
        }
        private static string Format(string title, IReadOnlyDictionary<string, int> values) { return title + ": " + String.Join(", ", values.Where(p => p.Value != 0).Select(p => p.Key + "=" + p.Value)); }
    }

    /// <summary>In-memory history snapshot repository with optional manifest serialization.</summary>
    public sealed class HistorySnapshotStore
    {
        private readonly List<HistorySnapshot> snapshots = new List<HistorySnapshot>();
        private string currentPayload = "";
        public IReadOnlyList<HistorySnapshot> Snapshots { get { return snapshots; } }
        public string CurrentPayload { get { return currentPayload; } }
        public HistorySnapshotStore() { }
        public HistorySnapshotStore(EventDB eventDB) { if (eventDB == null) throw new ArgumentNullException(nameof(eventDB)); currentPayload = eventDB.SaveToString(); }

        public HistorySnapshot Create(EventDB eventDB, string name, string comment = "")
        {
            if (eventDB == null) throw new ArgumentNullException(nameof(eventDB));
            currentPayload = eventDB.SaveToString();
            return CreateFromPayload(name, comment, currentPayload);
        }
        public HistorySnapshot CreateFromPayload(string name, string comment, string payload)
        {
            if (String.IsNullOrEmpty(payload)) throw new InvalidOperationException("No current event payload is available.");
            HistorySnapshot snapshot = new HistorySnapshot(name, comment, payload, snapshots.LastOrDefault());
            snapshots.Add(snapshot); return snapshot;
        }
        /// <summary>Finds by unique snapshot ID; a legacy payload hash is accepted only when unambiguous.</summary>
        public HistorySnapshot Find(string snapshotIdOrHash)
        {
            HistorySnapshot snapshot = snapshots.FirstOrDefault(item => item.SnapshotId == snapshotIdOrHash);
            if (snapshot != null)
                return snapshot;
            HistorySnapshot[] matches = snapshots.Where(item => item.Hash == snapshotIdOrHash).ToArray();
            if (matches.Length == 1)
                return matches[0];
            if (matches.Length > 1)
                throw new ArgumentException("The payload hash identifies more than one snapshot; use SnapshotId.", nameof(snapshotIdOrHash));
            throw new KeyNotFoundException("Snapshot not found.");
        }
        public void Rename(string snapshotIdOrHash, string name) { Find(snapshotIdOrHash).Rename(name); }
        public void SetComment(string snapshotIdOrHash, string comment) { Find(snapshotIdOrHash).SetComment(comment); }
        public HistorySnapshotDiff Compare(string leftSnapshotIdOrHash, string rightSnapshotIdOrHash) { return Compare(Find(leftSnapshotIdOrHash), Find(rightSnapshotIdOrHash)); }
        public static HistorySnapshotDiff Compare(HistorySnapshot left, HistorySnapshot right)
        {
            Dictionary<string, Dictionary<string, string>> a = ReadObjects(left.EventPayload), b = ReadObjects(right.EventPayload);
            Dictionary<string, int> added = new Dictionary<string, int>(), removed = new Dictionary<string, int>(), changed = new Dictionary<string, int>();
            foreach (string category in a.Keys.Union(b.Keys)) {
                Dictionary<string, string> x = a.ContainsKey(category) ? a[category] : new Dictionary<string, string>();
                Dictionary<string, string> y = b.ContainsKey(category) ? b[category] : new Dictionary<string, string>();
                removed[category] = x.Keys.Except(y.Keys).Count(); added[category] = y.Keys.Except(x.Keys).Count();
                changed[category] = x.Keys.Intersect(y.Keys).Count(k => x[k] != y[k]);
            }
            return new HistorySnapshotDiff { Added = added, Removed = removed, Changed = changed };
        }
        public string CreateReport(string leftHash, string rightHash, bool json = false)
        {
            HistorySnapshotDiff diff = Compare(leftHash, rightHash);
            if (!json) return diff.ToText();
            return JsonSerializer.Serialize(new { left = leftHash, right = rightHash, identical = diff.IsIdentical, added = diff.Added, removed = diff.Removed, changed = diff.Changed });
        }
        /// <summary>Saves the manifest atomically, preserving the prior manifest if serialization or replacement fails.</summary>
        public void SaveManifest(string filename)
        {
            if (String.IsNullOrWhiteSpace(filename))
                throw new ArgumentException("A manifest file name is required.", nameof(filename));

            string temporaryFileName = filename + ".tmp-" + Guid.NewGuid().ToString("N");
            try {
                File.WriteAllText(temporaryFileName, JsonSerializer.Serialize(snapshots));
                File.Move(temporaryFileName, filename, true);
            }
            finally {
                if (File.Exists(temporaryFileName))
                    File.Delete(temporaryFileName);
            }
        }
        public static HistorySnapshotStore LoadManifest(string filename)
        {
            try {
                List<HistorySnapshot> loaded = JsonSerializer.Deserialize<List<HistorySnapshot>>(File.ReadAllText(filename));
                if (loaded == null) throw new InvalidDataException("Snapshot manifest is empty.");
                ValidateManifest(loaded);
                HistorySnapshotStore store = new HistorySnapshotStore();
                store.snapshots.AddRange(loaded);
                store.currentPayload = loaded.LastOrDefault()?.EventPayload ?? "";
                return store;
            }
            catch (JsonException ex) { throw new InvalidDataException("Snapshot manifest is corrupt.", ex); }
            catch (ArgumentException ex) { throw new InvalidDataException("Snapshot manifest is corrupt.", ex); }
        }

        /// <summary>Checks payload hashes and the ordered parent chain before accepting persisted history.</summary>
        private static void ValidateManifest(IReadOnlyList<HistorySnapshot> loaded)
        {
            string expectedParentHash = "";
            string expectedParentSnapshotId = "";
            HashSet<string> snapshotIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (HistorySnapshot snapshot in loaded) {
                if (!String.Equals(snapshot.PayloadHash, HistorySnapshot.ComputeHash(snapshot.EventPayload), StringComparison.OrdinalIgnoreCase) ||
                    !String.Equals(snapshot.Hash, snapshot.PayloadHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Snapshot manifest contains an invalid payload hash.");
                if (!snapshotIds.Add(snapshot.SnapshotId))
                    throw new InvalidDataException("Snapshot manifest contains duplicate snapshot identities.");
                if (!String.Equals(snapshot.ParentHash, expectedParentHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Snapshot manifest contains an invalid parent chain.");
                if (!String.IsNullOrEmpty(snapshot.ParentSnapshotId) && !String.Equals(snapshot.ParentSnapshotId, expectedParentSnapshotId, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Snapshot manifest contains an invalid snapshot parent chain.");
                expectedParentHash = snapshot.Hash;
                expectedParentSnapshotId = snapshot.SnapshotId;
            }
        }

        private static Dictionary<string, Dictionary<string, string>> ReadObjects(string payload)
        {
            XmlDocument doc = new XmlDocument();
            try { doc.LoadXml(payload); } catch (XmlException ex) { throw new InvalidDataException("Snapshot event payload is corrupt.", ex); }
            Dictionary<string, Dictionary<string, string>> result = new Dictionary<string, Dictionary<string, string>>();
            foreach (XmlElement element in doc.DocumentElement.SelectNodes("*").OfType<XmlElement>()) {
                string category = Category(element.Name); string id = element.GetAttribute("id");
                if (category == null || String.IsNullOrEmpty(id)) continue;
                if (!result.ContainsKey(category)) result[category] = new Dictionary<string, string>();
                result[category][id] = element.OuterXml;
            }
            return result;
        }
        private static string Category(string name)
        {
            if (name == "control") return "controls"; if (name == "course") return "courses";
            if (name == "course-control") return "course-controls"; if (name == "leg") return "legs";
            if (name == "special") return "specials"; if (name == "training-exercise") return "training-exercises";
            if (name == "event-class") return "event-classes"; if (name == "route-choice-candidate") return "route-choice-candidates";
            if (name == "event") return "event-settings"; return null;
        }
    }
}
