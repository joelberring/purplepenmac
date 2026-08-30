/* Copyright (c) 2006-2008, Peter Golde
 * All rights reserved.
 * 
 * Redistribution and use in source and binary forms, with or without 
 * modification, are permitted provided that the following conditions are 
 * met:
 * 
 * 1. Redistributions of source code must retain the above copyright
 * notice, this list of conditions and the following disclaimer.
 * 
 * 2. Redistributions in binary form must reproduce the above copyright
 * notice, this list of conditions and the following disclaimer in the
 * documentation and/or other materials provided with the distribution.
 * 
 * 3. Neither the name of Peter Golde, nor "Purple Pen", nor the names
 * of its contributors may be used to endorse or promote products
 * derived from this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND
 * CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES,
 * INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF
 * MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
 * BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
 * SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
 * WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE
 * USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY
 * OF SUCH DAMAGE.
 */

#if TEST
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml;
using System.Diagnostics;
using System.Drawing;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestingUtils;

namespace PurplePen.Tests
{
    using PurplePen.MapModel;

    [TestClass]
    public class ReportTests
    {
        UndoMgr undomgr;
        EventDB eventDB;

        public void Setup(string basename)
        {
            undomgr = new UndoMgr(10);
            eventDB = new EventDB(undomgr);

            eventDB.Load(TestUtil.GetTestFile(basename));
            eventDB.Validate();
        }

        [TestMethod]
        public void CourseLoad1()
        {
            Setup(@"reports\marymoor2.coursescribe");

            Reports reports = new Reports();
            string result = reports.CreateLoadReport(eventDB);

            string expectedFile = TestUtil.GetTestFile(@"reports\CourseLoad1_expected.html");
            string actualFile = TestUtil.GetTestFile(@"reports\CourseLoad1_result.html");

            File.WriteAllText(actualFile, result);
            try {
                TextFileTestUtil.CompareTextFileBaseline(actualFile, expectedFile);
            }
            finally {
                File.Delete(actualFile);
            }
        }

        [TestMethod]
        public void CourseLoad2()
        {
            Setup(@"reports\marymoor3.coursescribe");

            Reports reports = new Reports();
            string result = reports.CreateLoadReport(eventDB);

            string expectedFile = TestUtil.GetTestFile(@"reports\CourseLoad2_expected.html");
            string actualFile = TestUtil.GetTestFile(@"reports\CourseLoad2_result.html");

            File.WriteAllText(actualFile, result);
            try {
                TextFileTestUtil.CompareTextFileBaseline(actualFile, expectedFile);
            }
            finally {
                File.Delete(actualFile);
            }
        }


        [TestMethod]
        public void CourseLoad3()
        {
            Setup(@"reports\visitload.ppen");

            Reports reports = new Reports();
            string result = reports.CreateLoadReport(eventDB);

            string expectedFile = TestUtil.GetTestFile(@"reports\CourseLoad3_expected.html");
            string actualFile = TestUtil.GetTestFile(@"reports\CourseLoad3_result.html");

            File.WriteAllText(actualFile, result);
            try {
                TextFileTestUtil.CompareTextFileBaseline(actualFile, expectedFile);
            }
            finally {
                File.Delete(actualFile);
            }
        }


        [TestMethod]
        public void CrossRef()
        {
            Setup(@"reports\marymoor4.coursescribe");

            Reports reports = new Reports();
            string result = reports.CreateCrossReferenceReport(eventDB);

            string expectedFile = TestUtil.GetTestFile(@"reports\CrossRef_expected.html");
            string actualFile = TestUtil.GetTestFile(@"reports\CrossRef_result.html");

            File.WriteAllText(actualFile, result);
            try {
                TextFileTestUtil.CompareTextFileBaseline(actualFile, expectedFile);
            }
            finally {
                File.Delete(actualFile);
            }
    }

        [TestMethod]
        public void NearbyControls1()
        {
            Setup(@"reports\close1.ppen");

            Reports reports = new Reports();
            string result = reports.CreateEventAuditReport(eventDB);

            string expectedFile = TestUtil.GetTestFile(@"reports\NearbyControls1_expected.html");
            string actualFile = TestUtil.GetTestFile(@"reports\NearbyControls1_result.html");

            File.WriteAllText(actualFile, result);
            try {
                TextFileTestUtil.CompareTextFileBaseline(actualFile, expectedFile);
            }
            finally {
                File.Delete(actualFile);
            }
        }

        [TestMethod]
        public void NearbyControls2()
        {
            Setup(@"reports\close2.ppen");

            Reports reports = new Reports();
            string result = reports.CreateEventAuditReport(eventDB);

           string expectedFile = TestUtil.GetTestFile(@"reports\NearbyControls2_expected.html");
            string actualFile = TestUtil.GetTestFile(@"reports\NearbyControls2_result.html");

            File.WriteAllText(actualFile, result);
            try {
                TextFileTestUtil.CompareTextFileBaseline(actualFile, expectedFile);
            }
            finally {
                File.Delete(actualFile);
            }
        }

        [TestMethod]
        public void DuplicatedControls()
        {
            Setup(@"reports\dupcontrol.ppen");

            Reports reports = new Reports();
            string result = reports.CreateEventAuditReport(eventDB);

            string expectedFile = TestUtil.GetTestFile(@"reports\DuplicatedControls_expected.html");
            string actualFile = TestUtil.GetTestFile(@"reports\DuplicatedControls_result.html");

            File.WriteAllText(actualFile, result);
            try {
                TextFileTestUtil.CompareTextFileBaseline(actualFile, expectedFile);
            }
            finally {
                File.Delete(actualFile);
            }
        }

        [TestMethod]
        public void LegsBothDirections()
        {
            Setup(@"reports\reversal.ppen");

            Reports reports = new Reports();
            string result = reports.CreateEventAuditReport(eventDB);

            string expectedFile = TestUtil.GetTestFile(@"reports\LegsBothDirections_expected.html");
            string actualFile = TestUtil.GetTestFile(@"reports\LegsBothDirections_result.html");

            File.WriteAllText(actualFile, result);
            try {
                TextFileTestUtil.CompareTextFileBaseline(actualFile, expectedFile);
            }
            finally {
                File.Delete(actualFile);
            }
        }

        [TestMethod]
        public void EventAudit()
        {
            Setup(@"reports\marymoor6.ppen");

            Reports reports = new Reports();
            string result = reports.CreateEventAuditReport(eventDB);

            string expectedFile = TestUtil.GetTestFile(@"reports\EventAudit_expected.html");
            string actualFile = TestUtil.GetTestFile(@"reports\EventAudit_result.html");

            File.WriteAllText(actualFile, result);
            try {
                TextFileTestUtil.CompareTextFileBaseline(actualFile, expectedFile);
            }
            finally {
                File.Delete(actualFile);
            }
        }

        [TestMethod]
        public void EventReadiness()
        {
            Setup(@"reports\marymoor6.ppen");

            Reports reports = new Reports();
            string result = reports.CreateEventReadinessReport(eventDB);

            Assert.IsTrue(result.Contains(ReportText.ResourceManager.GetString("EventReadiness_Checks")));
            Assert.IsTrue(result.Contains(ReportText.ResourceManager.GetString("EventReadiness_CourseStructure")));
            Assert.IsTrue(result.Contains(ReportText.ResourceManager.GetString("EventReadiness_ControlSafety")));
            Assert.IsTrue(result.Contains(ReportText.ResourceManager.GetString("EventReadiness_ShortLegs")));
            Assert.IsTrue(result.Contains(ReportText.ResourceManager.GetString("EventReadiness_SharpTurns")));
            Assert.IsTrue(result.Contains(ReportText.ResourceManager.GetString("EventReadiness_ControlData")));
        }

        [TestMethod]
        public void EventReadinessDetectsCrossedLegs()
        {
            SetupCrossedLegEvent();

            Reports reports = new Reports();
            string result = reports.CreateEventReadinessReport(eventDB);

            Assert.IsTrue(result.Contains(ReportText.ResourceManager.GetString("EventReadiness_LegCrossings")));
            Assert.AreEqual(1, CountOccurrences(result, ReportText.ResourceManager.GetString("EventReadiness_LegCrossing")));
            Assert.IsTrue(result.Contains("31"));
            Assert.IsTrue(result.Contains("32"));
        }

        [TestMethod]
        public void EventReadinessDetectsNumberPrintAndClassIssues()
        {
            Setup(@"reports\marymoor6.ppen");
            Id<Course> courseId = QueryEvent.SortedCourseIds(eventDB, false).First();
            Course course = eventDB.GetCourse(courseId);
            course.printScale = 0;
            course.className = null;
            List<Id<CourseControl>> controls = QueryEvent.EnumCourseControlIds(eventDB, new CourseDesignator(courseId)).ToList();
            if (controls.Count >= 2) {
                CourseControl first = eventDB.GetCourseControl(controls[0]);
                CourseControl second = eventDB.GetCourseControl(controls[1]);
                PointF firstLocation = eventDB.GetControl(first.control).location;
                PointF secondLocation = eventDB.GetControl(second.control).location;
                second.customNumberPlacement = true;
                second.numberDeltaX = firstLocation.X - secondLocation.X;
                second.numberDeltaY = firstLocation.Y - secondLocation.Y;
            }

            string result = new Reports().CreateEventReadinessReport(eventDB, new CoursePdfSettings { IncludeBacksideInfo = true });
            Assert.IsTrue(result.Contains(ReportText.ResourceManager.GetString("EventReadiness_Numbering")));
            Assert.IsTrue(result.Contains(ReportText.ResourceManager.GetString("EventReadiness_Print")));
            Assert.IsTrue(result.Contains(ReportText.ResourceManager.GetString("EventReadiness_Classes")));
            Assert.IsTrue(result.Contains(ReportText.ResourceManager.GetString("EventReadiness_Backside")));
            Assert.IsTrue(result.Contains(ReportText.ResourceManager.GetString("EventReadiness_InvalidScale")));
        }

        [TestMethod]
        public void EventReadinessDetectsActualNumberNumberBoundsCollision()
        {
            Setup(@"reports\marymoor6.ppen");
            Id<Course> courseId = QueryEvent.SortedCourseIds(eventDB, false).First();
            List<Id<CourseControl>> controls = QueryEvent.EnumCourseControlIds(eventDB, new CourseDesignator(courseId)).ToList();
            Assert.IsTrue(controls.Count >= 2);
            CourseControl first = eventDB.GetCourseControl(controls[0]);
            CourseControl second = eventDB.GetCourseControl(controls[1]);
            PointF a = eventDB.GetControl(first.control).location;
            PointF b = eventDB.GetControl(second.control).location;
            second.customNumberPlacement = true;
            second.numberDeltaX = a.X - b.X;
            second.numberDeltaY = a.Y - b.Y;
            string result = new Reports().CreateEventReadinessReport(eventDB);
            Assert.IsTrue(result.Contains(ReportText.ResourceManager.GetString("EventReadiness_NumberingDetails")));
            Assert.IsTrue(result.Contains(eventDB.GetControl(first.control).code + " / " + eventDB.GetControl(second.control).code));
        }

        [TestMethod]
        public void EventReadinessDetectsNumberOverControlBounds()
        {
            Setup(@"reports\marymoor6.ppen");
            Id<Course> courseId = QueryEvent.SortedCourseIds(eventDB, false).First();
            List<Id<CourseControl>> controls = QueryEvent.EnumCourseControlIds(eventDB, new CourseDesignator(courseId)).ToList();
            Assert.IsTrue(controls.Count >= 2);
            CourseControl first = eventDB.GetCourseControl(controls[0]);
            CourseControl second = eventDB.GetCourseControl(controls[1]);
            PointF a = eventDB.GetControl(first.control).location;
            PointF b = eventDB.GetControl(second.control).location;
            second.customNumberPlacement = true;
            second.numberDeltaX = a.X - b.X;
            second.numberDeltaY = a.Y - b.Y;
            string result = new Reports().CreateEventReadinessReport(eventDB);
            Assert.IsTrue(result.Contains(ReportText.ResourceManager.GetString("EventReadiness_NumberingDetails")));
        }

        [TestMethod]
        public void EventReadinessDoesNotFlagDistantNumberBounds()
        {
            Setup(@"reports\marymoor6.ppen");
            Id<Course> courseId = QueryEvent.SortedCourseIds(eventDB, false).First();
            List<Id<CourseControl>> controls = QueryEvent.EnumCourseControlIds(eventDB, new CourseDesignator(courseId)).ToList();
            Assert.IsTrue(controls.Count >= 2);
            CourseControl first = eventDB.GetCourseControl(controls[0]);
            CourseControl second = eventDB.GetCourseControl(controls[1]);
            PointF a = eventDB.GetControl(first.control).location;
            PointF b = eventDB.GetControl(second.control).location;
            second.customNumberPlacement = true;
            second.numberDeltaX = a.X - b.X + 1000;
            second.numberDeltaY = a.Y - b.Y + 1000;
            string pair = eventDB.GetControl(first.control).code + " / " + eventDB.GetControl(second.control).code;
            string result = new Reports().CreateEventReadinessReport(eventDB);
            Assert.IsFalse(result.Contains(pair));
        }

        [TestMethod]
        public void EventReadinessBacksideReportsMissingParticipantRecords()
        {
            Setup(@"reports\marymoor6.ppen");
            CoursePdfSettings pdfSettings = new CoursePdfSettings { IncludeBacksideInfo = true };
            string result = new Reports().CreateEventReadinessReport(eventDB, pdfSettings);
            Assert.IsTrue(result.Contains(ReportText.ResourceManager.GetString("EventReadiness_BacksideNoRecords")));
        }

        /// <summary>Creates a normal course with one proper intersection between non-adjacent legs.</summary>
        private void SetupCrossedLegEvent()
        {
            undomgr = new UndoMgr(10);
            eventDB = new EventDB(undomgr);
            Event ev = new Event();
            ev.mapScale = 15000;
            ev.allControlsPrintScale = 15000;

            undomgr.BeginCommand(1, "Create crossed legs");
            eventDB.ChangeEvent(ev);

            Id<ControlPoint> start = eventDB.AddControlPoint(new ControlPoint(ControlPointKind.Start, null, new PointF(-100, 0)));
            Id<ControlPoint> control1 = eventDB.AddControlPoint(new ControlPoint(ControlPointKind.Normal, "31", new PointF(0, 100)));
            Id<ControlPoint> control2 = eventDB.AddControlPoint(new ControlPoint(ControlPointKind.Normal, "32", new PointF(100, 0)));
            Id<ControlPoint> control3 = eventDB.AddControlPoint(new ControlPoint(ControlPointKind.Normal, "33", new PointF(100, 100)));
            Id<ControlPoint> finish = eventDB.AddControlPoint(new ControlPoint(ControlPointKind.Finish, null, new PointF(0, 0)));

            Id<CourseControl> finishCourseControl = eventDB.AddCourseControl(new CourseControl(finish, Id<CourseControl>.None));
            Id<CourseControl> thirdCourseControl = eventDB.AddCourseControl(new CourseControl(control3, finishCourseControl));
            Id<CourseControl> secondCourseControl = eventDB.AddCourseControl(new CourseControl(control2, thirdCourseControl));
            Id<CourseControl> firstCourseControl = eventDB.AddCourseControl(new CourseControl(control1, secondCourseControl));
            Id<CourseControl> startCourseControl = eventDB.AddCourseControl(new CourseControl(start, firstCourseControl));

            Course course = new Course(CourseKind.Normal, "Crossing course", 15000, 1);
            course.firstCourseControl = startCourseControl;
            eventDB.AddCourse(course);
            undomgr.EndCommand(1);
        }

        /// <summary>Counts non-overlapping occurrences of a string in report HTML.</summary>
        private static int CountOccurrences(string text, string value)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0) {
                ++count;
                index += value.Length;
            }

            return count;
        }

        [TestMethod]
        public void CourseSummary()
        {
            Setup(@"reports\marymoor.coursescribe");

            Reports reports = new Reports();
            string result = reports.CreateCourseSummaryReport(eventDB);


            string expectedFile = TestUtil.GetTestFile(@"reports\CourseSummary_expected.html");
            string actualFile = TestUtil.GetTestFile(@"reports\CourseSummary_result.html");

            File.WriteAllText(actualFile, result);
            try {
                TextFileTestUtil.CompareTextFileBaseline(actualFile, expectedFile);
            }
            finally {
                File.Delete(actualFile);
            }

        }

        [TestMethod]
        public void CourseSummary2()
        {
            Setup(@"reports\relay1.ppen");

            Reports reports = new Reports();
            string result = reports.CreateCourseSummaryReport(eventDB);

            string expectedFile = TestUtil.GetTestFile(@"reports\CourseSummary2_expected.html");
            string actualFile = TestUtil.GetTestFile(@"reports\CourseSummary2_result.html");

            File.WriteAllText(actualFile, result);
            try {
                TextFileTestUtil.CompareTextFileBaseline(actualFile, expectedFile);
            }
            finally {
                File.Delete(actualFile);
            }

        }

        [TestMethod]
        public void LegLength()
        {
            Setup(@"reports\marymoor5.ppen");

            Reports reports = new Reports();
            string result = reports.CreateLegLengthReport(eventDB);

            string expectedFile = TestUtil.GetTestFile(@"reports\LegLength_expected.html");
            string actualFile = TestUtil.GetTestFile(@"reports\LegLength_result.html");

            File.WriteAllText(actualFile, result);
            try {
                TextFileTestUtil.CompareTextFileBaseline(actualFile, expectedFile);
            }
            finally {
                File.Delete(actualFile);
            }
        }

        [TestMethod]
        public void LegLength2()
        {
            Setup(@"reports\relay2.ppen");

            Reports reports = new Reports();
            string result = reports.CreateLegLengthReport(eventDB);

            string expectedFile = TestUtil.GetTestFile(@"reports\LegLength2_expected.html");
            string actualFile = TestUtil.GetTestFile(@"reports\LegLength2_result.html");

            File.WriteAllText(actualFile, result);
            try {
                TextFileTestUtil.CompareTextFileBaseline(actualFile, expectedFile);
            }
            finally {
                File.Delete(actualFile);
            }
        }

        [TestMethod]
        public void TestReport()
        {
            Setup(@"reports\marymoor.coursescribe");
            string expectedFile = TestUtil.GetTestFile(@"reports\TestReport_expected.html");
            string actualFile = TestUtil.GetTestFile(@"reports\TestReport_result.html");

            Reports reports = new Reports();
            string result = reports.CreateTestReport(eventDB);

            File.WriteAllText(actualFile, result);
            try {
                TextFileTestUtil.CompareTextFileBaseline(actualFile, expectedFile);
            }
            finally {
                File.Delete(actualFile);
            }
        }
    }
}

#endif //TEST
