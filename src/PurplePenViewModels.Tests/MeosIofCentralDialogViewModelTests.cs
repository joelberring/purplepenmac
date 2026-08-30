using NUnit.Framework;
using PurplePen;
using PurplePen.MapModel;
using PurplePen.ViewModels;
using System.IO;

namespace PurplePenViewModels.Tests
{
    /// <summary>Tests observable MeOS/IOF central routing and diagnostics.</summary>
    [TestFixture]
    public class MeosIofCentralDialogViewModelTests
    {
        [Test]
        public void ImportStartList_PopulatesObservableClasses()
        {
            string file = Path.Combine(Path.GetTempPath(), "meos-startlist.csv");
            File.WriteAllText(file, "Lag;Sträcka;Namn;Klass\n1;1;Ada;D21\n2;1;Bo;H21");
            try {
                MeosIofCentralDialogViewModel viewModel = new MeosIofCentralDialogViewModel { FileName = file };
                viewModel.ImportStartList();
                Assert.That(viewModel.ImportedClasses, Is.EquivalentTo(new[] { "D21", "H21" }));
            Assert.That(viewModel.ResultState, Is.EqualTo(MeosOperationState.Imported));
            Assert.That(viewModel.ResultCount, Is.EqualTo(2));
            }
            finally { File.Delete(file); }
        }

        [Test]
        public void ApplySafeClassSuggestions_UsesOnlySafeSuggestions()
        {
            int applied = 0;
            MeosIofCentralDialogViewModel viewModel = new MeosIofCentralDialogViewModel { ApplyClassCounts = counts => applied = counts.Count };
            viewModel.ClassSuggestions.Add(new MeosClassSuggestion { ClassName = "D21", ImportedCount = 3, IsConflict = false });
            viewModel.ClassSuggestions.Add(new MeosClassSuggestion { ClassName = "H21", ImportedCount = 2, IsConflict = true });
            Assert.That(viewModel.HasSafeSuggestions, Is.True);
            viewModel.ApplySafeClassSuggestions();
            Assert.That(applied, Is.EqualTo(0), "No imported records means no mutation is allowed.");
        }

        [Test]
        public void ExportAndLiveloxCallbacks_ReceiveChosenDestination()
        {
            string exportPath = "";
            string liveloxPath = "";
            MeosIofCentralDialogViewModel viewModel = new MeosIofCentralDialogViewModel {
                FileName = "/chosen/course.xml",
                ExportCourseData = path => { exportPath = path; return new MeosOperationResult { State = MeosOperationState.ExportInvalid, Path = path }; },
                CreateLiveloxPackage = path => { liveloxPath = path; return new MeosOperationResult { State = MeosOperationState.LiveloxCreated, Path = path }; },
            };
            viewModel.ExportCourseDataFile();
            viewModel.CreateLiveloxPackageFile();
            Assert.That(exportPath, Is.EqualTo("/chosen/course.xml"));
            Assert.That(liveloxPath, Is.EqualTo("/chosen/course.xml"));
            Assert.That(viewModel.ResultPath, Is.EqualTo("/chosen/course.xml"));
        }

        [Test]
        public void ExportCallback_CanReturnLineAndColumnSchemaDiagnostics()
        {
            MeosIofCentralDialogViewModel viewModel = new MeosIofCentralDialogViewModel {
                FileName = "/chosen/course.xml",
                ExportCourseData = path => {
                    IofCourseDataValidationResult validation = IofCourseDataExchange.Validate("<CourseData xmlns=\"http://www.orienteering.org/datastandard/3.0\" iofVersion=\"3.0\">\n  <Broken />\n</CourseData>");
                    MeosOperationResult result = new MeosOperationResult { State = validation.IsValid ? MeosOperationState.ExportValid : MeosOperationState.ExportInvalid };
                    result.Diagnostics.AddRange(validation.Errors.ConvertAll(error => error.ToString())); return result;
                },
            };
            viewModel.ExportCourseDataFile();
            Assert.That(viewModel.ResultState, Is.EqualTo(MeosOperationState.ExportInvalid));
            Assert.That(viewModel.Diagnostics, Has.Some.Contains(":"));
        }

        [Test]
        public void ExportAndLiveloxCallbackFailures_AreReportedInsteadOfEscaping()
        {
            MeosIofCentralDialogViewModel viewModel = new MeosIofCentralDialogViewModel {
                FileName = "/chosen/course.xml",
                ExportCourseData = path => throw new IOException("Export is unavailable"),
                CreateLiveloxPackage = path => throw new IOException("Package is unavailable"),
            };

            viewModel.ExportCourseDataFile();
            Assert.That(viewModel.ResultState, Is.EqualTo(MeosOperationState.Error));
            Assert.That(viewModel.Diagnostics, Is.EqualTo(new[] { "Export is unavailable" }));

            Assert.That(viewModel.CreateLiveloxPackageFile(), Is.False);
            Assert.That(viewModel.ResultState, Is.EqualTo(MeosOperationState.Error));
            Assert.That(viewModel.Diagnostics, Is.EqualTo(new[] { "Package is unavailable" }));
        }

        [Test]
        public void FailedComparison_ClearsPreviousDiffAndDiagnostics()
        {
            MeosIofCentralDialogViewModel viewModel = new MeosIofCentralDialogViewModel {
                EventDB = new EventDB(new UndoMgr(20)),
                FileName = "/does/not/exist.xml",
            };
            viewModel.DiffItems.Add("Old difference");
            viewModel.Diagnostics.Add("Old diagnostic");

            viewModel.CompareCourseData();

            Assert.That(viewModel.ResultState, Is.EqualTo(MeosOperationState.Error));
            Assert.That(viewModel.DiffItems, Is.Empty);
            Assert.That(viewModel.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(viewModel.Diagnostics[0], Does.Not.Contain("Old"));
        }

        [Test]
        public void CompareWithoutEvent_IsExplicitAndNonMutating()
        {
            MeosIofCentralDialogViewModel viewModel = new MeosIofCentralDialogViewModel { FileName = "/does/not/exist.xml" };
            viewModel.CompareCourseData();
            Assert.That(viewModel.IsNoEvent, Is.True);
        }

        [Test]
        public void FailedRetry_ClearsPreviousImportSoItCannotBeApplied()
        {
            string goodFile = Path.Combine(Path.GetTempPath(), "meos-good-" + System.Guid.NewGuid().ToString("N") + ".csv");
            string badFile = Path.Combine(Path.GetTempPath(), "meos-bad-" + System.Guid.NewGuid().ToString("N") + ".xml");
            File.WriteAllText(goodFile, "Lag;Sträcka;Namn;Klass\n1;1;Ada;D21");
            File.WriteAllText(badFile, "<StartList>");
            try {
                UndoMgr undo = new UndoMgr(20);
                EventDB eventDB = new EventDB(undo);
                undo.BeginCommand(1, "class");
                Id<Course> courseId = eventDB.AddCourse(new Course(CourseKind.Normal, "Blue", 15000, 1));
                eventDB.AddEventClass(new EventClass { Name = "D21", CourseId = courseId });
                undo.EndCommand(1);
                int applied = 0;
                MeosIofCentralDialogViewModel viewModel = new MeosIofCentralDialogViewModel {
                    EventDB = eventDB,
                    ApplyClassCounts = counts => applied = counts.Count,
                    FileName = goodFile,
                };

                viewModel.ImportStartList();
                Assert.That(viewModel.HasSafeSuggestions, Is.True);
                viewModel.FileName = badFile;
                viewModel.ImportStartList();

                Assert.That(viewModel.ResultState, Is.EqualTo(MeosOperationState.Error));
                Assert.That(viewModel.ResultCount, Is.EqualTo(0));
                Assert.That(viewModel.ImportedClasses, Is.Empty);
                Assert.That(viewModel.ClassSuggestions, Is.Empty);
                Assert.That(viewModel.HasSafeSuggestions, Is.False);
                viewModel.ApplySafeClassSuggestions();
                Assert.That(applied, Is.EqualTo(0));
            }
            finally {
                File.Delete(goodFile);
                File.Delete(badFile);
            }
        }
    }
}
