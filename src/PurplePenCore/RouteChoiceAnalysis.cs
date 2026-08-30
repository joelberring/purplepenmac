/* Copyright (c) 2026, Purple Pen contributors.
 * All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Drawing;

using PurplePen.MapModel;
using PurplePen.Graphics2D;

namespace PurplePen
{
    /// <summary>
    /// Calculates route-choice measurements from an explicitly supplied route polyline.
    /// The analysis deliberately has no terrain or elevation assumptions; callers can use
    /// it both for the stored geometry of a leg and for a candidate route that has not yet
    /// been persisted.
    /// </summary>
    public static class RouteChoiceAnalysis
    {
        /// <summary>
        /// Analyzes the actual polyline of a leg, automatically including its stored bends when present.
        /// </summary>
        /// <param name="eventDB">Event containing the controls and map scale.</param>
        /// <param name="controlId1">The first control of the leg.</param>
        /// <param name="controlId2">The second control of the leg.</param>
        /// <returns>Immutable measurements for the leg route.</returns>
        public static RouteChoiceStatistics AnalyzeLeg(EventDB eventDB, Id<ControlPoint> controlId1, Id<ControlPoint> controlId2)
        {
            if (eventDB == null)
                throw new ArgumentNullException(nameof(eventDB));

            return AnalyzeLeg(eventDB, controlId1, controlId2, QueryEvent.FindLeg(eventDB, controlId1, controlId2));
        }

        /// <summary>
        /// Analyzes the actual polyline of a leg, including any stored bends.
        /// </summary>
        /// <param name="eventDB">Event containing the controls and map scale.</param>
        /// <param name="controlId1">The first control of the leg.</param>
        /// <param name="controlId2">The second control of the leg.</param>
        /// <param name="legId">The leg whose bends should be included, or None for a straight leg.</param>
        /// <returns>Immutable measurements for the leg route.</returns>
        public static RouteChoiceStatistics AnalyzeLeg(EventDB eventDB, Id<ControlPoint> controlId1, Id<ControlPoint> controlId2, Id<Leg> legId)
        {
            if (eventDB == null)
                throw new ArgumentNullException(nameof(eventDB));

            SymPath path = QueryEvent.GetLegPath(eventDB, controlId1, controlId2, legId);
            return AnalyzeRoute(path.Points, eventDB.GetEvent().mapScale);
        }

        /// <summary>
        /// Analyzes an arbitrary candidate route described by points in map coordinates.
        /// Consecutive duplicate points are ignored, which makes editing a route deterministic.
        /// </summary>
        /// <param name="routePoints">Ordered route points in map coordinates.</param>
        /// <param name="mapScale">The event map scale used to convert map millimeters to meters.</param>
        /// <returns>Immutable measurements for the supplied route.</returns>
        public static RouteChoiceStatistics AnalyzeRoute(IEnumerable<PointF> routePoints, float mapScale)
        {
            if (routePoints == null)
                throw new ArgumentNullException(nameof(routePoints));
            if (mapScale <= 0 || Single.IsNaN(mapScale) || Single.IsInfinity(mapScale))
                throw new ArgumentOutOfRangeException(nameof(mapScale), "Map scale must be a positive finite value.");

            List<PointF> points = new List<PointF>();
            foreach (PointF point in routePoints) {
                if (Single.IsNaN(point.X) || Single.IsInfinity(point.X) || Single.IsNaN(point.Y) || Single.IsInfinity(point.Y))
                    throw new ArgumentException("Route points must have finite coordinates.", nameof(routePoints));

                if (points.Count == 0 || points[points.Count - 1] != point)
                    points.Add(point);
            }

            double mapLength = 0;
            int directionChanges = 0;
            double totalDirectionChangeDegrees = 0;
            double maximumDirectionChangeDegrees = 0;
            double? previousDirection = null;

            for (int index = 1; index < points.Count; ++index) {
                PointF firstPoint = points[index - 1];
                PointF secondPoint = points[index];
                double segmentLength = Geometry.Distance(firstPoint, secondPoint);
                if (segmentLength == 0)
                    continue;

                mapLength += segmentLength;
                double direction = Math.Atan2(secondPoint.Y - firstPoint.Y, secondPoint.X - firstPoint.X);
                if (previousDirection.HasValue) {
                    double directionChangeDegrees = AbsoluteDirectionDifferenceRadians(previousDirection.Value, direction) * 180.0 / Math.PI;
                    if (directionChangeDegrees > 0) {
                        ++directionChanges;
                        totalDirectionChangeDegrees += directionChangeDegrees;
                        maximumDirectionChangeDegrees = Math.Max(maximumDirectionChangeDegrees, directionChangeDegrees);
                    }
                }

                previousDirection = direction;
            }

            return new RouteChoiceStatistics(mapLength, mapLength * mapScale / 1000.0,
                                             directionChanges, totalDirectionChangeDegrees, maximumDirectionChangeDegrees);
        }

        /// <summary>Gets the smallest absolute difference between two directions, including a 180 degree U-turn.</summary>
        /// <param name="firstDirection">The first direction in radians.</param>
        /// <param name="secondDirection">The second direction in radians.</param>
        /// <returns>An angle from zero through PI radians.</returns>
        private static double AbsoluteDirectionDifferenceRadians(double firstDirection, double secondDirection)
        {
            double difference = Math.Abs(firstDirection - secondDirection) % (Math.PI * 2.0);
            return (difference > Math.PI) ? (Math.PI * 2.0) - difference : difference;
        }
    }

    /// <summary>Immutable geometry-only measurements for a route choice.</summary>
    public sealed class RouteChoiceStatistics
    {
        /// <summary>Initializes the immutable route-choice measurements.</summary>
        internal RouteChoiceStatistics(double mapLength, double lengthMeters, int directionChanges,
                                       double totalDirectionChangeDegrees, double maximumDirectionChangeDegrees)
        {
            MapLength = mapLength;
            LengthMeters = lengthMeters;
            DirectionChanges = directionChanges;
            TotalDirectionChangeDegrees = totalDirectionChangeDegrees;
            MaximumDirectionChangeDegrees = maximumDirectionChangeDegrees;
        }

        /// <summary>Gets the total length in the map coordinate system, normally millimeters on the real map.</summary>
        public double MapLength { get; }

        /// <summary>Gets the total route length in terrain meters.</summary>
        public double LengthMeters { get; }

        /// <summary>Gets the number of non-straight joins between route segments.</summary>
        public int DirectionChanges { get; }

        /// <summary>Gets the sum of absolute direction changes in degrees.</summary>
        public double TotalDirectionChangeDegrees { get; }

        /// <summary>Gets the largest single direction change in degrees.</summary>
        public double MaximumDirectionChangeDegrees { get; }
    }
}
