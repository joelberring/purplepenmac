// PrintProfileCourseLayering.cs
//
// Decides, for one print export, which colours the course overprint uses and
// where the "lower" course purple is placed inside the base map's colour order.
// This is what makes a course PDF behave like an OCAD course-setting export at
// the printer: the transparent course purple lies below brown, black and 100 %
// blue line colours but above all area colours, and the upper purple (start,
// finish, etc.) is drawn on top. Nothing here changes the source map or the
// event's saved course appearance.

using System;
using System.Collections.Generic;
using System.Linq;

namespace PurplePen
{
    /// <summary>Explains how the lower course purple was positioned for an export.</summary>
    public enum PrintProfileLayeringStatus
    {
        /// <summary>Lower purple is drawn directly above the map's own matching course-purple colour.</summary>
        AtMapCoursePurple,

        /// <summary>Lower purple is positioned from confirmed profile colours in the map.</summary>
        FromProfileColorOrder,

        /// <summary>No profile colours were confirmed; a fallback map layer was used.</summary>
        Fallback,

        /// <summary>No usable position exists; the whole course is drawn on top of the map.</summary>
        CourseOnTop,

        /// <summary>The base map is not a vector map (PDF or image), so the course is drawn on top of it.</summary>
        NotVectorMap,
    }

    /// <summary>One concrete course colour used for an export.</summary>
    public sealed class PrintProfileCourseColor
    {
        /// <summary>Gets or sets the OCAD colour number written for the colour.</summary>
        public short OcadId { get; set; }

        /// <summary>Gets or sets the colour name.</summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>Gets or sets the cyan component in the 0–1 range.</summary>
        public float Cyan { get; set; }

        /// <summary>Gets or sets the magenta component in the 0–1 range.</summary>
        public float Magenta { get; set; }

        /// <summary>Gets or sets the yellow component in the 0–1 range.</summary>
        public float Yellow { get; set; }

        /// <summary>Gets or sets the black component in the 0–1 range.</summary>
        public float Black { get; set; }

        /// <summary>Gets or sets whether the colour overprints (is transparent) instead of knocking out.</summary>
        public bool Overprint { get; set; }

        /// <summary>Gets the CMYK value as percentages, e.g. "35/95/0/0".</summary>
        public string CmykText {
            get {
                return String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.#}/{1:0.#}/{2:0.#}/{3:0.#}",
                                     Cyan * 100F, Magenta * 100F, Yellow * 100F, Black * 100F);
            }
        }
    }

    /// <summary>
    /// The export-only course-colour plan derived from a print profile and the open map.
    /// </summary>
    public sealed class PrintProfileCourseLayering
    {
        /// <summary>Gets the profile the plan was created from.</summary>
        public PrintProfile Profile { get; private set; }

        /// <summary>Gets the colour for upper (top-most, opaque) course objects.</summary>
        public PrintProfileCourseColor UpperColor { get; private set; }

        /// <summary>Gets the colour for lower (in-map, usually transparent) course objects.</summary>
        public PrintProfileCourseColor LowerColor { get; private set; }

        /// <summary>
        /// Gets the OCAD id of the map colour that the lower purple is drawn immediately above,
        /// or null when the whole course is drawn on top of the map.
        /// Suitable for <see cref="MapDisplay.LowerPurpleMapLayer"/>.
        /// </summary>
        public int? LowerPurpleMapLayer { get; private set; }

        /// <summary>Gets how the lower purple position was determined.</summary>
        public PrintProfileLayeringStatus Status { get; private set; }

        /// <summary>Gets the name of the map colour directly below the lower purple, or empty.</summary>
        public string MapColorBelowLowerPurple { get; private set; } = String.Empty;

        /// <summary>Gets the name of the map colour directly above the lower purple, or empty.</summary>
        public string MapColorAboveLowerPurple { get; private set; } = String.Empty;

        /// <summary>
        /// Gets names of confirmed map colours whose order in the map contradicts the profile, so they
        /// end up on the wrong side of the lower purple. Empty when the map follows the profile.
        /// </summary>
        public List<string> ConflictingMapColors { get; private set; } = new List<string>();

        /// <summary>
        /// Gets names of map surface colours (area fills, road fills, wide lines) that would have been
        /// drawn on top of the course. The lower purple was raised above them so the course stays visible.
        /// </summary>
        public List<string> SurfaceColorsKeptUnderCourse { get; private set; } = new List<string>();

        /// <summary>
        /// Gets names of map colours that the profile puts above the course but that now lie under the
        /// transparent lower purple because a surface colour above them had to stay below the course.
        /// They remain visible through the transparent (Darken/overprint) purple.
        /// </summary>
        public List<string> LineColorsUnderTransparentCourse { get; private set; } = new List<string>();

        /// <summary>
        /// Gets names of surface colours that still cover the lower purple because it is opaque in the
        /// profile and therefore cannot be raised above line colours.
        /// </summary>
        public List<string> SurfaceColorsCoveringCourse { get; private set; } = new List<string>();

        /// <summary>Gets whether the lower purple is placed inside the map's colour order.</summary>
        public bool IsInterleaved {
            get { return LowerPurpleMapLayer.HasValue; }
        }

        /// <summary>
        /// Creates the course-colour plan for an export.
        /// </summary>
        /// <param name="profile">The selected print profile; must define a course colour rule.</param>
        /// <param name="sourceColors">Snapshots of the base-map colours ordered from lowest to highest. Empty for non-vector maps.</param>
        /// <param name="isVectorMap">True when the base map is an OCAD/OpenMapper vector map that can be split by colour.</param>
        /// <param name="confirmedRuleIds">Partial rule matches explicitly confirmed by the user.</param>
        /// <param name="colorMappings">Explicit source-map colour choices made by the user.</param>
        /// <param name="fallbackLayer">OCAD id to use when no profile colour can be matched (e.g. the event's own
        /// lower-purple layer or a heuristic), or null to draw the course on top.</param>
        /// <returns>The plan, or null when the profile has no usable course colour rule.</returns>
        public static PrintProfileCourseLayering Create(PrintProfile profile, IList<SourceMapColor> sourceColors, bool isVectorMap,
                                                        IEnumerable<string> confirmedRuleIds, IEnumerable<PrintProfileColorMapping> colorMappings,
                                                        int? fallbackLayer)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            PrintProfileColorRule courseRule = BuiltInPrintProfiles.GetCourseColorRule(profile);
            if (courseRule == null)
                return null;

            PrintProfileColorRule upperRule = BuiltInPrintProfiles.GetUpperCourseColorRule(profile) ?? courseRule;

            PrintProfileCourseLayering layering = new PrintProfileCourseLayering();
            layering.Profile = profile;
            layering.LowerColor = CreateColor(courseRule, NormalCourseAppearance.lowerPurpleOcadId,
                                              courseRule.OverprintIntent == PrintProfileOverprintIntent.Overprint);
            // The upper purple is drawn on top of everything and must stay opaque (knockout).
            layering.UpperColor = CreateColor(upperRule, NormalCourseAppearance.courseOcadId, false);
            if (layering.UpperColor.OcadId == layering.LowerColor.OcadId) {
                layering.UpperColor.OcadId = NormalCourseAppearance.courseOcadId;
                layering.LowerColor.OcadId = NormalCourseAppearance.lowerPurpleOcadId;
            }

            List<SourceMapColor> colors = sourceColors == null ? new List<SourceMapColor>() : sourceColors.ToList();
            if (!isVectorMap || colors.Count == 0) {
                layering.Status = PrintProfileLayeringStatus.NotVectorMap;
                return layering;
            }

            ColorPreflightReport report = ColorPreflightReport.Analyze(profile, colors, PdfExportCapabilities.CreateCurrentImplementation(),
                                                                        confirmedRuleIds, colorMappings);

            int index = -1;
            ColorPreflightRuleResult courseResult = report.RuleResults.FirstOrDefault(result => result.Rule.Id == courseRule.Id);
            if (courseResult != null && courseResult.HasConfirmedMatch) {
                // The map already has the printer's course-purple colour at the right place: use it.
                index = IndexOf(colors, courseResult.CandidateColors[0]);
                if (index >= 0)
                    layering.Status = PrintProfileLayeringStatus.AtMapCoursePurple;
            }

            if (index < 0) {
                index = FindIndexFromProfileOrder(report, courseRule, colors, layering.ConflictingMapColors);
                if (index >= 0)
                    layering.Status = PrintProfileLayeringStatus.FromProfileColorOrder;
            }

            if (index < 0 && fallbackLayer.HasValue) {
                index = colors.FindIndex(color => color.OcadId == fallbackLayer.Value);
                if (index >= 0)
                    layering.Status = PrintProfileLayeringStatus.Fallback;
            }

            if (index < 0) {
                layering.Status = PrintProfileLayeringStatus.CourseOnTop;
                return layering;
            }

            // Map drawing stops at the first colour with the given OCAD id. If the id is duplicated in the
            // map, move down to a colour whose id occurs first at that position so the split is exact.
            while (index > 0 && colors.FindIndex(color => color.OcadId == colors[index].OcadId) != index)
                --index;
            if (colors.FindIndex(color => color.OcadId == colors[index].OcadId) != index) {
                layering.Status = PrintProfileLayeringStatus.CourseOnTop;
                return layering;
            }

            index = KeepSurfacesUnderCourse(layering, report, courseRule, colors, index);

            layering.LowerPurpleMapLayer = colors[index].OcadId;
            layering.MapColorBelowLowerPurple = colors[index].Name;
            layering.MapColorAboveLowerPurple = index + 1 < colors.Count ? colors[index + 1].Name : String.Empty;
            return layering;
        }

        /// <summary>
        /// Finds the map-colour index that the lower purple should be drawn directly above, using the
        /// profile's relative draw order of confirmed colours.
        /// </summary>
        /// <param name="report">Preflight report for the profile and map.</param>
        /// <param name="courseRule">The profile's course (lower purple) rule.</param>
        /// <param name="colors">Map colours ordered from lowest to highest.</param>
        /// <param name="conflicts">Receives names of map colours that end up on the wrong side.</param>
        /// <returns>The index, or -1 when no confirmed colour constrains the position.</returns>
        private static int FindIndexFromProfileOrder(ColorPreflightReport report, PrintProfileColorRule courseRule,
                                                     List<SourceMapColor> colors, List<string> conflicts)
        {
            // In the profile, a lower RelativeDrawOrder means higher up in the printer's colour table.
            List<int> mustBeAbove = new List<int>();   // map indexes of colours that belong above the course purple
            List<int> mustBeBelow = new List<int>();   // map indexes of colours that belong below the course purple
            foreach (ColorPreflightRuleResult result in report.RuleResults) {
                if (result.Rule.Id == courseRule.Id || !result.HasConfirmedMatch)
                    continue;

                int mapIndex = IndexOf(colors, result.CandidateColors[0]);
                if (mapIndex < 0 || result.Rule.RelativeDrawOrder == courseRule.RelativeDrawOrder)
                    continue;

                if (result.Rule.RelativeDrawOrder < courseRule.RelativeDrawOrder)
                    mustBeAbove.Add(mapIndex);
                else
                    mustBeBelow.Add(mapIndex);
            }

            if (mustBeAbove.Count == 0 && mustBeBelow.Count == 0)
                return -1;

            int highestBelow = mustBeBelow.Count > 0 ? mustBeBelow.Max() : -1;
            int index;
            if (mustBeAbove.Count > 0) {
                // As high as possible: directly below the lowest colour that must cover the course purple
                // (brown/black/blue lines). This keeps the purple above every area fill, including map
                // colours the profile does not know about.
                index = mustBeAbove.Min() - 1;
                if (index < highestBelow) {
                    // The map's own order contradicts the profile. Course visibility wins: an area colour
                    // must never hide the course, while a transparent purple over a line colour only
                    // darkens it.
                    index = highestBelow;
                }
            }
            else {
                index = highestBelow;
            }

            if (index < 0)
                return -1;

            foreach (int aboveIndex in mustBeAbove.Where(item => item <= index))
                conflicts.Add(colors[aboveIndex].Name);
            foreach (int belowIndex in mustBeBelow.Where(item => item > index))
                conflicts.Add(colors[belowIndex].Name);

            return index;
        }

        /// <summary>
        /// Raises the lower purple above every surface colour (area fill, road fill, wide line) that
        /// would otherwise be drawn on top of it and hide the course. The map's own colour order is
        /// never changed; black (buildings etc.) and registration colours are left alone.
        /// </summary>
        /// <param name="layering">Receives the lists describing the adjustment.</param>
        /// <param name="report">Preflight report for the profile and map.</param>
        /// <param name="courseRule">The profile's course (lower purple) rule.</param>
        /// <param name="colors">Map colours ordered from lowest to highest.</param>
        /// <param name="index">The position found from the profile.</param>
        /// <returns>The adjusted position.</returns>
        private static int KeepSurfacesUnderCourse(PrintProfileCourseLayering layering, ColorPreflightReport report,
                                                   PrintProfileColorRule courseRule, List<SourceMapColor> colors, int index)
        {
            int highestSurface = -1;
            for (int i = colors.Count - 1; i > index; --i) {
                if (colors[i].FillsSurfaces && !IsBlackOrRegistration(colors[i].Cmyk)) {
                    highestSurface = i;
                    break;
                }
            }

            if (highestSurface < 0)
                return index;

            List<string> surfaces = colors.Skip(index + 1).Take(highestSurface - index)
                                          .Where(color => color.FillsSurfaces && !IsBlackOrRegistration(color.Cmyk))
                                          .Select(color => color.Name).ToList();

            if (!layering.LowerColor.Overprint) {
                // An opaque lower purple above line colours would hide them; report instead.
                layering.SurfaceColorsCoveringCourse.AddRange(surfaces);
                return index;
            }

            // Make sure the raised position is a first occurrence of its OCAD id (exact split).
            int newIndex = highestSurface;
            while (newIndex + 1 < colors.Count && colors.FindIndex(color => color.OcadId == colors[newIndex].OcadId) != newIndex)
                ++newIndex;
            if (colors.FindIndex(color => color.OcadId == colors[newIndex].OcadId) != newIndex) {
                layering.SurfaceColorsCoveringCourse.AddRange(surfaces);
                return index;
            }

            layering.SurfaceColorsKeptUnderCourse.AddRange(surfaces);

            // Confirmed colours the profile wants above the course that now end up below it.
            HashSet<string> nowBelow = new HashSet<string>(StringComparer.Ordinal);
            foreach (ColorPreflightRuleResult result in report.RuleResults) {
                if (result.Rule.Id == courseRule.Id || !result.HasConfirmedMatch || result.Rule.RelativeDrawOrder >= courseRule.RelativeDrawOrder)
                    continue;
                int mapIndex = IndexOf(colors, result.CandidateColors[0]);
                if (mapIndex > index && mapIndex <= newIndex && !surfaces.Contains(colors[mapIndex].Name))
                    nowBelow.Add(colors[mapIndex].Name);
            }
            layering.LineColorsUnderTransparentCourse.AddRange(colors.Where(color => nowBelow.Contains(color.Name)).Select(color => color.Name).Distinct());

            return newIndex;
        }

        /// <summary>
        /// Black (buildings, black areas: nothing would show through anyway) and registration colours
        /// are never moved under the course. White area fills are: a white surface on top would erase
        /// circles, lines and numbers. (White mask lines are not reported as surfaces at all.)
        /// </summary>
        private static bool IsBlackOrRegistration(PrintProfileCmyk cmyk)
        {
            bool black = cmyk.Black >= 90F && cmyk.Cyan <= 10F && cmyk.Magenta <= 10F && cmyk.Yellow <= 10F;
            bool registration = cmyk.Cyan >= 99F && cmyk.Magenta >= 99F && cmyk.Yellow >= 99F && cmyk.Black >= 99F;
            return black || registration;
        }

        /// <summary>Finds a source colour snapshot in the ordered colour list.</summary>
        private static int IndexOf(List<SourceMapColor> colors, SourceMapColor color)
        {
            return colors.FindIndex(candidate => candidate.OcadId == color.OcadId
                                                 && candidate.DrawOrder == color.DrawOrder
                                                 && String.Equals(candidate.Name, color.Name, StringComparison.Ordinal));
        }

        /// <summary>Creates a concrete course colour from a profile rule.</summary>
        /// <param name="rule">The profile rule.</param>
        /// <param name="defaultOcadId">OCAD id used when the rule does not name exactly one.</param>
        /// <param name="overprint">Whether the colour overprints.</param>
        private static PrintProfileCourseColor CreateColor(PrintProfileColorRule rule, short defaultOcadId, bool overprint)
        {
            short ocadId = rule.Identifier != null && rule.Identifier.OcadIds != null && rule.Identifier.OcadIds.Count == 1
                ? rule.Identifier.OcadIds[0]
                : defaultOcadId;
            if (ocadId == NormalCourseAppearance.blackColorOcadId || ocadId == CourseLayout.WHITEOUT_COLOR_OCADID)
                ocadId = defaultOcadId;

            return new PrintProfileCourseColor {
                OcadId = ocadId,
                Name = rule.Name,
                Cyan = rule.EffectiveCmyk.Cyan / 100F,
                Magenta = rule.EffectiveCmyk.Magenta / 100F,
                Yellow = rule.EffectiveCmyk.Yellow / 100F,
                Black = rule.EffectiveCmyk.Black / 100F,
                Overprint = overprint,
            };
        }
    }
}
