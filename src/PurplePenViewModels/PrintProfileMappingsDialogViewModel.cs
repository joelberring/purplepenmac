// PrintProfileMappingsDialogViewModel.cs
// Presents source-map colour layers first, then lets the user choose a target profile colour.

using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace PurplePen.ViewModels
{
    /// <summary>One possible handling choice for a source-map colour layer.</summary>
    public sealed class PrintProfileTargetOptionViewModel
    {
        public bool IsKeepOriginal { get; set; }
        public PrintProfileColorRule? Rule { get; set; }
        public string RuleId => Rule?.Id ?? String.Empty;
        public string RuleName => Rule?.Name ?? String.Empty;
        public string TargetCmyk => Rule == null ? String.Empty : String.Format(CultureInfo.InvariantCulture,
            "C{0:0.#} M{1:0.#} Y{2:0.#} K{3:0.#}", Rule.EffectiveCmyk.Cyan, Rule.EffectiveCmyk.Magenta,
            Rule.EffectiveCmyk.Yellow, Rule.EffectiveCmyk.Black);
        public string PreviewColor => Rule == null ? "#FFFFFF" : PrintProfileMappingsDialogViewModel.ToPreviewColor(Rule.EffectiveCmyk);
    }

    /// <summary>One actual source-map colour layer and the profile handling selected for it.</summary>
    public partial class PrintProfileSourceLayerViewModel : ViewModelBase
    {
        public SourceMapColor SourceColor { get; set; } = new SourceMapColor();
        public List<PrintProfileTargetOptionViewModel> TargetOptions { get; set; } = new List<PrintProfileTargetOptionViewModel>();
        public string SourcePreviewColor => PrintProfileMappingsDialogViewModel.ToPreviewColor(SourceColor.Cmyk);
        public string SourceCmyk => String.Format(CultureInfo.InvariantCulture, "C{0:0.#} M{1:0.#} Y{2:0.#} K{3:0.#}",
            SourceColor.Cmyk.Cyan, SourceColor.Cmyk.Magenta, SourceColor.Cmyk.Yellow, SourceColor.Cmyk.Black);
        public string SourceIdentity => String.Format(CultureInfo.InvariantCulture, "OCAD {0} · {1}", SourceColor.OcadId, SourceColor.DrawOrder);
        public bool HasSymbols => SourceColor.Symbols.Count > 0;

        [ObservableProperty]
        private PrintProfileTargetOptionViewModel? selectedTarget;
    }

    /// <summary>Maps visible source-map layers to a finished print profile without requiring memorized identifiers.</summary>
    public sealed class PrintProfileMappingsDialogViewModel : ViewModelBase
    {
        public string ProfileName { get; set; } = String.Empty;
        public List<PrintProfileSourceLayerViewModel> SourceLayers { get; } = new List<PrintProfileSourceLayerViewModel>();

        /// <summary>Gets whether every source layer has an explicit, non-conflicting handling choice.</summary>
        public bool CanApplyMappings {
            get {
                if (SourceLayers.Count == 0 || SourceLayers.Any(item => item.SelectedTarget == null))
                    return false;
                List<string> mappedRuleIds = SourceLayers.Where(item => item.SelectedTarget != null && !item.SelectedTarget.IsKeepOriginal)
                                                         .Select(item => item.SelectedTarget!.RuleId).ToList();
                return mappedRuleIds.Count == mappedRuleIds.Distinct(StringComparer.Ordinal).Count();
            }
        }

        /// <summary>Creates source-first rows and preselects only unambiguous matches.</summary>
        public void Configure(PrintProfile profile, IEnumerable<SourceMapColor> sourceColors, IEnumerable<ColorPreflightRuleResult> ruleResults)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            ProfileName = profile.Name;
            SourceLayers.Clear();
            List<ColorPreflightRuleResult> results = (ruleResults ?? Enumerable.Empty<ColorPreflightRuleResult>()).ToList();

            foreach (SourceMapColor sourceColor in sourceColors ?? Enumerable.Empty<SourceMapColor>()) {
                PrintProfileSourceLayerViewModel item = new PrintProfileSourceLayerViewModel { SourceColor = sourceColor };
                PrintProfileTargetOptionViewModel keepOriginal = new PrintProfileTargetOptionViewModel { IsKeepOriginal = true };
                item.TargetOptions.Add(keepOriginal);
                foreach (PrintProfileColorRule rule in profile.ColorRules)
                    item.TargetOptions.Add(new PrintProfileTargetOptionViewModel { Rule = rule });

                List<ColorPreflightRuleResult> sourceMatches = results.Where(result => result.CandidateColors.Any(candidate => SameColor(candidate, sourceColor))).ToList();
                if (sourceMatches.Count == 1 && sourceMatches[0].CandidateColors.Count == 1 &&
                    sourceMatches[0].MatchStatus != ColorPreflightMatchStatus.Ambiguous) {
                    string ruleId = sourceMatches[0].Rule.Id;
                    item.SelectedTarget = item.TargetOptions.Single(option => option.RuleId == ruleId);
                }
                else if (sourceMatches.Count == 0) {
                    item.SelectedTarget = keepOriginal;
                }

                item.PropertyChanged += SourceLayerPropertyChanged;
                SourceLayers.Add(item);
            }
            OnPropertyChanged(nameof(CanApplyMappings));
        }

        /// <summary>Creates persistent rule-to-source mappings from the inverse visual editor.</summary>
        public List<PrintProfileColorMapping> CreateMappings()
        {
            List<PrintProfileColorMapping> mappings = new List<PrintProfileColorMapping>();
            foreach (PrintProfileSourceLayerViewModel item in SourceLayers.Where(item => item.SelectedTarget != null && !item.SelectedTarget.IsKeepOriginal)) {
                mappings.Add(new PrintProfileColorMapping {
                    RuleId = item.SelectedTarget!.RuleId,
                    MapColorName = item.SourceColor.Name,
                    MapColorOcadId = item.SourceColor.OcadId,
                    MapColorDrawOrder = item.SourceColor.DrawOrder,
                });
            }
            return mappings;
        }

        /// <summary>Raises Apply-button validation when a row's handling changes.</summary>
        private void SourceLayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PrintProfileSourceLayerViewModel.SelectedTarget))
                OnPropertyChanged(nameof(CanApplyMappings));
        }

        /// <summary>Compares the stable identity fields of two source-map colours.</summary>
        private static bool SameColor(SourceMapColor first, SourceMapColor second)
        {
            return first.OcadId == second.OcadId && first.DrawOrder == second.DrawOrder &&
                   String.Equals(first.Name, second.Name, StringComparison.Ordinal);
        }

        /// <summary>Creates an sRGB approximation for a CMYK colour swatch.</summary>
        public static string ToPreviewColor(PrintProfileCmyk cmyk)
        {
            float cyan = Math.Max(0F, Math.Min(100F, cmyk.Cyan)) / 100F;
            float magenta = Math.Max(0F, Math.Min(100F, cmyk.Magenta)) / 100F;
            float yellow = Math.Max(0F, Math.Min(100F, cmyk.Yellow)) / 100F;
            float black = Math.Max(0F, Math.Min(100F, cmyk.Black)) / 100F;
            int red = (int)Math.Round(255F * (1F - cyan) * (1F - black), MidpointRounding.AwayFromZero);
            int green = (int)Math.Round(255F * (1F - magenta) * (1F - black), MidpointRounding.AwayFromZero);
            int blue = (int)Math.Round(255F * (1F - yellow) * (1F - black), MidpointRounding.AwayFromZero);
            return String.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", red, green, blue);
        }
    }
}
