using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System;

namespace PurplePen.ViewModels
{
    public partial class EventClassDialogViewModel : ViewModelBase
    {
        public ObservableCollection<EventClassRow> Rows { get; } = new ObservableCollection<EventClassRow>();
        public ObservableCollection<CourseChoice> Courses { get; } = new ObservableCollection<CourseChoice>();
        public ObservableCollection<EventClassSuggestion> Suggestions { get; } = new ObservableCollection<EventClassSuggestion>();
        public EventDB? EventDB { get; set; }
        [ObservableProperty] private string validationMessageKey = "";
        public bool HasValidationError => !String.IsNullOrEmpty(ValidationMessageKey);
        partial void OnValidationMessageKeyChanged(string value) { OnPropertyChanged(nameof(HasValidationError)); }
        [ObservableProperty] private bool hasImportedStartList;
        public bool HasSafeSuggestions => Suggestions.Any(item => !item.IsConflict);
        public int TotalParticipants => Rows.Sum(row => Math.Max(0, row.ParticipantCount));
        public int TotalRequiredMaps => Rows.Sum(row => row.RequiredMapCount);

        public void Load(Controller.EventClassInfo[] values, CourseChoice[] courses)
        {
            ClearImportedStartList();
            ValidationMessageKey = "";
            Rows.Clear(); Courses.Clear();
            foreach (CourseChoice course in courses) Courses.Add(course);
            foreach (Controller.EventClassInfo value in values) Rows.Add(new EventClassRow(value, Courses, NotifyTotals));
            NotifyTotals();
        }

        [RelayCommand]
        private void Add()
        {
            Rows.Add(new EventClassRow(Courses, NotifyTotals));
            NotifyTotals();
        }

        private bool CanRemove() => SelectedRow != null;

        [RelayCommand(CanExecute = nameof(CanRemove))]
        private void Remove()
        {
            if (SelectedRow != null) Rows.Remove(SelectedRow);
            NotifyTotals();
        }

        [ObservableProperty, NotifyCanExecuteChangedFor(nameof(RemoveCommand))] private EventClassRow? selectedRow;

        public Controller.EventClassInfo[] Values => Rows.Select(row => row.ToInfo()).ToArray();

        private void NotifyTotals()
        {
            OnPropertyChanged(nameof(TotalParticipants));
            OnPropertyChanged(nameof(TotalRequiredMaps));
            OnPropertyChanged(nameof(Values));
        }

        public void RefreshTotals() => NotifyTotals();

        public void SetImportedStartList(System.Collections.Generic.IEnumerable<BacksideInfoRecord> records)
        {
            BeginStartListImport();
            foreach (System.Linq.IGrouping<string, BacksideInfoRecord> item in records
                .Where(record => !String.IsNullOrWhiteSpace(record.ClassName))
                .GroupBy(record => record.ClassName.Trim(), StringComparer.CurrentCultureIgnoreCase)) {
                int matchingRows = Rows.Count(row => String.Equals(row.Name.Trim(), item.Key, StringComparison.CurrentCultureIgnoreCase));
                Suggestions.Add(new EventClassSuggestion(item.Key, item.Count(), matchingRows == 1, matchingRows != 1));
            }
            HasImportedStartList = Suggestions.Count > 0;
            OnPropertyChanged(nameof(HasSafeSuggestions));
            ApplySuggestionsCommand.NotifyCanExecuteChanged();
        }

        /// <summary>Clears imported start-list suggestions and validation from an earlier import attempt.</summary>
        public void ClearImportedStartList()
        {
            Suggestions.Clear();
            HasImportedStartList = false;
            OnPropertyChanged(nameof(HasSafeSuggestions));
            ApplySuggestionsCommand.NotifyCanExecuteChanged();
        }

        /// <summary>Clears all import-related feedback before a new import attempt.</summary>
        public void BeginStartListImport()
        {
            ClearImportedStartList();
            ValidationMessageKey = "";
        }

        private bool CanApplySuggestions() => HasSafeSuggestions;

        [RelayCommand(CanExecute = nameof(CanApplySuggestions))]
        private void ApplySuggestions()
        {
            foreach (EventClassSuggestion suggestion in Suggestions.Where(item => !item.IsConflict)) {
                EventClassRow? row = Rows.FirstOrDefault(item => String.Equals(item.Name.Trim(), suggestion.ClassName, StringComparison.CurrentCultureIgnoreCase));
                if (row != null) row.ParticipantCount = suggestion.ImportedCount;
            }
            NotifyTotals();
        }

        public bool ValidateRows()
        {
            ValidationMessageKey = "";
            if (Rows.Any(row => String.IsNullOrWhiteSpace(row.Name))) { ValidationMessageKey = "EventClassDialog_ErrorName"; return false; }
            if (Rows.GroupBy(row => row.Name.Trim(), StringComparer.CurrentCultureIgnoreCase).Any(group => group.Count() > 1)) { ValidationMessageKey = "EventClassDialog_ErrorDuplicate"; return false; }
            if (Rows.Any(row => row.Course == null)) { ValidationMessageKey = "EventClassDialog_ErrorCourse"; return false; }
            if (Rows.Any(row => row.ParticipantCount < 0 || row.StartInterval < 0 || row.BibNumberStart < 0 || row.BibNumberEnd < 0 || row.MapCount < 0 || row.ReserveCount < 0)) { ValidationMessageKey = "EventClassDialog_ErrorNegative"; return false; }
            if (Rows.Any(row => row.BibNumberStart > 0 && row.BibNumberEnd > 0 && row.BibNumberEnd < row.BibNumberStart)) { ValidationMessageKey = "EventClassDialog_ErrorBibRange"; return false; }
            OnPropertyChanged(nameof(HasValidationError));
            return true;
        }
    }

    public sealed class CourseChoice
    {
        public Id<Course> Id { get; set; }
        public string Name { get; set; } = "";
        public override string ToString() => Name;
    }

    public sealed class EventClassSuggestion
    {
        public string ClassName { get; }
        public int ImportedCount { get; }
        public int ExistingCount { get; }
        public bool IsConflict { get; }
        public bool IsSafe => !IsConflict;
        public string StatusKey => IsConflict ? "EventClassDialog_Conflict" : "EventClassDialog_Matched";
        public EventClassSuggestion(EventClassSupport.ClassParticipantSuggestion item) : this(item.ClassName, item.ImportedCount, item.ExistingCount > 0, item.IsConflict) { }
        public EventClassSuggestion(string className, int importedCount, bool matched, bool conflict) { ClassName = className; ImportedCount = importedCount; ExistingCount = matched ? 1 : 0; IsConflict = conflict; }
    }

    public partial class EventClassRow : ObservableObject
    {
        private readonly Id<EventClass> classId;
        public ObservableCollection<CourseChoice> Courses { get; }
        [ObservableProperty] private string name = "";
        [ObservableProperty] private CourseChoice? course;
        [ObservableProperty] private int participantCount;
        [ObservableProperty] private int startInterval;
        [ObservableProperty] private int bibNumberStart;
        [ObservableProperty] private int bibNumberEnd;
        [ObservableProperty] private int mapCount = 1;
        [ObservableProperty] private int reserveCount;
        public int RequiredMapCount => Math.Max(0, ParticipantCount) * Math.Max(0, MapCount) + Math.Max(0, ReserveCount);

        private readonly Action changed;
        public EventClassRow(ObservableCollection<CourseChoice> courses, Action? changed = null) { Courses = courses; this.changed = changed ?? (() => { }); }
        public EventClassRow(Controller.EventClassInfo value, ObservableCollection<CourseChoice> courses, Action? changed = null) : this(courses, changed)
        {
            classId = value.classId; Name = value.name; ParticipantCount = value.participantCount;
            StartInterval = value.startInterval; BibNumberStart = value.bibNumberStart; BibNumberEnd = value.bibNumberEnd;
            MapCount = value.mapCount; ReserveCount = value.reserveCount;
            Course = Courses.FirstOrDefault(item => item.Id == value.courseId);
        }
        partial void OnParticipantCountChanged(int value) { OnPropertyChanged(nameof(RequiredMapCount)); changed(); }
        partial void OnMapCountChanged(int value) { OnPropertyChanged(nameof(RequiredMapCount)); changed(); }
        partial void OnReserveCountChanged(int value) { OnPropertyChanged(nameof(RequiredMapCount)); changed(); }
        partial void OnNameChanged(string value) { changed(); }
        partial void OnCourseChanged(CourseChoice? value) { changed(); }
        public Controller.EventClassInfo ToInfo() => new Controller.EventClassInfo { classId = classId, name = Name, courseId = Course?.Id ?? Id<Course>.None,
            participantCount = ParticipantCount, startInterval = StartInterval, bibNumberStart = BibNumberStart, bibNumberEnd = BibNumberEnd,
            mapCount = MapCount, reserveCount = ReserveCount, requiredMapCount = RequiredMapCount };
    }
}
