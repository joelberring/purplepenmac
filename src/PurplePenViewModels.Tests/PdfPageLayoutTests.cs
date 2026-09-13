using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace PurplePen.ViewModels.Tests
{
    /// <summary>Tests for placing independent course views together on PDF pages.</summary>
    [TestFixture]
    public class PdfPageLayoutTests
    {
        [Test]
        public void CalibrationFactor_UsesMeasuredLengthAndCanReset()
        {
            CreatePdfCoursesDialogViewModel viewModel = new CreatePdfCoursesDialogViewModel();
            viewModel.MeasuredCalibrationMm = 101.5m;
            viewModel.ApplyCalibrationCommand.Execute(null);

            Assert.That(viewModel.ScaleCalibrationFactor, Is.EqualTo(1.015).Within(0.0001));
            Assert.That(viewModel.CalculatedCalibrationFactor, Is.EqualTo(1.015).Within(0.0001));

            viewModel.ResetCalibrationCommand.Execute(null);
            Assert.That(viewModel.ScaleCalibrationFactor, Is.EqualTo(1.0).Within(0.0001));
        }
        /// <summary>Four landscape A6 map pages become one landscape A4 sheet without changing map scale.</summary>
        [Test]
        public void FourUp_PromotesLandscapeA6PagesToLandscapeA4()
        {
            List<CoursePage> pages = new List<CoursePage>();
            for (int i = 0; i < 5; ++i) {
                pages.Add(new CoursePage {
                    paperSize = new PrintingPaperSize("Custom", 413, 583),
                    landscape = true,
                    printRectangle = new RectangleF(0, 0, 583, 413),
                    mapRectangle = new RectangleF(i, i, 50, 60),
                });
            }

            List<CoursePageSheet> sheets = CoursePageSheetLayout.LayoutSheets(pages, CoursePdfSettings.PdfPageLayout.FourPerPage);

            Assert.That(sheets, Has.Count.EqualTo(2));
            Assert.That(sheets[0].pages, Has.Count.EqualTo(4));
            Assert.That(sheets[1].pages, Has.Count.EqualTo(1));
            Assert.That(sheets[0].paperSize.Name, Is.EqualTo("A4"));
            Assert.That(sheets[0].landscape, Is.True);
            Assert.That(sheets[0].pages[0].printRectangle.Left, Is.EqualTo(0.75F).Within(0.01F));
            Assert.That(sheets[0].pages[0].printRectangle.Top, Is.EqualTo(0.25F).Within(0.01F));
            Assert.That(sheets[0].pages[0].printRectangle.Width, Is.EqualTo(583F).Within(0.01F));
            Assert.That(sheets[0].pages[0].printRectangle.Height, Is.EqualTo(413F).Within(0.01F));
            Assert.That(sheets[0].pages[1].printRectangle.Left, Is.EqualTo(585.25F).Within(0.01F));
            Assert.That(sheets[0].pages[2].printRectangle.Top, Is.EqualTo(413.75F).Within(0.01F));
            Assert.That(sheets[0].pages[0].mapRectangle, Is.EqualTo(new RectangleF(0, 0, 50, 60)));
            Assert.That(sheets[1].pages[0].mapRectangle.Left, Is.EqualTo(4F));
        }

        /// <summary>Four-up crops an A4 logical page to an A6 slot while preserving physical scale.</summary>
        [Test]
        public void FourUp_CropsLargerLogicalPagesInsteadOfShrinkingThem()
        {
            CoursePage page = new CoursePage {
                paperSize = new PrintingPaperSize("A4", 827, 1169),
                printRectangle = new RectangleF(0, 0, 827, 1169),
                mapRectangle = new RectangleF(0, 0, 50, 60),
            };

            List<CoursePageSheet> sheets = CoursePageSheetLayout.LayoutSheets(new[] { page }, CoursePdfSettings.PdfPageLayout.FourPerPage);

            Assert.That(sheets, Has.Count.EqualTo(1));
            Assert.That(sheets[0].paperSize.Name, Is.EqualTo("A4"));
            Assert.That(sheets[0].pages[0].printRectangle.Left, Is.EqualTo(0F).Within(0.01F));
            Assert.That(sheets[0].pages[0].printRectangle.Top, Is.EqualTo(0F).Within(0.01F));
            Assert.That(sheets[0].pages[0].printRectangle.Width, Is.EqualTo(413.5F).Within(0.01F));
            Assert.That(sheets[0].pages[0].printRectangle.Height, Is.EqualTo(584.5F).Within(0.01F));
            Assert.That(sheets[0].pages[0].mapRectangle.Left, Is.EqualTo(12.5F).Within(0.01F));
            Assert.That(sheets[0].pages[0].mapRectangle.Top, Is.EqualTo(15F).Within(0.01F));
            Assert.That(sheets[0].pages[0].mapRectangle.Width, Is.EqualTo(25F).Within(0.01F));
            Assert.That(sheets[0].pages[0].mapRectangle.Height, Is.EqualTo(30F).Within(0.01F));
        }

        /// <summary>Two landscape A5 maps stack on one portrait A4 sheet at unchanged scale.</summary>
        [Test]
        public void TwoUp_PromotesLandscapeA5PagesToPortraitA4()
        {
            CoursePage page = new CoursePage {
                paperSize = new PrintingPaperSize("Custom", 583, 827),
                landscape = true,
                printRectangle = new RectangleF(0, 0, 827, 583),
                mapRectangle = new RectangleF(0, 0, 50, 60),
            };

            List<CoursePageSheet> sheets = CoursePageSheetLayout.LayoutSheets(new[] { page, page }, CoursePdfSettings.PdfPageLayout.TwoPerPage);

            Assert.That(sheets, Has.Count.EqualTo(1));
            Assert.That(sheets[0].paperSize.Name, Is.EqualTo("A4"));
            Assert.That(sheets[0].landscape, Is.False);
            Assert.That(sheets[0].pages[0].printRectangle, Is.EqualTo(new RectangleF(0, 0.75F, 827, 583)));
            Assert.That(sheets[0].pages[1].printRectangle.Top, Is.EqualTo(585.25F).Within(0.01F));
            Assert.That(sheets[0].pages[0].mapRectangle, Is.EqualTo(page.mapRectangle));
        }

        /// <summary>The PDF dialog persists the selected number of map views per page.</summary>
        [Test]
        public void PdfDialog_PageLayout_RoundTripsFourUpChoice()
        {
            CreatePdfCoursesDialogViewModel viewModel = new CreatePdfCoursesDialogViewModel {
                PageLayoutIndex = 2,
            };

            CoursePdfSettings settings = viewModel.Settings;
            CreatePdfCoursesDialogViewModel restoredViewModel = new CreatePdfCoursesDialogViewModel {
                Settings = settings,
            };

            Assert.That(settings.PageLayout, Is.EqualTo(CoursePdfSettings.PdfPageLayout.FourPerPage));
            Assert.That(restoredViewModel.PageLayoutIndex, Is.EqualTo(2));
        }

        /// <summary>Multi-up choices always combine selected courses into one PDF file.</summary>
        [Test]
        public void PdfDialog_MultiUpForcesSingleSharedFile()
        {
            CreatePdfCoursesDialogViewModel viewModel = new CreatePdfCoursesDialogViewModel {
                FileFormatIndex = (int)CoursePdfSettings.PdfFileCreation.FilePerCourse,
                PageLayoutIndex = 2,
            };

            Assert.That(viewModel.FileFormatIndex, Is.EqualTo((int)CoursePdfSettings.PdfFileCreation.SingleFile));
            Assert.That(viewModel.IsFileFormatEnabled, Is.False);
            Assert.That(viewModel.IsMultiUpLayout, Is.True);
            Assert.That(viewModel.Settings.FileCreation, Is.EqualTo(CoursePdfSettings.PdfFileCreation.SingleFile));
        }

        /// <summary>The PDF dialog persists the selected training presentation.</summary>
        [Test]
        public void PdfDialog_TrainingProfile_RoundTrips()
        {
            CreatePdfCoursesDialogViewModel viewModel = new CreatePdfCoursesDialogViewModel {
                TrainingExerciseRenderProfileIndex = (int)TrainingExerciseRenderProfile.Answer,
            };

            CoursePdfSettings settings = viewModel.Settings;
            CreatePdfCoursesDialogViewModel restoredViewModel = new CreatePdfCoursesDialogViewModel {
                Settings = settings,
            };

            Assert.That(settings.TrainingExerciseRenderProfile, Is.EqualTo(TrainingExerciseRenderProfile.Answer));
            Assert.That(restoredViewModel.TrainingExerciseRenderProfileIndex, Is.EqualTo((int)TrainingExerciseRenderProfile.Answer));
        }

        /// <summary>Copies repeat the selected page set in order before starting the next repetition.</summary>
        [Test]
        public void FourUp_TwoCopies_RepeatsTheFullVariationSet()
        {
            List<CoursePage> pages = new List<CoursePage>();
            for (int i = 0; i < 4; ++i) {
                pages.Add(new CoursePage {
                    paperSize = new PrintingPaperSize("A4", 827, 1169),
                    printRectangle = new RectangleF(0, 0, 827, 1169),
                    mapRectangle = new RectangleF(i, 0, 10, 10),
                });
            }

            List<CoursePageSheet> sheets = CoursePageSheetLayout.LayoutSheets(pages, CoursePdfSettings.PdfPageLayout.FourPerPage, 2);

            Assert.That(sheets, Has.Count.EqualTo(2));
            Assert.That(sheets[0].pages[0].mapRectangle.Left, Is.EqualTo(2.5F).Within(0.001F));
            Assert.That(sheets[0].pages[3].mapRectangle.Left, Is.EqualTo(5.5F).Within(0.001F));
            Assert.That(sheets[1].pages[0].mapRectangle.Left, Is.EqualTo(2.5F).Within(0.001F));
            Assert.That(sheets[1].pages[3].mapRectangle.Left, Is.EqualTo(5.5F).Within(0.001F));
        }

        /// <summary>Each copy starts on a new sheet when its predecessor has a partial sheet.</summary>
        [Test]
        public void FourUp_TwoCopies_PartialSetMatchesPreviewSheetPlan()
        {
            List<CoursePage> pages = new List<CoursePage>();
            for (int i = 0; i < 5; ++i) {
                pages.Add(new CoursePage {
                    paperSize = new PrintingPaperSize("A4", 827, 1169),
                    printRectangle = new RectangleF(0, 0, 827, 1169),
                    mapRectangle = new RectangleF(i, 0, 10, 10),
                });
            }

            List<CoursePageSheet> sheets = CoursePageSheetLayout.LayoutSheets(pages, CoursePdfSettings.PdfPageLayout.FourPerPage, 2);
            List<int> previewSlotCounts = CoursePageSheetLayout.GetSheetSlotCounts(5, CoursePdfSettings.PdfPageLayout.FourPerPage, 2);

            Assert.That(sheets.Select(sheet => sheet.pages.Count), Is.EqualTo(previewSlotCounts));
            Assert.That(sheets, Has.Count.EqualTo(4));
        }

        /// <summary>The PDF dialog persists the requested number of copies.</summary>
        [Test]
        public void PdfDialog_Copies_RoundTripsRequestedCount()
        {
            CreatePdfCoursesDialogViewModel viewModel = new CreatePdfCoursesDialogViewModel {
                Copies = 12m,
            };

            CoursePdfSettings settings = viewModel.Settings;
            CreatePdfCoursesDialogViewModel restoredViewModel = new CreatePdfCoursesDialogViewModel {
                Settings = settings,
            };

            Assert.That(settings.Copies, Is.EqualTo(12));
            Assert.That(restoredViewModel.Copies, Is.EqualTo(12m));
        }

        /// <summary>The PDF dialog keeps the duplex information-back settings.</summary>
        [Test]
        public void PdfDialog_BacksideInformation_RoundTrips()
        {
            CreatePdfCoursesDialogViewModel viewModel = new CreatePdfCoursesDialogViewModel {
                IncludeBacksideInfo = true,
                BacksideText = "Night relay – do not turn before the start signal",
            };

            CoursePdfSettings settings = viewModel.Settings;
            CreatePdfCoursesDialogViewModel restoredViewModel = new CreatePdfCoursesDialogViewModel {
                Settings = settings,
            };

            Assert.That(settings.IncludeBacksideInfo, Is.True);
            Assert.That(settings.BacksideText, Is.EqualTo("Night relay – do not turn before the start signal"));
            Assert.That(restoredViewModel.IncludeBacksideInfo, Is.True);
            Assert.That(restoredViewModel.BacksideText, Is.EqualTo("Night relay – do not turn before the start signal"));
        }

        /// <summary>Paper workshop lists physical sheets as front/back pairs and preserves 2-up slots.</summary>
        [Test]
        public void WorkshopPreview_TwoUp_UsesPhysicalDuplexPairs()
        {
            CreatePdfCoursesDialogViewModel viewModel = new CreatePdfCoursesDialogViewModel {
                PageLayoutIndex = 1,
                Copies = 1m,
                IncludeBacksideInfo = true,
                SelectedCourseDesignators = new[] {
                    new CourseDesignator(new Id<Course>(1)),
                    new CourseDesignator(new Id<Course>(2)),
                    new CourseDesignator(new Id<Course>(3)),
                },
            };
            viewModel.RefreshWorkshopPreview();

            Assert.That(viewModel.WorkshopPreview, Has.Count.EqualTo(4));
            Assert.That(viewModel.WorkshopPreview.Select(item => item.IsFront), Is.EqualTo(new[] { true, false, true, false }));
            Assert.That(viewModel.WorkshopPreview.Select(item => item.SheetNumber), Is.EqualTo(new[] { 1, 1, 2, 2 }));
            Assert.That(viewModel.WorkshopPreview.Select(item => item.SlotCount), Is.EqualTo(new[] { 2, 2, 1, 1 }));
        }

        /// <summary>Four-up preview keeps the final partial sheet and duplex pairing.</summary>
        [Test]
        public void WorkshopPreview_FourUp_DuplexesPartialSheet()
        {
            CreatePdfCoursesDialogViewModel viewModel = new CreatePdfCoursesDialogViewModel {
                PageLayoutIndex = 2,
                Copies = 1m,
                IncludeBacksideInfo = true,
                SelectedCourseDesignators = Enumerable.Range(1, 5)
                    .Select(number => new CourseDesignator(new Id<Course>(number))).ToArray(),
            };
            viewModel.RefreshWorkshopPreview();

            Assert.That(viewModel.WorkshopPreview.Select(item => item.SlotCount), Is.EqualTo(new[] { 4, 4, 1, 1 }));
            Assert.That(viewModel.WorkshopPreview[0].SlotSummary, Does.Contain("[1]"));
            Assert.That(viewModel.WorkshopPreview[2].SlotSummary, Does.Contain("[1]"));
        }

        /// <summary>MeOS-style Swedish CSV headings import participant information for backsides.</summary>
        [Test]
        public void BacksideInfoCsv_ImportsSwedishRelayColumns()
        {
            List<BacksideInfoRecord> records;
            using (StringReader reader = new StringReader("Lag;Sträcka;Namn;Klass;Lagnamn;Bana\n12;2;Ada Löpare;D21;Stjärnans OK;Långa")) {
                records = BacksideInfoCsv.Import(reader);
            }

            Assert.That(records, Has.Count.EqualTo(1));
            Assert.That(records[0].Team, Is.EqualTo(12));
            Assert.That(records[0].Leg, Is.EqualTo(2));
            Assert.That(records[0].Name, Is.EqualTo("Ada Löpare"));
            Assert.That(records[0].ClassName, Is.EqualTo("D21"));
            Assert.That(records[0].TeamName, Is.EqualTo("Stjärnans OK"));
            Assert.That(records[0].Course, Is.EqualTo("Långa"));
        }

        /// <summary>Start-list matching prefers explicit course/class data and never chooses between duplicates by row order.</summary>
        [Test]
        public void BacksideInfoRecord_FindUniqueMatch_PrefersExplicitKeysAndRejectsDuplicates()
        {
            List<BacksideInfoRecord> records = new List<BacksideInfoRecord> {
                new BacksideInfoRecord { Team = 7, Leg = 2, Name = "Fallback" },
                new BacksideInfoRecord { Team = 7, Leg = 2, Course = "Long", ClassName = "H21", Name = "Correct" },
                new BacksideInfoRecord { Team = 7, Leg = 2, Course = "Long", ClassName = "D21", Name = "Wrong class" },
            };

            BacksideInfoRecord match = BacksideInfoRecord.FindUniqueMatch(records, 7, 2, "Long", "Long", "H21");

            Assert.That(match, Is.Not.Null);
            Assert.That(match.Name, Is.EqualTo("Correct"));

            records.Add(new BacksideInfoRecord { Team = 7, Leg = 2, Course = "Long", ClassName = "H21", Name = "Duplicate" });
            Assert.That(BacksideInfoRecord.FindUniqueMatch(records, 7, 2, "Long", "Long", "H21"), Is.Null);
        }
    }
}
