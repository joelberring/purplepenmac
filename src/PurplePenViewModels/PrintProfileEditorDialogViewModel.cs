using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace PurplePen.ViewModels
{
    /// <summary>Edits a private copy of a print profile without modifying the source map.</summary>
    public partial class PrintProfileEditorDialogViewModel : ViewModelBase
    {
        [ObservableProperty] private string name = "";
        [ObservableProperty] private string versionDate = DateTime.Today.ToString("yyyy-MM-dd");
        [ObservableProperty] private int mapKindIndex;
        [ObservableProperty] private string printerName = "";
        [ObservableProperty] private string paperSpecification = "";
        [ObservableProperty] private string iccProfile = "";
        [ObservableProperty] private string validationMessageKey = "";
        public bool HasValidationError => !String.IsNullOrEmpty(ValidationMessageKey);
        partial void OnValidationMessageKeyChanged(string value) { OnPropertyChanged(nameof(HasValidationError)); }
        [ObservableProperty] private PrintProfilePreflightState preflightState;
        public bool IsPreflightPassed => PreflightState == PrintProfilePreflightState.Passed;
        public bool IsPreflightReview => PreflightState == PrintProfilePreflightState.Review;
        public bool CanPairAgainstMap => currentMapColors.Count > 0 && Rules.Count > 0;
        public Func<PrintProfile, bool>? PreflightRunner { get; set; }
        public ObservableCollection<PrintProfileHistoryEntry> History { get; } = new ObservableCollection<PrintProfileHistoryEntry>();
        public ObservableCollection<PrintProfilePreflightItem> PreflightFindings { get; } = new ObservableCollection<PrintProfilePreflightItem>();
        [ObservableProperty] private int courseColorRuleIndex = -1;
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RemoveSelectedRuleCommand))]
        private PrintProfileRuleEditorItem? selectedRule;
        private string sourceCourseColorRuleId = "";
        private string sourceUrl = "";
        private string sourceDescription = "";
        private PrintProfileRequirements requirements = new PrintProfileRequirements();
        public string Id { get; private set; } = "";
        /// <summary>Profiles available as sources for the editable copy.</summary>
        public ObservableCollection<PrintProfileEditorSourceChoice> SourceProfiles { get; } = new ObservableCollection<PrintProfileEditorSourceChoice>();
        [ObservableProperty] private string selectedSourceProfileId = "";
        private List<PrintProfile> sourceProfiles = new List<PrintProfile>();
        private List<SourceMapColor> currentMapColors = new List<SourceMapColor>();
        private bool loadingCopy;
        public ObservableCollection<PrintProfileRuleEditorItem> Rules { get; } = new ObservableCollection<PrintProfileRuleEditorItem>();

        /// <summary>Populates the source selector and loads the requested profile.</summary>
        public void LoadProfiles(IEnumerable<PrintProfile> profiles, string? selectedId = null, bool includeCurrentMap = false)
        {
            sourceProfiles = profiles.ToList();
            SourceProfiles.Clear();
            foreach (PrintProfile profile in sourceProfiles)
                SourceProfiles.Add(new PrintProfileEditorSourceChoice { Id = profile.Id, Name = profile.Name });
            if (includeCurrentMap)
                SourceProfiles.Insert(0, new PrintProfileEditorSourceChoice { Id = CurrentMapId, Name = "Current map" });

            PrintProfile? source = sourceProfiles.FirstOrDefault(profile => selectedId != null && profile.Id == selectedId)
                ?? sourceProfiles.FirstOrDefault();
            if (source != null)
                LoadCopy(source);
        }

        /// <summary>Creates a profile editor seeded from the current map colour table.</summary>
        public void LoadFromMap(IEnumerable<Pair<int, string>> mapColors)
        {
            loadingCopy = true;
            ResetMapProfileState();
            Rules.Clear();
            int order = 1;
            foreach (Pair<int, string> color in mapColors ?? Enumerable.Empty<Pair<int, string>>()) {
                string id = "map-colour-" + order.ToString();
                PrintProfileColorRule sourceRule = new PrintProfileColorRule { Id = id, Name = color.Second ?? "", RelativeDrawOrder = order };
                sourceRule.Identifier.OcadIds.Add((short)color.First);
                Rules.Add(new PrintProfileRuleEditorItem { Id = id, Name = sourceRule.Name, DrawOrder = order, MatchNames = sourceRule.Name,
                    MatchOcadIds = new List<short>(sourceRule.Identifier.OcadIds), SourceRule = sourceRule });
                ++order;
            }
            CourseColorRuleIndex = Rules.Count > 0 ? 0 : -1;
            SelectedRule = Rules.FirstOrDefault();
            loadingCopy = false;
        }

        /// <summary>Creates a profile editor seeded from the map's complete colour table.</summary>
        public void LoadFromMap(IEnumerable<SourceMapColor> mapColors)
        {
            loadingCopy = true;
            ResetMapProfileState();
            Rules.Clear();
            int order = 1;
            foreach (SourceMapColor color in mapColors ?? Enumerable.Empty<SourceMapColor>()) {
                string id = "map-colour-" + order.ToString();
                PrintProfileColorRule sourceRule = new PrintProfileColorRule { Id = id, Name = color.Name ?? "", RelativeDrawOrder = order };
                sourceRule.Identifier.OcadIds.Add(color.OcadId);
                sourceRule.EffectiveCmyk = new PrintProfileCmyk { Cyan = color.Cmyk.Cyan, Magenta = color.Cmyk.Magenta, Yellow = color.Cmyk.Yellow, Black = color.Cmyk.Black };
                Rules.Add(new PrintProfileRuleEditorItem { Id = id, Name = sourceRule.Name, DrawOrder = order, MatchNames = sourceRule.Name,
                    MatchOcadIds = new List<short>(sourceRule.Identifier.OcadIds), Cyan = (decimal)color.Cmyk.Cyan,
                    Magenta = (decimal)color.Cmyk.Magenta, Yellow = (decimal)color.Cmyk.Yellow, Black = (decimal)color.Cmyk.Black,
                    SourceRule = sourceRule });
                ++order;
            }
            CourseColorRuleIndex = Rules.Count > 0 ? 0 : -1;
            SelectedRule = Rules.FirstOrDefault();
            loadingCopy = false;
        }

        partial void OnSelectedSourceProfileIdChanged(string value)
        {
            if (loadingCopy)
                return;

            if (String.Equals(value, CurrentMapId, StringComparison.Ordinal)) {
                LoadFromMap(currentMapColors);
                return;
            }

            PrintProfile? source = sourceProfiles.FirstOrDefault(profile => String.Equals(profile.Id, value, StringComparison.Ordinal));
            if (source != null)
                LoadCopy(source);
        }

        private const string CurrentMapId = "__current-map__";

        /// <summary>Loads a new profile seeded from the current map colour table.</summary>
        public void LoadCurrentMap(IEnumerable<SourceMapColor> colors)
        {
            currentMapColors = (colors ?? Enumerable.Empty<SourceMapColor>()).ToList();
            LoadFromMap(currentMapColors);
            SelectedSourceProfileId = CurrentMapId;
            if (!SourceProfiles.Any(choice => choice.Id == CurrentMapId))
                SourceProfiles.Insert(0, new PrintProfileEditorSourceChoice { Id = CurrentMapId, Name = "Current map" });
            OnPropertyChanged(nameof(CanPairAgainstMap));
        }

        /// <summary>Clears profile-only metadata before seeding a new profile from map colours.</summary>
        private void ResetMapProfileState()
        {
            Id = "custom-" + Guid.NewGuid().ToString("N");
            Name = "";
            VersionDate = DateTime.Today.ToString("yyyy-MM-dd");
            MapKindIndex = 0;
            PrinterName = "";
            PaperSpecification = "";
            IccProfile = "";
            ValidationMessageKey = "";
            PreflightState = PrintProfilePreflightState.None;
            PreflightFindings.Clear();
            History.Clear();
            sourceCourseColorRuleId = "";
            sourceUrl = "";
            sourceDescription = "";
            requirements = new PrintProfileRequirements();
        }

        /// <summary>Creates an editable, uniquely identified copy of a profile.</summary>
        public void LoadCopy(PrintProfile source)
        {
            loadingCopy = true;
            Id = source.Id.StartsWith("custom-", StringComparison.Ordinal) ? source.Id : "custom-" + Guid.NewGuid().ToString("N");
            SelectedSourceProfileId = source.Id;
            Name = source.Name + " copy";
            VersionDate = DateTime.Today.ToString("yyyy-MM-dd");
            MapKindIndex = source.MapKind == PrintProfileMapKind.Forest ? 0 : 1;
            PrinterName = source.PrinterName;
            PaperSpecification = source.PaperSpecification;
            IccProfile = source.IccProfile;
            History.Clear();
            foreach (PrintProfileHistoryEntry entry in source.History ?? new List<PrintProfileHistoryEntry>()) History.Add(entry);
            sourceCourseColorRuleId = source.CourseColorRuleId;
            sourceUrl = source.SourceUrl;
            sourceDescription = source.SourceDescription;
            requirements = CloneRequirements(source.Requirements);
            Rules.Clear();
            foreach (PrintProfileColorRule rule in source.ColorRules) {
                Rules.Add(new PrintProfileRuleEditorItem {
                    Id = rule.Id, Name = rule.Name, DrawOrder = rule.RelativeDrawOrder,
                    MatchNames = String.Join("; ", rule.Identifier.Names),
                    MatchOcadIds = new List<short>(rule.Identifier.OcadIds),
                    Cyan = (decimal)rule.EffectiveCmyk.Cyan, Magenta = (decimal)rule.EffectiveCmyk.Magenta,
                    Yellow = (decimal)rule.EffectiveCmyk.Yellow, Black = (decimal)rule.EffectiveCmyk.Black,
                    UsesOverprint = rule.OverprintIntent == PrintProfileOverprintIntent.Overprint,
                    SourceRule = rule,
                });
            }

            CourseColorRuleIndex = FindRuleIndex(sourceCourseColorRuleId);
            SelectedRule = Rules.FirstOrDefault();
            loadingCopy = false;
        }

        /// <summary>Adds a new, independently editable colour rule to the copied profile.</summary>
        [RelayCommand]
        private void AddRule()
        {
            int sequence = 1;
            string id;
            do {
                id = "custom-colour-" + sequence.ToString();
                ++sequence;
            } while (Rules.Any(rule => String.Equals(rule.Id, id, StringComparison.Ordinal)));

            PrintProfileColorRule sourceRule = new PrintProfileColorRule {
                Id = id,
                Name = id,
                RelativeDrawOrder = Rules.Count == 0 ? 1 : Rules.Max(rule => rule.DrawOrder) + 1,
            };
            PrintProfileRuleEditorItem item = new PrintProfileRuleEditorItem {
                Id = id,
                Name = id,
                DrawOrder = sourceRule.RelativeDrawOrder,
                SourceRule = sourceRule,
            };
            Rules.Add(item);
            SelectedRule = item;
        }

        /// <summary>Moves the selected rule one step toward the top of the print layer stack.</summary>
        [RelayCommand]
        private void MoveRuleUp() { MoveRule(-1); }

        /// <summary>Moves the selected rule one step toward the bottom of the print layer stack.</summary>
        [RelayCommand]
        private void MoveRuleDown() { MoveRule(1); }

        private void MoveRule(int delta)
        {
            if (SelectedRule == null) return;
            int index = Rules.IndexOf(SelectedRule);
            int target = index + delta;
            if (index < 0 || target < 0 || target >= Rules.Count) return;
            Rules.Move(index, target);
        }

        /// <summary>Runs a non-destructive preflight against the map supplied by the caller.</summary>
        [RelayCommand]
        private void TestAgainstMap()
        {
            PreflightFindings.Clear();
            bool passed = PreflightRunner != null && PreflightRunner(CreateProfile());
            PreflightState = PreflightRunner == null ? PrintProfilePreflightState.None : (passed ? PrintProfilePreflightState.Passed : PrintProfilePreflightState.Review);
        }

        /// <summary>Shows every profile colour beside the current map's colour table so the user can create explicit pairings.</summary>
        [RelayCommand]
        private async Task PairAgainstMap()
        {
            if (!CanPairAgainstMap)
                return;

            PrintProfile profile = CreateProfile();
            ColorPreflightReport report = ColorPreflightReport.Analyze(profile, currentMapColors,
                PdfExportCapabilities.CreateCurrentImplementation());
            PrintProfileMappingsDialogViewModel mappingViewModel = new PrintProfileMappingsDialogViewModel();
            mappingViewModel.Configure(profile, currentMapColors, report.RuleResults);

            if (await Services.DialogService.ShowDialogAsync(mappingViewModel)) {
                ApplyMapPairings(mappingViewModel.CreateMappings());
                TestAgainstMap();
            }
        }

        /// <summary>Stores selected map identities as the exact match identifiers in this editable profile copy.</summary>
        /// <param name="mappings">Pairings selected against the current map colour table.</param>
        public void ApplyMapPairings(IEnumerable<PrintProfileColorMapping> mappings)
        {
            foreach (PrintProfileColorMapping mapping in mappings ?? Enumerable.Empty<PrintProfileColorMapping>()) {
                PrintProfileRuleEditorItem? rule = Rules.FirstOrDefault(item => String.Equals(item.Id, mapping.RuleId, StringComparison.Ordinal));
                SourceMapColor? mapColor = currentMapColors.FirstOrDefault(color =>
                    String.Equals(color.Name, mapping.MapColorName, StringComparison.Ordinal)
                    && color.OcadId == mapping.MapColorOcadId
                    && color.DrawOrder == mapping.MapColorDrawOrder);
                if (rule == null || mapColor == null)
                    continue;

                rule.MatchNames = mapColor.Name;
                rule.MatchOcadIds = new List<short> { mapColor.OcadId };
            }
        }

        partial void OnPreflightStateChanged(PrintProfilePreflightState value)
        {
            OnPropertyChanged(nameof(IsPreflightPassed)); OnPropertyChanged(nameof(IsPreflightReview));
        }

        /// <summary>Removes the selected rule while retaining a valid course-colour selection.</summary>
        [RelayCommand(CanExecute = nameof(CanRemoveSelectedRule))]
        private void RemoveSelectedRule()
        {
            if (SelectedRule == null)
                return;

            int removedIndex = Rules.IndexOf(SelectedRule);
            if (removedIndex < 0 || Rules.Count <= 1)
                return;

            Rules.RemoveAt(removedIndex);
            if (CourseColorRuleIndex == removedIndex)
                CourseColorRuleIndex = Math.Min(removedIndex, Rules.Count - 1);
            else if (CourseColorRuleIndex > removedIndex)
                --CourseColorRuleIndex;

            SelectedRule = Rules[Math.Min(removedIndex, Rules.Count - 1)];
            RemoveSelectedRuleCommand.NotifyCanExecuteChanged();
        }

        /// <summary>Determines whether a rule can be removed without producing an invalid empty profile.</summary>
        private bool CanRemoveSelectedRule()
        {
            return SelectedRule != null && Rules.Count > 1;
        }

        /// <summary>Builds the user-owned profile from the edited values.</summary>
        public PrintProfile CreateProfile()
        {
            PrintProfile profile = new PrintProfile { Id = Id, Name = Name.Trim(), VersionDate = VersionDate.Trim(), MapKind = MapKindIndex == 0 ? PrintProfileMapKind.Forest : PrintProfileMapKind.Sprint,
                PrinterName = PrinterName.Trim(), PaperSpecification = PaperSpecification.Trim(), IccProfile = IccProfile.Trim(),
                SourceUrl = sourceUrl, SourceDescription = sourceDescription,
                Requirements = CloneRequirements(requirements), History = History.ToList() };
            HashSet<string> retainedRuleIds = new HashSet<string>(Rules.Select(item => item.Id), StringComparer.Ordinal);
            foreach (PrintProfileRuleEditorItem item in Rules) {
                PrintProfileColorRule sourceRule = item.SourceRule;
                PrintProfileColorRule rule = new PrintProfileColorRule {
                    Id = item.Id, Name = item.Name.Trim(), RelativeDrawOrder = Rules.IndexOf(item) + 1,
                    ColorKind = sourceRule.ColorKind,
                    Identifier = new PrintProfileColorIdentifier { Names = ParseMatchNames(item.MatchNames), OcadIds = new List<short>(item.MatchOcadIds) },
                    MustBeAboveRuleIds = sourceRule.MustBeAboveRuleIds.Where(retainedRuleIds.Contains).ToList(),
                    MustBeBelowRuleIds = sourceRule.MustBeBelowRuleIds.Where(retainedRuleIds.Contains).ToList(),
                };
                rule.EffectiveCmyk = new PrintProfileCmyk { Cyan = (float)item.Cyan, Magenta = (float)item.Magenta, Yellow = (float)item.Yellow, Black = (float)item.Black };
                rule.OverprintIntent = item.UsesOverprint ? PrintProfileOverprintIntent.Overprint : PrintProfileOverprintIntent.Knockout;
                profile.ColorRules.Add(rule);
            }
            profile.CourseColorRuleId = CourseColorRuleIndex >= 0 && CourseColorRuleIndex < Rules.Count
                ? Rules[CourseColorRuleIndex].Id
                : sourceCourseColorRuleId;
            return profile;
        }

        /// <summary>Creates an independent requirements object for a copied profile.</summary>
        private static PrintProfileRequirements CloneRequirements(PrintProfileRequirements source)
        {
            if (source == null)
                return new PrintProfileRequirements();

            return new PrintProfileRequirements {
                RequireCmykOutput = source.RequireCmykOutput,
                RequirePdfXOutputIntent = source.RequirePdfXOutputIntent,
                RequireEmbeddedIccProfile = source.RequireEmbeddedIccProfile,
                RequireTrueOverprint = source.RequireTrueOverprint,
            };
        }

        /// <summary>Validates values required before saving a private profile copy.</summary>
        public bool ValidateForSave()
        {
            ValidationMessageKey = String.IsNullOrWhiteSpace(Name) ? "PrintProfileEditorDialog_ErrorNameRequired" : "";
            return String.IsNullOrEmpty(ValidationMessageKey);
        }

        /// <summary>Parses the semicolon-separated source-map colour names entered for a rule.</summary>
        private static List<string> ParseMatchNames(string matchNames)
        {
            return matchNames.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(name => name.Trim())
                .Where(name => !String.IsNullOrEmpty(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>Finds the retained rule index for the export course colour.</summary>
        private int FindRuleIndex(string ruleId)
        {
            for (int index = 0; index < Rules.Count; ++index) {
                if (String.Equals(Rules[index].Id, ruleId, StringComparison.Ordinal))
                    return index;
            }

            return -1;
        }
    }

    /// <summary>Display item used to select the source of a new print-profile copy.</summary>
    public sealed class PrintProfileEditorSourceChoice
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public bool IsCurrentMap => Id == "__current-map__";
    }

    /// <summary>One rule-level diagnostic returned by a print-profile preflight.</summary>
    public sealed class PrintProfilePreflightItem
    {
        public string RuleName { get; set; } = "";
        public string Status { get; set; } = "";
        public string Detail { get; set; } = "";
    }

    public enum PrintProfilePreflightState { None, Passed, Review }

    /// <summary>Editable CMYK values for a retained print-profile rule.</summary>
    public partial class PrintProfileRuleEditorItem : ViewModelBase
    {
        public string Id { get; set; } = "";
        [ObservableProperty] private string name = "";
        [ObservableProperty] private string matchNames = "";
        public int DrawOrder { get; set; }
        public PrintProfileColorRule SourceRule { get; set; } = new PrintProfileColorRule();
        public List<short> MatchOcadIds { get; set; } = new List<short>();
        [ObservableProperty] private decimal cyan;
        [ObservableProperty] private decimal magenta;
        [ObservableProperty] private decimal yellow;
        [ObservableProperty] private decimal black;
        [ObservableProperty] private bool usesOverprint;
    }
}
