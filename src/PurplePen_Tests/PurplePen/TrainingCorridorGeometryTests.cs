/* Copyright (c) 2026, Purple Pen contributors.
 * All rights reserved.
 */

using System;
using System.Drawing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PurplePen.Tests
{
    /// <summary>Tests geometry generation for corridor-orienteering exercises.</summary>
    [TestClass]
    public class TrainingCorridorGeometryTests
    {
        /// <summary>A straight centre line produces parallel boundaries at half the requested width.</summary>
        [TestMethod]
        public void StraightCorridorProducesExpectedPolygon()
        {
            TrainingCorridorOutline outline = TrainingCorridorGeometry.CreateOutline(new PointF[] { new PointF(0, 0), new PointF(10, 0) }, 4);

            CollectionAssert.AreEqual(new PointF[] { new PointF(0, 2), new PointF(10, 2) }, (System.Collections.ICollection)outline.LeftBoundary);
            CollectionAssert.AreEqual(new PointF[] { new PointF(0, -2), new PointF(10, -2) }, (System.Collections.ICollection)outline.RightBoundary);
            CollectionAssert.AreEqual(new PointF[] { new PointF(0, 2), new PointF(10, 2), new PointF(10, -2), new PointF(0, -2) }, (System.Collections.ICollection)outline.Polygon);
        }

        /// <summary>A bent centre line keeps the two mitered boundaries continuous.</summary>
        [TestMethod]
        public void BentCorridorUsesMiteredJoin()
        {
            TrainingCorridorOutline outline = TrainingCorridorGeometry.CreateOutline(new PointF[] { new PointF(0, 0), new PointF(10, 0), new PointF(10, 10) }, 4);

            Assert.AreEqual(new PointF(8, 2), outline.LeftBoundary[1]);
            Assert.AreEqual(new PointF(12, -2), outline.RightBoundary[1]);
            Assert.AreEqual(6, outline.Polygon.Count);
        }

        /// <summary>Invalid widths, duplicate segments, and self-intersecting centre lines are rejected.</summary>
        [TestMethod]
        public void InvalidGeometryIsRejected()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => TrainingCorridorGeometry.CreateOutline(new PointF[] { new PointF(0, 0), new PointF(10, 0) }, 0));
            Assert.ThrowsException<ArgumentException>(() => TrainingCorridorGeometry.CreateOutline(new PointF[] { new PointF(0, 0), new PointF(0, 0) }, 4));
            Assert.ThrowsException<ArgumentException>(() => TrainingCorridorGeometry.CreateOutline(new PointF[] { new PointF(0, 0), new PointF(10, 10), new PointF(0, 10), new PointF(10, 0) }, 4));
        }

        /// <summary>A sharp bend cannot create the giant miter spikes produced by raw mouse geometry.</summary>
        [TestMethod]
        public void SharpBendLimitsMiterLength()
        {
            TrainingCorridorOutline outline = TrainingCorridorGeometry.CreateOutline(
                new PointF[] { new PointF(0, 0), new PointF(10, 0), new PointF(1, 1) }, 2);

            Assert.IsTrue(Geometry.Distance(new PointF(10, 0), outline.LeftBoundary[1]) <= 2.001F);
            Assert.IsTrue(Geometry.Distance(new PointF(10, 0), outline.RightBoundary[1]) <= 2.001F);
        }
    }
}
