using NUnit.Framework;
using PurplePen;
using PurplePen.ViewModels;
using System.Collections.Generic;
using System.Drawing;
using System;

namespace PurplePenViewModels.Tests
{
    /// <summary>Tests editable mobile inspection rows and file-system-independent exchange.</summary>
    [TestFixture]
    public class MobileControlInspectionDialogViewModelTests
    {
        [Test]
        public void ExportAndImportPreservesEditableInspectionFields()
        {
            MobileControlInspectionDialogViewModel viewModel = CreateViewModel();
            MobileControlInspectionRowViewModel row = viewModel.Controls[0];
            row.Status = MobileControlInspectionStatus.Ready;
            row.ObservedCode = "31";
            row.Comment = "North side of stone";
            row.Latitude = 59.3;
            row.Longitude = 18.1;
            row.AccuracyMeters = 3;

            string json = viewModel.ExportPackageJson();
            MobileControlInspectionDialogViewModel imported = CreateViewModel();
            imported.ImportPackageJson(json);

            Assert.That(imported.Controls[0].Status, Is.EqualTo(MobileControlInspectionStatus.Ready));
            Assert.That(imported.Controls[0].ObservedCode, Is.EqualTo("31"));
            Assert.That(imported.Controls[0].Comment, Is.EqualTo("North side of stone"));
            Assert.That(imported.Controls[0].CodeValidationStatus, Is.EqualTo(MobileControlCodeValidationStatus.Correct));
            Assert.That(imported.HasConflicts, Is.False);
        }

        [Test]
        public void ImportReportsMissingAndUnknownControls()
        {
            MobileControlInspectionDialogViewModel viewModel = CreateViewModel();
            MobileControlInspectionPackage package = viewModel.CreatePackage();
            package.Controls.RemoveAt(0);
            package.Controls.Add(new MobileControlInspectionRecord { ControlId = 999, Sequence = 2 });

            MobileControlInspectionDialogViewModel imported = CreateViewModel();
            MobileControlInspectionMergeResult result = imported.ImportPackageJson(MobileControlInspectionPackageSerializer.Serialize(package));

            Assert.That(result.HasConflicts, Is.True);
            Assert.That(imported.HasConflicts, Is.True);
            Assert.That(imported.Conflicts, Has.Count.EqualTo(2));
        }

        /// <summary>Prevents a package from another course or course part from changing the active overview.</summary>
        [Test]
        public void ImportRejectsPackageForAnotherCourseOrPart()
        {
            MobileControlInspectionDialogViewModel viewModel = CreateViewModel();
            MobileControlInspectionPackage package = viewModel.CreatePackage();
            package.CourseId = 8;

            Assert.Throws<MobileControlInspectionPackageCourseMismatchException>(() =>
                viewModel.ImportPackageJson(MobileControlInspectionPackageSerializer.Serialize(package)));
            Assert.That(viewModel.ImportError, Is.EqualTo(MobileControlInspectionImportError.CourseMismatch));
            Assert.That(viewModel.Controls[0].Status, Is.EqualTo(MobileControlInspectionStatus.Uninspected));

            package.CourseId = 7;
            package.Part = 0;
            Assert.Throws<MobileControlInspectionPackageCourseMismatchException>(() =>
                viewModel.ImportPackageJson(MobileControlInspectionPackageSerializer.Serialize(package)));
            Assert.That(viewModel.ImportError, Is.EqualTo(MobileControlInspectionImportError.CourseMismatch));
        }

        /// <summary>Converts malformed JSON from the UI command into an observable import error.</summary>
        [Test]
        public void ImportCommandReportsInvalidJsonWithoutThrowing()
        {
            MobileControlInspectionDialogViewModel viewModel = CreateViewModel();
            bool hasImportErrorChanged = false;
            viewModel.PropertyChanged += (sender, args) => {
                if (args.PropertyName == nameof(MobileControlInspectionDialogViewModel.HasImportError))
                    hasImportErrorChanged = true;
            };
            viewModel.ImportJsonText = "{ invalid JSON";

            Assert.DoesNotThrow(() => viewModel.ImportJsonCommand.Execute(null));
            Assert.That(viewModel.ImportError, Is.EqualTo(MobileControlInspectionImportError.InvalidPackage));
            Assert.That(viewModel.HasImportError, Is.True);
            Assert.That(hasImportErrorChanged, Is.True);
            Assert.That(viewModel.Controls[0].Status, Is.EqualTo(MobileControlInspectionStatus.Uninspected));
        }

        private static MobileControlInspectionDialogViewModel CreateViewModel()
        {
            MobileControlInspectionDialogViewModel viewModel = new MobileControlInspectionDialogViewModel();
            viewModel.LoadOverview(new List<MobileControlInspectionEntry> {
                new MobileControlInspectionEntry(1, new Id<ControlPoint>(12), "31", new PointF(10, 20), ControlPointKind.Normal),
                new MobileControlInspectionEntry(2, new Id<ControlPoint>(13), "32", new PointF(20, 30), ControlPointKind.Normal)
            }, new CourseDesignator(new Id<Course>(7)));
            return viewModel;
        }
    }
}
