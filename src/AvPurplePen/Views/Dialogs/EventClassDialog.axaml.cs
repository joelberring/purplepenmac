using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml;
using PurplePen;
using PurplePen.ViewModels;

namespace AvPurplePen.Views
{
    public partial class EventClassDialog : Window
    {
        public EventClassDialog() { InitializeComponent(); }
        private async void Ok_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not EventClassDialogViewModel vm) { Close(false); return; }
            if (vm.ValidateRows()) { Close(true); return; }
            if (!String.IsNullOrEmpty(vm.ValidationMessageKey))
                await ShowErrorAsync(UIText.ResourceManager.GetString(vm.ValidationMessageKey) ?? vm.ValidationMessageKey);
        }
        private void Cancel_Click(object? sender, RoutedEventArgs e) { Close(false); }
        private async void Import_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not EventClassDialogViewModel vm) return;
            vm.BeginStartListImport();
            IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = UIText.ResourceManager.GetString("EventClassDialog_ImportTitle") ?? "Import start list", AllowMultiple = false, FileTypeFilter = new[] { new FilePickerFileType(UIText.ResourceManager.GetString("EventClassDialog_FileType") ?? "CSV or IOF XML") { Patterns = new[] { "*.csv", "*.txt", "*.xml" } } } });
            if (files.Count == 0) return;
            try {
                using Stream stream = await files[0].OpenReadAsync();
                using StreamReader reader = new StreamReader(stream);
                string path = files[0].Path.LocalPath;
                List<BacksideInfoRecord> records = String.Equals(Path.GetExtension(path), ".xml", StringComparison.OrdinalIgnoreCase) ? BacksideInfoIofXml.Import(reader) : BacksideInfoCsv.Import(reader);
                vm.SetImportedStartList(records);
            }
            catch (Exception ex) when (ex is IOException || ex is FormatException || ex is XmlException || ex is InvalidDataException) {
                vm.ClearImportedStartList();
                vm.ValidationMessageKey = "EventClassDialog_ErrorImport";
            }
        }

        private static async System.Threading.Tasks.Task ShowErrorAsync(string message)
        {
            await Services.DialogService.ShowDialogAsync(new MessageBoxDialogViewModel { Message = message, Buttons = MessageBoxButtons.Ok, DefaultButton = MessageBoxButton.Ok, Icon = MessageBoxIcon.Error });
        }
    }
}
