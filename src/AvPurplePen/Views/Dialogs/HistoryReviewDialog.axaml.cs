using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AvPurplePen.Views
{
    /// <summary>Displays a read-only snapshot of event history state.</summary>
    public partial class HistoryReviewDialog : Window
    {
        public HistoryReviewDialog() { InitializeComponent(); }
        private void Close_Click(object? sender, RoutedEventArgs e) { Close(); }
    }
}
