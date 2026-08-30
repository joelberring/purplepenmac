using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AvPurplePen.Views
{
    /// <summary>Dialog shell for editing training overlay metadata.</summary>
    public partial class TrainingExercisesDialog : Window
    {
        public TrainingExercisesDialog() { InitializeComponent(); }
        private void SaveButton_Click(object? sender, RoutedEventArgs e) => Close(true);
        private void CancelButton_Click(object? sender, RoutedEventArgs e) => Close(false);
    }
}
