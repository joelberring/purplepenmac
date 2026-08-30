/* Copyright (c) 2026, Purple Pen contributors.
 * All rights reserved.
 */

using PurplePen.Graphics2D;

namespace PurplePen
{
    /// <summary>UI-neutral description of one symbol layer available in the current vector map.</summary>
    public sealed class TrainingMapSymbolInfo
    {
        /// <summary>Creates an immutable map-symbol description.</summary>
        public TrainingMapSymbolInfo(string symbolId, string name, CmykColor color)
        {
            SymbolId = symbolId ?? "";
            Name = name ?? "";
            Color = color ?? CmykColor.FromCmyk(0, 0, 0, 1);
        }

        public string SymbolId { get; private set; }
        public string Name { get; private set; }
        public CmykColor Color { get; private set; }
    }
}
