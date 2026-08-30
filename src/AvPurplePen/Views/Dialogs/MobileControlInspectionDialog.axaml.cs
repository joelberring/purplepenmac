using Avalonia.Controls;
using Avalonia.Interactivity;
using PurplePen.ViewModels;

namespace AvPurplePen.Views
{
    /// <summary>Displays the active course's mobile control inspection overview.</summary>
    public partial class MobileControlInspectionDialog : Window
    {
        public MobileControlInspectionDialog()
        {
            InitializeComponent();
            LocalizedStringManager.Instance.LanguageChanged += OnLanguageChanged;
            Closed += OnClosed;
        }

        private void OnLanguageChanged(object? sender, System.EventArgs e)
        {
            if (DataContext is MobileControlInspectionDialogViewModel viewModel)
                viewModel.RefreshLocalizedPresentation();
        }

        private void OnClosed(object? sender, System.EventArgs e)
        {
            LocalizedStringManager.Instance.LanguageChanged -= OnLanguageChanged;
        }
        private void CloseButton_Click(object? sender, RoutedEventArgs e) => Close(true);
    }
}
