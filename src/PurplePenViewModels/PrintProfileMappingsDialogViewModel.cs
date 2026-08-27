// PrintProfileMappingsDialogViewModel.cs
//
// Lets a user resolve source-map colours that cannot be identified safely by
// a print profile's name, OCAD identifier, or CMYK values alone.

using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Globalization;

namespace PurplePen.ViewModels
{
    /// <summary>Represents one profile rule that needs an explicit source-map colour selection.</summary>
    public partial class PrintProfileMappingItemViewModel : ViewModelBase
    {
        /// <summary>Gets or sets the stable profile rule identifier.</summary>
        public string RuleId { get; set; } = String.Empty;

        /// <summary>Gets or sets the user-facing profile rule name.</summary>
        public string RuleName { get; set; } = String.Empty;

        /// <summary>Gets or sets the possible source-map colours for this rule.</summary>
        public List<SourceMapColor> Candidates { get; set; } = new List<SourceMapColor>();

        /// <summary>Gets or sets the profile CMYK values that the selected source colour will be checked against.</summary>
        public string TargetCmyk { get; set; } = String.Empty;

        /// <summary>Gets or sets whether the profile intends this colour to overprint.</summary>
        public bool TargetUsesOverprint { get; set; }

        /// <summary>Gets whether the profile intends this colour to knock out underlying colour.</summary>
        public bool TargetUsesKnockout { get { return !TargetUsesOverprint; } }

        /// <summary>Gets or sets an sRGB approximation used only for the on-screen target-colour swatch.</summary>
        public string TargetPreviewColor { get; set; } = "#FFFFFF";

        /// <summary>Gets or sets the source-map colour selected by the user.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SelectedColorDetails))]
        [NotifyPropertyChangedFor(nameof(SelectedPreviewColor))]
        private SourceMapColor? selectedColor;

        /// <summary>Gets the selected source colour's technical details for review.</summary>
        public string SelectedColorDetails => SelectedColor?.DisplayName ?? String.Empty;

        /// <summary>Gets an sRGB approximation used only for the selected source-colour swatch.</summary>
        public string SelectedPreviewColor => SelectedColor == null ? "#FFFFFF" : ToPreviewColor(SelectedColor.Cmyk);

        /// <summary>Creates an sRGB approximation for a CMYK colour swatch.</summary>
        /// <param name="cmyk">CMYK percentages to approximate.</param>
        /// <returns>A hexadecimal colour string suitable for an Avalonia brush binding.</returns>
        public static string ToPreviewColor(PrintProfileCmyk cmyk)
        {
            float cyan = ClampPercentage(cmyk.Cyan) / 100F;
            float magenta = ClampPercentage(cmyk.Magenta) / 100F;
            float yellow = ClampPercentage(cmyk.Yellow) / 100F;
            float black = ClampPercentage(cmyk.Black) / 100F;
            int red = (int)Math.Round(255F * (1F - cyan) * (1F - black), MidpointRounding.AwayFromZero);
            int green = (int)Math.Round(255F * (1F - magenta) * (1F - black), MidpointRounding.AwayFromZero);
            int blue = (int)Math.Round(255F * (1F - yellow) * (1F - black), MidpointRounding.AwayFromZero);
            return String.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", red, green, blue);
        }

        /// <summary>Keeps a CMYK percentage within the valid display range.</summary>
        private static float ClampPercentage(float value)
        {
            return Math.Max(0F, Math.Min(100F, value));
        }
    }

    /// <summary>ViewModel for choosing explicit mappings for ambiguous print-profile colours.</summary>
    public class PrintProfileMappingsDialogViewModel : ViewModelBase
    {
        /// <summary>Gets or sets the profile display name shown by the dialog.</summary>
        public string ProfileName { get; set; } = String.Empty;

        /// <summary>Gets or sets the ambiguous profile rules that need a source-map colour selection.</summary>
        public List<PrintProfileMappingItemViewModel> MappingItems { get; set; } = new List<PrintProfileMappingItemViewModel>();

        /// <summary>Gets whether every ambiguous profile rule has a selected map colour.</summary>
        public bool CanApplyMappings {
            get { return MappingItems.Count > 0 && MappingItems.All(item => item.SelectedColor != null); }
        }

        /// <summary>Creates persistent mapping records from the user's selected source-map colours.</summary>
        /// <returns>Mappings that safely identify the current map's chosen colours.</returns>
        public List<PrintProfileColorMapping> CreateMappings()
        {
            List<PrintProfileColorMapping> mappings = new List<PrintProfileColorMapping>();
            foreach (PrintProfileMappingItemViewModel item in MappingItems) {
                SourceMapColor selectedColor = item.SelectedColor!;
                if (selectedColor == null)
                    continue;

                mappings.Add(new PrintProfileColorMapping {
                    RuleId = item.RuleId,
                    MapColorName = selectedColor.Name,
                    MapColorOcadId = selectedColor.OcadId,
                    MapColorDrawOrder = selectedColor.DrawOrder,
                });
            }

            return mappings;
        }

        /// <summary>Refreshes the dialog's Apply button after a child selection changes.</summary>
        public void ListenToMappingItems()
        {
            foreach (PrintProfileMappingItemViewModel item in MappingItems) {
                item.PropertyChanged += MappingItemPropertyChanged;
            }
        }

        /// <summary>Raises a notification when a candidate selection changes.</summary>
        private void MappingItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PrintProfileMappingItemViewModel.SelectedColor))
                OnPropertyChanged(nameof(CanApplyMappings));
        }
    }
}
