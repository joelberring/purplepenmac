// CourseControlOverviewDialogViewModel.cs
// Presents concise course and control information for an event.

using System.Collections.ObjectModel;

namespace PurplePen.ViewModels
{
    /// <summary>ViewModel for the combined course and control overview.</summary>
    public partial class CourseControlOverviewDialogViewModel : ViewModelBase
    {
        /// <summary>Courses in the event.</summary>
        public ObservableCollection<CourseOverviewItem> Courses { get; } = new ObservableCollection<CourseOverviewItem>();

        /// <summary>Controls in the event.</summary>
        public ObservableCollection<ControlOverviewItem> Controls { get; } = new ObservableCollection<ControlOverviewItem>();
    }

    /// <summary>Summary information for one course.</summary>
    public sealed class CourseOverviewItem
    {
        /// <summary>Name of the course.</summary>
        public string Name { get; set; } = "";

        /// <summary>Number of ordinary controls on the course.</summary>
        public int ControlCount { get; set; }

        /// <summary>Formatted course length.</summary>
        public string Length { get; set; } = "";

        /// <summary>Formatted climb, if present.</summary>
        public string Climb { get; set; } = "";
    }

    /// <summary>Summary information for one control point.</summary>
    public sealed class ControlOverviewItem
    {
        /// <summary>Displayed control name or code.</summary>
        public string Name { get; set; } = "";

        /// <summary>Courses which visit the control.</summary>
        public string Courses { get; set; } = "";

        /// <summary>Number of incoming legs.</summary>
        public int Incoming { get; set; }

        /// <summary>Number of outgoing legs.</summary>
        public int Outgoing { get; set; }
    }
}
