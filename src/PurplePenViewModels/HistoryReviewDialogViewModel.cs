// HistoryReviewDialogViewModel.cs
// Read-only presentation of the current event revision and undo state.
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace PurplePen.ViewModels
{
    /// <summary>ViewModel for the read-only event history review dialog.</summary>
    public partial class HistoryReviewDialogViewModel : ViewModelBase
    {
        [ObservableProperty] private string eventRevision = "";
        [ObservableProperty] private bool isSaved;
        [ObservableProperty] private bool isCommandInProgress;
        [ObservableProperty] private bool canUndo;
        [ObservableProperty] private bool canRedo;
        [ObservableProperty] private string undoName = "";
        [ObservableProperty] private string redoName = "";
        [ObservableProperty] private int controlCount;
        [ObservableProperty] private int courseCount;
        [ObservableProperty] private int courseControlCount;
        [ObservableProperty] private int legCount;
        [ObservableProperty] private int specialCount;
        [ObservableProperty] private int totalCount;
        [ObservableProperty] private int trainingExerciseCount;
        [ObservableProperty, NotifyCanExecuteChangedFor(nameof(CreateSnapshotCommand)), NotifyCanExecuteChangedFor(nameof(RenameSnapshotCommand))]
        private string snapshotName = "";
        [ObservableProperty, NotifyCanExecuteChangedFor(nameof(AddCommentCommand))]
        private string snapshotComment = "";
        [ObservableProperty] private string reportText = "";
        [ObservableProperty, NotifyCanExecuteChangedFor(nameof(RenameSnapshotCommand)), NotifyCanExecuteChangedFor(nameof(AddCommentCommand)), NotifyCanExecuteChangedFor(nameof(CompareSnapshotsCommand)), NotifyCanExecuteChangedFor(nameof(GenerateJsonReportCommand))]
        private HistorySnapshotItem? selectedSnapshot;
        [ObservableProperty, NotifyCanExecuteChangedFor(nameof(CompareSnapshotsCommand)), NotifyCanExecuteChangedFor(nameof(GenerateJsonReportCommand))]
        private HistorySnapshotItem? compareSnapshot;
        public ObservableCollection<HistorySnapshotItem> Snapshots { get; } = new ObservableCollection<HistorySnapshotItem>();
        public HistorySnapshotStore? SnapshotStore { get; private set; }
        private EventDB? eventDB;

        /// <summary>Loads a snapshot without changing the event or undo manager.</summary>
        public void Load(HistoryReviewStatus status)
        {
            EventRevision = status.ChangeNumber.ToString();
            IsSaved = !status.IsDirty;
            IsCommandInProgress = status.IsCommandInProgress;
            CanUndo = status.CanUndo;
            CanRedo = status.CanRedo;
            UndoName = status.UndoName ?? "";
            RedoName = status.RedoName ?? "";
            EventObjectCounts counts = status.ObjectCounts;
            ControlCount = counts.ControlPointCount;
            CourseCount = counts.CourseCount;
            CourseControlCount = counts.CourseControlCount;
            LegCount = counts.LegCount;
            SpecialCount = counts.SpecialCount;
            TrainingExerciseCount = counts.TrainingExerciseCount;
            TotalCount = counts.TotalCount;
        }

        /// <summary>Loads status and an event-local snapshot store for the review surface.</summary>
        public void Load(HistoryReviewStatus status, HistorySnapshotStore store)
        {
            Load(status); SnapshotStore = store ?? throw new ArgumentNullException(nameof(store)); eventDB = null;
            Snapshots.Clear(); foreach (HistorySnapshot snapshot in store.Snapshots) Snapshots.Add(new HistorySnapshotItem(snapshot));
        }

        /// <summary>Loads the review and keeps the live event database for fresh snapshot payloads.</summary>
        public void Load(HistoryReviewStatus status, HistorySnapshotStore store, EventDB currentEventDB)
        {
            Load(status, store);
            eventDB = currentEventDB ?? throw new ArgumentNullException(nameof(currentEventDB));
        }

        private bool CanCreateSnapshot() => SnapshotStore != null && eventDB != null && !String.IsNullOrWhiteSpace(SnapshotName);

        [RelayCommand(CanExecute = nameof(CanCreateSnapshot))]
        private void CreateSnapshot()
        {
            if (SnapshotStore == null || eventDB == null || String.IsNullOrWhiteSpace(SnapshotName)) return;
            HistorySnapshot snapshot = SnapshotStore.Create(eventDB, SnapshotName, SnapshotComment);
            Snapshots.Add(new HistorySnapshotItem(snapshot)); SelectedSnapshot = Snapshots.Last(); SnapshotName = ""; SnapshotComment = "";
        }

        private bool CanRenameSnapshot() => SnapshotStore != null && SelectedSnapshot != null && !String.IsNullOrWhiteSpace(SnapshotName);

        [RelayCommand(CanExecute = nameof(CanRenameSnapshot))]
        private void RenameSnapshot()
        {
            if (SnapshotStore == null || SelectedSnapshot == null || String.IsNullOrWhiteSpace(SnapshotName)) return;
            SnapshotStore.Rename(SelectedSnapshot.SnapshotId, SnapshotName); SelectedSnapshot.Name = SnapshotName; SnapshotName = "";
        }

        private bool CanAddComment() => SnapshotStore != null && SelectedSnapshot != null && !String.IsNullOrWhiteSpace(SnapshotComment);

        [RelayCommand(CanExecute = nameof(CanAddComment))]
        private void AddComment()
        {
            if (SnapshotStore == null || SelectedSnapshot == null) return;
            SnapshotStore.SetComment(SelectedSnapshot.SnapshotId, SnapshotComment); SelectedSnapshot.Comment = SnapshotComment; SnapshotComment = "";
        }

        private bool CanCompareSnapshots() => SnapshotStore != null && SelectedSnapshot != null && CompareSnapshot != null && SelectedSnapshot.SnapshotId != CompareSnapshot.SnapshotId;

        [RelayCommand(CanExecute = nameof(CanCompareSnapshots))]
        private void CompareSnapshots()
        {
            if (SnapshotStore == null || SelectedSnapshot == null || CompareSnapshot == null) return;
            ReportText = SnapshotStore.CreateReport(SelectedSnapshot.SnapshotId, CompareSnapshot.SnapshotId);
        }

        [RelayCommand(CanExecute = nameof(CanCompareSnapshots))]
        private void GenerateJsonReport()
        {
            if (SnapshotStore == null || SelectedSnapshot == null || CompareSnapshot == null) return;
            ReportText = SnapshotStore.CreateReport(SelectedSnapshot.SnapshotId, CompareSnapshot.SnapshotId, true);
        }
    }

    /// <summary>Bindable snapshot row for the history review dialog.</summary>
    public sealed partial class HistorySnapshotItem : ObservableObject
    {
        public string Hash { get; }
        public string SnapshotId { get; }
        public DateTime CreatedUtc { get; }
        public string CreatedLocalText => CreatedUtc.ToLocalTime().ToString("g");
        [ObservableProperty] private string name;
        [ObservableProperty] private string comment;
        public HistorySnapshotItem(HistorySnapshot snapshot) { Hash = snapshot.Hash; SnapshotId = snapshot.SnapshotId; CreatedUtc = snapshot.CreatedUtc; name = snapshot.Name; comment = snapshot.Comment; }
    }
}
