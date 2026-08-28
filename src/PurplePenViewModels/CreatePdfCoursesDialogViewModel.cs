// CreatePdfCoursesDialogViewModel.cs
//
// ViewModel for the Create PDF Files dialog. Follows the same Settings-class
// ViewModel pattern as CreateOcadFilesDialogViewModel and
// CreateImageFilesDialogViewModel (see AGENTS.md): each dialog field is an
// individual ObservableProperty, and CoursePdfSettings is a computed property
// whose getter assembles a fresh settings object and whose setter decomposes
// an incoming one.

using CommunityToolkit.Mvvm.ComponentModel;
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
        }

        // ===== Inputs (set by caller before showing) =====

        /// <summary>The event database used to populate the course list.</summary>
        [ObservableProperty]
        private EventDB? eventDB;

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

        /// <summary>Localized feedback from profile import or export actions.</summary>
        [ObservableProperty]
        private string printProfileMessage = "";

        /// <summary>Profiles available to select for this PDF export.</summary>
        public ObservableCollection<PrintProfileChoice> PrintProfiles { get; } = new ObservableCollection<PrintProfileChoice>();

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
        private int pageLayoutIndex;

        /// <summary>
        /// Number of complete selected course/variation sets to create in the PDF.
        /// Bound to the copies NumericUpDown.
        /// </summary>
        [ObservableProperty]
        private decimal copies = 1m;

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
                    PageLayout = (CoursePdfSettings.PdfPageLayout)(PageLayoutIndex == 0 ? 1 : PageLayoutIndex == 1 ? 2 : 4),
                    IncludeBacksideInfo = IncludeBacksideInfo,
                    BacksideText = BacksideText,
                    BacksideInfoRecords = BacksideInfoRecords.Select(record => record.Clone()).ToList(),
                    // The combo lists RGB then CMYK; the underlying enum is
                    // OCADCompatible=0, RGB=1, CMYK=2. So index + 1.
                    ColorModel = (ColorModel)(ColorModelIndex + 1),
                    PrintProfileId = PrintProfileId,
                    ConfirmedPrintProfileRuleIds = new List<string>(ConfirmedPrintProfileRuleIds),
                    PrintProfileColorMappings = new List<PrintProfileColorMapping>(PrintProfileColorMappings),
                    FileCreation = (CoursePdfSettings.PdfFileCreation)FileFormatIndex,
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
}
