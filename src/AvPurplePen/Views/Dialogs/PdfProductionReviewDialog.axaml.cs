// PdfProductionReviewDialog.axaml.cs
// Code-behind for the final course-PDF production review dialog.

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AvPurplePen.Views
{
    /// <summary>Shows the production summary and lets the user return to settings or create the PDF.</summary>
    public partial class PdfProductionReviewDialog : Window
    {
        /// <summary>Initializes the view.</summary>
        public PdfProductionReviewDialog()
        {
            InitializeComponent();
        }

        /// <summary>Returns to the PDF settings dialog.</summary>
        private void BackButton_Click(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }

        /// <summary>Accepts the reviewed production plan.</summary>
        private void CreateButton_Click(object? sender, RoutedEventArgs e)
        {
            Close(true);
        }
    }
}
