using Avalonia.Controls;

namespace AvPurplePen.Views
{
    /// <summary>Code-behind for the route-choice analysis dialog.</summary>
    public partial class RouteChoiceAnalysisDialog : Window
    {
        public RouteChoiceAnalysisDialog()
        {
            InitializeComponent();
            DataContextChanged += (_, _) => {
                if (DataContext is PurplePen.ViewModels.RouteChoiceAnalysisDialogViewModel vm)
                    vm.CloseRequested += CloseFromViewModel;
            };
        }

        private void CloseFromViewModel() { Close(false); }

        private void Close_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) { Close(); }
    }
}
