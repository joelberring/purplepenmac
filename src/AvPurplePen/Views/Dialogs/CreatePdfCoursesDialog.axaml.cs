// CreatePdfCoursesDialog.axaml.cs
//
// Code-behind for the Create PDF Files dialog. Almost everything is data-bound
// to the CreatePdfCoursesDialogViewModel — see the ViewModel for the property
// layout. This file only handles:
//   1. Selection state on the CourseSelector (not bindable). Pulled from VM
//      on Opened, pushed back on OK.
//   2. The "Select folder..." button, which needs the parent window's
//      StorageProvider to show the platform folder picker.
//
// Migrated from WinForms PurplePen/CreatePdfCourses.cs.

using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PurplePen;
using PurplePen.ViewModels;

namespace AvPurplePen.Views
{
    /// <summary>
    /// Dialog for creating PDF files of one or more courses. The caller must
    /// set DataContext to a <see cref="CreatePdfCoursesDialogViewModel"/>
    /// (with EventDB, ShowMergeParts, EnableChangeCropping, and Settings
    /// populated) before showing.
    /// </summary>
    public partial class CreatePdfCoursesDialog : Window
    {
        public CreatePdfCoursesDialog()
        {
            InitializeComponent();
            Opened += OnOpened;
        }

        /// <summary>
        /// Push the ViewModel's initial selection state into the CourseSelector.
        /// Done in Opened (not the constructor) because DataContext is set by
        /// the caller after construction.
        /// </summary>
        private void OnOpened(object? sender, EventArgs e)
        {
            if (DataContext is not CreatePdfCoursesDialogViewModel vm)
                return;

            courseSelector.SelectedCourseDesignators = vm.SelectedCourseDesignators;
            courseSelector.VariationChoicesPerCourse = vm.VariationChoicesPerCourse;
        }

        /// <summary>
        /// Opens the platform folder picker and writes the selected folder
        /// back into the ViewModel's OutputDirectory.
        /// </summary>
        private async void SelectOtherDirectoryButton_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not CreatePdfCoursesDialogViewModel vm)
                return;

            IStorageProvider storage = StorageProvider;

            FolderPickerOpenOptions options = new FolderPickerOpenOptions {
                Title = UIText.CreatePdfCourses_folderBrowserDialog_Description,
                AllowMultiple = false,
            };

            if (!string.IsNullOrEmpty(vm.OutputDirectory)) {
                options.SuggestedStartLocation = await storage.TryGetFolderFromPathAsync(vm.OutputDirectory);
            }

            IReadOnlyList<IStorageFolder> folders = await storage.OpenFolderPickerAsync(options);
            if (folders.Count > 0) {
                vm.OutputDirectory = folders[0].Path.LocalPath;
            }
        }

        /// <summary>
        /// Imports a CSV start list exported from MeOS. The parser belongs in
        /// PurplePenCore so the same matching contract can be reused by future
        /// print workflows; this code only obtains the user-selected file.
        /// </summary>
        private async void ImportBacksideInfoButton_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not CreatePdfCoursesDialogViewModel vm)
                return;

            FilePickerOpenOptions options = new FilePickerOpenOptions {
                Title = UIText.ResourceManager.GetString("CreatePdfCourses_importBacksideInfoDialog_Text") ?? "Import MeOS start list CSV",
                AllowMultiple = false,
                FileTypeFilter = new[] {
                    new FilePickerFileType("CSV") { Patterns = new[] { "*.csv", "*.txt" } },
                },
            };
            IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(options);
            if (files.Count == 0)
                return;

            try {
                using (Stream stream = await files[0].OpenReadAsync())
                using (StreamReader reader = new StreamReader(stream)) {
                    vm.BacksideInfoRecords = BacksideInfoCsv.Import(reader);
                    vm.BacksideInfoImportError = String.Empty;
                }
            }
            catch (IOException) {
                vm.BacksideInfoImportError = UIText.ResourceManager.GetString("CreatePdfCourses_backsideImportError_Text") ?? "Could not read the selected CSV file.";
            }
            catch (FormatException) {
                vm.BacksideInfoImportError = UIText.ResourceManager.GetString("CreatePdfCourses_backsideImportError_Text") ?? "Could not read the selected CSV file.";
            }
        }

        /// <summary>Imports and stores a reusable print profile.</summary>
        private async void ImportPrintProfileButton_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not CreatePdfCoursesDialogViewModel vm)
                return;

            FilePickerOpenOptions options = new FilePickerOpenOptions {
                Title = UIText.ResourceManager.GetString("CreatePdfCourses_importPrintProfileDialog_Text") ?? "Import print profile",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("Print profile JSON") { Patterns = new[] { "*.json" } } },
            };
            IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(options);
            if (files.Count == 0)
                return;

            try {
                PrintProfile profile = PrintProfileCatalog.Import(files[0].Path.LocalPath);
                vm.RefreshPrintProfiles(profile.Id);
                vm.PrintProfileMessage = UIText.ResourceManager.GetString("CreatePdfCourses_printProfileImported_Text") ?? "Print profile imported.";
            }
            catch (IOException) {
                vm.PrintProfileMessage = UIText.ResourceManager.GetString("CreatePdfCourses_printProfileImportError_Text") ?? "Could not import the print profile.";
            }
            catch (ArgumentException) {
                vm.PrintProfileMessage = UIText.ResourceManager.GetString("CreatePdfCourses_printProfileImportError_Text") ?? "Could not import the print profile.";
            }
        }

        /// <summary>Exports the selected print profile as portable JSON.</summary>
        private async void ExportPrintProfileButton_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not CreatePdfCoursesDialogViewModel vm)
                return;

            PrintProfile? profile = PrintProfileCatalog.FindById(vm.PrintProfileId);
            if (profile == null) {
                vm.PrintProfileMessage = UIText.ResourceManager.GetString("CreatePdfCourses_selectPrintProfile_Text") ?? "Select a print profile first.";
                return;
            }

            FilePickerSaveOptions options = new FilePickerSaveOptions {
                Title = UIText.ResourceManager.GetString("CreatePdfCourses_exportPrintProfileDialog_Text") ?? "Export print profile",
                SuggestedFileName = profile.Id + ".json",
                FileTypeChoices = new[] { new FilePickerFileType("Print profile JSON") { Patterns = new[] { "*.json" } } },
            };
            IStorageFile? file = await StorageProvider.SaveFilePickerAsync(options);
            if (file == null)
                return;

            try {
                PrintProfileCatalog.Export(profile, file.Path.LocalPath);
                vm.PrintProfileMessage = UIText.ResourceManager.GetString("CreatePdfCourses_printProfileExported_Text") ?? "Print profile exported.";
            }
            catch (IOException) {
                vm.PrintProfileMessage = UIText.ResourceManager.GetString("CreatePdfCourses_printProfileExportError_Text") ?? "Could not export the print profile.";
            }
            catch (UnauthorizedAccessException) {
                vm.PrintProfileMessage = UIText.ResourceManager.GetString("CreatePdfCourses_printProfileExportError_Text") ?? "Could not export the print profile.";
            }
        }

        /// <summary>
        /// Pulls the CourseSelector's selection state back into the ViewModel
        /// and closes with OK.
        /// </summary>
        private void OkButton_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is CreatePdfCoursesDialogViewModel vm) {
                vm.SelectedCourseDesignators = courseSelector.SelectedCourseDesignators;
                vm.VariationChoicesPerCourse = courseSelector.VariationChoicesPerCourse;
            }
            Close(true);
        }

        /// <summary>Cancels and closes the dialog.</summary>
        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}
