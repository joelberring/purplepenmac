// CreatePdfCoursesDialogViewModel.cs
//
// ViewModel for the Create PDF Files dialog. Follows the same Settings-class
// ViewModel pattern as CreateOcadFilesDialogViewModel and
// CreateImageFilesDialogViewModel (see AGENTS.md): each dialog field is an
// individual ObservableProperty, and CoursePdfSettings is a computed property
// whose getter assembles a fresh settings object and whose setter decomposes
// an incoming one.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;

namespace PurplePen.ViewModels
{
    /// <summary>
    /// ViewModel for the Create PDF Files dialog.
    /// Usage: the caller sets <see cref="EventDB"/>, <see cref="ShowMergeParts"/>,
    /// and <see cref="EnableChangeCropping"/>, then assigns <see cref="Settings"/>
    /// to seed the dialog. After OK, read <see cref="Settings"/> for the user's
    /// choices. The window title is fixed ("Create PDF Files") and set directly
    /// in the dialog AXAML.
    /// </summary>
    public partial class CreatePdfCoursesDialogViewModel : ViewModelBase
    {
        /// <summary>Initializes a PDF dialog with the currently installed print profiles.</summary>
        public CreatePdfCoursesDialogViewModel()
        {
            RefreshPrintProfiles(null);
            LoadPrintTemplates();
        }

        // ===== Inputs (set by caller before showing) =====

        /// <summary>The event database used to populate the course list.</summary>
        [ObservableProperty]
        private EventDB? eventDB;

        /// <summary>Read-only snapshots of the open map's colours, used when editing and pairing print profiles.</summary>
        public List<SourceMapColor> CurrentMapColors { get; set; } = new List<SourceMapColor>();

        /// <summary>
        /// Whether the "Print Map Exchanges on Same Map" checkbox is visible.
        /// Only meaningful when at least one course has multiple parts.
        /// </summary>
        [ObservableProperty]
        private bool showMergeParts;

        /// <summary>
        /// Whether the user can change the "Crop / Print on multiple pages" combo.
        /// Disabled when the underlying map is a PDF (forced to crop).
        /// </summary>
        [ObservableProperty]
        private bool enableChangeCropping = true;

        // ===== UI state — bound directly to dialog controls =====

        /// <summary>Course designators selected by the user (set by code-behind on Open/OK).</summary>
        [ObservableProperty]
        private CourseDesignator[] selectedCourseDesignators = Array.Empty<CourseDesignator>();

        /// <summary>Per-course variation choices (set by code-behind on Open/OK).</summary>
        [ObservableProperty]
        private Dictionary<Id<Course>, VariationChoices> variationChoicesPerCourse =
            new Dictionary<Id<Course>, VariationChoices>();

        /// <summary>
        /// 0 = Crop to a single page, 1 = Print on multiple pages.
        /// Bound to the multi-page combo's SelectedIndex.
        /// </summary>
        [ObservableProperty]
        private int multiPageIndex; // 0 = crop, 1 = multi-page

        /// <summary>
        /// 0 = Course and map, 1 = Course only.
        /// Bound to the print-base-map combo's SelectedIndex.
        /// </summary>
        [ObservableProperty]
        private int printBaseMapIndex;

        /// <summary>
        /// 0 = RGB, 1 = CMYK. Bound to the color-model combo's SelectedIndex.
        /// Maps to <see cref="PurplePen.ColorModel"/> via +1 (the underlying
        /// enum starts at OCADCompatible=0, RGB=1, CMYK=2).
        /// </summary>
        [ObservableProperty]
        private int colorModelIndex;

        /// <summary>Index of the selected profile in <see cref="PrintProfiles"/>.</summary>
        [ObservableProperty]
        private int printProfileIndex;

        /// <summary>0 = none, 1 = runner, 2 = coach, 3 = answer.</summary>
        [ObservableProperty]
        private int trainingExerciseRenderProfileIndex;

        /// <summary>Localized feedback from profile import or export actions.</summary>
        [ObservableProperty]
        private string printProfileMessage = "";

        /// <summary>Profiles available to select for this PDF export.</summary>
        public ObservableCollection<PrintProfileChoice> PrintProfiles { get; } = new ObservableCollection<PrintProfileChoice>();

        /// <summary>Colour rules in the currently selected profile, for visual review before export.</summary>
        public ObservableCollection<PrintProfileRulePreview> SelectedPrintProfileRules { get; } = new ObservableCollection<PrintProfileRulePreview>();

        /// <summary>Version date of the selected profile.</summary>
        [ObservableProperty]
        private string selectedPrintProfileVersionDate = "";

        /// <summary>Whether the selected profile targets a forest map.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SelectedPrintProfileIsSprint))]
        private bool selectedPrintProfileIsForest;

        /// <summary>Whether the selected profile targets a sprint map.</summary>
        public bool SelectedPrintProfileIsSprint => !SelectedPrintProfileIsForest;

        /// <summary>Whether the visual print-profile review should be displayed.</summary>
        public bool HasSelectedPrintProfile => PrintProfileIndex > 0 && SelectedPrintProfileRules.Count > 0;

        /// <summary>"Print Map Exchanges on Same Map" checkbox.</summary>
        [ObservableProperty]
        private bool mergeParts;

        /// <summary>
        /// 0 = One for all courses, 1 = One per course, 2 = One per course part/variation.
        /// Bound to the file-format combo's SelectedIndex.
        /// </summary>
        [ObservableProperty]
        private int fileFormatIndex; // matches CoursePdfSettings.PdfFileCreation

        /// <summary>
        /// 0 = one map view per PDF page, 1 = two views, 2 = four views.
        /// Bound to the PDF page-layout combo's SelectedIndex.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFileFormatEnabled))]
        [NotifyPropertyChangedFor(nameof(IsMultiUpLayout))]
        private int pageLayoutIndex;

        /// <summary>Whether separate output files are compatible with the selected page layout.</summary>
        public bool IsFileFormatEnabled => PageLayoutIndex == 0;

        /// <summary>Whether multiple logical maps will be assembled on each A4 sheet.</summary>
        public bool IsMultiUpLayout => PageLayoutIndex != 0;

        /// <summary>
        /// Number of complete selected course/variation sets to create in the PDF.
        /// Bound to the copies NumericUpDown.
        /// </summary>
        [ObservableProperty]
        private decimal copies = 1m;

        /// <summary>Measured length of a nominal 100 mm calibration line.</summary>
        [ObservableProperty, NotifyPropertyChangedFor(nameof(CalculatedCalibrationFactor))]
        private decimal measuredCalibrationMm = 100m;

        /// <summary>Printer correction factor used by PDF page layout.</summary>
        [ObservableProperty]
        private double scaleCalibrationFactor = 1.0;

        /// <summary>Named reusable PDF print templates stored in user settings.</summary>
        public ObservableCollection<PrintWorkshopTemplate> PrintTemplates { get; } = new ObservableCollection<PrintWorkshopTemplate>();

        /// <summary>Physical sheet preview in front/back order.</summary>
        public ObservableCollection<PrintWorkshopPreviewItem> WorkshopPreview { get; } = new ObservableCollection<PrintWorkshopPreviewItem>();

        [ObservableProperty, NotifyCanExecuteChangedFor(nameof(ApplyPrintTemplateCommand)), NotifyCanExecuteChangedFor(nameof(DeletePrintTemplateCommand))]
        private PrintWorkshopTemplate? selectedPrintTemplate;

        [ObservableProperty]
        private string printTemplateName = "";

        /// <summary>Calculated factor from a measured 100 mm test line.</summary>
        // The measured value is the printed length of a nominal 100 mm line.
        // Use measured/100: CoursePageLayout's scaleRatio is inverse physical
        // size, so this correction increases/decreases the exported map scale
        // in the direction needed to make the paper measurement 100 mm.
        public double CalculatedCalibrationFactor => MeasuredCalibrationMm <= 0 ? 1.0 : (double)MeasuredCalibrationMm / 100.0;

        private void LoadPrintTemplates()
        {
            if (UserSettings.Current?.PrintWorkshopTemplates == null)
                return;
            foreach (PrintWorkshopTemplate template in UserSettings.Current.PrintWorkshopTemplates)
                PrintTemplates.Add(template.Clone());
        }

        private bool CanApplyPrintTemplate() => SelectedPrintTemplate != null;
        private bool CanDeletePrintTemplate() => SelectedPrintTemplate != null;

        /// <summary>Applies saved layout, duplex and calibration settings.</summary>
        [RelayCommand(CanExecute = nameof(CanApplyPrintTemplate))]
        private void ApplyPrintTemplate()
        {
            if (SelectedPrintTemplate == null)
                return;
            PageLayoutIndex = SelectedPrintTemplate.PageLayout == 2 ? 1 : SelectedPrintTemplate.PageLayout == 4 ? 2 : 0;
            IncludeBacksideInfo = SelectedPrintTemplate.IncludeBackside;
            BacksideText = SelectedPrintTemplate.BacksideText ?? "";
            ScaleCalibrationFactor = SelectedPrintTemplate.ScaleCalibrationFactor > 0 ? SelectedPrintTemplate.ScaleCalibrationFactor : 1.0;
            MeasuredCalibrationMm = (decimal)(ScaleCalibrationFactor * 100.0);
        }

        /// <summary>Saves current PDF layout, duplex and calibration settings.</summary>
        [RelayCommand]
        private void SavePrintTemplate()
        {
            string name = PrintTemplateName.Trim();
            if (name.Length == 0)
                return;
            PrintWorkshopTemplate template = new PrintWorkshopTemplate {
                Name = name,
                PageLayout = PageLayoutIndex == 0 ? 1 : PageLayoutIndex == 1 ? 2 : 4,
                IncludeBackside = IncludeBacksideInfo,
                BacksideText = BacksideText ?? "",
                ScaleCalibrationFactor = (float)(ScaleCalibrationFactor > 0 ? ScaleCalibrationFactor : 1.0),
            };
            PrintWorkshopTemplate? existing = PrintTemplates.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.CurrentCultureIgnoreCase));
            if (existing != null) {
                template.PageWidth = existing.PageWidth;
                template.PageHeight = existing.PageHeight;
                template.PageMargins = existing.PageMargins;
                template.Landscape = existing.Landscape;
                template.Rotation = existing.Rotation;
                template.FixSizeToPaper = existing.FixSizeToPaper;
                PrintTemplates.Remove(existing);
            }
            PrintTemplates.Add(template);
            SelectedPrintTemplate = template;
            if (UserSettings.Current != null) {
                UserSettings.Current.PrintWorkshopTemplates = PrintTemplates.Select(item => item.Clone()).ToList();
                UserSettings.Current.Save();
            }
        }

        /// <summary>Deletes the selected PDF print template.</summary>
        [RelayCommand(CanExecute = nameof(CanDeletePrintTemplate))]
        private void DeletePrintTemplate()
        {
            if (SelectedPrintTemplate == null)
                return;
            PrintTemplates.Remove(SelectedPrintTemplate);
            SelectedPrintTemplate = null;
            if (UserSettings.Current != null) {
                UserSettings.Current.PrintWorkshopTemplates = PrintTemplates.Select(item => item.Clone()).ToList();
                UserSettings.Current.Save();
            }
        }

        /// <summary>Applies the measured 100 mm calibration to subsequent PDF pages.</summary>
        [RelayCommand]
        private void ApplyCalibration()
        {
            ScaleCalibrationFactor = CalculatedCalibrationFactor;
        }

        /// <summary>Restores nominal (uncorrected) output scale.</summary>
        [RelayCommand]
        private void ResetCalibration()
        {
            MeasuredCalibrationMm = 100m;
            ScaleCalibrationFactor = 1.0;
        }

        /// <summary>Suggested complete map-set count from the selected event classes.</summary>
        public int SuggestedCopies
        {
            get
            {
                if (EventDB == null)
                    return 1;
                Id<Course>[] ids = SelectedCourseDesignators.Select(item => item.CourseId)
                    .Where(id => id.IsNotNone).Distinct().ToArray();
                if (ids.Length == 0)
                    ids = EventDB.AllCourseIds.ToArray();
                return Math.Max(1, ids.Select(id => EventClassSupport.GetCourseRequiredMapCount(EventDB, id)).DefaultIfEmpty(1).Max());
            }
        }

        /// <summary>Applies the class-derived default, after which the user may override it.</summary>
        public void ApplySuggestedCopies()
        {
            Copies = SuggestedCopies;
        }

        /// <summary>Whether each map PDF page should be followed by an information backside.</summary>
        [ObservableProperty]
        private bool includeBacksideInfo;

        /// <summary>Optional common heading printed on every information backside.</summary>
        [ObservableProperty]
        private string backsideText = "";

        /// <summary>Participant records imported from a MeOS-compatible CSV start list.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ImportedBacksideInfoCount))]
        private List<BacksideInfoRecord> backsideInfoRecords = new List<BacksideInfoRecord>();

        /// <summary>Localized import feedback set by the view after an invalid or unreadable CSV file.</summary>
        [ObservableProperty]
        private string backsideInfoImportError = "";

        /// <summary>File name prefix.</summary>
        [ObservableProperty]
        private string filePrefix = "";

        /// <summary>Output folder (only meaningful when <see cref="UseOtherDirectory"/> is true).</summary>
        [ObservableProperty]
        private string outputDirectory = "";

        /// <summary>True when the "Same folder as Purple Pen file" radio is selected.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsOtherDirectoryVisible))]
        private bool useFileDirectory;

        /// <summary>True when the "Same folder as map file" radio is selected.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsOtherDirectoryVisible))]
        private bool useMapDirectory;

        /// <summary>True when the "Other folder" radio is selected.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsOtherDirectoryVisible))]
        private bool useOtherDirectory;

        partial void OnEventDBChanged(EventDB? value)
        {
            OnPropertyChanged(nameof(SuggestedCopies));
            RefreshWorkshopPreview();
        }

        partial void OnSelectedCourseDesignatorsChanged(CourseDesignator[] value)
        {
            OnPropertyChanged(nameof(SuggestedCopies));
            RefreshWorkshopPreview();
        }

        partial void OnPageLayoutIndexChanged(int value)
        {
            // Maps can only share a physical sheet when they are written to the
            // same PDF. Make the compatible choice explicit in the output UI.
            if (value != 0)
                FileFormatIndex = (int)CoursePdfSettings.PdfFileCreation.SingleFile;
            RefreshWorkshopPreview();
        }
        partial void OnCopiesChanged(decimal value) => RefreshWorkshopPreview();
        partial void OnIncludeBacksideInfoChanged(bool value) => RefreshWorkshopPreview();
        partial void OnBacksideTextChanged(string value) => RefreshWorkshopPreview();
        partial void OnBacksideInfoRecordsChanged(List<BacksideInfoRecord> value) => RefreshWorkshopPreview();

        /// <summary>Rebuilds physical front/back sheets using the selected layout and copies.</summary>
        public void RefreshWorkshopPreview()
        {
            WorkshopPreview.Clear();
            List<CourseDesignator> designators = SelectedCourseDesignators
                .Where(item => item.CourseId.IsNotNone)
                .ToList();
            if (designators.Count == 0 && EventDB != null)
                designators.AddRange(EventDB.AllCourseIds.Select(id => new CourseDesignator(id)));
            int perSheet = PageLayoutIndex == 0 ? 1 : PageLayoutIndex == 1 ? 2 : 4;
            int copiesCount = Math.Max(1, (int)Copies);
            List<CourseDesignator> pages = designators;
            int sheetNumber = 0;
            List<int> sheetSlotCounts = CoursePageSheetLayout.GetSheetSlotCounts(
                pages.Count, (CoursePdfSettings.PdfPageLayout)perSheet, copiesCount);
            int pageOffset = 0;
            foreach (int slotCount in sheetSlotCounts) {
                    if (pageOffset >= pages.Count)
                        pageOffset = 0;
                    List<CourseDesignator> slots = pages.Skip(pageOffset).Take(slotCount).ToList();
                    pageOffset += slotCount;
                    ++sheetNumber;
                    WorkshopPreview.Add(new PrintWorkshopPreviewItem {
                        SheetNumber = sheetNumber, IsFront = true, SlotCount = slots.Count,
                        SlotSummary = FormatSlots(slots, FrontsidePreviewText),
                    });
                    if (IncludeBacksideInfo) {
                        WorkshopPreview.Add(new PrintWorkshopPreviewItem {
                            SheetNumber = sheetNumber, IsBack = true, SlotCount = slots.Count,
                            // CoursePdf emits one backside immediately after the matching
                            // front sheet. Keep the physical slot order identical to the
                            // front sheet; a duplex printer mirrors the paper itself.
                            SlotSummary = FormatSlots(slots, BacksidePreviewText),
                        });
                    }
            }
        }

        private string FormatSlots(List<CourseDesignator> slots, Func<CourseDesignator, string> formatter)
        {
            return string.Join("  •  ", slots.Select((slot, index) => "[" + (index + 1) + "] " + formatter(slot)));
        }

        private string FrontsidePreviewText(CourseDesignator designator)
        {
            if (EventDB == null)
                return designator.CourseId.ToString();
            CourseView courseView = CourseView.CreatePrintingCourseView(EventDB, designator);
            string className = string.Join(", ", BacksideInfoFormatter.GetClassNames(EventDB, courseView));
            string result = courseView.CourseFullName;
            if (!string.IsNullOrWhiteSpace(className))
                result += " / " + className;
            return result;
        }

        private string BacksidePreviewText(CourseDesignator designator)
        {
            if (EventDB == null)
                return string.IsNullOrWhiteSpace(BacksideText) ? designator.CourseId.ToString() : BacksideText.Trim();

            CourseView courseView = CourseView.CreatePrintingCourseView(EventDB, designator);
            BacksideInfoRecord? importedRecord = BacksideInfoFormatter.FindImportedRecord(EventDB, courseView, BacksideInfoRecords);

            List<string> lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(BacksideText))
                lines.Add(BacksideText.Trim());
            lines.Add(courseView.CourseNameAndPart);
            string className = string.Join(", ", BacksideInfoFormatter.GetClassNames(EventDB, courseView));
            if (!string.IsNullOrWhiteSpace(className))
                lines.Add(className);
            if (courseView.RelayTeam.HasValue)
                lines.Add(courseView.RelayTeam.Value.ToString());
            if (courseView.RelayLeg.HasValue)
                lines.Add(courseView.RelayLeg.Value.ToString());
            if (importedRecord != null) {
                if (!string.IsNullOrWhiteSpace(importedRecord.Name)) lines.Add(importedRecord.Name);
                if (!string.IsNullOrWhiteSpace(importedRecord.ClassName)) lines.Add(importedRecord.ClassName);
                if (!string.IsNullOrWhiteSpace(importedRecord.TeamName)) lines.Add(importedRecord.TeamName);
            }
            return string.Join(" / ", lines);
        }

        private string[] GetEventClassNamesArray(Id<Course> courseId)
        {
            if (EventDB == null || courseId.IsNotNone == false)
                return Array.Empty<string>();
            return EventClassSupport.GetClasses(EventDB, courseId)
                .Select(pair => pair.Value.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToArray();
        }

        private string GetEventClassNames(Id<Course> courseId)
        {
            return string.Join(", ", GetEventClassNamesArray(courseId));
        }

        // ===== Pass-through (set by caller, not UI-bound, preserved through dialog) =====

        /// <summary>Whether to render control descriptions on the page (not exposed in the UI).</summary>
        public bool RenderControlDescriptions { get; set; } = true;

        /// <summary>Whether to show a progress dialog while creating the PDFs (not exposed in the UI).</summary>
        public bool ShowProgressDialog { get; set; } = true;

        /// <summary>Partial print-profile matches explicitly confirmed by the user.</summary>
        public List<string> ConfirmedPrintProfileRuleIds { get; set; } = new List<string>();

        /// <summary>Explicit source-map colour choices for ambiguous profile rules.</summary>
        public List<PrintProfileColorMapping> PrintProfileColorMappings { get; set; } = new List<PrintProfileColorMapping>();

        // ===== Computed properties =====

        /// <summary>True when the "Other folder" textbox + button should be visible.</summary>
        public bool IsOtherDirectoryVisible => UseOtherDirectory;

        /// <summary>Number of participant records currently available for backside matching.</summary>
        public int ImportedBacksideInfoCount => BacksideInfoRecords.Count;

        /// <summary>Gets the stable identifier selected by the print-profile combo.</summary>
        public string PrintProfileId
        {
            get
            {
                return PrintProfileIndex > 0 && PrintProfileIndex < PrintProfiles.Count
                    ? PrintProfiles[PrintProfileIndex].Id
                    : String.Empty;
            }
        }

        /// <summary>Keeps profile exports on the CMYK path supported by the preflight.</summary>
        /// <param name="value">The newly selected profile index.</param>
        partial void OnPrintProfileIndexChanged(int value)
        {
            if (value != 0)
                ColorModelIndex = 1;

            RefreshSelectedPrintProfilePreview();
        }

        /// <summary>Reloads imported profiles and optionally selects one by its stable ID.</summary>
        /// <param name="profileId">The profile to select, or null to retain the current selection.</param>
        public void RefreshPrintProfiles(string? profileId)
        {
            string selectedId = profileId ?? PrintProfileId;
            PrintProfiles.Clear();
            PrintProfiles.Add(new PrintProfileChoice { IsNone = true });
            foreach (PrintProfile profile in PrintProfileCatalog.CreateAll()) {
                PrintProfiles.Add(new PrintProfileChoice { Id = profile.Id, Name = profile.Name });
            }

            int selectedIndex = 0;
            for (int index = 1; index < PrintProfiles.Count; ++index) {
                if (String.Equals(PrintProfiles[index].Id, selectedId, StringComparison.Ordinal)) {
                    selectedIndex = index;
                    break;
                }
            }

            PrintProfileIndex = selectedIndex;
            RefreshSelectedPrintProfilePreview();
        }

        /// <summary>Rebuilds the screen-only profile preview from the selected profile.</summary>
        private void RefreshSelectedPrintProfilePreview()
        {
            SelectedPrintProfileRules.Clear();
            PrintProfile? profile = PrintProfileCatalog.FindById(PrintProfileId);
            if (profile == null) {
                SelectedPrintProfileVersionDate = "";
                SelectedPrintProfileIsForest = false;
                OnPropertyChanged(nameof(HasSelectedPrintProfile));
                return;
            }

            SelectedPrintProfileVersionDate = profile.VersionDate;
            SelectedPrintProfileIsForest = profile.MapKind == PrintProfileMapKind.Forest;
            foreach (PrintProfileColorRule rule in profile.ColorRules.OrderBy(item => item.RelativeDrawOrder)) {
                SelectedPrintProfileRules.Add(new PrintProfileRulePreview {
                    Name = rule.Name,
                    Cmyk = String.Format("C{0:0} M{1:0} Y{2:0} K{3:0}", rule.EffectiveCmyk.Cyan, rule.EffectiveCmyk.Magenta, rule.EffectiveCmyk.Yellow, rule.EffectiveCmyk.Black),
                    PreviewColor = PrintProfileMappingsDialogViewModel.ToPreviewColor(rule.EffectiveCmyk),
                    UsesOverprint = rule.OverprintIntent == PrintProfileOverprintIntent.Overprint,
                });
            }

            OnPropertyChanged(nameof(HasSelectedPrintProfile));
        }

        // ===== Settings: assembles / decomposes a CoursePdfSettings =====

        /// <summary>
        /// Bridge between the dialog's individual ViewModel properties and the
        /// <see cref="CoursePdfSettings"/> type the Controller expects. Getter
        /// assembles a fresh settings object; setter decomposes one into the
        /// individual ViewModel properties.
        /// </summary>
        public CoursePdfSettings Settings
        {
            get
            {
                Id<Course>[] courseIds = SelectedCourseDesignators
                    .Select(d => d.CourseId).ToArray();
                bool allCourses = EventDB != null
                                  && courseIds.Count(c => c != Id<Course>.None) == EventDB.AllCourseIds.Count;

                return new CoursePdfSettings {
                    CourseIds = courseIds,
                    AllCourses = allCourses,
                    DontPrintBaseMap = PrintBaseMapIndex == 1,
                    // WinForms uses index 0 = "Crop to a single page" -> CropLargePrintArea = true.
                    CropLargePrintArea = MultiPageIndex == 0,
                    PrintMapExchangesOnOneMap = MergeParts,
                    Copies = (int)Copies,
                    ScaleCalibrationFactor = (float)(ScaleCalibrationFactor > 0 ? ScaleCalibrationFactor : 1.0),
                    PageLayout = (CoursePdfSettings.PdfPageLayout)(PageLayoutIndex == 0 ? 1 : PageLayoutIndex == 1 ? 2 : 4),
                    IncludeBacksideInfo = IncludeBacksideInfo,
                    BacksideText = BacksideText,
                    BacksideInfoRecords = BacksideInfoRecords.Select(record => record.Clone()).ToList(),
                    // The combo lists RGB then CMYK; the underlying enum is
                    // OCADCompatible=0, RGB=1, CMYK=2. So index + 1.
                    ColorModel = (ColorModel)(ColorModelIndex + 1),
                    PrintProfileId = PrintProfileId,
                    TrainingExerciseRenderProfile = (TrainingExerciseRenderProfile)TrainingExerciseRenderProfileIndex,
                    ConfirmedPrintProfileRuleIds = new List<string>(ConfirmedPrintProfileRuleIds),
                    PrintProfileColorMappings = new List<PrintProfileColorMapping>(PrintProfileColorMappings),
                    FileCreation = PageLayoutIndex == 0
                        ? (CoursePdfSettings.PdfFileCreation)FileFormatIndex
                        : CoursePdfSettings.PdfFileCreation.SingleFile,
                    RenderControlDescriptions = RenderControlDescriptions,
                    ShowProgressDialog = ShowProgressDialog,
                    mapDirectory = UseMapDirectory,
                    fileDirectory = UseFileDirectory,
                    outputDirectory = OutputDirectory,
                    filePrefix = FilePrefix,
                    VariationChoicesPerCourse = VariationChoicesPerCourse,
                };
            }
            set
            {
                // Build the initial CourseDesignators selection: if AllCourses
                // is true, populate from EventDB.AllCourseIds (plus AllControls
                // if CourseIds included it); otherwise use CourseIds directly.
                List<CourseDesignator> designators = new List<CourseDesignator>();
                if (value.AllCourses && EventDB != null) {
                    designators.AddRange(EventDB.AllCourseIds.Select(id => new CourseDesignator(id)));
                    if (value.CourseIds != null && Array.IndexOf(value.CourseIds, Id<Course>.None) >= 0)
                        designators.Add(new CourseDesignator(Id<Course>.None));
                }
                else if (value.CourseIds != null) {
                    designators.AddRange(value.CourseIds.Select(id => new CourseDesignator(id)));
                }
                SelectedCourseDesignators = designators.ToArray();

                VariationChoicesPerCourse = value.VariationChoicesPerCourse
                                            ?? new Dictionary<Id<Course>, VariationChoices>();

                PrintBaseMapIndex = value.DontPrintBaseMap ? 1 : 0;
                MultiPageIndex = value.CropLargePrintArea ? 0 : 1;
                MergeParts = value.PrintMapExchangesOnOneMap;
                // Reverse of the +1 in the getter; clamp to [0, 1] for safety
                // since OCADCompatible (enum value 0) isn't selectable in the combo.
                int colorIndex = (int)value.ColorModel - 1;
                if (colorIndex < 0) colorIndex = 0;
                if (colorIndex > 1) colorIndex = 1;
                ColorModelIndex = colorIndex;

                RefreshPrintProfiles(value.PrintProfileId);
                TrainingExerciseRenderProfileIndex = (int)value.TrainingExerciseRenderProfile;

                ConfirmedPrintProfileRuleIds = value.ConfirmedPrintProfileRuleIds == null
                    ? new List<string>()
                    : new List<string>(value.ConfirmedPrintProfileRuleIds);
                PrintProfileColorMappings = value.PrintProfileColorMappings == null
                    ? new List<PrintProfileColorMapping>()
                    : new List<PrintProfileColorMapping>(value.PrintProfileColorMappings);

                FileFormatIndex = (int)value.FileCreation;
                PageLayoutIndex = value.PageLayout == CoursePdfSettings.PdfPageLayout.TwoPerPage ? 1 :
                                  value.PageLayout == CoursePdfSettings.PdfPageLayout.FourPerPage ? 2 : 0;
                Copies = Math.Max(1, value.Copies);
                ScaleCalibrationFactor = value.ScaleCalibrationFactor > 0 ? value.ScaleCalibrationFactor : 1.0;
                MeasuredCalibrationMm = (decimal)(ScaleCalibrationFactor * 100.0);
                IncludeBacksideInfo = value.IncludeBacksideInfo;
                BacksideText = value.BacksideText ?? "";
                BacksideInfoRecords = value.BacksideInfoRecords == null
                    ? new List<BacksideInfoRecord>()
                    : value.BacksideInfoRecords.Select(record => record.Clone()).ToList();
                BacksideInfoImportError = "";

                RenderControlDescriptions = value.RenderControlDescriptions;
                ShowProgressDialog = value.ShowProgressDialog;

                OutputDirectory = value.outputDirectory ?? "";
                FilePrefix = value.filePrefix ?? "";

                UseMapDirectory = value.mapDirectory;
                UseFileDirectory = value.fileDirectory;
                UseOtherDirectory = !value.mapDirectory && !value.fileDirectory;
            }
        }
    }

    /// <summary>A selectable print profile in the PDF export dialog.</summary>
    public sealed class PrintProfileChoice
    {
        /// <summary>Stable profile identifier; empty for the no-profile choice.</summary>
        public string Id { get; set; } = "";

        /// <summary>Display name supplied by the profile.</summary>
        public string Name { get; set; } = "";

        /// <summary>Whether this represents normal PDF export with no profile.</summary>
        public bool IsNone { get; set; }

        /// <summary>Whether this represents an installed profile.</summary>
        public bool IsProfile => !IsNone;
    }

    /// <summary>Screen-only preview of one print-profile colour rule.</summary>
    public sealed class PrintProfileRulePreview
    {
        /// <summary>Profile colour name.</summary>
        public string Name { get; set; } = "";

        /// <summary>CMYK values formatted for review.</summary>
        public string Cmyk { get; set; } = "";

        /// <summary>sRGB approximation used only for the screen colour swatch.</summary>
        public string PreviewColor { get; set; } = "#FFFFFF";

        /// <summary>Whether the profile expects this colour to overprint colours below.</summary>
        public bool UsesOverprint { get; set; }

        /// <summary>Whether the profile expects this colour to knock out colours below.</summary>
        public bool UsesKnockout => !UsesOverprint;
    }

    /// <summary>One physical-sheet side in the PDF paper workshop preview.</summary>
    public sealed class PrintWorkshopPreviewItem
    {
        public int SheetNumber { get; set; }
        public bool IsFront { get; set; }
        public bool IsBack { get; set; }
        public int SlotCount { get; set; }
        public string SlotSummary { get; set; } = "";
    }
}
