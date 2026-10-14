// AboutDialogViewModel.cs
//
// ViewModel for the About dialog. Provides the formatted version string
// and bitness/distribution text as read-only properties for binding.
// Localized format strings (e.g. "Version {0}") belong in the View layer
// and are applied via XAML StringFormat bindings.

using System;
using System.Linq;
using System.Reflection;

namespace PurplePen.ViewModels
{
    /// <summary>
    /// ViewModel for the About dialog. Exposes the application version
    /// and bitness information as bindable properties.
    /// </summary>
    public class AboutDialogViewModel : ViewModelBase
    {
        /// <summary>
        /// The pretty-printed version number (e.g. "3.5.5" or "3.5.5 Beta 2").
        /// Does not include the "Version" prefix — that is applied by the View
        /// using a localized StringFormat.
        /// </summary>
        public string PrettyVersion { get; }

        /// <summary>
        /// Describes the process bitness and distribution type,
        /// e.g. "64-bit (Standalone Setup)" or "32-bit (Windows Store)".
        /// </summary>
        public string BitnessText { get; }

        /// <summary>
        /// Identifies the exact build: its date and git commit (e.g. "2026-10-14 · a900e6e"),
        /// so builds with the same version number can be told apart. Empty when unknown.
        /// </summary>
        public string BuildText { get; }

        /// <summary>Gets whether build information is available.</summary>
        public bool HasBuildText => BuildText.Length > 0;

        /// <summary>
        /// Creates a new AboutDialogViewModel with version and bitness info
        /// computed from the running environment.
        /// </summary>
        public AboutDialogViewModel()
        {
            PrettyVersion = Util.PrettyVersionString(VersionNumber.Current);

            string bitness = Environment.Is64BitProcess ? MiscText.SixtyfourBit : MiscText.ThirtytwoBit;
#if MSSTORE
            bitness += " (" + MiscText.WindowsStore + ")";
#else
            bitness += " (" + MiscText.StandaloneSetup + ")";
#endif
            BitnessText = bitness;
            BuildText = CreateBuildText(Assembly.GetEntryAssembly());
        }

        /// <summary>
        /// Reads the build date and commit stamped into the application assembly at build time.
        /// </summary>
        /// <param name="assembly">The application assembly, or null when not available.</param>
        /// <returns>"date · commit", whichever parts exist, or an empty string.</returns>
        public static string CreateBuildText(Assembly? assembly)
        {
            if (assembly == null)
                return String.Empty;

            string date = GetMetadata(assembly, "PurplePenBuildDate");
            string commit = GetMetadata(assembly, "PurplePenCommit");
            return String.Join(" · ", new string[] { date, commit }.Where(part => part.Length > 0));
        }

        /// <summary>Returns one AssemblyMetadata value, or an empty string.</summary>
        /// <param name="assembly">The assembly to read.</param>
        /// <param name="key">The metadata key.</param>
        private static string GetMetadata(Assembly assembly, string key)
        {
            AssemblyMetadataAttribute? attribute = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(item => item.Key == key);
            return attribute?.Value?.Trim() ?? String.Empty;
        }
    }
}
