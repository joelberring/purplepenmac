// PdfProductionReviewDialogViewModel.cs
//
// Read-only data holder for the course-PDF production review dialog.

using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace PurplePen.ViewModels
{
    /// <summary>
    /// ViewModel for the final production check shown before creating course
    /// PDF files. The caller supplies a calculated <see cref="PdfProductionSummary"/>.
    /// </summary>
    public partial class PdfProductionReviewDialogViewModel : ViewModelBase
    {
        /// <summary>The calculated PDF-export quantities and review flags.</summary>
        [ObservableProperty]
        private PdfProductionSummary? summary;

        /// <summary>True when one or more production-review warnings are present.</summary>
        public bool HasWarnings => Summary != null && Summary.Warnings.Count > 0;

        /// <summary>True when the export intentionally omits the base map.</summary>
        public bool HasCourseOnlyWarning => HasWarning(PdfProductionWarning.CourseOnly);

        /// <summary>True when RGB output was selected.</summary>
        public bool HasRgbWarning => HasWarning(PdfProductionWarning.RgbColor);

        /// <summary>True when control descriptions are disabled.</summary>
        public bool HasNoDescriptionsWarning => HasWarning(PdfProductionWarning.NoControlDescriptions);

        /// <summary>True when a PDF base map needs the multi-up rendering path.</summary>
        public bool HasPdfMapMultiUpWarning => HasWarning(PdfProductionWarning.PdfMapMultiUp);

        /// <summary>True when a course or variation spans multiple map views.</summary>
        public bool HasMultiplePagesWarning => HasWarning(PdfProductionWarning.MultiplePagesPerCourse);

        /// <summary>Course/variation quantities to display.</summary>
        public IReadOnlyList<PdfProductionCourseSummary> Courses => Summary != null
            ? (IReadOnlyList<PdfProductionCourseSummary>)Summary.Courses
            : Array.Empty<PdfProductionCourseSummary>();

        /// <summary>Paper-sheet quantities to display.</summary>
        public IReadOnlyList<PdfProductionPaperSummary> Papers => Summary != null
            ? (IReadOnlyList<PdfProductionPaperSummary>)Summary.Papers
            : Array.Empty<PdfProductionPaperSummary>();

        /// <summary>True when the event has one or more class-to-course assignments.</summary>
        public bool HasClassPlan => Summary != null && Summary.Classes.Count > 0;

        /// <summary>Class-to-course assignments and known start-list quantities.</summary>
        public IReadOnlyList<PdfProductionClassSummary> Classes => Summary != null
            ? (IReadOnlyList<PdfProductionClassSummary>)Summary.Classes
            : Array.Empty<PdfProductionClassSummary>();

        /// <summary>Updates computed values when a new summary is supplied.</summary>
        partial void OnSummaryChanged(PdfProductionSummary? value)
        {
            OnPropertyChanged(nameof(HasWarnings));
            OnPropertyChanged(nameof(HasCourseOnlyWarning));
            OnPropertyChanged(nameof(HasRgbWarning));
            OnPropertyChanged(nameof(HasNoDescriptionsWarning));
            OnPropertyChanged(nameof(HasPdfMapMultiUpWarning));
            OnPropertyChanged(nameof(HasMultiplePagesWarning));
            OnPropertyChanged(nameof(Courses));
            OnPropertyChanged(nameof(Papers));
            OnPropertyChanged(nameof(HasClassPlan));
            OnPropertyChanged(nameof(Classes));
        }

        /// <summary>Checks whether the calculated summary contains a warning.</summary>
        /// <param name="warning">Warning to look for.</param>
        /// <returns>True if the warning applies.</returns>
        private bool HasWarning(PdfProductionWarning warning)
        {
            return Summary != null && Summary.Warnings.Contains(warning);
        }
    }
}
