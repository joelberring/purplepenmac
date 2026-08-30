using System;
using System.Globalization;
using Avalonia.Data.Converters;
using PurplePen;

namespace AvPurplePen
{
    /// <summary>Maps core control-point kinds to localized UI labels without putting UI text in a view model.</summary>
    public sealed class ControlPointKindLabelConverter : IValueConverter
    {
        public static readonly ControlPointKindLabelConverter Instance = new ControlPointKindLabelConverter();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string key = value is ControlPointKind kind
                ? "MobileControlInspectionDialog_Kind_" + kind
                : "MobileControlInspectionDialog_Kind_None";
            return UIText.ResourceManager.GetString(key, culture) ?? key;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>Maps import error types to localized messages without placing UI text in a view model.</summary>
    public sealed class MobileControlInspectionImportErrorLabelConverter : IValueConverter
    {
        public static readonly MobileControlInspectionImportErrorLabelConverter Instance = new MobileControlInspectionImportErrorLabelConverter();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string key = value is PurplePen.ViewModels.MobileControlInspectionImportError error
                ? "MobileControlInspectionDialog_ImportError_" + error
                : "MobileControlInspectionDialog_ImportError_None";
            return UIText.ResourceManager.GetString(key, culture) ?? String.Empty;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>Maps editable mobile inspection statuses to localized labels.</summary>
    public sealed class MobileControlInspectionStatusLabelConverter : IValueConverter
    {
        public static readonly MobileControlInspectionStatusLabelConverter Instance = new MobileControlInspectionStatusLabelConverter();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string key = value is MobileControlInspectionStatus status
                ? "MobileControlInspectionDialog_InspectionStatus_" + status
                : "MobileControlInspectionDialog_InspectionStatus_Uninspected";
            return UIText.ResourceManager.GetString(key, culture) ?? key;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>Maps observed-code validation outcomes to localized labels.</summary>
    public sealed class MobileControlCodeValidationStatusLabelConverter : IValueConverter
    {
        public static readonly MobileControlCodeValidationStatusLabelConverter Instance = new MobileControlCodeValidationStatusLabelConverter();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string key = value is MobileControlCodeValidationStatus status
                ? "MobileControlInspectionDialog_CodeValidation_" + status
                : "MobileControlInspectionDialog_CodeValidation_Missing";
            return UIText.ResourceManager.GetString(key, culture) ?? key;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>Formats MeOS operation counts with the localized result sentence.</summary>
    public sealed class MeosResultCountConverter : IValueConverter
    {
        public static readonly MeosResultCountConverter Instance = new MeosResultCountConverter();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string suffix = parameter as string ?? "Imported";
            string key = "MeosIofCentral_Result" + suffix;
            string format = UIText.ResourceManager.GetString(key, culture) ?? "{0}";
            return String.Format(culture, format, value ?? 0);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>Maps print-profile preflight states to short localized labels.</summary>
    public sealed class PrintProfilePreflightStatusConverter : IValueConverter
    {
        public static readonly PrintProfilePreflightStatusConverter Instance = new PrintProfilePreflightStatusConverter();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string status = value as string ?? "Unknown";
            string key = "PrintProfileEditor_PreflightStatus_" + status;
            return UIText.ResourceManager.GetString(key, culture) ?? status;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
