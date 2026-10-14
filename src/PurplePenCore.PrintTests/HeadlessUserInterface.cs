// HeadlessUserInterface.cs
//
// Minimal IUserInterface for driving Controller in automated tests without any UI.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;

namespace PurplePen.PrintTests
{
    /// <summary>An IUserInterface that records messages and answers every question with its default.</summary>
    internal sealed class HeadlessUserInterface : IUserInterface
    {
        /// <summary>Gets every message the controller tried to show.</summary>
        public List<string> Messages { get; } = new List<string>();

        /// <summary>Gets all recorded messages as one string, for assertion output.</summary>
        public string AllMessages => String.Join(Environment.NewLine, Messages);

        /// <summary>Stores nothing; the controller is used directly by the tests.</summary>
        public void Initialize(Controller controller, SymbolDB symbolDB) { }

        /// <summary>Ignores idle requests.</summary>
        public void QueueIdleEvent() { }

        /// <summary>Runs delayed actions (such as error messages) immediately.</summary>
        public void PostDelayedAction(Action action) { action(); }

        /// <summary>There is no pointer in a headless run.</summary>
        public bool GetCurrentLocation(out PointF location, out float pixelSize)
        {
            location = PointF.Empty;
            pixelSize = 0;
            return false;
        }

        /// <summary>Records an error message.</summary>
        public Task ErrorMessage(string message) { Messages.Add("Error: " + message); return Task.CompletedTask; }

        /// <summary>Records a warning message.</summary>
        public Task WarningMessage(string message) { Messages.Add("Warning: " + message); return Task.CompletedTask; }

        /// <summary>Records an information message.</summary>
        public Task InfoMessage(string message) { Messages.Add("Info: " + message); return Task.CompletedTask; }

        /// <summary>Answers with the default button.</summary>
        public Task<bool> OKCancelMessage(string message, bool okDefault) { Messages.Add("OKCancel: " + message); return Task.FromResult(okDefault); }

        /// <summary>Answers with the default button.</summary>
        public Task<bool> YesNoQuestion(string message, bool yesDefault) { Messages.Add("YesNo: " + message); return Task.FromResult(yesDefault); }

        /// <summary>Answers with the default button.</summary>
        public Task<YesNoCancel> YesNoCancelQuestion(string message, bool yesDefault)
        {
            Messages.Add("YesNoCancel: " + message);
            return Task.FromResult(yesDefault ? YesNoCancel.Yes : YesNoCancel.No);
        }

        /// <summary>Never moves shared controls.</summary>
        public Task<YesNoCancel> MovingSharedControl(string controlCode, string otherCourses) { return Task.FromResult(YesNoCancel.Cancel); }

        /// <summary>Cannot locate missing map files.</summary>
        public Task<bool> FindMissingMapFile(string missingMapFile) { Messages.Add("Missing map: " + missingMapFile); return Task.FromResult(false); }

        /// <summary>No topology view in a headless run.</summary>
        public void ShowTopologyView() { }

        /// <summary>No progress dialog in a headless run.</summary>
        public void ShowProgressDialog(bool knownDuration, Action onCancelPressed = null) { }

        /// <summary>Never cancels.</summary>
        public bool UpdateProgressDialog(string info, double fractionDone) { return false; }

        /// <summary>No progress dialog in a headless run.</summary>
        public void EndProgressDialog() { }
    }
}
