// PrintProfileMappingsDialog.axaml.cs
//
// Hosts the explicit source-map colour mapping dialog.

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AvPurplePen.Views
{
    /// <summary>Dialog for resolving ambiguous profile-to-map colour mappings.</summary>
    public partial class PrintProfileMappingsDialog : Window
    {
        /// <summary>Initializes the dialog controls.</summary>
        public PrintProfileMappingsDialog()
        {
            InitializeComponent();
        }

        /// <summary>Accepts the selected mappings.</summary>
        private void ApplyButton_Click(object? sender, RoutedEventArgs e)
        {
            Close(true);
        }

        /// <summary>Cancels mapping changes.</summary>
        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}
