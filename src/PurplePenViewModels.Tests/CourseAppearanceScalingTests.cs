// CourseAppearanceScalingTests.cs
//
// Regression tests for IOF course-symbol scaling across source-map and print
// scales. These tests verify that the printed dimensions depend on the selected
// IOF standard, not on the original OCAD/OpenMapper file scale.

using NUnit.Framework;
using PurplePen;
using PurplePen.ViewModels;

namespace PurplePenViewModels.Tests
{
    /// <summary>Tests standard-aware course-symbol scaling and dialog normalization.</summary>
    [TestFixture]
    public class CourseAppearanceScalingTests
    {
        /// <summary>ISOM dimensions follow the output scale and not the source map scale.</summary>
        [TestCase(7500F, 15000F, 5.35F, 0.35F, 4.0F)]
        [TestCase(10000F, 10000F, 8.025F, 0.525F, 6.0F)]
        [TestCase(15000F, 10000F, 8.025F, 0.525F, 6.0F)]
        [TestCase(7500F, 7500F, 10.7F, 0.7F, 8.0F)]
        [TestCase(15000F, 7500F, 10.7F, 0.7F, 8.0F)]
        public void IsomPrintedDimensionsFollowStandard(float mapScale, float printScale,
                                                        float expectedOutsideDiameter, float expectedLineWidth,
                                                        float expectedNumberHeight)
        {
            CourseAppearance appearance = CreateStandardAppearance("2017");

            AssertPrintedDimensions(appearance, mapScale, printScale, expectedOutsideDiameter,
                                    expectedLineWidth, expectedNumberHeight);
        }

        /// <summary>ISSprOM dimensions follow the output scale and not the source map scale.</summary>
        [TestCase(3000F, 4000F, 6.35F, 0.35F, 4.0F)]
        [TestCase(4000F, 4000F, 6.35F, 0.35F, 4.0F)]
        [TestCase(3000F, 3000F, 8.466667F, 0.466667F, 5.333333F)]
        [TestCase(4000F, 3000F, 8.466667F, 0.466667F, 5.333333F)]
        public void SprintPrintedDimensionsFollowStandard(float mapScale, float printScale,
                                                          float expectedOutsideDiameter, float expectedLineWidth,
                                                          float expectedNumberHeight)
        {
            CourseAppearance appearance = CreateStandardAppearance("Spr2019");

            AssertPrintedDimensions(appearance, mapScale, printScale, expectedOutsideDiameter,
                                    expectedLineWidth, expectedNumberHeight);
        }

        /// <summary>Opening an older standard-sized event replaces map-relative scaling with standard scaling.</summary>
        [Test]
        public void DialogNormalizesLegacyMapRelativeStandardSizes()
        {
            CourseAppearance oldAppearance = CreateStandardAppearance("2017");
            oldAppearance.itemScaling = ItemScaling.RelativeToMap;
            CourseAppearanceDialogViewModel viewModel = new CourseAppearanceDialogViewModel();

            viewModel.Settings = oldAppearance;

            Assert.That(viewModel.ScaleItemSizesIndex, Is.EqualTo(2));
            Assert.That(viewModel.ScaleItemSizesEnabled, Is.False);
            Assert.That(viewModel.Settings.itemScaling, Is.EqualTo(ItemScaling.RelativeTo15000));
        }

        /// <summary>An older file renders correctly immediately, before the appearance dialog is opened.</summary>
        [Test]
        public void LegacyMapRelativeStandardSizesRenderUsingIofReferenceScale()
        {
            CourseAppearance oldAppearance = CreateStandardAppearance("2017");
            oldAppearance.itemScaling = ItemScaling.RelativeToMap;

            float printedDiameter = PrintedControlDiameter(oldAppearance, 10000F, 10000F);

            Assert.That(printedDiameter, Is.EqualTo(8.025F).Within(0.0001F));
        }

        /// <summary>Creates standard-sized course appearance for the requested map standard.</summary>
        private static CourseAppearance CreateStandardAppearance(string mapStandard)
        {
            return new CourseAppearance {
                mapStandard = mapStandard,
                itemScaling = ItemScaling.RelativeTo15000,
                controlCircleSize = 1.0F,
                lineWidth = 1.0F,
                numberHeight = 1.0F,
                centerDotDiameter = 0.0F,
            };
        }

        /// <summary>Calculates the physical outside diameter after map-to-print scaling.</summary>
        private static float PrintedControlDiameter(CourseAppearance appearance, float mapScale, float printScale)
        {
            float courseObjectRatio = appearance.GetCourseObjectScaleRatio(mapScale, printScale);
            float mapToPrintRatio = mapScale / printScale;
            return appearance.ControlCircleOutsideDiameter * courseObjectRatio * mapToPrintRatio;
        }

        /// <summary>Asserts the physical paper dimensions after both course and map transforms.</summary>
        private static void AssertPrintedDimensions(CourseAppearance appearance, float mapScale, float printScale,
                                                    float expectedOutsideDiameter, float expectedLineWidth,
                                                    float expectedNumberHeight)
        {
            float courseObjectRatio = appearance.GetCourseObjectScaleRatio(mapScale, printScale);
            float mapToPrintRatio = mapScale / printScale;
            float printedLineWidth = NormalCourseAppearance.lineThickness * appearance.lineWidth *
                                     courseObjectRatio * mapToPrintRatio;
            float printedNumberHeight = NormalCourseAppearance.nominalControlNumberHeight * appearance.numberHeight *
                                        courseObjectRatio * mapToPrintRatio;

            Assert.That(PrintedControlDiameter(appearance, mapScale, printScale),
                        Is.EqualTo(expectedOutsideDiameter).Within(0.0001F));
            Assert.That(printedLineWidth, Is.EqualTo(expectedLineWidth).Within(0.0001F));
            Assert.That(printedNumberHeight, Is.EqualTo(expectedNumberHeight).Within(0.0001F));
        }
    }
}
