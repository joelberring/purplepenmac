using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PurplePen.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
namespace AvPurplePen.Views {
    public partial class MeosIofCentralDialog : Window {
        public MeosIofCentralDialog() { InitializeComponent(); }
        private void Close_Click(object? s, RoutedEventArgs e) => Close(false);
        private async void Import_Click(object? s, RoutedEventArgs e) {
            if (DataContext is not MeosIofCentralDialogViewModel vm) return;
            try {
                IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = false, FileTypeFilter = new[] { new FilePickerFileType("IOF/MeOS") { Patterns = new[] { "*.xml", "*.csv", "*.txt" } } } });
                if (files.Count == 0) return;
                vm.FileName = files[0].Path.LocalPath; vm.ImportStartList();
            }
            catch (Exception ex) { vm.ReportError(ex); }
        }
        private async void Export_Click(object? s, RoutedEventArgs e) {
            if (DataContext is not MeosIofCentralDialogViewModel vm) return;
            try {
                IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = "coursedata.xml", FileTypeChoices = new[] { new FilePickerFileType("IOF CourseData") { Patterns = new[] { "*.xml" } } } });
                if (file == null) return; vm.FileName = file.Path.LocalPath; vm.ExportCourseDataFile();
            }
            catch (Exception ex) { vm.ReportError(ex); }
        }
        private async void Compare_Click(object? s, RoutedEventArgs e) {
            if (DataContext is not MeosIofCentralDialogViewModel vm) return;
            try {
                IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = false, FileTypeFilter = new[] { new FilePickerFileType("IOF CourseData") { Patterns = new[] { "*.xml" } } } });
                if (files.Count == 0) return;
                vm.FileName = files[0].Path.LocalPath; vm.CompareCourseData();
            }
            catch (Exception ex) { vm.ReportError(ex); }
        }
        private void Apply_Click(object? s, RoutedEventArgs e) => (DataContext as MeosIofCentralDialogViewModel)?.ApplySafeClassSuggestions();
        private async void Livelox_Click(object? s, RoutedEventArgs e) {
            if (DataContext is not MeosIofCentralDialogViewModel vm) return;
            try {
                IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = "livelox-coursedata.xml", FileTypeChoices = new[] { new FilePickerFileType("Livelox CourseData") { Patterns = new[] { "*.xml" } } } });
                if (file == null) return;
                vm.FileName = file.Path.LocalPath;
                if (vm.CreateLiveloxPackageFile())
                    await new WebsiteLauncherService().ShowWebsite("https://www.livelox.com/Events");
            }
            catch (Exception ex) { vm.ReportError(ex); }
        }
    }
}
