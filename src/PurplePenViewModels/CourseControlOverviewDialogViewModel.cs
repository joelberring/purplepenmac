// CourseControlOverviewDialogViewModel.cs
// Presents concise course and control information for an event.

using System.Collections.ObjectModel;
using System;
using System.Linq;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PurplePen.ViewModels
{
    /// <summary>ViewModel for the combined course and control overview.</summary>
    public partial class CourseControlOverviewDialogViewModel : ViewModelBase
    {
        [ObservableProperty]
        private int selectedTabIndex;

        public bool IsCoursesTab => SelectedTabIndex == 0;
        public bool IsControlsTab => SelectedTabIndex == 1;

        partial void OnSelectedTabIndexChanged(int value)
        {
            OnPropertyChanged(nameof(IsCoursesTab));
            OnPropertyChanged(nameof(IsControlsTab));
        }

        /// <summary>Courses in the event.</summary>
        public ObservableCollection<CourseOverviewItem> Courses { get; } = new ObservableCollection<CourseOverviewItem>();

        /// <summary>Controls in the event.</summary>
        public ObservableCollection<ControlOverviewItem> Controls { get; } = new ObservableCollection<ControlOverviewItem>();

        /// <summary>Courses currently visible after applying the course search.</summary>
        public ObservableCollection<CourseOverviewItem> VisibleCourses { get; } = new ObservableCollection<CourseOverviewItem>();

        /// <summary>Controls currently visible after applying the selected course and control search.</summary>
        public ObservableCollection<ControlOverviewItem> VisibleControls { get; } = new ObservableCollection<ControlOverviewItem>();

        /// <summary>Course currently selected as the first side of a comparison.</summary>
        [ObservableProperty, NotifyCanExecuteChangedFor(nameof(CompareCoursesCommand))]
        private CourseOverviewItem? comparisonCourse;

        /// <summary>Course currently selected as the second side of a comparison.</summary>
        [ObservableProperty, NotifyCanExecuteChangedFor(nameof(CompareCoursesCommand))]
        private CourseOverviewItem? comparisonWithCourse;

        /// <summary>Control currently selected for map navigation.</summary>
        [ObservableProperty, NotifyCanExecuteChangedFor(nameof(NavigateToSelectedControlCommand))]
        private ControlOverviewItem? selectedControl;

        /// <summary>Human-readable result of the last course comparison.</summary>
        [ObservableProperty]
        private string comparisonSummary = "";

        [ObservableProperty, NotifyCanExecuteChangedFor(nameof(NavigateToSelectedCourseCommand))]
        private CourseOverviewItem? selectedCourse;

        /// <summary>Whether the dialog was accepted to navigate to the selected course.</summary>
        public bool NavigateToSelectedCourseRequested { get; private set; }

        /// <summary>Whether the caller should select and focus the selected control.</summary>
        public bool NavigateToSelectedControlRequested { get; private set; }

        /// <summary>Whether the caller should activate the first comparison course.</summary>
        public bool CompareCoursesRequested { get; private set; }

        /// <summary>Whether edited class/load/code values should be committed.</summary>
        public bool SaveEditsRequested { get; private set; }

        /// <summary>Localized resource key for an invalid load entry.</summary>
        [ObservableProperty]
        private string loadValidationMessageKey = "";

        /// <summary>Gets whether the current load edits contain an error.</summary>
        public bool HasLoadValidationError => !String.IsNullOrEmpty(LoadValidationMessageKey);

        partial void OnLoadValidationMessageKeyChanged(string value)
        {
            OnPropertyChanged(nameof(HasLoadValidationError));
        }

        [ObservableProperty]
        private string courseSearchText = "";

        [ObservableProperty]
        private string controlSearchText = "";

        partial void OnSelectedCourseChanged(CourseOverviewItem? value)
        {
            RefreshVisibleControls();
        }

        partial void OnCourseSearchTextChanged(string value)
        {
            RefreshVisibleCourses();
        }

        partial void OnControlSearchTextChanged(string value)
        {
            RefreshVisibleControls();
        }

        /// <summary>Refreshes filtered rows after the caller has populated the source lists.</summary>
        public void RefreshVisibleItems()
        {
            RefreshVisibleCourses();
            RefreshVisibleControls();
        }

        [RelayCommand]
        private void ShowAllCourses()
        {
            SelectedCourse = null;
            CourseSearchText = "";
        }

        private bool CanNavigateToSelectedCourse()
        {
            return SelectedCourse != null;
        }

        /// <summary>Records that the selected course should be shown in the main map.</summary>
        [RelayCommand(CanExecute = nameof(CanNavigateToSelectedCourse))]
        private void NavigateToSelectedCourse()
        {
            NavigateToSelectedCourseRequested = true;
        }

        private void RefreshVisibleCourses()
        {
            string search = CourseSearchText.Trim();
            ReplaceItems(VisibleCourses, Courses.Where(course => string.IsNullOrEmpty(search) || course.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)));
        }

        private void RefreshVisibleControls()
        {
            string search = ControlSearchText.Trim();
            string? selectedCourseName = SelectedCourse?.Name;
            ReplaceItems(VisibleControls, Controls.Where(control =>
                (string.IsNullOrEmpty(selectedCourseName) || control.CourseNames.Contains(selectedCourseName, StringComparer.CurrentCulture)) &&
                (string.IsNullOrEmpty(search) || control.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase) || control.Courses.Contains(search, StringComparison.CurrentCultureIgnoreCase))));
        }

        /// <summary>Sorts courses by competitor load, with unset loads last.</summary>
        [RelayCommand]
        private void SortCoursesByLoad()
        {
            ReplaceItems(VisibleCourses, VisibleCourses.OrderBy(course => course.Load < 0 ? int.MaxValue : course.Load).ThenBy(course => course.Name, StringComparer.CurrentCulture));
        }

        /// <summary>Sorts courses alphabetically.</summary>
        [RelayCommand]
        private void SortCoursesByName()
        {
            ReplaceItems(VisibleCourses, VisibleCourses.OrderBy(course => course.Name, StringComparer.CurrentCulture));
        }

        /// <summary>Sorts controls by their code.</summary>
        [RelayCommand]
        private void SortControlsByCode()
        {
            ReplaceItems(VisibleControls, VisibleControls.OrderBy(control => control.Name, StringComparer.CurrentCulture));
        }

        /// <summary>Sorts controls by the number of courses visiting them.</summary>
        [RelayCommand]
        private void SortControlsByLoad()
        {
            ReplaceItems(VisibleControls, VisibleControls.OrderByDescending(control => control.CourseIds.Length).ThenBy(control => control.Name, StringComparer.CurrentCulture));
        }

        private bool CanNavigateToSelectedControl() => SelectedControl != null;

        /// <summary>Requests map navigation to the selected control.</summary>
        [RelayCommand(CanExecute = nameof(CanNavigateToSelectedControl))]
        private void NavigateToSelectedControl()
        {
            NavigateToSelectedControlRequested = true;
        }

        private bool CanCompareCourses() => ComparisonCourse != null && ComparisonWithCourse != null && ComparisonCourse.CourseId != ComparisonWithCourse.CourseId;

        /// <summary>Calculates common and differing controls and legs for two courses.</summary>
        [RelayCommand(CanExecute = nameof(CanCompareCourses))]
        private void CompareCourses()
        {
            if (ComparisonCourse == null || ComparisonWithCourse == null)
                return;

            HashSet<Id<ControlPoint>> firstControls = new HashSet<Id<ControlPoint>>(ComparisonCourse.ControlIds);
            HashSet<Id<ControlPoint>> secondControls = new HashSet<Id<ControlPoint>>(ComparisonWithCourse.ControlIds);
            int commonControls = firstControls.Intersect(secondControls).Count();
            int onlyFirst = firstControls.Except(secondControls).Count();
            int onlySecond = secondControls.Except(firstControls).Count();
            HashSet<string> firstLegs = new HashSet<string>(ComparisonCourse.LegKeys, StringComparer.Ordinal);
            HashSet<string> secondLegs = new HashSet<string>(ComparisonWithCourse.LegKeys, StringComparer.Ordinal);
            int commonLegs = firstLegs.Intersect(secondLegs).Count();
            ComparisonSummary = $"{ComparisonCourse.Name}: {commonControls} common controls, {onlyFirst} only here; {ComparisonWithCourse.Name}: {onlySecond} only there; {commonLegs} common legs.";
            CompareCoursesRequested = true;
        }

        /// <summary>Requests the caller to commit the editable overview fields.</summary>
        [RelayCommand]
        private void SaveEdits()
        {
            if (!ValidateLoadTexts())
                return;
            SaveEditsRequested = true;
        }

        /// <summary>Accepts blank (unset) or non-negative integer load values before saving edits.</summary>
        public bool ValidateLoadTexts()
        {
            bool valid = Courses.All(course => {
                string load = (course.LoadText ?? String.Empty).Trim();
                return String.IsNullOrEmpty(load) || (Int32.TryParse(load, out int value) && value >= 0);
            });
            LoadValidationMessageKey = valid ? "" : "CourseControlOverviewDialog_InvalidLoad";
            return valid;
        }

        private static void ReplaceItems<T>(ObservableCollection<T> target, IEnumerable<T> items)
        {
            target.Clear();
            foreach (T item in items)
                target.Add(item);
        }
    }

    /// <summary>Summary information for one course.</summary>
    public sealed class CourseOverviewItem
    {
        /// <summary>Identity of the course to activate when navigating from the overview.</summary>
        public Id<Course> CourseId { get; set; }

        /// <summary>Name of the course.</summary>
        public string Name { get; set; } = "";

        /// <summary>Number of ordinary controls on the course.</summary>
        public int ControlCount { get; set; }

        /// <summary>Formatted course length.</summary>
        public string Length { get; set; } = "";

        /// <summary>Formatted climb, if present.</summary>
        public string Climb { get; set; } = "";

        /// <summary>Competitor load, or -1 if unset.</summary>
        public int Load { get; set; } = -1;

        /// <summary>Editable display value for the competitor load.</summary>
        public string LoadText { get; set; } = "";

        /// <summary>Original load used to avoid recording a no-op change.</summary>
        public int OriginalLoad { get; set; } = -1;

        /// <summary>Optional class assignment.</summary>
        public string ClassName { get; set; } = "";

        /// <summary>Original class used to avoid recording a no-op change.</summary>
        public string OriginalClassName { get; set; } = "";

        /// <summary>Stable control identities on this course.</summary>
        public Id<ControlPoint>[] ControlIds { get; set; } = Array.Empty<Id<ControlPoint>>();

        /// <summary>Stable directed leg keys used for comparison.</summary>
        public string[] LegKeys { get; set; } = Array.Empty<string>();

        /// <summary>Readiness/load warning shown in the overview.</summary>
        public string Warning { get; set; } = "";
    }

    /// <summary>Summary information for one control point.</summary>
    public sealed class ControlOverviewItem
    {
        /// <summary>Displayed control name or code.</summary>
        public string Name { get; set; } = "";

        /// <summary>Stable control identity.</summary>
        public Id<ControlPoint> ControlId { get; set; }

        /// <summary>Editable control code.</summary>
        public string Code { get; set; } = "";

        /// <summary>Original code used to avoid recording a no-op change.</summary>
        public string OriginalCode { get; set; } = "";

        /// <summary>Courses which visit the control.</summary>
        public string Courses { get; set; } = "";

        /// <summary>Course names represented by this row, used for interactive filtering.</summary>
        public string[] CourseNames { get; set; } = Array.Empty<string>();

        /// <summary>Stable identities of courses visiting this control.</summary>
        public Id<Course>[] CourseIds { get; set; } = Array.Empty<Id<Course>>();

        /// <summary>Number of incoming legs.</summary>
        public int Incoming { get; set; }

        /// <summary>Number of outgoing legs.</summary>
        public int Outgoing { get; set; }

        /// <summary>Concrete readiness warning for this control.</summary>
        public string Warning { get; set; } = "";
    }
}
