/* Copyright (c) 2026, Purple Pen contributors.
 * All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using PurplePen.Graphics2D;
using PurplePen.MapModel;

namespace PurplePen
{
    /// <summary>Shared, non-mutating rendering rules for contour-only training areas.</summary>
    public static class TrainingContourOnlyRendering
    {
        /// <summary>
        /// Determines whether a symbol belongs in a contour-only re-render. Empty selections use
        /// the ISOM contour families 101, 102, and 103 as a dependable default.
        /// </summary>
        public static bool IncludesSymbol(SymDef symdef, string[] allowedSymbolIds)
        {
            if (symdef == null)
                throw new ArgumentNullException(nameof(symdef));

            if (allowedSymbolIds != null && allowedSymbolIds.Length > 0) {
                foreach (string symbolId in allowedSymbolIds) {
                    if (string.Equals(symdef.SymbolId, symbolId, StringComparison.Ordinal))
                        return true;
                }
                return false;
            }

            return IsStandardContourFamily(symdef.SymbolId, "101") ||
                   IsStandardContourFamily(symdef.SymbolId, "102") ||
                   IsStandardContourFamily(symdef.SymbolId, "103");
        }

        /// <summary>Matches an exact ISOM contour family ID or one of its dot subfamilies.</summary>
        private static bool IsStandardContourFamily(string symbolId, string familyId)
        {
            return string.Equals(symbolId, familyId, StringComparison.Ordinal) ||
                   (symbolId != null && symbolId.StartsWith(familyId + ".", StringComparison.Ordinal));
        }

        /// <summary>Builds the closed clipping path used for one polygonal training area.</summary>
        public static List<GraphicsPathPart> CreatePolygonPath(PointF[] locations)
        {
            if (locations == null || locations.Length < 3)
                throw new ArgumentException("A contour-only area requires at least three points.", nameof(locations));

            PointF[] remainingLocations = new PointF[locations.Length - 1];
            Array.Copy(locations, 1, remainingLocations, 0, remainingLocations.Length);
            return new List<GraphicsPathPart> {
                new GraphicsPathPart(GraphicsPathPartKind.Start, new PointF[] { locations[0] }),
                new GraphicsPathPart(GraphicsPathPartKind.Lines, remainingLocations),
                new GraphicsPathPart(GraphicsPathPartKind.Close, new PointF[0])
            };
        }

        /// <summary>
        /// Builds an alternating-fill path for the white separation band surrounding a
        /// corridor. The first subpath is the band's outer edge and the second is the
        /// map-visible corridor hole.
        /// </summary>
        public static List<GraphicsPathPart> CreateCorridorMaskPath(PointF[] outerPolygon, PointF[] corridorPolygon)
        {
            List<GraphicsPathPart> parts = new List<GraphicsPathPart>();
            parts.AddRange(CreatePolygonPath(outerPolygon));
            PointF[] reversedCorridor = (PointF[])corridorPolygon.Clone();
            Array.Reverse(reversedCorridor);
            parts.AddRange(CreatePolygonPath(reversedCorridor));
            return parts;
        }
    }
}
