using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AvPurplePen.Views
{
    /// <summary>Displays a combined overview of courses and controls.</summary>
    public partial class CourseControlOverviewDialog : Window
    {
        /// <summary>Initializes the dialog.</summary>
        public CourseControlOverviewDialog() { InitializeComponent(); }

        private void CloseButton_Click(object? sender, RoutedEventArgs e) { Close(true); }
    }
}
