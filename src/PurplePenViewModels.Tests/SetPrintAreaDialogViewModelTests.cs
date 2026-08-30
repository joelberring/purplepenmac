// SetPrintAreaDialogViewModelTests.cs
//
// Tests for safety feedback in the interactive print-area tool.

using NUnit.Framework;
using PurplePen.ViewModels;
using PurplePen;
using System.Drawing;

namespace PurplePenViewModels.Tests
{
    /// <summary>Tests for the print-area dialog's margin safety feedback.</summary>
    [TestFixture]
    public class SetPrintAreaDialogViewModelTests
    {
        /// <summary>Margins under six millimetres visibly warn about printer clipping.</summary>
        [Test]
        public void LowMarginWarning_TracksConfiguredPaperMargin()
        {
            SetPrintAreaDialogViewModel viewModel = new SetPrintAreaDialogViewModel();

            viewModel.PaperMargin = 23;
            Assert.That(viewModel.HasLowMarginWarning, Is.True);

            viewModel.PaperMargin = 24;
            Assert.That(viewModel.HasLowMarginWarning, Is.False);
        }

        /// <summary>Rotation survives the dialog's PrintArea bridge.</summary>
        [Test]
        public void Rotation_RoundTripsThroughPrintArea()
        {
            SetPrintAreaDialogViewModel viewModel = new SetPrintAreaDialogViewModel {
                PrintArea = new PrintArea {
                    autoPrintArea = false,
                    printAreaRectangle = new RectangleF(1, 2, 30, 40),
                    rotation = 27.5F,
                }
            };

            Assert.That(viewModel.RotationDegrees, Is.EqualTo(27.5).Within(0.001));
            Assert.That(viewModel.PrintArea.rotation, Is.EqualTo(27.5F).Within(0.001F));
        }

        /// <summary>Named print templates clone without sharing mutable state.</summary>
        [Test]
        public void PrintWorkshopTemplate_ClonesConfiguration()
        {
            PrintWorkshopTemplate source = new PrintWorkshopTemplate {
                Name = "A5 duplex", PageWidth = 827, PageHeight = 583,
                PageMargins = 24, Landscape = true, Rotation = 12.5F,
                FixSizeToPaper = true, PageLayout = 2, IncludeBackside = true,
                BacksideText = "Club",
            };
            PrintWorkshopTemplate copy = source.Clone();

            Assert.That(copy.Name, Is.EqualTo(source.Name));
            Assert.That(copy.Rotation, Is.EqualTo(12.5F));
            Assert.That(copy.IncludeBackside, Is.True);
        }
    }
}
