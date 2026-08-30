using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace PurplePen.ViewModels
{
    /// <summary>Coordinates non-destructive MeOS/IOF import, export validation and comparison.</summary>
    public partial class MeosIofCentralDialogViewModel : ViewModelBase
    {
        public EventDB? EventDB { get; set; }
        public Func<string, MeosOperationResult>? ExportCourseData { get; set; }
        public Func<string, MeosOperationResult>? CreateLiveloxPackage { get; set; }
        public Action<IReadOnlyDictionary<string, int>>? ApplyClassCounts { get; set; }
        [ObservableProperty] private string fileName = "";
        [ObservableProperty] private string result = "";
        [ObservableProperty] private MeosOperationState resultState;
        [ObservableProperty] private int resultCount;
        [ObservableProperty] private string resultPath = "";
        public ObservableCollection<string> Diagnostics { get; } = new ObservableCollection<string>();
        public bool HasDiagnostics => Diagnostics.Count > 0;
        public bool IsImported => ResultState == MeosOperationState.Imported;
        public bool IsApplied => ResultState == MeosOperationState.Applied;
        public bool IsNoEvent => ResultState == MeosOperationState.NoEvent;
        public bool IsCompared => ResultState == MeosOperationState.Compared;
        public bool IsNoDifferences => ResultState == MeosOperationState.NoDifferences;
        public bool IsExportValid => ResultState == MeosOperationState.ExportValid;
        public bool IsExportInvalid => ResultState == MeosOperationState.ExportInvalid;
        public bool IsLiveloxCreated => ResultState == MeosOperationState.LiveloxCreated;
        partial void OnResultStateChanged(MeosOperationState value)
        {
            OnPropertyChanged(nameof(IsImported)); OnPropertyChanged(nameof(IsApplied)); OnPropertyChanged(nameof(IsNoEvent));
            OnPropertyChanged(nameof(IsCompared)); OnPropertyChanged(nameof(IsNoDifferences)); OnPropertyChanged(nameof(IsExportValid));
            OnPropertyChanged(nameof(IsExportInvalid)); OnPropertyChanged(nameof(IsLiveloxCreated));
        }
        public ObservableCollection<string> ImportedClasses { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> DiffItems { get; } = new ObservableCollection<string>();
        public ObservableCollection<MeosClassSuggestion> ClassSuggestions { get; } = new ObservableCollection<MeosClassSuggestion>();
        public bool HasSafeSuggestions => ClassSuggestions.Any(item => item.IsSafe);
        private List<BacksideInfoRecord> importedRecords = new List<BacksideInfoRecord>();

        /// <summary>Imports an IOF StartList or MeOS-compatible CSV and reports classes/conflicts.</summary>
        public void ImportStartList()
        {
            ClearImportedStartList();
            Diagnostics.Clear();
            OnPropertyChanged(nameof(HasDiagnostics));
            try {
                List<BacksideInfoRecord> records;
                using (StreamReader reader = File.OpenText(FileName))
                    records = FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ? BacksideInfoCsv.Import(reader) : BacksideInfoIofXml.Import(reader);
                importedRecords = records;
                foreach (string name in records.Where(r => !String.IsNullOrWhiteSpace(r.ClassName)).Select(r => r.ClassName).Distinct(StringComparer.CurrentCultureIgnoreCase)) ImportedClasses.Add(name);
                if (EventDB != null) foreach (EventClassSupport.ClassParticipantSuggestion item in EventClassSupport.MatchStartList(EventDB, records)) ClassSuggestions.Add(new MeosClassSuggestion { ClassName = item.ClassName, ImportedCount = item.ImportedCount, IsConflict = item.IsConflict });
                OnPropertyChanged(nameof(HasSafeSuggestions));
                ResultCount = records.Count; ResultState = MeosOperationState.Imported; Result = "";
            }
            catch (Exception ex) {
                ClearImportedStartList();
                Diagnostics.Add(ex.Message);
                OnPropertyChanged(nameof(HasDiagnostics));
                ResultState = MeosOperationState.Error;
                Result = "";
            }
        }

        /// <summary>Removes data from a previous start-list import so it cannot be applied after a failed retry.</summary>
        private void ClearImportedStartList()
        {
            importedRecords.Clear();
            ImportedClasses.Clear();
            ClassSuggestions.Clear();
            ResultCount = 0;
            ResultPath = "";
            OnPropertyChanged(nameof(HasSafeSuggestions));
        }

        /// <summary>Applies only uniquely matched class participant counts through the caller's undo path.</summary>
        public void ApplySafeClassSuggestions()
        {
            if (EventDB == null || ApplyClassCounts == null || !IsImported || importedRecords.Count == 0) return;
            Dictionary<string, int> counts = EventClassSupport.SuggestParticipantCounts(importedRecords.Select(record => record.ClassName));
            Dictionary<string, int> safe = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);
            foreach (EventClassSupport.ClassParticipantSuggestion item in EventClassSupport.MatchStartList(EventDB, importedRecords)) if (!item.IsConflict) safe[item.ClassName] = item.ImportedCount;
            ApplyClassCounts(safe); ResultCount = safe.Count; ResultState = MeosOperationState.Applied; Result = "";
        }

        /// <summary>Exports CourseData 3.0 through the existing controller export path.</summary>
        public void ExportCourseDataFile() { RunOperation(ExportCourseData); }

        /// <summary>Parses CourseData and reports a non-mutating course/control diff.</summary>
        public void CompareCourseData()
        {
            ClearComparisonResults();
            if (EventDB == null) { ResultState = MeosOperationState.NoEvent; Result = ""; return; }
            try {
                IofCourseDataModel model = IofCourseDataExchange.Parse(File.ReadAllText(FileName));
                List<IofCourseDataDifference> differences = IofCourseDataExchange.Compare(model, EventDB!);
                DiffItems.Clear(); foreach (IofCourseDataDifference difference in differences) DiffItems.Add(difference.Kind + ": " + difference.Course + " — " + difference.Detail);
                ResultState = DiffItems.Count == 0 ? MeosOperationState.NoDifferences : MeosOperationState.Compared; Result = "";
            }
            catch (Exception ex) { ReportError(ex); }
        }

        /// <summary>Creates a real export package for the Livelox hand-off.</summary>
        public bool CreateLiveloxPackageFile()
        {
            RunOperation(CreateLiveloxPackage);
            return ResultState == MeosOperationState.LiveloxCreated;
        }

        /// <summary>Records an exception as a user-visible operation failure instead of allowing it to escape an UI callback.</summary>
        public void ReportError(Exception exception)
        {
            Diagnostics.Clear();
            if (exception != null && !String.IsNullOrWhiteSpace(exception.Message))
                Diagnostics.Add(exception.Message);
            OnPropertyChanged(nameof(HasDiagnostics));
            ResultCount = 0;
            ResultPath = "";
            ResultState = MeosOperationState.Error;
            Result = "";
        }

        /// <summary>Clears outcomes from a prior comparison before beginning another comparison.</summary>
        private void ClearComparisonResults()
        {
            Diagnostics.Clear();
            OnPropertyChanged(nameof(HasDiagnostics));
            DiffItems.Clear();
            ResultCount = 0;
            ResultPath = "";
            Result = "";
        }

        /// <summary>Runs a synchronous export callback while converting file and export failures into diagnostics.</summary>
        private void RunOperation(Func<string, MeosOperationResult>? operation)
        {
            if (operation == null)
                return;

            try {
                ApplyOperationResult(operation(FileName));
            }
            catch (Exception ex) {
                ReportError(ex);
            }
        }
        private void ApplyOperationResult(MeosOperationResult? operation)
        {
            if (operation == null) return;
            ResultState = operation.State; ResultCount = operation.Count; ResultPath = operation.Path ?? "";
            Diagnostics.Clear(); foreach (string diagnostic in operation.Diagnostics) Diagnostics.Add(diagnostic); Result = "";
            OnPropertyChanged(nameof(HasDiagnostics));
        }
    }

    public enum MeosOperationState { None, Imported, Applied, NoEvent, Compared, NoDifferences, ExportValid, ExportInvalid, LiveloxCreated, Error }
    public sealed class MeosOperationResult
    {
        public MeosOperationState State { get; set; }
        public int Count { get; set; }
        public string? Path { get; set; }
        public List<string> Diagnostics { get; } = new List<string>();
    }

    /// <summary>Structured imported-class match result for diagnostic UI.</summary>
    public sealed class MeosClassSuggestion
    {
        public string ClassName { get; set; } = "";
        public int ImportedCount { get; set; }
        public bool IsConflict { get; set; }
        public bool IsSafe => !IsConflict;
    }
}
