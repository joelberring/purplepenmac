/* Copyright (c) 2026, Purple Pen contributors.
 * All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace PurplePen
{
    /// <summary>Builds a deterministic, platform-independent control overview for mobile course-setting tools.</summary>
    public static class MobileControlInspection
    {
        /// <summary>
        /// Creates an overview of the distinct control points visited by a course designator.
        /// Fork branches are traversed in their stored order and a physical control point is included only at
        /// its first occurrence, so an all-variations designator never produces duplicate inspection entries.
        /// </summary>
        /// <param name="eventDB">Event data containing the course.</param>
        /// <param name="courseDesignator">Course, part, or relay variation to inspect.</param>
        /// <returns>One-based, ordered inspection entries.</returns>
        public static IList<MobileControlInspectionEntry> CreateOverview(EventDB eventDB, CourseDesignator courseDesignator)
        {
            if (eventDB == null)
                throw new ArgumentNullException(nameof(eventDB));
            if (courseDesignator == null)
                throw new ArgumentNullException(nameof(courseDesignator));
            if (courseDesignator.IsAllControls)
                throw new ArgumentException("A mobile inspection overview requires a course designator.", nameof(courseDesignator));

            List<MobileControlInspectionEntry> overview = new List<MobileControlInspectionEntry>();
            HashSet<Id<ControlPoint>> includedControls = new HashSet<Id<ControlPoint>>();

            foreach (Id<CourseControl> courseControlId in QueryEvent.EnumCourseControlIds(eventDB, courseDesignator)) {
                ControlPoint control = eventDB.GetControl(eventDB.GetCourseControl(courseControlId).control);
                if (includedControls.Add(eventDB.GetCourseControl(courseControlId).control)) {
                    overview.Add(new MobileControlInspectionEntry(overview.Count + 1, eventDB.GetCourseControl(courseControlId).control,
                                                                   control.code ?? String.Empty, control.location, control.kind));
                }
            }

            return overview;
        }

        /// <summary>
        /// Validates an observed sequence of control codes against the normal controls in an inspection overview.
        /// The returned list contains one result for every observed code and an additional result for each
        /// expected code that was not observed.
        /// </summary>
        /// <param name="overview">Inspection overview returned by <see cref="CreateOverview"/>.</param>
        /// <param name="observedCodes">Codes observed by a mobile client in visit order.</param>
        /// <returns>Validation results in observation order followed by missing controls.</returns>
        public static IList<MobileControlCodeValidationResult> ValidateControlCodes(IEnumerable<MobileControlInspectionEntry> overview, IEnumerable<string> observedCodes)
        {
            if (overview == null)
                throw new ArgumentNullException(nameof(overview));
            if (observedCodes == null)
                throw new ArgumentNullException(nameof(observedCodes));

            List<MobileControlInspectionEntry> expectedEntries = overview.Where(entry => entry.Kind == ControlPointKind.Normal && !String.IsNullOrEmpty(entry.ControlCode)).ToList();
            Dictionary<string, int> expectedIndexes = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < expectedEntries.Count; ++index) {
                if (!expectedIndexes.ContainsKey(expectedEntries[index].ControlCode))
                    expectedIndexes.Add(expectedEntries[index].ControlCode, index);
            }

            List<MobileControlCodeValidationResult> results = new List<MobileControlCodeValidationResult>();
            HashSet<int> observedExpectedIndexes = new HashSet<int>();
            int nextExpectedIndex = 0;

            foreach (string observedCode in observedCodes) {
                string code = observedCode ?? String.Empty;
                int expectedIndex;
                if (!expectedIndexes.TryGetValue(code, out expectedIndex)) {
                    results.Add(new MobileControlCodeValidationResult(code, MobileControlCodeValidationStatus.Unexpected, null));
                }
                else if (observedExpectedIndexes.Contains(expectedIndex)) {
                    results.Add(new MobileControlCodeValidationResult(code, MobileControlCodeValidationStatus.Duplicate, expectedEntries[expectedIndex].SequenceNumber));
                }
                else {
                    observedExpectedIndexes.Add(expectedIndex);
                    MobileControlCodeValidationStatus status = (expectedIndex == nextExpectedIndex) ? MobileControlCodeValidationStatus.Correct : MobileControlCodeValidationStatus.OutOfOrder;
                    results.Add(new MobileControlCodeValidationResult(code, status, expectedEntries[expectedIndex].SequenceNumber));

                    while (nextExpectedIndex < expectedEntries.Count && observedExpectedIndexes.Contains(nextExpectedIndex))
                        ++nextExpectedIndex;
                }
            }

            for (int index = 0; index < expectedEntries.Count; ++index) {
                if (!observedExpectedIndexes.Contains(index))
                    results.Add(new MobileControlCodeValidationResult(expectedEntries[index].ControlCode, MobileControlCodeValidationStatus.Missing, expectedEntries[index].SequenceNumber));
            }

            return results;
        }
    }

    /// <summary>Describes one distinct control point in a mobile course-setting inspection overview.</summary>
    public sealed class MobileControlInspectionEntry
    {
        /// <summary>Initializes a control inspection entry.</summary>
        /// <param name="sequenceNumber">One-based position in the overview.</param>
        /// <param name="controlId">Event database identifier of the physical control point.</param>
        /// <param name="controlCode">Control code, or an empty string for uncoded points.</param>
        /// <param name="location">Control position in map coordinates.</param>
        /// <param name="kind">Kind of control point.</param>
        public MobileControlInspectionEntry(int sequenceNumber, Id<ControlPoint> controlId, string controlCode, PointF location, ControlPointKind kind)
        {
            SequenceNumber = sequenceNumber;
            ControlId = controlId;
            ControlCode = controlCode;
            Location = location;
            Kind = kind;
        }

        /// <summary>Gets the one-based position in the deterministic inspection order.</summary>
        public int SequenceNumber { get; private set; }

        /// <summary>Gets the event database identifier of the physical control point.</summary>
        public Id<ControlPoint> ControlId { get; private set; }

        /// <summary>Gets the control code, or an empty string for uncoded points.</summary>
        public string ControlCode { get; private set; }

        /// <summary>Gets the control position in map coordinates.</summary>
        public PointF Location { get; private set; }

        /// <summary>Gets the kind of control point.</summary>
        public ControlPointKind Kind { get; private set; }
    }

    /// <summary>Describes how an observed control code relates to a course inspection overview.</summary>
    public enum MobileControlCodeValidationStatus
    {
        Correct,
        Missing,
        Unexpected,
        Duplicate,
        OutOfOrder
    }

    /// <summary>Describes one result from mobile control-code sequence validation.</summary>
    public sealed class MobileControlCodeValidationResult
    {
        /// <summary>Initializes a mobile control-code validation result.</summary>
        /// <param name="controlCode">Observed or expected control code.</param>
        /// <param name="status">Validation outcome.</param>
        /// <param name="expectedSequenceNumber">Expected overview position, if the code belongs to the course.</param>
        public MobileControlCodeValidationResult(string controlCode, MobileControlCodeValidationStatus status, int? expectedSequenceNumber)
        {
            ControlCode = controlCode;
            Status = status;
            ExpectedSequenceNumber = expectedSequenceNumber;
        }

        /// <summary>Gets the observed or expected control code.</summary>
        public string ControlCode { get; private set; }

        /// <summary>Gets the validation outcome.</summary>
        public MobileControlCodeValidationStatus Status { get; private set; }

        /// <summary>Gets the expected one-based overview position, if known.</summary>
        public int? ExpectedSequenceNumber { get; private set; }
    }
}
