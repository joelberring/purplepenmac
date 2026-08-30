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

using NUnit.Framework;
using PurplePen;
using PurplePen.ViewModels;

namespace PurplePenViewModels.Tests
{
    /// <summary>
    /// Tests for view-model behavior that does not require application services.
    /// </summary>
    [TestFixture]
    public class MainWindowViewModelTests
    {
        /// <summary>
        /// A course row can request navigation only after the user selects it.
        /// </summary>
        [Test]
        public void CourseOverviewNavigationRequiresAndRecordsSelectedCourse()
        {
            CourseControlOverviewDialogViewModel overview = new CourseControlOverviewDialogViewModel();

            Assert.That(overview.NavigateToSelectedCourseCommand.CanExecute(null), Is.False);
            overview.SelectedCourse = new CourseOverviewItem { CourseId = new Id<Course>(2), Name = "Yellow" };

            Assert.That(overview.NavigateToSelectedCourseCommand.CanExecute(null), Is.True);
            overview.NavigateToSelectedCourseCommand.Execute(null);
            Assert.That(overview.NavigateToSelectedCourseRequested, Is.True);
        }

        /// <summary>
        /// Class assignments made in the course-load grid accompany the participant count.
        /// </summary>
        [Test]
        public void CourseLoadRowRetainsClassAndParticipantCount()
        {
            Controller.CourseLoadInfo info = new Controller.CourseLoadInfo {
                courseName = "Blue",
                className = "H21",
                load = 42,
            };
            LoadRow row = new LoadRow(info) { ClassName = "D21", LoadText = "36" };

            Controller.CourseLoadInfo result = row.ToCourseLoadInfo();

            Assert.That(result.className, Is.EqualTo("D21"));
            Assert.That(result.load, Is.EqualTo(36));
        }

        /// <summary>Overview exposes stable control identity and records control navigation.</summary>
        [Test]
        public void ControlOverviewNavigationUsesStableIdentity()
        {
            CourseControlOverviewDialogViewModel overview = new CourseControlOverviewDialogViewModel();
            ControlOverviewItem control = new ControlOverviewItem {
                ControlId = new Id<ControlPoint>(7),
                Name = "42",
                CourseIds = new[] { new Id<Course>(3) },
            };
            overview.SelectedControl = control;

            Assert.That(overview.NavigateToSelectedControlCommand.CanExecute(null), Is.True);
            overview.NavigateToSelectedControlCommand.Execute(null);
            Assert.That(overview.NavigateToSelectedControlRequested, Is.True);
            Assert.That(overview.SelectedControl.ControlId, Is.EqualTo(new Id<ControlPoint>(7)));
        }

        /// <summary>Course comparison reports common controls and distinct controls/legs.</summary>
        [Test]
        public void CourseOverviewComparisonReportsDifferences()
        {
            CourseControlOverviewDialogViewModel overview = new CourseControlOverviewDialogViewModel();
            CourseOverviewItem first = new CourseOverviewItem {
                CourseId = new Id<Course>(1), Name = "A",
                ControlIds = new[] { new Id<ControlPoint>(1), new Id<ControlPoint>(2) },
                LegKeys = new[] { "1-2" },
            };
            CourseOverviewItem second = new CourseOverviewItem {
                CourseId = new Id<Course>(2), Name = "B",
                ControlIds = new[] { new Id<ControlPoint>(2), new Id<ControlPoint>(3) },
                LegKeys = new[] { "2-3" },
            };
            overview.ComparisonCourse = first;
            overview.ComparisonWithCourse = second;

            overview.CompareCoursesCommand.Execute(null);

            Assert.That(overview.CompareCoursesRequested, Is.True);
            Assert.That(overview.ComparisonSummary, Does.Contain("1 common controls"));
            Assert.That(overview.ComparisonSummary, Does.Contain("1 only here"));
        }

        [Test]
        public void CourseOverviewLoadValidation_AllowsBlankOrNonNegativeWholeNumberOnly()
        {
            CourseControlOverviewDialogViewModel blank = new CourseControlOverviewDialogViewModel();
            blank.Courses.Add(new CourseOverviewItem { LoadText = "  " });
            Assert.That(blank.ValidateLoadTexts(), Is.True);
            blank.SaveEditsCommand.Execute(null);
            Assert.That(blank.SaveEditsRequested, Is.True);

            CourseControlOverviewDialogViewModel zero = new CourseControlOverviewDialogViewModel();
            zero.Courses.Add(new CourseOverviewItem { LoadText = "0" });
            Assert.That(zero.ValidateLoadTexts(), Is.True);

            CourseControlOverviewDialogViewModel invalid = new CourseControlOverviewDialogViewModel();
            invalid.Courses.Add(new CourseOverviewItem { LoadText = "-1" });
            invalid.SaveEditsCommand.Execute(null);
            Assert.That(invalid.SaveEditsRequested, Is.False);
            Assert.That(invalid.LoadValidationMessageKey, Is.EqualTo("CourseControlOverviewDialog_InvalidLoad"));

            invalid.Courses[0].LoadText = "many";
            Assert.That(invalid.ValidateLoadTexts(), Is.False);
        }

    }
}
