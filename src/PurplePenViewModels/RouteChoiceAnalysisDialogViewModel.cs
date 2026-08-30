// RouteChoiceAnalysisDialogViewModel.cs
//
// Presents the route-choice measurements for each leg of the active course.
// Candidate route generation remains outside this view model; it only consumes
// the geometry already stored in the event database.

using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Generic;
using System;
using CommunityToolkit.Mvvm.Input;

namespace PurplePen.ViewModels
{
    /// <summary>View model for the route-choice analysis report.</summary>
    public partial class RouteChoiceAnalysisDialogViewModel : ViewModelBase
    {
        /// <summary>Course name shown in the report heading.</summary>
        [ObservableProperty]
        private string courseName = "";

        /// <summary>Measured legs in course order.</summary>
        public ObservableCollection<RouteChoiceLegRow> Legs { get; } = new ObservableCollection<RouteChoiceLegRow>();

        [ObservableProperty, NotifyCanExecuteChangedFor(nameof(StartCandidateCommand))]
        private RouteChoiceLegRow? selectedLeg;

        [ObservableProperty, NotifyCanExecuteChangedFor(nameof(DeleteCandidateCommand), nameof(SaveCandidateCommand))]
        private RouteChoiceCandidateRow? selectedCandidate;

        public Action<PurplePen.Id<PurplePen.CourseControl>>? BeginCandidateRequested { get; set; }
        public Action<PurplePen.Id<PurplePen.RouteChoiceCandidate>>? DeleteCandidateRequested { get; set; }
        public Action<PurplePen.Id<PurplePen.RouteChoiceCandidate>, string, string, string>? ChangeCandidateRequested { get; set; }
        public event Action? CloseRequested;

        [RelayCommand(CanExecute = nameof(CanStartCandidate))]
        private void StartCandidate() { if (SelectedLeg != null) BeginCandidateRequested?.Invoke(SelectedLeg.LegStartCourseControlId); CloseRequested?.Invoke(); }
        private bool CanStartCandidate() { return SelectedLeg != null && BeginCandidateRequested != null; }

        [RelayCommand(CanExecute = nameof(CanDeleteCandidate))]
        private void DeleteCandidate() { if (SelectedCandidate != null) DeleteCandidateRequested?.Invoke(SelectedCandidate.Id); }
        private bool CanDeleteCandidate() { return SelectedCandidate != null && DeleteCandidateRequested != null; }

        [RelayCommand(CanExecute = nameof(CanChangeCandidate))]
        private void SaveCandidate() { if (SelectedCandidate != null) ChangeCandidateRequested?.Invoke(SelectedCandidate.Id, SelectedCandidate.EditableName, SelectedCandidate.EditableSource, SelectedCandidate.EditableNotes); }
        private bool CanChangeCandidate() { return SelectedCandidate != null && ChangeCandidateRequested != null; }

        /// <summary>Loads the stored route geometry for the supplied course.</summary>
        public void Load(PurplePen.EventDB eventDB, PurplePen.CourseDesignator designator)
        {
            Legs.Clear();
            if (designator.IsAllControls || designator.IsVariation || PurplePen.QueryEvent.HasVariations(eventDB, designator.CourseId))
                throw new ArgumentException("Route-choice analysis requires a course without variations.", nameof(designator));
            PurplePen.Course course = eventDB.GetCourse(designator.CourseId);
            CourseName = course.name ?? "";

            System.Collections.Generic.List<PurplePen.Id<PurplePen.CourseControl>> ids =
                PurplePen.QueryEvent.EnumCourseControlIds(eventDB, designator).ToList();
            for (int index = 0; index + 1 < ids.Count; ++index) {
                PurplePen.CourseControl first = eventDB.GetCourseControl(ids[index]);
                PurplePen.CourseControl second = eventDB.GetCourseControl(ids[index + 1]);
                PurplePen.ControlPoint firstPoint = eventDB.GetControl(first.control);
                PurplePen.ControlPoint secondPoint = eventDB.GetControl(second.control);
                PurplePen.RouteChoiceStatistics statistics = PurplePen.RouteChoiceAnalysis.AnalyzeLeg(
                    eventDB, first.control, second.control, PurplePen.QueryEvent.FindLeg(eventDB, first.control, second.control));
                PurplePen.Id<PurplePen.CourseControl> startId = ids[index];
                List<PurplePen.RouteChoiceCandidate> candidates = eventDB.AllRouteChoiceCandidates
                    .Where(candidate => candidate.courseDesignator != null && candidate.courseDesignator.CourseId == designator.CourseId &&
                                        (candidate.courseDesignator.AllParts || candidate.courseDesignator.Part == designator.Part) &&
                                        candidate.legStartCourseControlId == startId).ToList();
                Legs.Add(new RouteChoiceLegRow(index + 1, Label(firstPoint, index), Label(secondPoint, index + 1), statistics, candidates, eventDB, startId));
            }
            SelectedLeg = Legs.FirstOrDefault();
        }

        private static string Label(PurplePen.ControlPoint point, int ordinal)
        {
            return string.IsNullOrEmpty(point.code) ? ordinal.ToString() : point.code;
        }
    }

    /// <summary>One row in the route-choice analysis table.</summary>
    public sealed class RouteChoiceLegRow
    {
        internal RouteChoiceLegRow(int number, string from, string to, PurplePen.RouteChoiceStatistics statistics,
                                   IEnumerable<PurplePen.RouteChoiceCandidate> candidates, PurplePen.EventDB eventDB,
                                   PurplePen.Id<PurplePen.CourseControl> legStartCourseControlId)
        {
            Number = number; From = from; To = to; LegStartCourseControlId = legStartCourseControlId;
            LengthMeters = statistics.LengthMeters;
            DirectionChanges = statistics.DirectionChanges;
            TotalDirectionChangeDegrees = statistics.TotalDirectionChangeDegrees;
            MaximumDirectionChangeDegrees = statistics.MaximumDirectionChangeDegrees;
            Candidates = new ObservableCollection<RouteChoiceCandidateRow>(candidates.Select(candidate => new RouteChoiceCandidateRow(candidate, statistics, eventDB)));
            CandidateSummary = string.Join("; ", Candidates.Select(candidate => string.Format("{0}: {1:+0.0;-0.0;0} m / {2:+0;-0;0} turns", candidate.Name, candidate.LengthDeltaMeters, candidate.DirectionChangeDelta)));
        }

        public int Number { get; }
        public PurplePen.Id<PurplePen.CourseControl> LegStartCourseControlId { get; }
        public string From { get; }
        public string To { get; }
        public double LengthMeters { get; }
        public int DirectionChanges { get; }
        public double TotalDirectionChangeDegrees { get; }
        public double MaximumDirectionChangeDegrees { get; }
        public ObservableCollection<RouteChoiceCandidateRow> Candidates { get; }
        public string CandidateSummary { get; }
    }

    /// <summary>Comparison row for one manually recorded alternative route.</summary>
    public sealed class RouteChoiceCandidateRow
    {
        internal RouteChoiceCandidateRow(PurplePen.RouteChoiceCandidate candidate, PurplePen.RouteChoiceStatistics baseline, PurplePen.EventDB eventDB)
        {
            Id = eventDB.AllRouteChoiceCandidatePairs.First(pair => Object.ReferenceEquals(pair.Value, candidate)).Key;
            Name = string.IsNullOrEmpty(candidate.name) ? "Unnamed" : candidate.name;
            Source = candidate.source ?? "";
            Notes = candidate.notes ?? "";
            EditableName = candidate.name ?? "";
            EditableSource = candidate.source ?? "";
            EditableNotes = candidate.notes ?? "";
            PurplePen.RouteChoiceStatistics statistics = candidate.GetCandidateStatistics(eventDB);
            LengthMeters = statistics.LengthMeters;
            LengthDeltaMeters = statistics.LengthMeters - baseline.LengthMeters;
            DirectionChanges = statistics.DirectionChanges;
            DirectionChangeDelta = statistics.DirectionChanges - baseline.DirectionChanges;
            TotalDirectionChangeDegrees = statistics.TotalDirectionChangeDegrees;
        }

        public string Name { get; }
        public string EditableName { get; set; }
        public string EditableSource { get; set; }
        public string EditableNotes { get; set; }
        public PurplePen.Id<PurplePen.RouteChoiceCandidate> Id { get; }
        public string Source { get; }
        public string Notes { get; }
        public double LengthMeters { get; }
        public double LengthDeltaMeters { get; }
        public int DirectionChanges { get; }
        public int DirectionChangeDelta { get; }
        public double TotalDirectionChangeDegrees { get; }
    }
}
