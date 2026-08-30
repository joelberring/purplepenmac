using Avalonia.Controls;
using Avalonia.Interactivity;
using PurplePen.ViewModels;
namespace AvPurplePen.Views { public partial class PrintProfileEditorDialog : Window { public PrintProfileEditorDialog() { InitializeComponent(); } private void SaveButton_Click(object? sender, RoutedEventArgs e) { if (DataContext is PrintProfileEditorDialogViewModel viewModel && !viewModel.ValidateForSave()) return; Close(true); } private void CancelButton_Click(object? sender, RoutedEventArgs e) { Close(false); } } }
