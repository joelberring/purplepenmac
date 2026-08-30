/* Copyright (c) 2026, Purple Pen contributors.
 * All rights reserved.
 */

#if TEST
using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PurplePen.MapModel;

namespace PurplePen.Tests
{
    /// <summary>Tests the non-mutating contour-only map-filter decisions.</summary>
    [TestClass]
    public class TrainingContourOnlyRenderingTests
    {
        /// <summary>Uses standard contour families when a training exercise has no explicit symbol choices.</summary>
        [TestMethod]
        public void AutomaticSelectionUsesStandardContourFamilies()
        {
            SymDef contour = new LineSymDef("Contour", "101.3", null, 1, LineJoinMode.Round, LineCapMode.Round);
            SymDef indexContour = new LineSymDef("Index contour", "102.0", null, 1, LineJoinMode.Round, LineCapMode.Round);
            SymDef vegetation = new LineSymDef("Vegetation", "410.0", null, 1, LineJoinMode.Round, LineCapMode.Round);
            SymDef unrelated101 = new LineSymDef("Unrelated", "1010", null, 1, LineJoinMode.Round, LineCapMode.Round);

            Assert.IsTrue(TrainingContourOnlyRendering.IncludesSymbol(contour, new string[0]));
            Assert.IsTrue(TrainingContourOnlyRendering.IncludesSymbol(indexContour, null));
            Assert.IsFalse(TrainingContourOnlyRendering.IncludesSymbol(vegetation, new string[0]));
            Assert.IsFalse(TrainingContourOnlyRendering.IncludesSymbol(unrelated101, new string[0]));
        }

        /// <summary>Honors an explicit symbol list and emits a closed path for clipping.</summary>
        [TestMethod]
        public void ExplicitSelectionAndPolygonPath()
        {
            SymDef contour = new LineSymDef("Contour", "101.3", null, 1, LineJoinMode.Round, LineCapMode.Round);
            SymDef indexContour = new LineSymDef("Index contour", "102.0", null, 1, LineJoinMode.Round, LineCapMode.Round);

            Assert.IsTrue(TrainingContourOnlyRendering.IncludesSymbol(contour, new string[] { "101.3" }));
            Assert.IsFalse(TrainingContourOnlyRendering.IncludesSymbol(indexContour, new string[] { "101.3" }));
            Assert.AreEqual(3, TrainingContourOnlyRendering.CreatePolygonPath(new PointF[] {
                new PointF(0, 0), new PointF(10, 0), new PointF(10, 10)
            }).Count);
            Assert.AreEqual(6, TrainingContourOnlyRendering.CreateCorridorMaskPath(new RectangleF(0, 0, 20, 20), new PointF[] {
                new PointF(5, 8), new PointF(15, 8), new PointF(15, 12), new PointF(5, 12)
            }).Count);
        }
    }
}
#endif
