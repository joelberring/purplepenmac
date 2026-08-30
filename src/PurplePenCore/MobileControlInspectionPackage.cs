// MobileControlInspectionPackage.cs
//
// Portable, versioned data exchange for field inspection of a course.  The
// package deliberately contains plain JSON values only; it can therefore be
// produced by the desktop application and consumed by a mobile field tool.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PurplePen
{
    /// <summary>Defines the stable JSON format identifier for field inspections.</summary>
    public static class MobileControlInspectionPackageFormat
    {
        public const string Format = "purplepen.mobile-control-inspection";
        public const int CurrentSchemaVersion = 1;
    }

    /// <summary>Describes the state of one control during a field inspection.</summary>
    public enum MobileControlInspectionStatus
    {
        Uninspected,
        Ready,
        NeedsAttention,
        Missing
    }

    /// <summary>Stores portable inspection information for one physical control.</summary>
    public sealed class MobileControlInspectionRecord
    {
        public int ControlId { get; set; }
        public int Sequence { get; set; }
        public string ExpectedCode { get; set; } = String.Empty;
        public string Kind { get; set; } = String.Empty;
        public MobileControlInspectionStatus Status { get; set; }
        public string Comment { get; set; } = String.Empty;
        public string ObservedCode { get; set; } = String.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? AccuracyMeters { get; set; }
        public DateTimeOffset? ObservedAtUtc { get; set; }
        public List<string> PhotoReferencePaths { get; set; } = new List<string>();
    }

    /// <summary>Defines a complete course inspection package.</summary>
    public sealed class MobileControlInspectionPackage
    {
        public string Format { get; set; } = MobileControlInspectionPackageFormat.Format;
        public int SchemaVersion { get; set; } = MobileControlInspectionPackageFormat.CurrentSchemaVersion;
        public int CourseId { get; set; }
        public int Part { get; set; } = -1;
        public string CreatedUtc { get; set; } = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        public List<MobileControlInspectionRecord> Controls { get; set; } = new List<MobileControlInspectionRecord>();
    }

    /// <summary>Identifies an inspection package that belongs to a different course or course part.</summary>
    public sealed class MobileControlInspectionPackageCourseMismatchException : ArgumentException
    {
        /// <summary>Creates an exception for a package that does not match the active course designator.</summary>
        public MobileControlInspectionPackageCourseMismatchException()
            : base("The inspection package belongs to a different course or course part.")
        {
        }
    }

    /// <summary>Identifies why an imported record could not be matched cleanly.</summary>
    public enum MobileControlInspectionConflictKind
    {
        UnknownControl,
        MissingControl,
        DuplicateControl,
        ExpectedCodeMismatch
    }

    /// <summary>Describes one merge conflict without discarding imported data.</summary>
    public sealed class MobileControlInspectionConflict
    {
        public MobileControlInspectionConflict(MobileControlInspectionConflictKind kind, int controlId, string message)
        {
            Kind = kind;
            ControlId = controlId;
            Message = message;
        }

        public MobileControlInspectionConflictKind Kind { get; private set; }
        public int ControlId { get; private set; }
        public string Message { get; private set; }
    }

    /// <summary>Result of merging an imported package into a current overview.</summary>
    public sealed class MobileControlInspectionMergeResult
    {
        public MobileControlInspectionMergeResult(IList<MobileControlInspectionRecord> controls,
                                                   IList<MobileControlInspectionConflict> conflicts)
        {
            Controls = controls;
            Conflicts = conflicts;
        }

        public IList<MobileControlInspectionRecord> Controls { get; private set; }
        public IList<MobileControlInspectionConflict> Conflicts { get; private set; }
        public bool HasConflicts { get { return Conflicts.Count != 0; } }
    }

    /// <summary>Serializes, validates, creates and merges mobile inspection packages.</summary>
    public static class MobileControlInspectionPackageSerializer
    {
        private static readonly JsonSerializerOptions serializerOptions = new JsonSerializerOptions {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        /// <summary>Creates an empty observation package from a deterministic course overview.</summary>
        public static MobileControlInspectionPackage CreateFromOverview(IEnumerable<MobileControlInspectionEntry> overview,
                                                                         CourseDesignator courseDesignator)
        {
            if (overview == null)
                throw new ArgumentNullException(nameof(overview));
            if (courseDesignator == null || courseDesignator.IsAllControls)
                throw new ArgumentException("A course designator is required.", nameof(courseDesignator));

            MobileControlInspectionPackage package = new MobileControlInspectionPackage {
                CourseId = courseDesignator.CourseId.id,
                Part = courseDesignator.AllParts ? -1 : courseDesignator.Part,
                CreatedUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            };
            foreach (MobileControlInspectionEntry entry in overview) {
                package.Controls.Add(new MobileControlInspectionRecord {
                    ControlId = entry.ControlId.id,
                    Sequence = entry.SequenceNumber,
                    ExpectedCode = entry.ControlCode ?? String.Empty,
                    Kind = entry.Kind.ToString(),
                    Status = MobileControlInspectionStatus.Uninspected
                });
            }
            return package;
        }

        /// <summary>Serializes a package after validating all portable values.</summary>
        public static string Serialize(MobileControlInspectionPackage package)
        {
            ValidatePackage(package);
            return JsonSerializer.Serialize(package, serializerOptions);
        }

        /// <summary>Deserializes and strictly validates a package.</summary>
        public static MobileControlInspectionPackage Deserialize(string json)
        {
            if (String.IsNullOrWhiteSpace(json))
                throw new ArgumentException("An inspection package is required.", nameof(json));

            JsonDocument document;
            try {
                document = JsonDocument.Parse(json);
            }
            catch (JsonException ex) {
                throw new ArgumentException("The inspection package is not valid JSON.", nameof(json), ex);
            }
            using (document) {
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    throw new ArgumentException("The inspection package must be a JSON object.", nameof(json));
                RequireProperty(root, "format", JsonValueKind.String, json);
                RequireProperty(root, "schemaVersion", JsonValueKind.Number, json);
                RequireProperty(root, "courseId", JsonValueKind.Number, json);
                RequireProperty(root, "controls", JsonValueKind.Array, json);
                MobileControlInspectionPackage package;
                try {
                    package = JsonSerializer.Deserialize<MobileControlInspectionPackage>(json, serializerOptions);
                }
                catch (JsonException ex) {
                    throw new ArgumentException("The inspection package contains invalid values.", nameof(json), ex);
                }
                if (package == null)
                    throw new ArgumentException("The inspection package is empty.", nameof(json));
                ValidatePackage(package);
                return package;
            }
        }

        /// <summary>Validates values that would otherwise be unsafe for a field client.</summary>
        public static void ValidatePackage(MobileControlInspectionPackage package)
        {
            if (package == null)
                throw new ArgumentNullException(nameof(package));
            if (!String.Equals(package.Format, MobileControlInspectionPackageFormat.Format, StringComparison.Ordinal))
                throw new ArgumentException("The inspection package format is not supported.", nameof(package));
            if (package.SchemaVersion != MobileControlInspectionPackageFormat.CurrentSchemaVersion)
                throw new ArgumentException("The inspection package schema version is not supported.", nameof(package));
            if (package.CourseId <= 0)
                throw new ArgumentException("The inspection package course id is invalid.", nameof(package));
            if (package.Part < -1)
                throw new ArgumentException("The inspection package course part is invalid.", nameof(package));
            if (package.Controls == null)
                throw new ArgumentException("The inspection package controls are missing.", nameof(package));

            HashSet<int> ids = new HashSet<int>();
            foreach (MobileControlInspectionRecord record in package.Controls) {
                if (record == null || record.ControlId <= 0 || record.Sequence <= 0)
                    throw new ArgumentException("An inspection control has an invalid identifier or sequence.", nameof(package));
                if (!ids.Add(record.ControlId))
                    throw new ArgumentException("The inspection package contains duplicate control ids.", nameof(package));
                if (!Enum.IsDefined(typeof(MobileControlInspectionStatus), record.Status))
                    throw new ArgumentException("An inspection control has an invalid status.", nameof(package));
                if (record.PhotoReferencePaths == null)
                    throw new ArgumentException("An inspection control has invalid photo references.", nameof(package));
                foreach (string photoPath in record.PhotoReferencePaths)
                    if (String.IsNullOrWhiteSpace(photoPath))
                        throw new ArgumentException("An inspection control has an empty photo reference.", nameof(package));
                ValidateGps(record, package);
            }
        }

        /// <summary>Validates that a package belongs to the specified course and course part.</summary>
        public static void ValidatePackageForCourse(MobileControlInspectionPackage package, CourseDesignator courseDesignator)
        {
            ValidatePackage(package);
            if (courseDesignator == null || courseDesignator.IsAllControls)
                throw new ArgumentException("A course designator is required.", nameof(courseDesignator));

            int expectedPart = courseDesignator.AllParts ? -1 : courseDesignator.Part;
            if (package.CourseId != courseDesignator.CourseId.id || package.Part != expectedPart)
                throw new MobileControlInspectionPackageCourseMismatchException();
        }

        /// <summary>Merges imported observations into the current course overview.</summary>
        public static MobileControlInspectionMergeResult Merge(MobileControlInspectionPackage package,
                                                                IEnumerable<MobileControlInspectionEntry> overview)
        {
            ValidatePackage(package);
            if (overview == null)
                throw new ArgumentNullException(nameof(overview));

            List<MobileControlInspectionEntry> expected = overview.ToList();
            Dictionary<int, MobileControlInspectionEntry> expectedById = expected.ToDictionary(entry => entry.ControlId.id);
            Dictionary<int, MobileControlInspectionRecord> importedById = package.Controls.ToDictionary(record => record.ControlId);
            List<MobileControlInspectionRecord> merged = new List<MobileControlInspectionRecord>();
            List<MobileControlInspectionConflict> conflicts = new List<MobileControlInspectionConflict>();

            foreach (MobileControlInspectionEntry entry in expected) {
                MobileControlInspectionRecord record;
                if (!importedById.TryGetValue(entry.ControlId.id, out record)) {
                    merged.Add(CreateRecord(entry));
                    conflicts.Add(new MobileControlInspectionConflict(MobileControlInspectionConflictKind.MissingControl,
                        entry.ControlId.id, "The current course control has no imported inspection record."));
                    continue;
                }
                MobileControlInspectionRecord copy = Clone(record);
                if (!String.Equals(copy.ExpectedCode ?? String.Empty, entry.ControlCode ?? String.Empty, StringComparison.Ordinal)) {
                    conflicts.Add(new MobileControlInspectionConflict(MobileControlInspectionConflictKind.ExpectedCodeMismatch,
                        entry.ControlId.id, "The expected control code differs from the current course."));
                    copy.ExpectedCode = entry.ControlCode ?? String.Empty;
                }
                copy.Sequence = entry.SequenceNumber;
                copy.Kind = entry.Kind.ToString();
                merged.Add(copy);
            }
            foreach (MobileControlInspectionRecord record in package.Controls)
                if (!expectedById.ContainsKey(record.ControlId))
                    conflicts.Add(new MobileControlInspectionConflict(MobileControlInspectionConflictKind.UnknownControl,
                        record.ControlId, "The imported inspection control is not present in the current course."));

            return new MobileControlInspectionMergeResult(merged, conflicts);
        }

        /// <summary>Validates one observed code against an expected code.</summary>
        public static MobileControlCodeValidationStatus ValidateObservedCode(string expectedCode, string observedCode)
        {
            string expected = expectedCode ?? String.Empty;
            string observed = observedCode ?? String.Empty;
            if (String.IsNullOrEmpty(expected) || String.IsNullOrEmpty(observed))
                return String.Equals(expected, observed, StringComparison.Ordinal) ? MobileControlCodeValidationStatus.Correct : MobileControlCodeValidationStatus.Missing;
            return String.Equals(expected, observed, StringComparison.Ordinal) ? MobileControlCodeValidationStatus.Correct : MobileControlCodeValidationStatus.Unexpected;
        }

        private static MobileControlInspectionRecord CreateRecord(MobileControlInspectionEntry entry)
        {
            return new MobileControlInspectionRecord {
                ControlId = entry.ControlId.id,
                Sequence = entry.SequenceNumber,
                ExpectedCode = entry.ControlCode ?? String.Empty,
                Kind = entry.Kind.ToString(),
                Status = MobileControlInspectionStatus.Uninspected
            };
        }

        private static MobileControlInspectionRecord Clone(MobileControlInspectionRecord source)
        {
            return new MobileControlInspectionRecord {
                ControlId = source.ControlId, Sequence = source.Sequence, ExpectedCode = source.ExpectedCode,
                Kind = source.Kind, Status = source.Status, Comment = source.Comment, ObservedCode = source.ObservedCode,
                Latitude = source.Latitude, Longitude = source.Longitude, AccuracyMeters = source.AccuracyMeters,
                ObservedAtUtc = source.ObservedAtUtc, PhotoReferencePaths = new List<string>(source.PhotoReferencePaths ?? new List<string>())
            };
        }

        private static void ValidateGps(MobileControlInspectionRecord record, MobileControlInspectionPackage package)
        {
            bool hasLatitude = record.Latitude.HasValue;
            bool hasLongitude = record.Longitude.HasValue;
            if (hasLatitude != hasLongitude)
                throw new ArgumentException("GPS latitude and longitude must be provided together.", nameof(package));
            if (hasLatitude && (Double.IsNaN(record.Latitude.Value) || Double.IsNaN(record.Longitude.Value) ||
                                record.Latitude.Value < -90 || record.Latitude.Value > 90 ||
                                record.Longitude.Value < -180 || record.Longitude.Value > 180))
                throw new ArgumentException("GPS latitude or longitude is outside its valid range.", nameof(package));
            if (record.AccuracyMeters.HasValue && (Double.IsNaN(record.AccuracyMeters.Value) || record.AccuracyMeters.Value < 0))
                throw new ArgumentException("GPS accuracy must be zero or greater.", nameof(package));
            if (record.AccuracyMeters.HasValue && !hasLatitude)
                throw new ArgumentException("GPS accuracy requires latitude and longitude.", nameof(package));
        }

        private static void RequireProperty(JsonElement root, string name, JsonValueKind kind, string json)
        {
            JsonElement value;
            if (!root.TryGetProperty(name, out value) || value.ValueKind != kind)
                throw new ArgumentException("The inspection package is missing or has an invalid '" + name + "' property.", nameof(json));
        }
    }
}
