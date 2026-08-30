/* Copyright (c) 2026, Purple Pen contributors.
 * All rights reserved.
 */

#if TEST
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Drawing;

namespace PurplePen.Tests
{
    /// <summary>Tests geometry-only route-choice measurements.</summary>
    [TestClass]
    public class RouteChoiceAnalysisTests
    {
        /// <summary>Calculates length and turn measurements for a route with two bends.</summary>
        [TestMethod]
        public void AnalyzeRouteMeasuresLengthAndDirectionChanges()
        {
            RouteChoiceStatistics statistics = RouteChoiceAnalysis.AnalyzeRoute(new PointF[] {
                new PointF(0, 0), new PointF(3, 0), new PointF(3, 4), new PointF(0, 4)
            }, 10000);

            Assert.AreEqual(10.0, statistics.MapLength, 0.0001);
            Assert.AreEqual(100.0, statistics.LengthMeters, 0.0001);
            Assert.AreEqual(2, statistics.DirectionChanges);
            Assert.AreEqual(180.0, statistics.TotalDirectionChangeDegrees, 0.0001);
            Assert.AreEqual(90.0, statistics.MaximumDirectionChangeDegrees, 0.0001);
        }

        /// <summary>Ignores duplicate points and treats a straight route as having no direction changes.</summary>
        [TestMethod]
        public void AnalyzeRouteIgnoresDuplicatePoints()
        {
            RouteChoiceStatistics statistics = RouteChoiceAnalysis.AnalyzeRoute(new PointF[] {
                new PointF(0, 0), new PointF(0, 0), new PointF(5, 0), new PointF(10, 0)
            }, 15000);

            Assert.AreEqual(10.0, statistics.MapLength, 0.0001);
            Assert.AreEqual(150.0, statistics.LengthMeters, 0.0001);
            Assert.AreEqual(0, statistics.DirectionChanges);
            Assert.AreEqual(0.0, statistics.TotalDirectionChangeDegrees, 0.0001);
        }

        /// <summary>Counts a U-turn as the full 180 degree direction change.</summary>
        [TestMethod]
        public void AnalyzeRouteCountsUTurn()
        {
            RouteChoiceStatistics statistics = RouteChoiceAnalysis.AnalyzeRoute(new PointF[] {
                new PointF(0, 0), new PointF(4, 0), new PointF(0, 0)
            }, 10000);

            Assert.AreEqual(1, statistics.DirectionChanges);
            Assert.AreEqual(180.0, statistics.TotalDirectionChangeDegrees, 0.0001);
            Assert.AreEqual(180.0, statistics.MaximumDirectionChangeDegrees, 0.0001);
        }

        /// <summary>Uses the bends stored for a leg when analyzing that leg's route.</summary>
        [TestMethod]
        public void AnalyzeLegUsesStoredBends()
        {
            UndoMgr undoMgr = new UndoMgr(5);
            EventDB eventDB = new EventDB(undoMgr);
            undoMgr.BeginCommand(1, "Create route-choice test leg");
            Id<ControlPoint> firstControlId = eventDB.AddControlPoint(new ControlPoint(ControlPointKind.Normal, "31", new PointF(0, 0)));
            Id<ControlPoint> secondControlId = eventDB.AddControlPoint(new ControlPoint(ControlPointKind.Normal, "32", new PointF(4, 4)));
            Leg leg = new Leg(firstControlId, secondControlId) { bends = new PointF[] { new PointF(4, 0) } };
            eventDB.AddLeg(leg);
            eventDB.ChangeEvent(new Event { mapScale = 10000 });
            undoMgr.EndCommand(1);

            RouteChoiceStatistics statistics = RouteChoiceAnalysis.AnalyzeLeg(eventDB, firstControlId, secondControlId);

            Assert.AreEqual(8.0, statistics.MapLength, 0.0001);
            Assert.AreEqual(80.0, statistics.LengthMeters, 0.0001);
            Assert.AreEqual(1, statistics.DirectionChanges);
            Assert.AreEqual(90.0, statistics.TotalDirectionChangeDegrees, 0.0001);
        }

        /// <summary>Rejects a route measurement without a usable map scale.</summary>
        [TestMethod]
        public void AnalyzeRouteRequiresPositiveFiniteMapScale()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => RouteChoiceAnalysis.AnalyzeRoute(new PointF[0], 0));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => RouteChoiceAnalysis.AnalyzeRoute(new PointF[0], Single.NaN));
        }
    }
}
#endif
