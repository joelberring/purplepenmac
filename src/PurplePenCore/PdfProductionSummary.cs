// PdfProductionSummary.cs
//
// Data-only summary of a planned PDF course export. The UI uses this as a
// production check before creating files, without rendering or writing a PDF.

using System;
using System.Collections.Generic;

namespace PurplePen
{
    /// <summary>
    /// Identifies an export setting that deserves an explicit review before a
    /// production PDF is created.
    /// </summary>
    public enum PdfProductionWarning
    {
        /// <summary>The base map is intentionally omitted from the output.</summary>
        CourseOnly,
        /// <summary>The export uses RGB rather than the normal CMYK production path.</summary>
        RgbColor,
        /// <summary>Control descriptions have been disabled for the export.</summary>
        NoControlDescriptions,
        /// <summary>A PDF base map will be rendered instead of copied as vector artwork.</summary>
        PdfMapMultiUp,
        /// <summary>One or more selected course views require more than one map page.</summary>
        MultiplePagesPerCourse,
    }

    /// <summary>Number of map views planned for one course or relay variation.</summary>
    public class PdfProductionCourseSummary
    {
        /// <summary>Creates a course summary entry.</summary>
        /// <param name="courseName">Full display name of the course or variation.</param>
        /// <param name="mapViews">Number of map views planned for this entry.</param>
        public PdfProductionCourseSummary(string courseName, int mapViews)
        {
            CourseName = courseName;
            MapViews = mapViews;
        }

        /// <summary>Full display name of the course or variation.</summary>
        public string CourseName { get; private set; }

        /// <summary>Number of logical map views planned for this entry.</summary>
        public int MapViews { get; private set; }
    }

    /// <summary>Number of physical sheets planned for one paper size and orientation.</summary>
    public class PdfProductionPaperSummary
    {
        /// <summary>Creates a paper summary entry.</summary>
        /// <param name="paperName">Configured paper name.</param>
        /// <param name="landscape">True when the sheet is landscape.</param>
        /// <param name="frontSheets">Number of map-front sheets.</param>
        public PdfProductionPaperSummary(string paperName, bool landscape, int frontSheets)
        {
            PaperName = paperName;
            Landscape = landscape;
            FrontSheets = frontSheets;
        }

        /// <summary>Configured paper name.</summary>
        public string PaperName { get; private set; }

        /// <summary>True when the sheet is landscape.</summary>
        public bool Landscape { get; private set; }

        /// <summary>True when the sheet is portrait.</summary>
        public bool Portrait { get { return !Landscape; } }

        /// <summary>Number of map-front sheets on this paper.</summary>
        public int FrontSheets { get; private set; }
    }

    /// <summary>Class-to-course assignment and known participant quantity for a planned export.</summary>
    public class PdfProductionClassSummary
    {
        /// <summary>Creates a class production-plan entry.</summary>
        /// <param name="courseName">Purple Pen course name.</param>
        /// <param name="className">Class associated with the course.</param>
        /// <param name="participants">Known participants from the imported start list.</param>
        public PdfProductionClassSummary(string courseName, string className, int participants)
        {
            CourseName = courseName;
            ClassName = className;
            Participants = participants;
        }

        /// <summary>Purple Pen course name.</summary>
        public string CourseName { get; private set; }

        /// <summary>Class associated with the course.</summary>
        public string ClassName { get; private set; }

        /// <summary>Number of matching rows in the imported start list.</summary>
        public int Participants { get; private set; }
    }

    /// <summary>
    /// Complete data-only production summary for a planned CoursePdf operation.
    /// </summary>
    public class PdfProductionSummary
    {
        /// <summary>Number of independently rendered map views.</summary>
        public int MapViews { get; internal set; }

        /// <summary>Number of physical map-front sheets.</summary>
        public int FrontSheets { get; internal set; }

        /// <summary>Number of information back sheets.</summary>
        public int BacksideSheets { get; internal set; }

        /// <summary>Total number of pages in the generated PDF files.</summary>
        public int TotalPdfPages { get { return FrontSheets + BacksideSheets; } }

        /// <summary>Course and variation quantities in the planned export.</summary>
        public List<PdfProductionCourseSummary> Courses { get; } = new List<PdfProductionCourseSummary>();

        /// <summary>Physical paper-sheet quantities in the planned export.</summary>
        public List<PdfProductionPaperSummary> Papers { get; } = new List<PdfProductionPaperSummary>();

        /// <summary>Class-to-course assignments and known start-list quantities.</summary>
        public List<PdfProductionClassSummary> Classes { get; } = new List<PdfProductionClassSummary>();

        /// <summary>Settings that should be explicitly reviewed before export.</summary>
        public List<PdfProductionWarning> Warnings { get; } = new List<PdfProductionWarning>();
    }
}
