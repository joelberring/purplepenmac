// ColorPreflight.cs
//
// Evaluates a source map against a versioned print profile without changing
// map colours, their drawing order, or the selected export settings.

using System;
using System.Collections.Generic;
using System.Linq;
using PurplePen.MapModel;

namespace PurplePen
{
    /// <summary>Identifies the seriousness of a colour-preflight finding.</summary>
    public enum ColorPreflightSeverity
    {
        Information,
        Warning,
        Blocking
    }

    /// <summary>Describes how confidently a source-map colour matches a profile rule.</summary>
    public enum ColorPreflightMatchStatus
    {
        NotPresent,
        Matched,
        NeedsConfirmation,
        Ambiguous
    }

    /// <summary>Stores a read-only colour snapshot taken from a source map.</summary>
    public sealed class SourceMapColor
    {
        /// <summary>Gets or sets the source-map colour name.</summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>Gets or sets the source-map OCAD colour identifier.</summary>
        public short OcadId { get; set; }

        /// <summary>Gets or sets the original source-map CMYK colour.</summary>
        public PrintProfileCmyk Cmyk { get; set; } = new PrintProfileCmyk();

        /// <summary>Gets or sets whether the source colour is configured to overprint.</summary>
        public bool Overprint { get; set; }

        /// <summary>Gets or sets the source drawing-order index, from lowest to highest.</summary>
        public int DrawOrder { get; set; }

        /// <summary>Gets a descriptive string suitable for a colour-mapping selector.</summary>
        public string DisplayName {
            get { return String.Format("{0} (OCAD {1}, order {2}, CMYK {3}/{4}/{5}/{6})", Name, OcadId, DrawOrder, Cmyk.Cyan, Cmyk.Magenta, Cmyk.Yellow, Cmyk.Black); }
        }

        /// <summary>Creates a non-mutating snapshot of a MapModel colour.</summary>
        /// <param name="color">The source map colour.</param>
        /// <param name="drawOrder">The source map's lowest-to-highest colour order.</param>
        /// <returns>A preflight-safe source colour snapshot.</returns>
        public static SourceMapColor FromSymColor(SymColor color, int drawOrder)
        {
            if (color == null)
                throw new ArgumentNullException(nameof(color));

            return new SourceMapColor {
                Name = color.Name ?? String.Empty,
                OcadId = color.OcadId,
                Cmyk = new PrintProfileCmyk {
                    Cyan = ToPercentage(color.CmykColor.Cyan),
                    Magenta = ToPercentage(color.CmykColor.Magenta),
                    Yellow = ToPercentage(color.CmykColor.Yellow),
                    Black = ToPercentage(color.CmykColor.Black),
                },
                Overprint = color.OverPrint,
                DrawOrder = drawOrder,
            };
        }

        /// <summary>Converts a MapModel CMYK component to its percentage representation.</summary>
        private static float ToPercentage(float value)
        {
            return (float)Math.Round(value * 100.0F, 3, MidpointRounding.AwayFromZero);
        }
    }

    /// <summary>Describes the PDF-production features available to an export path.</summary>
    public sealed class PdfExportCapabilities
    {
        /// <summary>Gets or sets whether the export path can emit CMYK colour values.</summary>
        public bool SupportsCmykOutput { get; set; }

        /// <summary>Gets or sets whether the export path can emit a validated PDF/X OutputIntent.</summary>
        public bool SupportsPdfXOutputIntent { get; set; }

        /// <summary>Gets or sets whether the export path can embed an ICC output profile.</summary>
        public bool SupportsEmbeddedIccProfile { get; set; }

        /// <summary>Gets or sets whether the export path can preserve true PDF overprint semantics.</summary>
        public bool SupportsTrueOverprint { get; set; }

        /// <summary>Creates the capabilities currently verified by Purple Pen's PDF export path.</summary>
        /// <remarks>PDF/X and ICC embedding stay false until verified end-to-end.</remarks>
        public static PdfExportCapabilities CreateCurrentImplementation()
        {
            return new PdfExportCapabilities {
                SupportsCmykOutput = true,
                SupportsPdfXOutputIntent = false,
                SupportsEmbeddedIccProfile = false,
                SupportsTrueOverprint = true,
            };
        }
    }

    /// <summary>Captures a rule's source-map match and effective export colour.</summary>
    public sealed class ColorPreflightRuleResult
    {
        /// <summary>Gets or sets the profile rule evaluated by this result.</summary>
        public PrintProfileColorRule Rule { get; set; } = new PrintProfileColorRule();

        /// <summary>Gets or sets the candidates found in the source map.</summary>
        public List<SourceMapColor> CandidateColors { get; set; } = new List<SourceMapColor>();

        /// <summary>Gets or sets the confidence of the source-map match.</summary>
        public ColorPreflightMatchStatus MatchStatus { get; set; }

        /// <summary>Gets or sets whether the user explicitly confirmed a partial source-map match.</summary>
        public bool UserConfirmed { get; set; }

        /// <summary>Gets or sets whether this result was resolved through an explicit user mapping.</summary>
        public bool UserMapped { get; set; }

        /// <summary>Gets or sets whether a previously saved explicit mapping no longer identifies a map colour.</summary>
        public bool MappingMissing { get; set; }

        /// <summary>Gets whether a single source colour was confidently matched.</summary>
        public bool HasConfirmedMatch {
            get {
                return CandidateColors.Count == 1
                    && (MatchStatus == ColorPreflightMatchStatus.Matched || UserConfirmed);
            }
        }
    }

    /// <summary>Describes one human-readable preflight finding.</summary>
    public sealed class ColorPreflightFinding
    {
        /// <summary>Gets or sets the seriousness of the finding.</summary>
        public ColorPreflightSeverity Severity { get; set; }

        /// <summary>Gets or sets the stable profile rule associated with the finding.</summary>
        public string RuleId { get; set; } = String.Empty;

        /// <summary>Gets or sets the actionable user-facing finding text.</summary>
        public string Message { get; set; } = String.Empty;
    }

    /// <summary>Contains the complete non-destructive preflight result for one map and profile.</summary>
    public sealed class ColorPreflightReport
    {
        /// <summary>Gets or sets the profile used for this report.</summary>
        public PrintProfile Profile { get; set; } = new PrintProfile();

        /// <summary>Gets or sets all evaluated profile rules.</summary>
        public List<ColorPreflightRuleResult> RuleResults { get; set; } = new List<ColorPreflightRuleResult>();

        /// <summary>Gets or sets all preflight findings.</summary>
        public List<ColorPreflightFinding> Findings { get; set; } = new List<ColorPreflightFinding>();

        /// <summary>Gets whether the selected profile can be exported without a blocking finding.</summary>
        public bool CanExport {
            get { return !Findings.Any(finding => finding.Severity == ColorPreflightSeverity.Blocking); }
        }

        /// <summary>Creates source snapshots for all colours returned by a map display.</summary>
        /// <param name="mapDisplay">The map display whose colours should be read.</param>
        /// <returns>Map colours ordered from lowest to highest.</returns>
        public static List<SourceMapColor> CreateSourceColors(MapDisplay mapDisplay)
        {
            if (mapDisplay == null)
                throw new ArgumentNullException(nameof(mapDisplay));

            List<SymColor> mapColors = mapDisplay.GetMapColors();
            List<SourceMapColor> sourceColors = new List<SourceMapColor>();
            for (int index = 0; index < mapColors.Count; ++index) {
                sourceColors.Add(SourceMapColor.FromSymColor(mapColors[index], index));
            }

            return sourceColors;
        }

        /// <summary>Evaluates source colours against a profile and export capability declaration.</summary>
        /// <param name="profile">The selected, versioned print profile.</param>
        /// <param name="sourceColors">Read-only snapshots of the source-map colours.</param>
        /// <param name="capabilities">The real capabilities of the intended export path.</param>
        /// <param name="confirmedRuleIds">Partial rule matches explicitly confirmed by the user.</param>
        /// <param name="colorMappings">Explicit source-map colour choices made by the user.</param>
        /// <returns>A complete preflight report. No source data is modified.</returns>
        public static ColorPreflightReport Analyze(PrintProfile profile, IEnumerable<SourceMapColor> sourceColors,
                                                   PdfExportCapabilities capabilities, IEnumerable<string> confirmedRuleIds = null,
                                                   IEnumerable<PrintProfileColorMapping> colorMappings = null)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            if (sourceColors == null)
                throw new ArgumentNullException(nameof(sourceColors));
            if (capabilities == null)
                throw new ArgumentNullException(nameof(capabilities));

            List<SourceMapColor> sourceColorList = sourceColors.ToList();
            HashSet<string> confirmedRules = new HashSet<string>(confirmedRuleIds ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            List<PrintProfileColorMapping> mappings = (colorMappings ?? Enumerable.Empty<PrintProfileColorMapping>()).ToList();
            ColorPreflightReport report = new ColorPreflightReport { Profile = profile };

            AddCapabilityFindings(report, profile.Requirements, capabilities);
            foreach (PrintProfileColorRule rule in profile.ColorRules) {
                PrintProfileColorMapping mapping = mappings.FirstOrDefault(item => String.Equals(item.RuleId, rule.Id, StringComparison.Ordinal));
                ColorPreflightRuleResult result = CreateRuleResult(rule, sourceColorList, confirmedRules.Contains(rule.Id), mapping);
                report.RuleResults.Add(result);
                AddMatchFinding(report, result);
                AddEffectiveColorFindings(report, result);
            }

            AddDrawOrderFindings(report);
            return report;
        }

        /// <summary>Creates an evaluated result for a single profile rule.</summary>
        private static ColorPreflightRuleResult CreateRuleResult(PrintProfileColorRule rule, List<SourceMapColor> sourceColors,
                                                                 bool userConfirmed, PrintProfileColorMapping mapping)
        {
            List<SourceMapColor> candidates;
            ColorPreflightMatchStatus matchStatus;
            bool userMapped = mapping != null;
            if (userMapped) {
                candidates = sourceColors.Where(color => MappingMatches(mapping, color)).ToList();
                matchStatus = candidates.Count == 1 ? ColorPreflightMatchStatus.Matched : ColorPreflightMatchStatus.NotPresent;
            }
            else {
                candidates = sourceColors.Where(color => MatchesAnyIdentifier(rule.Identifier, color)).ToList();
                if (candidates.Count > 1) {
                    List<SourceMapColor> cmykCandidates = candidates.Where(color => CmykMatches(color.Cmyk, rule.EffectiveCmyk)).ToList();
                    if (cmykCandidates.Count == 1) {
                        candidates = cmykCandidates;
                        matchStatus = ColorPreflightMatchStatus.NeedsConfirmation;
                    }
                    else {
                        matchStatus = ColorPreflightMatchStatus.Ambiguous;
                    }
                }
                else if (candidates.Count == 0) {
                    if (HasVisibleCmykInk(rule.EffectiveCmyk)) {
                        List<SourceMapColor> cmykCandidates = sourceColors.Where(color => CmykMatches(color.Cmyk, rule.EffectiveCmyk)).ToList();
                        if (cmykCandidates.Count == 1) {
                            candidates = cmykCandidates;
                            matchStatus = ColorPreflightMatchStatus.NeedsConfirmation;
                        }
                        else {
                            candidates = cmykCandidates;
                            matchStatus = candidates.Count > 1 ? ColorPreflightMatchStatus.Ambiguous : ColorPreflightMatchStatus.NotPresent;
                        }
                    }
                    else {
                        matchStatus = ColorPreflightMatchStatus.NotPresent;
                    }
                }
                else if (MatchesAllSpecifiedIdentifiers(rule.Identifier, candidates[0])) {
                    matchStatus = ColorPreflightMatchStatus.Matched;
                }
                else {
                    matchStatus = ColorPreflightMatchStatus.NeedsConfirmation;
                }
            }

            if (candidates.Count == 0) {
                matchStatus = ColorPreflightMatchStatus.NotPresent;
            }

            return new ColorPreflightRuleResult {
                Rule = rule,
                CandidateColors = candidates,
                MatchStatus = matchStatus,
                UserConfirmed = userConfirmed && matchStatus == ColorPreflightMatchStatus.NeedsConfirmation && candidates.Count == 1,
                UserMapped = userMapped && candidates.Count == 1,
                MappingMissing = userMapped && candidates.Count == 0,
            };
        }

        /// <summary>Checks whether a source colour is the explicit mapping selected by the user.</summary>
        private static bool MappingMatches(PrintProfileColorMapping mapping, SourceMapColor color)
        {
            return String.Equals(mapping.MapColorName, color.Name, StringComparison.Ordinal)
                && mapping.MapColorOcadId == color.OcadId
                && mapping.MapColorDrawOrder == color.DrawOrder;
        }

        /// <summary>Checks whether a source colour has an allowed name or OCAD identifier.</summary>
        private static bool MatchesAnyIdentifier(PrintProfileColorIdentifier identifier, SourceMapColor color)
        {
            bool nameMatches = identifier.Names.Any(name => String.Equals(name, color.Name, StringComparison.OrdinalIgnoreCase));
            bool ocadIdMatches = identifier.OcadIds.Contains(color.OcadId);
            return nameMatches || ocadIdMatches;
        }

        /// <summary>Checks whether a source colour satisfies every populated identifier category.</summary>
        private static bool MatchesAllSpecifiedIdentifiers(PrintProfileColorIdentifier identifier, SourceMapColor color)
        {
            bool nameMatches = identifier.Names.Count == 0 || identifier.Names.Any(name => String.Equals(name, color.Name, StringComparison.OrdinalIgnoreCase));
            bool ocadIdMatches = identifier.OcadIds.Count == 0 || identifier.OcadIds.Contains(color.OcadId);
            return nameMatches && ocadIdMatches;
        }

        /// <summary>Adds findings for the real capabilities required by a selected profile.</summary>
        private static void AddCapabilityFindings(ColorPreflightReport report, PrintProfileRequirements requirements,
                                                  PdfExportCapabilities capabilities)
        {
            AddCapabilityFinding(report, requirements.RequireCmykOutput, capabilities.SupportsCmykOutput,
                                 "CMYK output is required, but the selected export path cannot provide it.");
            AddCapabilityFinding(report, requirements.RequirePdfXOutputIntent, capabilities.SupportsPdfXOutputIntent,
                                 "PDF/X OutputIntent is required, but the selected export path cannot provide it.");
            AddCapabilityFinding(report, requirements.RequireEmbeddedIccProfile, capabilities.SupportsEmbeddedIccProfile,
                                 "An embedded ICC profile is required, but the selected export path cannot provide it.");
            AddCapabilityFinding(report, requirements.RequireTrueOverprint, capabilities.SupportsTrueOverprint,
                                 "True PDF overprint is required, but the selected export path cannot provide it.");
        }

        /// <summary>Adds one blocking finding if a required capability is unavailable.</summary>
        private static void AddCapabilityFinding(ColorPreflightReport report, bool required, bool supported, string message)
        {
            if (required && !supported) {
                report.Findings.Add(new ColorPreflightFinding {
                    Severity = ColorPreflightSeverity.Blocking,
                    Message = message,
                });
            }
        }

        /// <summary>Adds the user-facing finding for a rule match result.</summary>
        private static void AddMatchFinding(ColorPreflightReport report, ColorPreflightRuleResult result)
        {
            if (result.MatchStatus == ColorPreflightMatchStatus.NotPresent) {
                report.Findings.Add(new ColorPreflightFinding {
                    Severity = result.MappingMissing ? ColorPreflightSeverity.Blocking : ColorPreflightSeverity.Information,
                    RuleId = result.Rule.Id,
                    Message = result.MappingMissing
                        ? String.Format("The saved mapping for profile colour '{0}' no longer identifies a map colour; choose the mapping again.", result.Rule.Name)
                        : String.Format("Profile colour '{0}' is not present in this map.", result.Rule.Name),
                });
            }
            else if (result.MatchStatus == ColorPreflightMatchStatus.NeedsConfirmation) {
                report.Findings.Add(new ColorPreflightFinding {
                    Severity = result.UserConfirmed ? ColorPreflightSeverity.Warning : ColorPreflightSeverity.Blocking,
                    RuleId = result.Rule.Id,
                    Message = result.UserConfirmed
                        ? String.Format("Profile colour '{0}' matched only part of its identifiers and was explicitly confirmed for this export.", result.Rule.Name)
                        : String.Format("Profile colour '{0}' matched only part of its identifiers; confirm the mapping before export.", result.Rule.Name),
                });
            }
            else if (result.MatchStatus == ColorPreflightMatchStatus.Ambiguous) {
                report.Findings.Add(new ColorPreflightFinding {
                    Severity = ColorPreflightSeverity.Blocking,
                    RuleId = result.Rule.Id,
                    Message = String.Format("Profile colour '{0}' matches multiple map colours; choose a mapping before export.", result.Rule.Name),
                });
            }
        }

        /// <summary>Adds non-destructive warnings when a confirmed source colour differs from its profile value.</summary>
        private static void AddEffectiveColorFindings(ColorPreflightReport report, ColorPreflightRuleResult result)
        {
            if (!result.HasConfirmedMatch)
                return;

            SourceMapColor sourceColor = result.CandidateColors[0];
            PrintProfileCmyk expected = result.Rule.EffectiveCmyk;
            if (!CmykMatches(sourceColor.Cmyk, expected)) {
                report.Findings.Add(new ColorPreflightFinding {
                    Severity = ColorPreflightSeverity.Warning,
                    RuleId = result.Rule.Id,
                    Message = String.Format("Profile colour '{0}' has CMYK {1}, but the map uses {2}.",
                                            result.Rule.Name, FormatCmyk(expected), FormatCmyk(sourceColor.Cmyk)),
                });
            }

            if (result.Rule.OverprintIntent == PrintProfileOverprintIntent.Overprint && !sourceColor.Overprint) {
                report.Findings.Add(new ColorPreflightFinding {
                    Severity = ColorPreflightSeverity.Warning,
                    RuleId = result.Rule.Id,
                    Message = String.Format("Profile colour '{0}' should overprint, but the map colour is set to knockout.", result.Rule.Name),
                });
            }
        }

        /// <summary>Compares CMYK percentage values using a tolerance for source-file precision.</summary>
        private static bool CmykMatches(PrintProfileCmyk source, PrintProfileCmyk expected)
        {
            const float tolerance = 0.01F;
            return Math.Abs(source.Cyan - expected.Cyan) <= tolerance
                && Math.Abs(source.Magenta - expected.Magenta) <= tolerance
                && Math.Abs(source.Yellow - expected.Yellow) <= tolerance
                && Math.Abs(source.Black - expected.Black) <= tolerance;
        }

        /// <summary>Determines whether CMYK alone is meaningful enough to suggest a source-map mapping.</summary>
        private static bool HasVisibleCmykInk(PrintProfileCmyk cmyk)
        {
            const float tolerance = 0.01F;
            return Math.Abs(cmyk.Cyan) > tolerance || Math.Abs(cmyk.Magenta) > tolerance
                || Math.Abs(cmyk.Yellow) > tolerance || Math.Abs(cmyk.Black) > tolerance;
        }

        /// <summary>Formats CMYK percentage values for a concise preflight finding.</summary>
        private static string FormatCmyk(PrintProfileCmyk cmyk)
        {
            return String.Format("{0}/{1}/{2}/{3}", cmyk.Cyan, cmyk.Magenta, cmyk.Yellow, cmyk.Black);
        }

        /// <summary>Checks all required relative drawing-order relationships for confirmed matches.</summary>
        private static void AddDrawOrderFindings(ColorPreflightReport report)
        {
            foreach (ColorPreflightRuleResult result in report.RuleResults.Where(ruleResult => ruleResult.HasConfirmedMatch)) {
                SourceMapColor sourceColor = result.CandidateColors[0];
                AddRelationshipFindings(report, result.Rule, sourceColor, result.Rule.MustBeBelowRuleIds, true);
                AddRelationshipFindings(report, result.Rule, sourceColor, result.Rule.MustBeAboveRuleIds, false);
            }
        }

        /// <summary>Adds a warning when two confirmed colours are in the wrong source drawing order.</summary>
        private static void AddRelationshipFindings(ColorPreflightReport report, PrintProfileColorRule rule,
                                                    SourceMapColor sourceColor, IEnumerable<string> relatedRuleIds,
                                                    bool mustBeBelow)
        {
            foreach (string relatedRuleId in relatedRuleIds) {
                ColorPreflightRuleResult relatedResult = report.RuleResults.SingleOrDefault(item => item.Rule.Id == relatedRuleId);
                if (relatedResult == null || !relatedResult.HasConfirmedMatch)
                    continue;

                SourceMapColor relatedColor = relatedResult.CandidateColors[0];
                bool hasExpectedOrder = mustBeBelow ? sourceColor.DrawOrder < relatedColor.DrawOrder : sourceColor.DrawOrder > relatedColor.DrawOrder;
                if (!hasExpectedOrder) {
                    string direction = mustBeBelow ? "below" : "above";
                    report.Findings.Add(new ColorPreflightFinding {
                        Severity = ColorPreflightSeverity.Warning,
                        RuleId = rule.Id,
                        Message = String.Format("Profile colour '{0}' should be {1} '{2}' in the map colour order.", rule.Name, direction, relatedResult.Rule.Name),
                    });
                }
            }
        }
    }
}
