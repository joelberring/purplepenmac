using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AvPurplePen.Views
{
    /// <summary>Displays incoming and outgoing legs for one control.</summary>
    public partial class LegConnectionsDialog : Window
    {
        /// <summary>Initializes the dialog.</summary>
        public LegConnectionsDialog() { InitializeComponent(); }

        private void CloseButton_Click(object? sender, RoutedEventArgs e) { Close(true); }
    }
}
