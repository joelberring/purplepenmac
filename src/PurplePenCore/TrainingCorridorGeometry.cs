/* Copyright (c) 2026, Purple Pen contributors.
 * All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Drawing;

using PurplePen.Graphics2D;

namespace PurplePen
{
    /// <summary>Creates the map-coordinate outline used by a corridor-orienteering exercise.</summary>
    public static class TrainingCorridorGeometry
    {
        const double Epsilon = 0.00001;
        const double MaximumMiterScale = 2.0;

        /// <summary>
        /// Creates a simple, miter-joined corridor around a non-self-intersecting centre line.
        /// The returned polygon is not repeated at its final point because drawing targets close filled polygons themselves.
        /// </summary>
        /// <param name="centreLine">Ordered corridor centre-line points in map coordinates.</param>
        /// <param name="width">Complete corridor width in map coordinates.</param>
        /// <returns>Immutable left, right, and filled-polygon outlines for the corridor.</returns>
        /// <exception cref="ArgumentException">The centre line or resulting corridor polygon is invalid.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The width is not a positive finite value.</exception>
        public static TrainingCorridorOutline CreateOutline(IList<PointF> centreLine, float width)
        {
            if (centreLine == null)
                throw new ArgumentNullException(nameof(centreLine));
            if (width <= 0 || Single.IsNaN(width) || Single.IsInfinity(width))
                throw new ArgumentOutOfRangeException(nameof(width), "Corridor width must be a positive finite value.");
            if (centreLine.Count < 2)
                throw new ArgumentException("A corridor centre line must have at least two points.", nameof(centreLine));

            PointF[] points = new PointF[centreLine.Count];
            for (int index = 0; index < points.Length; ++index) {
                PointF point = centreLine[index];
                if (!IsFinite(point))
                    throw new ArgumentException("Corridor centre-line points must have finite coordinates.", nameof(centreLine));
                if (index > 0 && Geometry.Distance(points[index - 1], point) <= Epsilon)
                    throw new ArgumentException("A corridor centre line cannot have duplicate adjacent points.", nameof(centreLine));
                points[index] = point;
            }

            ValidateSimpleCentreLine(points, nameof(centreLine));

            double halfWidth = width / 2.0;
            PointF[] leftBoundary = new PointF[points.Length];
            PointF[] rightBoundary = new PointF[points.Length];

            for (int index = 0; index < points.Length; ++index) {
                PointF leftOffset;
                if (index == 0)
                    leftOffset = LeftNormal(points[0], points[1]);
                else if (index == points.Length - 1)
                    leftOffset = LeftNormal(points[index - 1], points[index]);
                else
                    leftOffset = MiterNormal(points[index - 1], points[index], points[index + 1], nameof(centreLine));

                leftBoundary[index] = Offset(points[index], leftOffset, halfWidth);
                rightBoundary[index] = Offset(points[index], leftOffset, -halfWidth);
            }

            PointF[] polygon = new PointF[points.Length * 2];
            Array.Copy(leftBoundary, polygon, points.Length);
            for (int index = 0; index < rightBoundary.Length; ++index)
                polygon[points.Length + index] = rightBoundary[rightBoundary.Length - index - 1];

            // Tight hairpins may make the two sides overlap locally. The rendering path uses
            // a non-zero winding fill so this becomes one continuous tube instead of either
            // rejecting valid hand-drawn geometry or producing an unbounded miter spike.
            return new TrainingCorridorOutline(leftBoundary, rightBoundary, polygon);
        }

        /// <summary>Validates that a centre line has no intersections or repeated non-adjacent vertices.</summary>
        private static void ValidateSimpleCentreLine(PointF[] points, string parameterName)
        {
            for (int firstSegment = 0; firstSegment < points.Length - 1; ++firstSegment) {
                for (int secondSegment = firstSegment + 1; secondSegment < points.Length - 1; ++secondSegment) {
                    if (secondSegment == firstSegment + 1)
                        continue;
                    if (SegmentsIntersect(points[firstSegment], points[firstSegment + 1], points[secondSegment], points[secondSegment + 1]))
                        throw new ArgumentException("A corridor centre line cannot self-intersect.", parameterName);
                }
            }
        }

        /// <summary>Returns a left-side miter offset at a centre-line join.</summary>
        private static PointF MiterNormal(PointF previous, PointF current, PointF next, string parameterName)
        {
            PointF firstNormal = LeftNormal(previous, current);
            PointF secondNormal = LeftNormal(current, next);
            double x = firstNormal.X + secondNormal.X;
            double y = firstNormal.Y + secondNormal.Y;
            double length = Math.Sqrt(x * x + y * y);
            if (length <= Epsilon)
                throw new ArgumentException("A corridor centre line cannot reverse direction at a join.", parameterName);

            PointF miter = new PointF((float)(x / length), (float)(y / length));
            double denominator = miter.X * secondNormal.X + miter.Y * secondNormal.Y;
            if (denominator <= Epsilon)
                throw new ArgumentException("A corridor centre line has an invalid join.", parameterName);

            // Very sharp mouse-drawn corners otherwise create arbitrarily long spikes.
            // Limiting the join keeps the outline local to the centre line while retaining
            // exact miters for ordinary bends such as right angles.
            double miterScale = Math.Min(1.0 / denominator, MaximumMiterScale);
            return new PointF((float)(miter.X * miterScale), (float)(miter.Y * miterScale));
        }

        /// <summary>Returns the normalized left-side normal for a non-zero line segment.</summary>
        private static PointF LeftNormal(PointF first, PointF second)
        {
            double x = second.X - first.X;
            double y = second.Y - first.Y;
            double length = Math.Sqrt(x * x + y * y);
            return new PointF((float)(-y / length), (float)(x / length));
        }

        /// <summary>Offsets a point by a normalized direction and a distance.</summary>
        private static PointF Offset(PointF point, PointF normal, double distance)
        {
            return new PointF((float)(point.X + normal.X * distance), (float)(point.Y + normal.Y * distance));
        }

        /// <summary>Determines whether two closed segments intersect, including collinear contact.</summary>
        private static bool SegmentsIntersect(PointF firstStart, PointF firstEnd, PointF secondStart, PointF secondEnd)
        {
            // The existing helper handles ordinary intersections; the orientation checks below add reliable collinear contact.
            if (Geometry.LineSegmentsIntersect(firstStart, firstEnd, secondStart, secondEnd))
                return true;

            return IsPointOnSegment(firstStart, firstEnd, secondStart) || IsPointOnSegment(firstStart, firstEnd, secondEnd) ||
                   IsPointOnSegment(secondStart, secondEnd, firstStart) || IsPointOnSegment(secondStart, secondEnd, firstEnd);
        }

        /// <summary>Determines whether a point lies on a line segment using the corridor geometry tolerance.</summary>
        private static bool IsPointOnSegment(PointF start, PointF end, PointF point)
        {
            double cross = (end.X - start.X) * (point.Y - start.Y) - (end.Y - start.Y) * (point.X - start.X);
            if (Math.Abs(cross) > Epsilon)
                return false;

            return point.X >= Math.Min(start.X, end.X) - Epsilon && point.X <= Math.Max(start.X, end.X) + Epsilon &&
                   point.Y >= Math.Min(start.Y, end.Y) - Epsilon && point.Y <= Math.Max(start.Y, end.Y) + Epsilon;
        }

        /// <summary>Determines whether a map-coordinate point is finite.</summary>
        private static bool IsFinite(PointF point)
        {
            return !Single.IsNaN(point.X) && !Single.IsInfinity(point.X) && !Single.IsNaN(point.Y) && !Single.IsInfinity(point.Y);
        }
    }

    /// <summary>Immutable map-coordinate outlines for a corridor-orienteering exercise.</summary>
    public sealed class TrainingCorridorOutline
    {
        /// <summary>Initializes a corridor outline with defensive copies of its geometry.</summary>
        internal TrainingCorridorOutline(PointF[] leftBoundary, PointF[] rightBoundary, PointF[] polygon)
        {
            LeftBoundary = Array.AsReadOnly((PointF[])leftBoundary.Clone());
            RightBoundary = Array.AsReadOnly((PointF[])rightBoundary.Clone());
            Polygon = Array.AsReadOnly((PointF[])polygon.Clone());
        }

        /// <summary>Gets the left-side corridor boundary in centre-line order.</summary>
        public IReadOnlyList<PointF> LeftBoundary { get; }

        /// <summary>Gets the right-side corridor boundary in centre-line order.</summary>
        public IReadOnlyList<PointF> RightBoundary { get; }

        /// <summary>Gets the simple filled polygon, ordered left boundary forward then right boundary backward.</summary>
        public IReadOnlyList<PointF> Polygon { get; }
    }
}
