using Avalonia.Controls;
using Avalonia.Interactivity;
using PurplePen.ViewModels;

namespace AvPurplePen.Views
{
    /// <summary>Displays a combined overview of courses and controls.</summary>
    public partial class CourseControlOverviewDialog : Window
    {
        /// <summary>Initializes the dialog.</summary>
        public CourseControlOverviewDialog() { InitializeComponent(); }

        private void CloseButton_Click(object? sender, RoutedEventArgs e) { Close(true); }

        private void ShowSelectedCourseButton_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is CourseControlOverviewDialogViewModel viewModel &&
                viewModel.NavigateToSelectedCourseCommand.CanExecute(null)) {
                viewModel.NavigateToSelectedCourseCommand.Execute(null);
                Close(true);
            }
        }

        private void ShowSelectedControlButton_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is CourseControlOverviewDialogViewModel viewModel &&
                viewModel.NavigateToSelectedControlCommand.CanExecute(null)) {
                viewModel.NavigateToSelectedControlCommand.Execute(null);
                Close(true);
            }
        }

        private void SaveEditsButton_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is CourseControlOverviewDialogViewModel viewModel) {
                viewModel.SaveEditsCommand.Execute(null);
                if (viewModel.SaveEditsRequested)
                    Close(true);
            }
        }

        private void CompareCoursesButton_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is CourseControlOverviewDialogViewModel viewModel &&
                viewModel.CompareCoursesCommand.CanExecute(null)) {
                viewModel.CompareCoursesCommand.Execute(null);
                Close(true);
            }
        }
    }
}
