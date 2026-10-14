// BlCoursePdfTests.cs
//
// End-to-end checks that course PDFs created with a BL print profile behave like an OCAD
// course-setting export at the printer: the transparent course purple is interleaved in the
// map's colour order (above area colours, below brown/black/blue lines), it is written with
// overprint and the Darken blend mode, the upper purple is opaque, and the map colours carry
// the printer's CMYK values. The PDFs are inspected at content-stream level.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PurplePen.Graphics2D;
using PurplePen.MapModel;

namespace PurplePen.PrintTests
{
    /// <summary>End-to-end PDF production tests for the BL print profiles.</summary>
    [TestFixture]
    [NonParallelizable]
    public class BlCoursePdfTests
    {
        private const string EventFile = "Lincoln Park PrintAreas 2.ppen";
        private const string MapFile = "LincolnNov12.ocd";

        private static ServiceProvider serviceProvider;

        /// <summary>Registers the same rendering services as the application.</summary>
        [OneTimeSetUp]
        public void RegisterServices()
        {
            if (serviceProvider != null)
                return;

            ServiceCollection services = new ServiceCollection();
            services.AddSingleton<IGraphicsBitmapLoader, SkiaBitmapGraphicsLoader>();
            services.AddSingleton<IBitmapGraphicsTargetProvider, SkiaBitmapGraphicsTargetProvider>();
            services.AddSingleton<IFontLoader>(SkiaFontLoader.Instance);
            services.AddSingleton<ITextMetrics, Skia_TextMetrics>();
            services.AddSingleton<IFileLoaderProvider, SkiaFileLoaderProvider>();
            services.AddSingleton<IPdfWriter, PdfWriter>();
            serviceProvider = services.BuildServiceProvider();
            Services.RegisterServiceProvider(serviceProvider);
        }

        /// <summary>
        /// BL forest profile on an English ISOM OCAD map whose colours were mapped to the profile:
        /// lower course purple lies between the area colours and brown, is transparent (overprint +
        /// Darken), and mapped map colours are printed with BL's CMYK values.
        /// </summary>
        [Test]
        public async Task BlForest_LowerPurpleIsInterleavedTransparentAndMapUsesBlCmyk()
        {
            HeadlessUserInterface ui = new HeadlessUserInterface();
            Controller controller = await LoadEvent(ui);

            List<SymColor> mapColors = controller.MapDisplay.GetMapColors();
            List<PrintProfileColorMapping> mappings = new List<PrintProfileColorMapping> {
                Mapping("brown-contours", mapColors, "Brown"),
                Mapping("black", mapColors, "Black"),
                Mapping("green-100", mapColors, "Green"),
                Mapping("yellow-100", mapColors, "Yellow"),
            };

            string pdfFile = CreatePdf(controller, ui, "bl-skog", BuiltInPrintProfiles.CreateBlForest().Id, mappings);
            List<ColorOp> ops = ReadColorOps(pdfFile);

            Cmyk purple = new Cmyk(0.35F, 0.95F, 0, 0);
            Cmyk blBrown = new Cmyk(0, 0.75F, 1, 0.33F);
            Cmyk blGreen = new Cmyk(0.76F, 0, 0.91F, 0);
            Cmyk blYellow = new Cmyk(0, 0.27F, 0.94F, 0);

            Assert.That(ops.Any(op => op.Color.Equals(blBrown)), "Mapped brown must use BL's 0/75/100/33.");
            Assert.That(ops.Any(op => op.Color.Equals(blGreen)), "Mapped green must use BL's 76/0/91/0.");
            Assert.That(ops.Any(op => op.Color.Equals(new Cmyk(0, 0.75F, 1, 0.5F))), Is.False, "The map's own brown 0/75/100/50 must not be printed.");
            Assert.That(ops.Any(op => op.Color.Equals(purple)), "The course must be printed in BL violet 35/95/0/0.");

            int lastArea = ops.FindLastIndex(op => op.Color.Equals(blGreen) || op.Color.Equals(blYellow));
            int firstBrown = ops.FindIndex(op => op.Color.Equals(blBrown));
            List<ColorOp> interleavedPurple = ops.Where((op, index) => index > lastArea && index < firstBrown && op.Color.Equals(purple)).ToList();

            Assert.That(lastArea, Is.GreaterThanOrEqualTo(0), "Expected green/yellow area colours in the print area.");
            Assert.That(firstBrown, Is.GreaterThan(lastArea), "Map colours must be drawn in colour-table order.");
            Assert.That(interleavedPurple, Is.Not.Empty, "Lower course purple must be drawn after the area colours and before brown.");
            Assert.That(interleavedPurple.All(op => op.BlendMode == "/Darken"), "Lower course purple must use the Darken blend mode.");
            Assert.That(interleavedPurple.All(op => op.Overprint), "Lower course purple must overprint.");

            // Every purple object is either lower purple (transparent: Darken + overprint) or upper
            // purple (opaque: normal blending, knockout). Nothing in between.
            foreach (ColorOp op in ops.Where(op => op.Color.Equals(purple))) {
                bool transparent = op.BlendMode == "/Darken" && op.Overprint;
                bool opaque = op.BlendMode != "/Darken" && !op.Overprint;
                Assert.That(transparent || opaque, "Inconsistent purple graphics state: " + op);
            }

            Assert.That(ui.Messages.Where(message => message.StartsWith("Error", StringComparison.Ordinal)), Is.Empty, ui.AllMessages);
        }

        /// <summary>
        /// Without a profile the export is unchanged: the course is drawn on top of the map.
        /// The file is kept as a reference for visual comparison.
        /// </summary>
        [Test]
        public async Task NoProfile_CourseIsDrawnOnTopAsBefore()
        {
            HeadlessUserInterface ui = new HeadlessUserInterface();
            Controller controller = await LoadEvent(ui);

            string pdfFile = CreatePdf(controller, ui, "utan-profil", "", new List<PrintProfileColorMapping>());
            List<ColorOp> ops = ReadColorOps(pdfFile);
            Cmyk mapBrown = new Cmyk(0, 0.75F, 1, 0.5F);

            int lastBrown = ops.FindLastIndex(op => op.Color.Equals(mapBrown));
            Assert.That(lastBrown, Is.GreaterThanOrEqualTo(0), "The map's own brown must be printed unchanged.");
            Assert.That(ui.Messages.Where(message => message.StartsWith("Error", StringComparison.Ordinal)), Is.Empty, ui.AllMessages);
        }

        /// <summary>The production summary explains the placement of the course purple.</summary>
        [Test]
        public async Task BlForest_ProductionSummaryDescribesLayering()
        {
            HeadlessUserInterface ui = new HeadlessUserInterface();
            Controller controller = await LoadEvent(ui);
            List<SymColor> mapColors = controller.MapDisplay.GetMapColors();

            CoursePdfSettings settings = CreateSettings(controller, CreateOutputDirectory("summary"), "summary",
                BuiltInPrintProfiles.CreateBlForest().Id,
                new List<PrintProfileColorMapping> { Mapping("brown-contours", mapColors, "Brown"), Mapping("green-100", mapColors, "Green") });
            PdfProductionSummary summary = controller.GetPdfProductionSummary(settings);

            Assert.That(summary.PrintLayering, Is.Not.Null);
            Assert.That(summary.PrintLayering.IsInterleaved, Is.True);
            Assert.That(summary.PrintLayering.MapColorAboveLowerPurple, Is.EqualTo("Brown"));
            Assert.That(summary.Warnings, Does.Not.Contain(PdfProductionWarning.PrintProfileLayerNotFromProfile));
            TestContext.Out.WriteLine("Lower purple lies over '{0}' and under '{1}'.",
                summary.PrintLayering.MapColorBelowLowerPurple, summary.PrintLayering.MapColorAboveLowerPurple);
        }

        /// <summary>Copies the event and map to a fresh folder and loads them through the controller.</summary>
        private static async Task<Controller> LoadEvent(HeadlessUserInterface ui)
        {
            string sourceDirectory = Path.Combine(FindTestFilesDirectory(), "courseprinting");
            string workDirectory = Path.Combine(Path.GetTempPath(), "pp-print-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workDirectory);
            File.Copy(Path.Combine(sourceDirectory, EventFile), Path.Combine(workDirectory, EventFile));
            File.Copy(Path.Combine(sourceDirectory, MapFile), Path.Combine(workDirectory, MapFile));

            Controller controller = new Controller(ui);
            bool loaded = await controller.LoadInitialFile(Path.Combine(workDirectory, EventFile), false);
            Assert.That(loaded, Is.True, ui.AllMessages);
            Assert.That(controller.MapDisplay.MapType, Is.EqualTo(MapType.OCAD), ui.AllMessages);
            return controller;
        }

        /// <summary>Creates PDFs for the first course and returns the single created file.</summary>
        private static string CreatePdf(Controller controller, HeadlessUserInterface ui, string name, string profileId,
                                        List<PrintProfileColorMapping> mappings)
        {
            string outputDirectory = CreateOutputDirectory(name);
            CoursePdfSettings settings = CreateSettings(controller, outputDirectory, name, profileId, mappings);
            bool created = controller.CreateCoursePdfs(settings);
            Assert.That(created, Is.True, ui.AllMessages);

            string pdfFile = Directory.GetFiles(outputDirectory, "*.pdf").Single();
            string keepDirectory = Environment.GetEnvironmentVariable("PP_PRINT_TEST_OUTPUT");
            if (!String.IsNullOrEmpty(keepDirectory)) {
                Directory.CreateDirectory(keepDirectory);
                File.Copy(pdfFile, Path.Combine(keepDirectory, name + ".pdf"), true);
            }

            return pdfFile;
        }

        /// <summary>Creates PDF settings for the first course of the event.</summary>
        private static CoursePdfSettings CreateSettings(Controller controller, string outputDirectory, string prefix, string profileId,
                                                        List<PrintProfileColorMapping> mappings)
        {
            Id<Course> courseId = QueryEvent.SortedCourseIds(controller.GetEventDB(), false).First();
            return new CoursePdfSettings {
                AllCourses = false,
                CourseIds = new Id<Course>[] { courseId },
                mapDirectory = false,
                fileDirectory = false,
                outputDirectory = outputDirectory,
                filePrefix = prefix,
                FileCreation = CoursePdfSettings.PdfFileCreation.SingleFile,
                ColorModel = ColorModel.CMYK,
                ShowProgressDialog = false,
                PrintProfileId = profileId,
                PrintProfileColorMappings = mappings,
            };
        }

        /// <summary>Creates an empty output folder.</summary>
        private static string CreateOutputDirectory(string name)
        {
            string directory = Path.Combine(Path.GetTempPath(), "pp-print-out-" + name + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        /// <summary>Creates an explicit mapping from a profile rule to the map colour with the given name.</summary>
        private static PrintProfileColorMapping Mapping(string ruleId, List<SymColor> mapColors, string mapColorName)
        {
            int index = mapColors.FindIndex(color => color.Name == mapColorName);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "Map colour '" + mapColorName + "' not found. Colours: " +
                String.Join(", ", mapColors.Select(color => color.Name)));
            return new PrintProfileColorMapping {
                RuleId = ruleId,
                MapColorName = mapColors[index].Name,
                MapColorOcadId = mapColors[index].OcadId,
                MapColorDrawOrder = index,
            };
        }

        /// <summary>Finds src/TestFiles by walking up from the test assembly.</summary>
        private static string FindTestFilesDirectory()
        {
            DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null) {
                string candidate = Path.Combine(directory.FullName, "TestFiles");
                if (Directory.Exists(Path.Combine(candidate, "courseprinting")))
                    return candidate;
                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not find src/TestFiles from " + AppContext.BaseDirectory);
        }

        /// <summary>A CMYK value compared with a small tolerance.</summary>
        private readonly struct Cmyk : IEquatable<Cmyk>
        {
            public Cmyk(float c, float m, float y, float k) { C = c; M = m; Y = y; K = k; }
            public float C { get; }
            public float M { get; }
            public float Y { get; }
            public float K { get; }
            public bool Equals(Cmyk other)
            {
                const float tolerance = 0.006F;
                return Math.Abs(C - other.C) < tolerance && Math.Abs(M - other.M) < tolerance &&
                       Math.Abs(Y - other.Y) < tolerance && Math.Abs(K - other.K) < tolerance;
            }
            public override bool Equals(object obj) { return obj is Cmyk other && Equals(other); }
            public override int GetHashCode() { return 0; }
            public override string ToString() { return String.Format(CultureInfo.InvariantCulture, "{0}/{1}/{2}/{3}", C, M, Y, K); }
        }

        /// <summary>A CMYK colour-setting operator with the graphics state that is active for it.</summary>
        private sealed class ColorOp
        {
            public int Index { get; set; }
            public Cmyk Color { get; set; }
            public bool IsStroke { get; set; }
            public string BlendMode { get; set; } = "/Normal";
            public bool Overprint { get; set; }
            public override string ToString() { return (IsStroke ? "K " : "k ") + Color + " " + BlendMode + (Overprint ? " OP" : ""); }
        }

        /// <summary>Graphics state that matters for these tests.</summary>
        private sealed class GraphicsState
        {
            public string BlendMode = "/Normal";
            public bool FillOverprint;
            public bool StrokeOverprint;
            public GraphicsState Clone() { return (GraphicsState)MemberwiseClone(); }
        }

        /// <summary>Reads all CMYK colour operators of the first page, in drawing order, with their graphics state.</summary>
        private static List<ColorOp> ReadColorOps(string pdfFile)
        {
            using (PdfDocument document = PdfReader.Open(pdfFile, PdfDocumentOpenMode.Import)) {
                PdfPage page = document.Pages[0];
                byte[] contentBytes = page.Contents.CreateSingleContent().Stream.UnfilteredValue;
                string content = Encoding.Latin1.GetString(contentBytes);

                Dictionary<string, PdfDictionary> extGStates = new Dictionary<string, PdfDictionary>(StringComparer.Ordinal);
                PdfDictionary resources = page.Elements.GetDictionary("/Resources");
                PdfDictionary extGStateDictionary = resources?.Elements.GetDictionary("/ExtGState");
                if (extGStateDictionary != null) {
                    foreach (string key in extGStateDictionary.Elements.Keys)
                        extGStates[key] = extGStateDictionary.Elements.GetDictionary(key);
                }

                Regex tokens = new Regex(
                    @"(?<![\w./])(?<c>-?\d*\.?\d+)\s+(?<m>-?\d*\.?\d+)\s+(?<y>-?\d*\.?\d+)\s+(?<k>-?\d*\.?\d+)\s+(?<op>k|K)(?![\w])" +
                    @"|(?<gs>/[A-Za-z0-9_.+-]+)\s+gs(?![\w])" +
                    @"|(?<![\w/])(?<save>q|Q)(?![\w])");

                List<ColorOp> ops = new List<ColorOp>();
                Stack<GraphicsState> stack = new Stack<GraphicsState>();
                GraphicsState state = new GraphicsState();
                foreach (Match match in tokens.Matches(content)) {
                    if (match.Groups["save"].Success) {
                        if (match.Groups["save"].Value == "q")
                            stack.Push(state.Clone());
                        else if (stack.Count > 0)
                            state = stack.Pop();
                    }
                    else if (match.Groups["gs"].Success) {
                        if (extGStates.TryGetValue(match.Groups["gs"].Value, out PdfDictionary gs)) {
                            if (gs.Elements.ContainsKey("/BM"))
                                state.BlendMode = gs.Elements.GetName("/BM");
                            if (gs.Elements.ContainsKey("/op"))
                                state.FillOverprint = gs.Elements.GetBoolean("/op");
                            if (gs.Elements.ContainsKey("/OP")) {
                                state.StrokeOverprint = gs.Elements.GetBoolean("/OP");
                                if (!gs.Elements.ContainsKey("/op"))
                                    state.FillOverprint = state.StrokeOverprint;
                            }
                        }
                    }
                    else {
                        bool isStroke = match.Groups["op"].Value == "K";
                        ops.Add(new ColorOp {
                            Index = match.Index,
                            Color = new Cmyk(Parse(match.Groups["c"].Value), Parse(match.Groups["m"].Value),
                                             Parse(match.Groups["y"].Value), Parse(match.Groups["k"].Value)),
                            IsStroke = isStroke,
                            BlendMode = state.BlendMode,
                            Overprint = isStroke ? state.StrokeOverprint : state.FillOverprint,
                        });
                    }
                }

                TestContext.Out.WriteLine("{0}: {1} colour operators, {2} ExtGStates", Path.GetFileName(pdfFile), ops.Count, extGStates.Count);
                return ops;
            }
        }

        /// <summary>Parses a PDF number.</summary>
        private static float Parse(string text)
        {
            return Single.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}
