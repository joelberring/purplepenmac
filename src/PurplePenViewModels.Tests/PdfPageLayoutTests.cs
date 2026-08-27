using System.Collections.Generic;
using System.Drawing;
using System.IO;
using NUnit.Framework;

namespace PurplePen.ViewModels.Tests
{
    /// <summary>Tests for placing independent course views together on PDF pages.</summary>
    [TestFixture]
    public class PdfPageLayoutTests
    {
        /// <summary>Four map views share one physical sheet while retaining map scale by cropping each view.</summary>
        [Test]
        public void FourUp_UsesFourProportionalSlots()
        {
            List<CoursePage> pages = new List<CoursePage>();
            for (int i = 0; i < 5; ++i) {
                pages.Add(new CoursePage {
                    paperSize = new PrintingPaperSize("A4", 827, 1169),
                    printRectangle = new RectangleF(100, 200, 400, 500),
                    mapRectangle = new RectangleF(i, i, 50, 60),
                });
            }

            List<CoursePageSheet> sheets = CoursePageSheetLayout.LayoutSheets(pages, CoursePdfSettings.PdfPageLayout.FourPerPage);

            Assert.That(sheets, Has.Count.EqualTo(2));
            Assert.That(sheets[0].pages, Has.Count.EqualTo(4));
            Assert.That(sheets[1].pages, Has.Count.EqualTo(1));
            Assert.That(sheets[0].pages[0].printRectangle, Is.EqualTo(new RectangleF(50, 100, 200, 250)));
            Assert.That(sheets[0].pages[1].printRectangle.Left, Is.EqualTo(463.5F).Within(0.01F));
            Assert.That(sheets[0].pages[2].printRectangle.Top, Is.EqualTo(684.5F).Within(0.01F));
            Assert.That(sheets[0].pages[0].mapRectangle, Is.EqualTo(new RectangleF(12.5F, 15F, 25F, 30F)));
            Assert.That(sheets[1].pages[0].mapRectangle.Left, Is.EqualTo(16.5F));
        }

        /// <summary>Two map views make two A5 fields on an A4 sheet without changing scale.</summary>
        [Test]
        public void TwoUp_UsesTopAndBottomA5FieldsAndCropsHeight()
        {
            CoursePage page = new CoursePage {
                paperSize = new PrintingPaperSize("A4", 827, 1169),
                printRectangle = new RectangleF(100, 200, 400, 500),
                mapRectangle = new RectangleF(0, 0, 50, 60),
            };

            List<CoursePageSheet> sheets = CoursePageSheetLayout.LayoutSheets(new[] { page, page }, CoursePdfSettings.PdfPageLayout.TwoPerPage);

            Assert.That(sheets, Has.Count.EqualTo(1));
            Assert.That(sheets[0].pages[0].printRectangle, Is.EqualTo(new RectangleF(100, 100, 400, 250)));
            Assert.That(sheets[0].pages[1].printRectangle, Is.EqualTo(new RectangleF(100, 684.5F, 400, 250)));
            Assert.That(sheets[0].pages[0].mapRectangle, Is.EqualTo(new RectangleF(0, 15, 50, 30)));
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
            Assert.That(sheets[0].pages[0].mapRectangle.Left, Is.EqualTo(2.5F));
            Assert.That(sheets[0].pages[3].mapRectangle.Left, Is.EqualTo(5.5F));
            Assert.That(sheets[1].pages[0].mapRectangle.Left, Is.EqualTo(2.5F));
            Assert.That(sheets[1].pages[3].mapRectangle.Left, Is.EqualTo(5.5F));
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
    }
}
