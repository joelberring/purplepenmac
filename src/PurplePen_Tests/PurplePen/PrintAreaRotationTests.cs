#if TEST
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PurplePen.Tests
{
    /// <summary>Tests persistence-related rotation semantics used by print-area layout.</summary>
    [TestClass]
    public class PrintAreaRotationTests
    {
        [TestMethod]
        public void PrintAreaRotationParticipatesInEqualityAndClone()
        {
            PrintArea original = new PrintArea {
                autoPrintArea = false,
                printAreaRectangle = new RectangleF(1, 2, 30, 40),
                rotation = 17.5F,
            };
            PrintArea clone = (PrintArea)original.Clone();

            Assert.AreEqual(original, clone);
            clone.rotation = 18F;
            Assert.AreNotEqual(original, clone);
        }

        [TestMethod]
        public void SheetLayoutCarriesRotationToEverySlot()
        {
            CoursePage page = new CoursePage {
                mapRectangle = new RectangleF(0, 0, 100, 100),
                printRectangle = new RectangleF(0, 0, 100, 100),
                mapRotation = 22F,
                paperSize = new PrintingPaperSize("Test", 100, 100),
            };

            List<CoursePageSheet> sheets = CoursePageSheetLayout.LayoutSheets(new[] { page }, CoursePdfSettings.PdfPageLayout.OnePerPage);

            Assert.AreEqual(22F, sheets[0].pages[0].mapRotation);
        }

        [TestMethod]
        public void RotatedPdfMapDoesNotUseCopiedPagePath()
        {
            CoursePage page = new CoursePage { mapRectangle = new RectangleF(0, 0, 100, 100), mapRotation = 15F };

            bool canCopy = CoursePdf.CanCopyPdfMapPage(page, 1, new RectangleF(0, 0, 4, 4), new SizeF(4, 4), page.mapRectangle);

            Assert.IsFalse(canCopy);
        }

        [TestMethod]
        public void RotatedBandTransformMatchesCompletePageTransform()
        {
            CoursePage page = new CoursePage {
                mapRectangle = new RectangleF(10, 20, 100, 80),
                printRectangle = new RectangleF(50, 30, 400, 320),
                mapRotation = 22F,
            };
            CoursePage band = new CoursePage { printRectangle = new RectangleF(50, 190, 400, 160) };
            Matrix bandTransform = CoursePrinting.CreateBandTransform(page, band, 100, 400, 320);
            Matrix completeTransform = Geometry.CreateInvertedRectangleTransform(page.mapRectangle, new RectangleF(0, 0, 400, 320));
            completeTransform.RotateAt(page.mapRotation, Geometry.RectCenter(page.mapRectangle), MatrixOrder.Prepend);
            completeTransform.Translate(0, -160, MatrixOrder.Append);
            PointF point = new PointF(38, 72);
            PointF expected = Geometry.TransformPoint(point, completeTransform);
            PointF actual = Geometry.TransformPoint(point, bandTransform);

            Assert.AreEqual(expected.X, actual.X, 0.001F);
            Assert.AreEqual(expected.Y, actual.Y, 0.001F);
        }
    }
}
#endif
