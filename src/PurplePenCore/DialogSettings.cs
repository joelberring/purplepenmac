using PurplePen.MapModel;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PurplePen
{
    // Has all the settings for creating OCAD files.
    public class RouteGadgetCreationSettings
    {
        public bool mapDirectory, fileDirectory;   // directory to place output files in
        public string outputDirectory;              // the output directory if mapDirectory and fileDirectoy are false.
        public string fileBaseName;                      // base name for file names which are .xml,.gif
        public int xmlVersion = 3;                      // version of IOF XML to use (2 or 3).

        public RouteGadgetCreationSettings Clone()
        {
            return (RouteGadgetCreationSettings)base.MemberwiseClone();
        }
    }

    // All the information needed to print courses.
    public class CoursePrintSettings
    {
        public float ScaleCalibrationFactor = 1.0F;
        public TrainingExerciseRenderProfile TrainingExerciseRenderProfile = TrainingExerciseRenderProfile.None;
        public Id<Course>[] CourseIds;          // Courses to print, None is all controls.
        public bool AllCourses = true;          // If true, overrides the course ids in CourseIds except for "all controls".

        // variation choices for courses with variations.
        public Dictionary<Id<Course>, VariationChoices> VariationChoicesPerCourse = new Dictionary<Id<Course>, VariationChoices>();

        public int Count = 1;                         // count of copies to print
        public bool CropLargePrintArea = true;       // If true, crop a large print area instead of printing multiple pages 
        public bool PrintMapExchangesOnOneMap = false;
        public bool PauseAfterCourseOrPart = false;  // If true, printing pauses after each course or part of course printed.
        public ColorModel PrintingColorModel = ColorModel.CMYK;
    }

    // All the information needed to print courses.
    public class CoursePdfSettings
    {
        // Printer calibration factor: measured length / nominal 100 mm. Values
        // other than one adjust map layout scale while retaining the requested
        // printed scale.
        public float ScaleCalibrationFactor = 1.0F;
        public TrainingExerciseRenderProfile TrainingExerciseRenderProfile = TrainingExerciseRenderProfile.None;
        public Id<Course>[] CourseIds;          // Courses to print, None is all controls.
        public bool AllCourses = true;          // If true, overrides CourseIds except for all controls.

        public bool DontPrintBaseMap = false;    // If true, the base map is not rendered, just the course.
        public bool CropLargePrintArea = true;       // If true, crop a large print area instead of printing multiple pages
        public bool PrintMapExchangesOnOneMap = false;
        // Number of complete selected course/variation sets to include in the PDF export.
        public int Copies = 1;
        // Number of independent course/map views to place on each physical PDF page.
        // All views on a page are reduced proportionally; their map extents are not merged.
        public PdfPageLayout PageLayout = PdfPageLayout.OnePerPage;
        // Add a second, information-only PDF page after every map page. Information is
        // placed in the same slots as the corresponding maps, ready for duplex printing.
        public bool IncludeBacksideInfo = false;
        // Optional common heading shown before the automatically generated course,
        // relay team, leg, and variation-code information on every backside.
        public string BacksideText = String.Empty;
        // Optional participant records imported from a MeOS-compatible CSV
        // start list. Records are matched by relay team and leg.
        public List<BacksideInfoRecord> BacksideInfoRecords = new List<BacksideInfoRecord>();
        public PdfFileCreation FileCreation = PdfFileCreation.FilePerCourse;
        public ColorModel ColorModel = ColorModel.CMYK;
        // Empty means standard PDF export without a selected print-profile preflight.
        // A profile is always applied non-destructively and never changes the source map.
        public string PrintProfileId = String.Empty;
        // Rule IDs explicitly confirmed by the user after a partial source-map match.
        public List<string> ConfirmedPrintProfileRuleIds = new List<string>();
        // Explicit source-map colour choices made for otherwise ambiguous profile rules.
        public List<PrintProfileColorMapping> PrintProfileColorMappings = new List<PrintProfileColorMapping>();
        public bool RenderControlDescriptions = true;
        public bool ShowProgressDialog = true;

        public bool mapDirectory, fileDirectory;     // directory to place output files in
        public string outputDirectory;               // the output directory if mapDirectory and fileDirectoy are false.
        public string filePrefix;                    // if non-null, non-empty, prefix this an "-" onto the front of files.

        // variation choices for courses with variations.
        public Dictionary<Id<Course>, VariationChoices> VariationChoicesPerCourse = new Dictionary<Id<Course>, VariationChoices>();

        public enum PdfFileCreation { SingleFile, FilePerCourse, FilePerCoursePart };
        public enum PdfPageLayout { OnePerPage = 1, TwoPerPage = 2, FourPerPage = 4 };

        public CoursePdfSettings Clone()
        {
            CoursePdfSettings n = (CoursePdfSettings)base.MemberwiseClone();
            n.ConfirmedPrintProfileRuleIds = new List<string>(ConfirmedPrintProfileRuleIds);
            n.PrintProfileColorMappings = new List<PrintProfileColorMapping>(PrintProfileColorMappings);
            n.BacksideInfoRecords = BacksideInfoRecords == null
                ? new List<BacksideInfoRecord>()
                : BacksideInfoRecords.Select(record => record.Clone()).ToList();
            return n;
        }
    }

    /// <summary>Named reusable paper/workshop configuration stored in user settings.</summary>
    public class PrintWorkshopTemplate
    {
        public string Name { get; set; } = "";
        public int PageWidth { get; set; }
        public int PageHeight { get; set; }
        public int PageMargins { get; set; }
        public bool Landscape { get; set; }
        public float Rotation { get; set; }
        public bool FixSizeToPaper { get; set; }
        public int PageLayout { get; set; } = 1;
        public bool IncludeBackside { get; set; }
        public string BacksideText { get; set; } = "";
        public float ScaleCalibrationFactor { get; set; } = 1.0F;

        public PrintWorkshopTemplate Clone()
        {
            return (PrintWorkshopTemplate)MemberwiseClone();
        }
    }

    // Has all the settings for creating OCAD files.
    public class OcadCreationSettings
    {
        public Id<Course>[] CourseIds;          // Courses to print. Course.None means all controls.
        public bool AllCourses = true;          // If true, overrides CourseIds except for all controls.
        public MapFileFormat fileFormat;         // OCAD version to use/OpenMapper format
        public bool mapDirectory, fileDirectory;   // directory to place output files in
        public string outputDirectory;              // the output directory if mapDirectory and fileDirectoy are false.
        public string filePrefix;                      // if non-null, non-empty, prefix this an "-" onto the front of files.
        public short colorOcadId;                         // ocadID for the purple stuff.
        public float cyan, magenta, yellow, black;   // color to use for the "Purple" stuff.
        public bool purpleOverprint;

        // variation choices for courses with variations.
        public Dictionary<Id<Course>, VariationChoices> VariationChoicesPerCourse = new Dictionary<Id<Course>, VariationChoices>();

        public OcadCreationSettings Clone()
        {
            return (OcadCreationSettings)base.MemberwiseClone();
        }
    }

    // All the information needed to create bitmaps.
    public class BitmapCreationSettings
    {
        public Id<Course>[] CourseIds;          // Courses to print, None is all controls.
        public bool AllCourses = true;          // If true, overrides CourseIds except for all controls.

        public bool DontPrintBaseMap = false;    // If true, the base map is not rendered, just the course.
        public bool PrintMapExchangesOnOneMap = false;
        public BitmapKind ExportedBitmapKind = BitmapCreationSettings.BitmapKind.Png;
        public float Dpi;
        public bool WorldFile;                      // Create a world file?
        public ColorModel ColorModel = ColorModel.CMYK;

        public bool mapDirectory, fileDirectory;     // directory to place output files in
        public string outputDirectory;               // the output directory if mapDirectory and fileDirectoy are false.
        public string filePrefix;                    // if non-null, non-empty, prefix this an "-" onto the front of files.

        // variation choices for courses with variations.
        public Dictionary<Id<Course>, VariationChoices> VariationChoicesPerCourse = new Dictionary<Id<Course>, VariationChoices>();

        public enum BitmapKind { Gif, Png, Jpeg };

        public BitmapCreationSettings Clone()
        {
            BitmapCreationSettings n = (BitmapCreationSettings)base.MemberwiseClone();
            return n;
        }
    }
}
