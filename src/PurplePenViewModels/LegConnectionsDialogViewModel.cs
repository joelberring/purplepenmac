// LegConnectionsDialogViewModel.cs
// Displays every incoming and outgoing leg for a selected control.

using System.Collections.ObjectModel;

namespace PurplePen.ViewModels
{
    /// <summary>ViewModel for the control leg-connections overview.</summary>
    public partial class LegConnectionsDialogViewModel : ViewModelBase
    {
        /// <summary>Name of the selected control displayed by the dialog.</summary>
        public string ControlName { get; set; } = "";

        /// <summary>Legs which arrive at the selected control.</summary>
        public ObservableCollection<LegConnectionItem> Incoming { get; } = new ObservableCollection<LegConnectionItem>();

        /// <summary>Legs which depart from the selected control.</summary>
        public ObservableCollection<LegConnectionItem> Outgoing { get; } = new ObservableCollection<LegConnectionItem>();
    }

    /// <summary>A single incoming or outgoing course leg.</summary>
    public sealed class LegConnectionItem
    {
        /// <summary>Name of the course containing this leg.</summary>
        public string CourseName { get; set; } = "";

        /// <summary>The other endpoint of the leg.</summary>
        public string OtherControlName { get; set; } = "";

    }
}
