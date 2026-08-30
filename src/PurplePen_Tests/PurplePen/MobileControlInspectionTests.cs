/* Copyright (c) 2026, Purple Pen contributors.
 * All rights reserved.
 */

#if TEST
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System;
using TestingUtils;

namespace PurplePen.Tests
{
    /// <summary>Tests the platform-independent mobile control inspection helpers.</summary>
    [TestClass]
    public class MobileControlInspectionTests : TestFixtureBase
    {
        /// <summary>Builds entries with code, order, coordinates, and kind from a normal course.</summary>
        [TestMethod]
        public void CreateOverviewIncludesControlDetailsInCourseOrder()
        {
            EventDB eventDB = CreateSimpleCourse(out Id<Course> courseId);

            IList<MobileControlInspectionEntry> overview = MobileControlInspection.CreateOverview(eventDB, new CourseDesignator(courseId));

            Assert.AreEqual(4, overview.Count);
            Assert.AreEqual(1, overview[0].SequenceNumber);
            Assert.AreEqual(ControlPointKind.Start, overview[0].Kind);
            Assert.AreEqual("", overview[0].ControlCode);
            Assert.AreEqual("31", overview[1].ControlCode);
            Assert.AreEqual(new PointF(20, 30), overview[1].Location);
            Assert.AreEqual(ControlPointKind.Normal, overview[1].Kind);
            Assert.AreEqual(4, overview[3].SequenceNumber);
            Assert.AreEqual(ControlPointKind.Finish, overview[3].Kind);
        }

        /// <summary>Visits all fork branches once and retains the first stored occurrence of shared controls.</summary>
        [TestMethod]
        public void CreateOverviewDeduplicatesAllVariationForkControls()
        {
            UndoMgr undoMgr = new UndoMgr(10);
            EventDB eventDB = new EventDB(undoMgr);
            eventDB.Load(TestUtil.GetTestFile("queryevent\\variations.ppen"));

            IList<Id<CourseControl>> traversedCourseControls = QueryEvent.EnumCourseControlIds(eventDB, new CourseDesignator(CourseId(1))).ToList();
            Assert.IsTrue(traversedCourseControls.Select(id => eventDB.GetCourseControl(id).control).GroupBy(id => id).Any(group => group.Count() > 1));

            IList<MobileControlInspectionEntry> overview = MobileControlInspection.CreateOverview(eventDB, new CourseDesignator(CourseId(1)));

            Assert.AreEqual(overview.Count, overview.Select(entry => entry.ControlId).Distinct().Count());
            CollectionAssert.AreEqual(Enumerable.Range(1, overview.Count).ToArray(), overview.Select(entry => entry.SequenceNumber).ToArray());
        }

        /// <summary>Classifies correct, out-of-order, duplicate, unexpected, and missing control codes.</summary>
        [TestMethod]
        public void ValidateControlCodesClassifiesEachObservationAndMissingControls()
        {
            EventDB eventDB = CreateSimpleCourse(out Id<Course> courseId);
            IList<MobileControlInspectionEntry> overview = MobileControlInspection.CreateOverview(eventDB, new CourseDesignator(courseId));

            IList<MobileControlCodeValidationResult> results = MobileControlInspection.ValidateControlCodes(overview, new[] { "32", "31", "31", "99" });

            CollectionAssert.AreEqual(new[] {
                MobileControlCodeValidationStatus.OutOfOrder,
                MobileControlCodeValidationStatus.Correct,
                MobileControlCodeValidationStatus.Duplicate,
                MobileControlCodeValidationStatus.Unexpected,
                MobileControlCodeValidationStatus.Missing
            }, results.Select(result => result.Status).ToArray());
            Assert.AreEqual(3, results[0].ExpectedSequenceNumber);
            Assert.AreEqual(2, results[1].ExpectedSequenceNumber);
            Assert.IsNull(results[3].ExpectedSequenceNumber);
            Assert.AreEqual("33", results[4].ControlCode);
            Assert.AreEqual(4, results[4].ExpectedSequenceNumber);
        }

        /// <summary>Round-trips observations, GPS metadata and photo references through the portable format.</summary>
        [TestMethod]
        public void PackageRoundTripsFieldObservations()
        {
            IList<MobileControlInspectionEntry> overview = new List<MobileControlInspectionEntry> {
                new MobileControlInspectionEntry(1, ControlId(12), "31", new PointF(10, 20), ControlPointKind.Normal)
            };
            MobileControlInspectionPackage package = MobileControlInspectionPackageSerializer.CreateFromOverview(overview, Designator(7));
            package.Controls[0].Status = MobileControlInspectionStatus.Ready;
            package.Controls[0].Comment = "Flag is behind the boulder";
            package.Controls[0].ObservedCode = "31";
            package.Controls[0].Latitude = 59.3;
            package.Controls[0].Longitude = 18.1;
            package.Controls[0].AccuracyMeters = 4.5;
            package.Controls[0].ObservedAtUtc = DateTimeOffset.Parse("2026-08-29T10:00:00Z");
            package.Controls[0].PhotoReferencePaths.Add("photos/control-12.jpg");

            MobileControlInspectionPackage copy = MobileControlInspectionPackageSerializer.Deserialize(
                MobileControlInspectionPackageSerializer.Serialize(package));

            Assert.AreEqual(MobileControlInspectionStatus.Ready, copy.Controls[0].Status);
            Assert.AreEqual(package.Controls[0].Comment, copy.Controls[0].Comment);
            Assert.AreEqual(package.Controls[0].Latitude, copy.Controls[0].Latitude);
            CollectionAssert.AreEqual(new[] { "photos/control-12.jpg" }, copy.Controls[0].PhotoReferencePaths);
        }

        /// <summary>Rejects unsupported schemas and malformed GPS coordinates.</summary>
        [TestMethod]
        public void PackageRejectsUnsupportedSchemaAndInvalidGps()
        {
            Assert.ThrowsException<ArgumentException>(() => MobileControlInspectionPackageSerializer.Deserialize(
                "{\"format\":\"purplepen.mobile-control-inspection\",\"schemaVersion\":99,\"courseId\":7,\"controls\":[]}"));

            MobileControlInspectionPackage package = new MobileControlInspectionPackage { CourseId = 7 };
            package.Controls.Add(new MobileControlInspectionRecord { ControlId = 1, Sequence = 1, Latitude = 91, Longitude = 18 });
            Assert.ThrowsException<ArgumentException>(() => MobileControlInspectionPackageSerializer.Serialize(package));
        }

        /// <summary>Reports missing and unknown controls while retaining matched observations.</summary>
        [TestMethod]
        public void PackageMergeReportsUnknownAndMissingControls()
        {
            IList<MobileControlInspectionEntry> overview = new List<MobileControlInspectionEntry> {
                new MobileControlInspectionEntry(1, ControlId(12), "31", new PointF(10, 20), ControlPointKind.Normal),
                new MobileControlInspectionEntry(2, ControlId(13), "32", new PointF(20, 30), ControlPointKind.Normal)
            };
            MobileControlInspectionPackage package = MobileControlInspectionPackageSerializer.CreateFromOverview(overview, Designator(7));
            package.Controls.RemoveAt(1);
            package.Controls.Add(new MobileControlInspectionRecord { ControlId = 99, Sequence = 2, ExpectedCode = "99" });

            MobileControlInspectionMergeResult result = MobileControlInspectionPackageSerializer.Merge(package, overview);

            Assert.AreEqual(2, result.Controls.Count);
            CollectionAssert.AreEquivalent(new[] {
                MobileControlInspectionConflictKind.MissingControl,
                MobileControlInspectionConflictKind.UnknownControl
            }, result.Conflicts.Select(conflict => conflict.Kind).ToArray());
        }

        /// <summary>Rejects a valid package when its course id or course part does not match the import target.</summary>
        [TestMethod]
        public void PackageRejectsMismatchedCourseOrPart()
        {
            IList<MobileControlInspectionEntry> overview = new List<MobileControlInspectionEntry> {
                new MobileControlInspectionEntry(1, ControlId(12), "31", new PointF(10, 20), ControlPointKind.Normal)
            };
            MobileControlInspectionPackage package = MobileControlInspectionPackageSerializer.CreateFromOverview(overview, Designator(7));

            package.CourseId = 8;
            Assert.ThrowsException<MobileControlInspectionPackageCourseMismatchException>(() =>
                MobileControlInspectionPackageSerializer.ValidatePackageForCourse(package, Designator(7)));

            package.CourseId = 7;
            package.Part = 0;
            Assert.ThrowsException<MobileControlInspectionPackageCourseMismatchException>(() =>
                MobileControlInspectionPackageSerializer.ValidatePackageForCourse(package, Designator(7)));
        }

        /// <summary>Creates a small normal course used by inspection and sequence-validation tests.</summary>
        private static EventDB CreateSimpleCourse(out Id<Course> courseId)
        {
            UndoMgr undoMgr = new UndoMgr(10);
            EventDB eventDB = new EventDB(undoMgr);
            undoMgr.BeginCommand(1, "Create mobile inspection test course");

            Id<ControlPoint> start = eventDB.AddControlPoint(new ControlPoint(ControlPointKind.Start, null, new PointF(0, 0)));
            Id<ControlPoint> control31 = eventDB.AddControlPoint(new ControlPoint(ControlPointKind.Normal, "31", new PointF(20, 30)));
            Id<ControlPoint> control32 = eventDB.AddControlPoint(new ControlPoint(ControlPointKind.Normal, "32", new PointF(40, 50)));
            Id<ControlPoint> control33 = eventDB.AddControlPoint(new ControlPoint(ControlPointKind.Normal, "33", new PointF(50, 60)));
            Id<ControlPoint> finish = eventDB.AddControlPoint(new ControlPoint(ControlPointKind.Finish, null, new PointF(60, 70)));

            Id<CourseControl> finishCourseControl = eventDB.AddCourseControl(new CourseControl(finish, Id<CourseControl>.None));
            Id<CourseControl> control33CourseControl = eventDB.AddCourseControl(new CourseControl(control33, finishCourseControl));
            Id<CourseControl> control32CourseControl = eventDB.AddCourseControl(new CourseControl(control32, control33CourseControl));
            Id<CourseControl> control31CourseControl = eventDB.AddCourseControl(new CourseControl(control31, control32CourseControl));
            Id<CourseControl> startCourseControl = eventDB.AddCourseControl(new CourseControl(start, control31CourseControl));

            Course course = new Course(CourseKind.Normal, "Mobile inspection", 10000, 1);
            course.firstCourseControl = startCourseControl;
            courseId = eventDB.AddCourse(course);
            undoMgr.EndCommand(1);
            return eventDB;
        }
    }
}
#endif
