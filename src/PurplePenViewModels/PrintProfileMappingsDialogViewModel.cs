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

        /// <summary>Gets the option that keeps the layer's original colour.</summary>
        public PrintProfileTargetOptionViewModel KeepOriginalOption => TargetOptions.First(option => option.IsKeepOriginal);

        [ObservableProperty]
        private PrintProfileTargetOptionViewModel? selectedTarget;

        /// <summary>Gets or sets how the current selection was chosen; null once the user changed it.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAutoSuggested))]
        [NotifyPropertyChangedFor(nameof(IsAutoKeptOriginal))]
        private PrintProfileSuggestionKind? suggestionKind;

        /// <summary>Gets whether the selected profile colour was proposed automatically and not yet changed.</summary>
        public bool IsAutoSuggested => SuggestionKind == PrintProfileSuggestionKind.Suggested;

        /// <summary>Gets whether the original colour is kept because no profile colour fits.</summary>
        public bool IsAutoKeptOriginal => SuggestionKind == PrintProfileSuggestionKind.NoMatch;
    }

    /// <summary>Maps visible source-map layers to a finished print profile without requiring memorized identifiers.</summary>
    public sealed class PrintProfileMappingsDialogViewModel : ViewModelBase
    {
        public string ProfileName { get; set; } = String.Empty;
        public List<PrintProfileSourceLayerViewModel> SourceLayers { get; } = new List<PrintProfileSourceLayerViewModel>();

        /// <summary>Ids of every colour rule in the profile, used to record rules that no layer uses.</summary>
        private readonly List<string> profileRuleIds = new List<string>();

        /// <summary>Set while one row's selection is being adjusted because another row took its rule.</summary>
        private bool adjustingSelections;

        /// <summary>Gets the number of layers that were paired with a profile colour automatically.</summary>
        public int AutoPairedCount => SourceLayers.Count(item => item.SuggestionKind == PrintProfileSuggestionKind.Suggested
                                                              || item.SuggestionKind == PrintProfileSuggestionKind.Confirmed);

        /// <summary>Gets the number of map colour layers shown in the dialog.</summary>
        public int LayerCount => SourceLayers.Count;

        /// <summary>
        /// Gets whether the choices can be applied. Every layer always has a handling (a profile colour or
        /// its original colour), so this only fails if the same profile colour is used twice.
        /// </summary>
        public bool CanApplyMappings {
            get {
                if (SourceLayers.Count == 0 || SourceLayers.Any(item => item.SelectedTarget == null))
                    return false;
                List<string> mappedRuleIds = SourceLayers.Where(item => item.SelectedTarget != null && !item.SelectedTarget.IsKeepOriginal)
                                                         .Select(item => item.SelectedTarget!.RuleId).ToList();
                return mappedRuleIds.Count == mappedRuleIds.Distinct(StringComparer.Ordinal).Count();
            }
        }

        /// <summary>
        /// Creates source-first rows and preselects a handling for every row: exact and earlier matches,
        /// automatic suggestions from names, CMYK and symbols, and otherwise the original colour.
        /// </summary>
        /// <param name="profile">The selected print profile.</param>
        /// <param name="sourceColors">The map's colour layers, lowest first.</param>
        /// <param name="ruleResults">Preflight results for the profile.</param>
        public void Configure(PrintProfile profile, IEnumerable<SourceMapColor> sourceColors, IEnumerable<ColorPreflightRuleResult> ruleResults)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            ProfileName = profile.Name;
            foreach (PrintProfileSourceLayerViewModel oldItem in SourceLayers)
                oldItem.PropertyChanged -= SourceLayerPropertyChanged;
            SourceLayers.Clear();
            profileRuleIds.Clear();
            profileRuleIds.AddRange(profile.ColorRules.Select(rule => rule.Id));

            List<PrintProfileMappingSuggestion> suggestions = PrintProfileAutoMapper.Suggest(profile, sourceColors, ruleResults);
            foreach (PrintProfileMappingSuggestion suggestion in suggestions) {
                PrintProfileSourceLayerViewModel item = new PrintProfileSourceLayerViewModel { SourceColor = suggestion.SourceColor };
                item.TargetOptions.Add(new PrintProfileTargetOptionViewModel { IsKeepOriginal = true });
                foreach (PrintProfileColorRule rule in profile.ColorRules)
                    item.TargetOptions.Add(new PrintProfileTargetOptionViewModel { Rule = rule });

                item.SelectedTarget = suggestion.Rule == null
                    ? item.KeepOriginalOption
                    : item.TargetOptions.Single(option => !option.IsKeepOriginal && option.RuleId == suggestion.Rule.Id);
                item.SuggestionKind = suggestion.Kind;
                item.PropertyChanged += SourceLayerPropertyChanged;
                SourceLayers.Add(item);
            }
            OnPropertyChanged(nameof(CanApplyMappings));
            OnPropertyChanged(nameof(AutoPairedCount));
            OnPropertyChanged(nameof(LayerCount));
        }

        /// <summary>
        /// Creates persistent rule-to-source mappings from the inverse visual editor. Profile colours that no
        /// layer uses are recorded as not in the map, so the export does not ask about them again.
        /// </summary>
        /// <returns>One mapping per profile rule shown in the dialog.</returns>
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

            foreach (string ruleId in profileRuleIds) {
                if (!mappings.Any(mapping => String.Equals(mapping.RuleId, ruleId, StringComparison.Ordinal))) {
                    mappings.Add(new PrintProfileColorMapping {
                        RuleId = ruleId,
                        MapColorOcadId = -1,
                        MapColorDrawOrder = -1,
                        NotInMap = true,
                    });
                }
            }
            return mappings;
        }

        /// <summary>
        /// Reacts when the user changes a row: the row is no longer an automatic suggestion, and any other row
        /// that used the same profile colour falls back to its original colour, so Apply never gets blocked.
        /// </summary>
        private void SourceLayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(PrintProfileSourceLayerViewModel.SelectedTarget))
                return;

            if (!adjustingSelections && sender is PrintProfileSourceLayerViewModel changed) {
                adjustingSelections = true;
                try {
                    changed.SuggestionKind = null;
                    if (changed.SelectedTarget != null && !changed.SelectedTarget.IsKeepOriginal) {
                        string ruleId = changed.SelectedTarget.RuleId;
                        foreach (PrintProfileSourceLayerViewModel other in SourceLayers) {
                            if (other != changed && other.SelectedTarget != null && !other.SelectedTarget.IsKeepOriginal &&
                                String.Equals(other.SelectedTarget.RuleId, ruleId, StringComparison.Ordinal)) {
                                other.SelectedTarget = other.KeepOriginalOption;
                                other.SuggestionKind = null;
                            }
                        }
                    }
                }
                finally {
                    adjustingSelections = false;
                }
                OnPropertyChanged(nameof(AutoPairedCount));
            }
            OnPropertyChanged(nameof(CanApplyMappings));
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
