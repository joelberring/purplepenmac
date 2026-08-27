// TestCultureFixture.cs
//
// Establishes a deterministic numeric culture for view-model tests. The
// production application deliberately uses the user's current culture when
// parsing and formatting values in dialogs; tests that assert serialized
// values must not depend on the culture of the machine that runs them.

using System.Globalization;
using NUnit.Framework;

namespace PurplePenViewModels.Tests
{
    /// <summary>
    /// Configures invariant culture for the complete view-model test assembly.
    /// </summary>
    [SetUpFixture]
    public sealed class TestCultureFixture
    {
        private CultureInfo? originalCulture;
        private CultureInfo? originalUICulture;
        private CultureInfo? originalDefaultCulture;
        private CultureInfo? originalDefaultUICulture;

        /// <summary>
        /// Saves the caller's culture and applies invariant formatting before tests run.
        /// </summary>
        [OneTimeSetUp]
        public void SetInvariantCulture()
        {
            originalCulture = CultureInfo.CurrentCulture;
            originalUICulture = CultureInfo.CurrentUICulture;
            originalDefaultCulture = CultureInfo.DefaultThreadCurrentCulture;
            originalDefaultUICulture = CultureInfo.DefaultThreadCurrentUICulture;

            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        }

        /// <summary>
        /// Restores the caller's culture after the test assembly has finished.
        /// </summary>
        [OneTimeTearDown]
        public void RestoreCulture()
        {
            CultureInfo.CurrentCulture = originalCulture ?? CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = originalUICulture ?? CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentCulture = originalDefaultCulture;
            CultureInfo.DefaultThreadCurrentUICulture = originalDefaultUICulture;
        }
    }
}
