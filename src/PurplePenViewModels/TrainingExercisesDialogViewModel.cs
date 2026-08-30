using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PurplePen;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.ComponentModel;
using System.Globalization;

namespace PurplePen.ViewModels
{
    /// <summary>Edits the training overlays for a course without mutating EventDB.</summary>
    public partial class TrainingExercisesDialogViewModel : ViewModelBase
    {
        public IReadOnlyList<TrainingExerciseKind> ExerciseKinds { get; } = Enum.GetValues<TrainingExerciseKind>();
        public ObservableCollection<TrainingExerciseEditorItem> Exercises { get; } = new ObservableCollection<TrainingExerciseEditorItem>();

        public bool HasValidationErrors => Exercises.Any(item => !item.IsValid);
        public bool IsSaveEnabled => !HasValidationErrors;

        /// <summary>Course designator to assign to newly created exercises.</summary>
        public CourseDesignator? CourseDesignator { get; private set; }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RemoveExerciseCommand), nameof(DuplicateExerciseCommand), nameof(MoveExerciseUpCommand), nameof(MoveExerciseDownCommand))]
        private TrainingExerciseEditorItem? selectedExercise;

        /// <summary>Loads editable copies of the supplied overlays.</summary>
        public void LoadExercises(IEnumerable<TrainingExercise> exercises)
        {
            CourseDesignator = exercises?.FirstOrDefault()?.courseDesignator?.Clone();
            foreach (TrainingExerciseEditorItem oldItem in Exercises)
                oldItem.PropertyChanged -= EditorItemPropertyChanged;
            Exercises.Clear();
            foreach (TrainingExercise exercise in exercises ?? Enumerable.Empty<TrainingExercise>())
                AddEditorItem(new TrainingExerciseEditorItem(exercise));
            RefreshDisplayOrdinals();
            SelectedExercise = Exercises.FirstOrDefault();
            RefreshValidationState();
        }

        /// <summary>Loads exercises and explicitly establishes the owning course for new items.</summary>
        public void LoadExercises(IEnumerable<TrainingExercise> exercises, CourseDesignator courseDesignator)
        {
            LoadExercises(exercises, courseDesignator, Array.Empty<TrainingMapSymbolInfo>());
        }

        /// <summary>Loads exercises together with the vector-map layers available for visual selection.</summary>
        public void LoadExercises(IEnumerable<TrainingExercise> exercises, CourseDesignator courseDesignator, IEnumerable<TrainingMapSymbolInfo> mapSymbols)
        {
            CourseDesignator = courseDesignator?.Clone() ?? throw new ArgumentNullException(nameof(courseDesignator));
            TrainingMapSymbolInfo[] symbolInventory = (mapSymbols ?? Enumerable.Empty<TrainingMapSymbolInfo>()).ToArray();
            foreach (TrainingExerciseEditorItem oldItem in Exercises)
                oldItem.PropertyChanged -= EditorItemPropertyChanged;
            Exercises.Clear();
            foreach (TrainingExercise exercise in exercises ?? Enumerable.Empty<TrainingExercise>())
                AddEditorItem(new TrainingExerciseEditorItem(exercise, symbolInventory));
            RefreshDisplayOrdinals();
            SelectedExercise = Exercises.FirstOrDefault();
            RefreshValidationState();
        }

        /// <summary>Returns independent model copies suitable for the caller to persist.</summary>
        public IReadOnlyList<TrainingExercise> CreateExercises()
        {
            return Exercises.Select(item => item.ToTrainingExercise()).ToList();
        }

        /// <summary>Adds an empty exercise which must be completed with geometry using a map tool.</summary>
        [RelayCommand]
        private void AddExercise()
        {
            if (CourseDesignator == null)
                return;
            TrainingExercise exercise = new TrainingExercise(TrainingExerciseKind.Corridor, CourseDesignator, Array.Empty<PointF>());
            AddEditorItem(new TrainingExerciseEditorItem(exercise));
            RefreshDisplayOrdinals();
            SelectedExercise = Exercises[^1];
            RefreshValidationState();
        }

        [RelayCommand(CanExecute = nameof(CanRemoveExercise))]
        private void RemoveExercise()
        {
            if (SelectedExercise == null)
                return;
            int index = Exercises.IndexOf(SelectedExercise);
            SelectedExercise.PropertyChanged -= EditorItemPropertyChanged;
            Exercises.RemoveAt(index);
            RefreshDisplayOrdinals();
            SelectedExercise = Exercises.Count == 0 ? null : Exercises[Math.Min(index, Exercises.Count - 1)];
            RefreshValidationState();
        }

        private bool CanRemoveExercise() => SelectedExercise != null;

        /// <summary>Duplicates the selected exercise while keeping its geometry detached from the source.</summary>
        [RelayCommand(CanExecute = nameof(CanEditSelectedExercise))]
        private void DuplicateExercise()
        {
            if (SelectedExercise == null)
                return;
            int index = Exercises.IndexOf(SelectedExercise);
            TrainingExerciseEditorItem copy = new TrainingExerciseEditorItem(SelectedExercise.ToTrainingExercise());
            AddEditorItem(copy);
            Exercises.Move(Exercises.Count - 1, index + 1);
            RefreshDisplayOrdinals();
            SelectedExercise = copy;
            RefreshValidationState();
        }

        /// <summary>Moves the selected exercise one position toward the start of the list.</summary>
        [RelayCommand(CanExecute = nameof(CanMoveExerciseUp))]
        private void MoveExerciseUp()
        {
            if (SelectedExercise == null)
                return;
            int index = Exercises.IndexOf(SelectedExercise);
            Exercises.Move(index, index - 1);
            RefreshDisplayOrdinals();
            RefreshValidationState();
        }

        /// <summary>Moves the selected exercise one position toward the end of the list.</summary>
        [RelayCommand(CanExecute = nameof(CanMoveExerciseDown))]
        private void MoveExerciseDown()
        {
            if (SelectedExercise == null)
                return;
            int index = Exercises.IndexOf(SelectedExercise);
            Exercises.Move(index, index + 1);
            RefreshDisplayOrdinals();
            RefreshValidationState();
        }

        private bool CanEditSelectedExercise() => SelectedExercise != null;
        private bool CanMoveExerciseUp() => SelectedExercise != null && Exercises.IndexOf(SelectedExercise) > 0;
        private bool CanMoveExerciseDown() => SelectedExercise != null && Exercises.IndexOf(SelectedExercise) >= 0 && Exercises.IndexOf(SelectedExercise) < Exercises.Count - 1;

        private void AddEditorItem(TrainingExerciseEditorItem item)
        {
            Exercises.Add(item);
            item.PropertyChanged += EditorItemPropertyChanged;
        }

        /// <summary>Numbers exercises within each type so otherwise identical list captions remain distinguishable.</summary>
        private void RefreshDisplayOrdinals()
        {
            Dictionary<TrainingExerciseKind, int> counts = new Dictionary<TrainingExerciseKind, int>();
            foreach (TrainingExerciseEditorItem item in Exercises) {
                counts.TryGetValue(item.Kind, out int count);
                item.DisplayOrdinal = count + 1;
                counts[item.Kind] = count + 1;
            }
        }

        private void EditorItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            RefreshValidationState();
        }

        private void RefreshValidationState()
        {
            OnPropertyChanged(nameof(HasValidationErrors));
            OnPropertyChanged(nameof(IsSaveEnabled));
            RemoveExerciseCommand.NotifyCanExecuteChanged();
            DuplicateExerciseCommand.NotifyCanExecuteChanged();
            MoveExerciseUpCommand.NotifyCanExecuteChanged();
            MoveExerciseDownCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>Bindable, editable projection of one training overlay.</summary>
    public partial class TrainingExerciseEditorItem : ObservableObject
    {
        private readonly TrainingExercise source;

        private readonly TrainingExerciseKind kind;
        public TrainingExerciseKind Kind => kind;
        [ObservableProperty] private int displayOrdinal = 1;
        [ObservableProperty] private decimal width;
        [ObservableProperty] private decimal whiteMargin;
        [ObservableProperty] private decimal maskOpacityPercent;
        [ObservableProperty] private string allowedSymbolIdsText = "";
        [ObservableProperty] private string instruction = "";
        [ObservableProperty] private bool showToRunner;
        [ObservableProperty] private bool showToCoach;
        [ObservableProperty] private bool showAnswer;
        public ObservableCollection<TrainingMapSymbolChoice> SymbolChoices { get; } = new ObservableCollection<TrainingMapSymbolChoice>();

        public string CourseName => source.courseDesignator == null ? "" : source.courseDesignator.ToString();
        public bool IsCorridor => Kind == TrainingExerciseKind.Corridor;
        public bool IsAttackPoint => Kind == TrainingExerciseKind.AttackPoint;
        public bool IsLine => Kind == TrainingExerciseKind.Line;
        public bool IsContourOnly => Kind == TrainingExerciseKind.ContourOnly;
        public bool IsNotContourOnly => !IsContourOnly;
        public bool IsWidthVisible => IsCorridor || IsAttackPoint;
        public bool IsMaskOpacityVisible => IsCorridor || IsContourOnly;
        public bool HasSymbolChoices => SymbolChoices.Count > 0;
        public bool HasNoSymbolChoices => !HasSymbolChoices;
        public bool IsValid {
            get {
                PointF[] locations = source.locations;
                if (locations == null)
                    return false;
                if ((Kind == TrainingExerciseKind.Corridor || Kind == TrainingExerciseKind.Line) && locations.Length < 2)
                    return false;
                if (Kind == TrainingExerciseKind.AttackPoint && locations.Length != 1)
                    return false;
                if (Kind == TrainingExerciseKind.ContourOnly && locations.Length < 3)
                    return false;
                if ((Kind == TrainingExerciseKind.Corridor || Kind == TrainingExerciseKind.AttackPoint) && Width <= 0)
                    return false;
                if (Kind == TrainingExerciseKind.Corridor && WhiteMargin < 0)
                    return false;
                if (MaskOpacityPercent < 0 || MaskOpacityPercent > 100)
                    return false;
                if (IsContourOnly && HasSymbolChoices && !SymbolChoices.Any(choice => choice.IsSelected))
                    return false;
                if (!ShowToRunner && !ShowToCoach && !ShowAnswer)
                    return false;
                return AllowedSymbolIdsText.Split(new[] { ';', ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).All(id => !string.IsNullOrWhiteSpace(id));
            }
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.PropertyName != nameof(IsValid)) {
                OnPropertyChanged(nameof(IsValid));
            }
        }

        /// <summary>Creates an editable projection with copied scalar values and source geometry.</summary>
        public TrainingExerciseEditorItem(TrainingExercise exercise)
            : this(exercise, Array.Empty<TrainingMapSymbolInfo>())
        {
        }

        /// <summary>Creates an editable projection and visual choices for the current vector map.</summary>
        public TrainingExerciseEditorItem(TrainingExercise exercise, IEnumerable<TrainingMapSymbolInfo> mapSymbols)
        {
            source = exercise ?? throw new ArgumentNullException(nameof(exercise));
            kind = exercise.kind;
            Width = (decimal)exercise.width;
            WhiteMargin = (decimal)exercise.whiteMargin;
            MaskOpacityPercent = (decimal)exercise.maskOpacity * 100;
            AllowedSymbolIdsText = string.Join("; ", exercise.allowedSymbolIds ?? Array.Empty<string>());
            Instruction = exercise.instruction ?? "";
            ShowToRunner = (exercise.visibility & TrainingExerciseVisibility.Runner) != 0;
            ShowToCoach = (exercise.visibility & TrainingExerciseVisibility.Coach) != 0;
            ShowAnswer = (exercise.visibility & TrainingExerciseVisibility.Answer) != 0;

            if (Kind == TrainingExerciseKind.ContourOnly)
                InitializeSymbolChoices(mapSymbols);
        }

        /// <summary>Builds checked symbol rows, using standard contours for legacy empty selections.</summary>
        private void InitializeSymbolChoices(IEnumerable<TrainingMapSymbolInfo> mapSymbols)
        {
            HashSet<string> selectedIds = new HashSet<string>(source.allowedSymbolIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            bool useStandardContours = selectedIds.Count == 0;
            HashSet<string> availableIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (TrainingMapSymbolInfo symbol in mapSymbols ?? Enumerable.Empty<TrainingMapSymbolInfo>()) {
                availableIds.Add(symbol.SymbolId);
                bool selected = useStandardContours ? IsStandardContourId(symbol.SymbolId) : selectedIds.Contains(symbol.SymbolId);
                AddSymbolChoice(new TrainingMapSymbolChoice(symbol.SymbolId, symbol.Name, ToPreviewColor(symbol.Color), selected));
            }
            foreach (string missingId in selectedIds.Where(id => !availableIds.Contains(id)))
                AddSymbolChoice(new TrainingMapSymbolChoice(missingId, "", "#808080", true));
        }

        /// <summary>Adds one visual layer row and observes its checkbox.</summary>
        private void AddSymbolChoice(TrainingMapSymbolChoice choice)
        {
            SymbolChoices.Add(choice);
            choice.PropertyChanged += SymbolChoicePropertyChanged;
        }

        /// <summary>Updates validation and persisted IDs when a visual layer checkbox changes.</summary>
        private void SymbolChoicePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(TrainingMapSymbolChoice.IsSelected))
                return;
            AllowedSymbolIdsText = String.Join("; ", SymbolChoices.Where(choice => choice.IsSelected).Select(choice => choice.SymbolId));
            OnPropertyChanged(nameof(IsValid));
        }

        /// <summary>Recognizes the standard contour families used by an empty legacy selection.</summary>
        private static bool IsStandardContourId(string symbolId)
        {
            return symbolId == "101" || symbolId.StartsWith("101.", StringComparison.Ordinal) ||
                   symbolId == "102" || symbolId.StartsWith("102.", StringComparison.Ordinal) ||
                   symbolId == "103" || symbolId.StartsWith("103.", StringComparison.Ordinal);
        }

        /// <summary>Converts a map CMYK colour to an sRGB approximation for a small UI swatch.</summary>
        private static string ToPreviewColor(PurplePen.Graphics2D.CmykColor color)
        {
            int red = (int)Math.Round(255F * color.Red, MidpointRounding.AwayFromZero);
            int green = (int)Math.Round(255F * color.Green, MidpointRounding.AwayFromZero);
            int blue = (int)Math.Round(255F * color.Blue, MidpointRounding.AwayFromZero);
            return String.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", red, green, blue);
        }

        /// <summary>Creates a detached training overlay with the edited values.</summary>
        public TrainingExercise ToTrainingExercise()
        {
            TrainingExercise copy = (TrainingExercise)source.Clone();
            copy.kind = Kind;
            copy.width = (float)Width;
            copy.whiteMargin = (float)WhiteMargin;
            copy.maskOpacity = (float)(MaskOpacityPercent / 100);
            copy.allowedSymbolIds = HasSymbolChoices
                ? SymbolChoices.Where(choice => choice.IsSelected).Select(choice => choice.SymbolId).Distinct(StringComparer.Ordinal).ToArray()
                : AllowedSymbolIdsText.Split(new[] { ';', ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).ToArray();
            copy.instruction = Instruction;
            copy.visibility = (ShowToRunner ? TrainingExerciseVisibility.Runner : 0)
                | (ShowToCoach ? TrainingExerciseVisibility.Coach : 0)
                | (ShowAnswer ? TrainingExerciseVisibility.Answer : 0);
            return copy;
        }
    }

    /// <summary>One visually selectable map-symbol layer in a contour-only exercise.</summary>
    public partial class TrainingMapSymbolChoice : ObservableObject
    {
        public TrainingMapSymbolChoice(string symbolId, string name, string previewColor, bool isSelected)
        {
            SymbolId = symbolId;
            Name = name;
            PreviewColor = previewColor;
            this.isSelected = isSelected;
        }

        public string SymbolId { get; private set; }
        public string Name { get; private set; }
        public string PreviewColor { get; private set; }
        [ObservableProperty] private bool isSelected;
    }
}
