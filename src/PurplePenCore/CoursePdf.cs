/* Copyright (c) 2006-2008, Peter Golde
 * All rights reserved. 
 * 
 * Redistribution and use in source and binary forms, with or without 
 * modification, are permitted provided that the following conditions are 
 * met:
 * 
 * 1. Redistributions of source code must retain the above copyright
 * notice, this list of conditions and the following disclaimer.
 * 
 * 2. Redistributions in binary form must reproduce the above copyright
 * notice, this list of conditions and the following disclaimer in the
 * documentation and/or other materials provided with the distribution.
 * 
 * 3. Neither the name of Peter Golde, nor "Purple Pen", nor the names
 * of its contributors may be used to endorse or promote products
 * derived from this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND
 * CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES,
 * INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF
 * MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
 * BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
 * SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
 * WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE
 * USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY
 * OF SUCH DAMAGE.
 */

using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;


namespace PurplePen
{
    using PurplePen.Graphics2D;
    using PurplePen.MapModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;

    // Class to output courses to PDF
    public class CoursePdf 
    {
        private CoursePdfSettings coursePdfSettings;
        private EventDB eventDB;
        private SymbolDB symbolDB;
        private Controller controller;
        private MapDisplay mapDisplay;
        private CourseAppearance appearance;
        private RectangleF mapBounds;  // bounds of the map, in map coordinates.
        private string sourcePdfMapFileName;
        private int totalPages, currentPage;
        private IDictionary<SymColor, MapColorOverride> printProfileMapColorOverrides;

        // mapDisplay is a MapDisplay that contains the correct map. All other features of the map display need to be customized.
        public CoursePdf(EventDB eventDB, SymbolDB symbolDB, Controller controller, MapDisplay mapDisplay, 
                         CoursePdfSettings coursePdfSettings, CourseAppearance appearance)
        {
            this.eventDB = eventDB;
            this.symbolDB = symbolDB;
            this.controller = controller;
            this.mapDisplay = mapDisplay;
            this.coursePdfSettings = coursePdfSettings;
            this.appearance = appearance;

            // Set default features for printing.
            mapDisplay.MapIntensity = 1.0F;
            mapDisplay.AntiAlias = false;
            mapDisplay.Printing = true;
            mapDisplay.ColorModel = coursePdfSettings.ColorModel;

            mapBounds = mapDisplay.MapBounds;

            if (mapDisplay.MapType == MapType.PDF) {
                // For PDF maps, we remove the PDF map from the MapDisplay and add it in separately.
                sourcePdfMapFileName = mapDisplay.FileName;
            }

            printProfileMapColorOverrides = CreatePrintProfileMapColorOverrides();
        }

        // Is the map a PDF map?
        private bool IsPdfMap
        {
            get { return sourcePdfMapFileName != null; }
        }

        public List<string> OverwrittenFiles()
        {
            return (from filePair in GetFilesToCreate() 
                    let fileName = filePair.First
                    where File.Exists(fileName)
                    select fileName).ToList();
        }

        public void CreatePdfs()
        {
            List<Pair<string, IEnumerable<CourseDesignator>>> fileList = GetFilesToCreate();

            // Test that we can read the page. 
            if (IsPdfMap) {
                if (! Services.PdfWriter.CanReadPdfPage(sourcePdfMapFileName, 0)) {
                    // We couldn't read the page. Fall back to normal map rendering methods.
                    sourcePdfMapFileName = null; // IsPdfMap will now be false.
                }
            }

            totalPages = 0;
            foreach (var pair in fileList) {
                totalPages += LayoutSheets(pair.Second).Count * (coursePdfSettings.IncludeBacksideInfo ? 2 : 1);
            }

            if (coursePdfSettings.ShowProgressDialog)
            {
                controller.ShowProgressDialog(true);
            }

            try {
                currentPage = 0;
                foreach (var pair in fileList) {
                    CreateOnePdfFile(pair.First, pair.Second);
                }
            }
            finally {
                if (coursePdfSettings.ShowProgressDialog)
                {
                    controller.EndProgressDialog();
                }
            }
        }

        // Get the files that we should create. along with the corresponding courses on them.
#if TEST
        internal
#endif
        List<Pair<string, IEnumerable<CourseDesignator>>> GetFilesToCreate()
        {
            List<Pair<string, IEnumerable<CourseDesignator>>> fileList = new List<Pair<string, IEnumerable<CourseDesignator>>>();

            switch (coursePdfSettings.FileCreation) {
                case CoursePdfSettings.PdfFileCreation.SingleFile:
                    // All pages go into a single file.
                    fileList.Add(new Pair<string, IEnumerable<CourseDesignator>>(CreateOutputFileName(null),
                                 QueryEvent.EnumerateCourseDesignators(eventDB, coursePdfSettings.CourseIds, coursePdfSettings.VariationChoicesPerCourse, !coursePdfSettings.PrintMapExchangesOnOneMap)));
                    break;

                case CoursePdfSettings.PdfFileCreation.FilePerCourse:
                    // Create a file for each course.
                    foreach (Id<Course> courseId in coursePdfSettings.CourseIds) {
                        fileList.Add(new Pair<string, IEnumerable<CourseDesignator>>(CreateOutputFileName(new CourseDesignator(courseId)),
                                     QueryEvent.EnumerateCourseDesignators(eventDB, new Id<Course>[1] { courseId }, coursePdfSettings.VariationChoicesPerCourse, !coursePdfSettings.PrintMapExchangesOnOneMap)));
                    }
                    break;

                case CoursePdfSettings.PdfFileCreation.FilePerCoursePart:
                    // Create a file for each course part or variation (or both)
                    foreach (CourseDesignator designator in 
                             QueryEvent.EnumerateCourseDesignators(eventDB, coursePdfSettings.CourseIds, 
                                                                   coursePdfSettings.VariationChoicesPerCourse, !coursePdfSettings.PrintMapExchangesOnOneMap)) {
                        fileList.Add(new Pair<string, IEnumerable<CourseDesignator>>(CreateOutputFileName(designator), new[] { designator }));
                    }

                    break;
            }

            return fileList;
        }

        // Get the full output file name. Uses the name of the course, removes bad characters,
        // checks for duplication of the map file name. Puts in the directory given in the creationSettings.
        string CreateOutputFileName(CourseDesignator courseDesignator)
        {
            string basename = QueryEvent.CreateOutputFileName(eventDB, courseDesignator, coursePdfSettings.filePrefix, "", ".pdf");

            return Path.GetFullPath(Path.Combine(coursePdfSettings.outputDirectory, basename));
        }

        // Create a single PDF file
        void CreateOnePdfFile(string fileName, IEnumerable<CourseDesignator> courseDesignators)
        {
            List<CoursePageSheet> sheets = LayoutSheets(courseDesignators);
            IPdfDocumentWriter pdfDocumentWriter = Services.PdfWriter.CreateDocument(fileName, Path.GetFileNameWithoutExtension(fileName), coursePdfSettings.ColorModel == ColorModel.CMYK);

            foreach (CoursePageSheet sheet in sheets) {
                CoursePage pageToDraw = sheet.pages[0];

                SizeF paperSize = new SizeF(pageToDraw.paperSize.SizeInInches.Width, pageToDraw.paperSize.SizeInInches.Height);
                if (pageToDraw.landscape)
                    paperSize = new SizeF(paperSize.Height, paperSize.Width);

                if (coursePdfSettings.ShowProgressDialog)
                {
                    if (controller.UpdateProgressDialog(string.Format(MiscText.CreatingFile, Path.GetFileName(fileName)), (double) currentPage / (double) totalPages))
                    {
                        throw new Exception(MiscText.CancelledByUser);
                    }
                }

                IGraphicsTarget grTarget;

                if (coursePdfSettings.DontPrintBaseMap) {
                    // Don't print the base map, just the course.
                    mapDisplay.SetMapFile(MapType.None, null);
                    grTarget = pdfDocumentWriter.BeginPage(paperSize);
                }
                else if (IsPdfMap && coursePdfSettings.PageLayout == CoursePdfSettings.PdfPageLayout.OnePerPage &&
                         Math.Abs(pageToDraw.mapRotation) < 0.0001F) {
                    // Import the base map from the PDF map file, so that it is vector, not raster.

                    // We need to re-obtain a PdfImporter every time, or else very strange bugs start to crop up.

                    float scaleRatio = CourseView.CreatePrintingCourseView(eventDB, pageToDraw.courseDesignator).ScaleRatio;
                    RectangleF sourcePortionInInches = new RectangleF(
                        Geometry.InchesFromMm(pageToDraw.mapRectangle.Left),
                        Geometry.InchesFromMm(mapBounds.Height - pageToDraw.mapRectangle.Bottom),
                        Geometry.InchesFromMm(pageToDraw.mapRectangle.Width),
                        Geometry.InchesFromMm(pageToDraw.mapRectangle.Height));
                    RectangleF cropRectangleInInches = new RectangleF(pageToDraw.printRectangle.Left / 100F, pageToDraw.printRectangle.Top / 100F,
                                                                    pageToDraw.printRectangle.Width / 100F, pageToDraw.printRectangle.Height / 100F);

                    if (CanCopyPdfMapPage(pageToDraw, scaleRatio, cropRectangleInInches, paperSize, mapBounds))
                    {
                        // If we're doing a PDF at scale 1, no cropping, and the print area is the same as the page size, we just copy the page directly.
                        grTarget = pdfDocumentWriter.BeginCopiedPage(sourcePdfMapFileName, 0);
                    }
                    else {
                        grTarget = pdfDocumentWriter.BeginCopiedPartialPage(sourcePdfMapFileName, 0, paperSize, sourcePortionInInches, cropRectangleInInches);
                    }

                    // Don't draw the map normally, which would case the rasterized map to be drawn over the PDF map.
                    mapDisplay.SetMapFile(MapType.None, null);
                }
                else {
                    // The copied-PDF path can crop and scale, but cannot rotate the imported
                    // PDF content. Restore the normal PDF map display so a rotated page uses
                    // the raster rendering path, which applies the same transform to map and
                    // course graphics.
                    if (IsPdfMap && mapDisplay.MapType != MapType.PDF)
                        mapDisplay.SetMapFile(MapType.PDF, sourcePdfMapFileName);
                    grTarget = pdfDocumentWriter.BeginPage(paperSize);
                }

                foreach (CoursePage page in sheet.pages) {
                    DrawPage(grTarget, page);
                }
                pdfDocumentWriter.EndPage(grTarget);
                grTarget.Dispose();

                currentPage += 1;

                if (coursePdfSettings.IncludeBacksideInfo) {
                    if (coursePdfSettings.ShowProgressDialog)
                    {
                        if (controller.UpdateProgressDialog(string.Format(MiscText.CreatingFile, Path.GetFileName(fileName)), (double) currentPage / (double) totalPages))
                        {
                            throw new Exception(MiscText.CancelledByUser);
                        }
                    }

                    IGraphicsTarget backsideTarget = pdfDocumentWriter.BeginPage(paperSize);
                    foreach (CoursePage page in sheet.pages) {
                        DrawBacksideInfo(backsideTarget, page);
                    }
                    pdfDocumentWriter.EndPage(backsideTarget);
                    backsideTarget.Dispose();

                    currentPage += 1;
                }
            }

            pdfDocumentWriter.Save();
        }

        // Layout the pages for a set of course designators.
        List<CoursePage> LayoutPages(IEnumerable<CourseDesignator> courseDesignators)
        {
            CoursePageLayout pageLayout = new CoursePageLayout(eventDB, symbolDB, controller, appearance,
                                                               coursePdfSettings.CropLargePrintArea,
                                                               coursePdfSettings.ScaleCalibrationFactor);

            return pageLayout.LayoutPages(courseDesignators);
        }

        /// <summary>Determines whether the PDF page-copy API can reproduce the requested map view.</summary>
#if TEST
        internal
#else
        private
#endif
        static bool CanCopyPdfMapPage(CoursePage page, float scaleRatio, RectangleF cropRectangleInInches,
                                      SizeF paperSize, RectangleF mapBounds)
        {
            // BeginCopiedPage and BeginCopiedPartialPage can scale and crop, but have no
            // rotation parameter. Rotated views must be rendered by MapDisplay instead.
            return Math.Abs(page.mapRotation) < 0.0001F && scaleRatio == 1.0 &&
                   Geometry.SimilarRectangles(cropRectangleInInches, new RectangleF(0, 0, paperSize.Width, paperSize.Height), 0.01F) &&
                   Geometry.SimilarRectangles(page.mapRectangle, mapBounds, 0.01F);
        }

        // Creates a data-only overview of the pages this instance would export.
        // No output files are opened or written by this method.
        public PdfProductionSummary GetProductionSummary()
        {
            PdfProductionSummary summary = new PdfProductionSummary();
            Dictionary<string, int> courseViews = new Dictionary<string, int>();
            Dictionary<string, PdfProductionPaperSummary> paperSheets = new Dictionary<string, PdfProductionPaperSummary>();

            foreach (Pair<string, IEnumerable<CourseDesignator>> filePair in GetFilesToCreate()) {
                List<CoursePageSheet> sheets = LayoutSheets(filePair.Second);
                summary.FrontSheets += sheets.Count;

                foreach (CoursePageSheet sheet in sheets) {
                    string paperKey = sheet.paperSize.Name + "|" + sheet.landscape;
                    PdfProductionPaperSummary paperSummary;
                    if (!paperSheets.TryGetValue(paperKey, out paperSummary)) {
                        paperSummary = new PdfProductionPaperSummary(sheet.paperSize.Name, sheet.landscape, 0);
                        paperSheets.Add(paperKey, paperSummary);
                    }

                    paperSummary = new PdfProductionPaperSummary(paperSummary.PaperName, paperSummary.Landscape, paperSummary.FrontSheets + 1);
                    paperSheets[paperKey] = paperSummary;

                    foreach (CoursePage page in sheet.pages) {
                        string courseName = CourseView.CreatePrintingCourseView(eventDB, page.courseDesignator).CourseFullName;
                        int count;
                        courseViews.TryGetValue(courseName, out count);
                        courseViews[courseName] = count + 1;
                        summary.MapViews += 1;
                    }
                }
            }

            foreach (KeyValuePair<string, int> courseView in courseViews.OrderBy(pair => pair.Key, StringComparer.CurrentCulture))
                summary.Courses.Add(new PdfProductionCourseSummary(courseView.Key, courseView.Value));
            foreach (PdfProductionPaperSummary paperSummary in paperSheets.Values.OrderBy(summaryEntry => summaryEntry.PaperName, StringComparer.CurrentCulture))
                summary.Papers.Add(paperSummary);

            AddClassProductionPlan(summary);

            summary.BacksideSheets = coursePdfSettings.IncludeBacksideInfo ? summary.FrontSheets : 0;
            if (coursePdfSettings.DontPrintBaseMap)
                summary.Warnings.Add(PdfProductionWarning.CourseOnly);
            if (coursePdfSettings.ColorModel == ColorModel.RGB)
                summary.Warnings.Add(PdfProductionWarning.RgbColor);
            if (!coursePdfSettings.RenderControlDescriptions)
                summary.Warnings.Add(PdfProductionWarning.NoControlDescriptions);
            if (IsPdfMap && coursePdfSettings.PageLayout != CoursePdfSettings.PdfPageLayout.OnePerPage)
                summary.Warnings.Add(PdfProductionWarning.PdfMapMultiUp);
            if (courseViews.Values.Any(count => count > coursePdfSettings.Copies))
                summary.Warnings.Add(PdfProductionWarning.MultiplePagesPerCourse);

            return summary;
        }

        // Adds the class-to-course information stored in Purple Pen, with
        // start-list quantities when a compatible MeOS CSV was imported.
        void AddClassProductionPlan(PdfProductionSummary summary)
        {
            List<BacksideInfoRecord> startList = coursePdfSettings.BacksideInfoRecords ?? new List<BacksideInfoRecord>();
            IEnumerable<Id<Course>> courseIds = coursePdfSettings.AllCourses
                ? QueryEvent.SortedCourseIds(eventDB, false)
                : coursePdfSettings.CourseIds ?? Array.Empty<Id<Course>>();

            foreach (Id<Course> courseId in courseIds.Distinct()) {
                Course course = eventDB.GetCourse(courseId);
                foreach (KeyValuePair<Id<EventClass>, EventClass> classPair in EventClassSupport.GetClasses(eventDB, courseId)) {
                    string className = classPair.Value.Name == null ? String.Empty : classPair.Value.Name.Trim();
                    if (className.Length == 0)
                        continue;
                    int participants = startList.Count(record =>
                        (String.IsNullOrWhiteSpace(record.Course) || String.Equals(record.Course.Trim(), course.name, StringComparison.OrdinalIgnoreCase)) &&
                        String.Equals(record.ClassName.Trim(), className, StringComparison.OrdinalIgnoreCase));
                    if (participants == 0)
                        participants = classPair.Value.ParticipantCount;
                    summary.Classes.Add(new PdfProductionClassSummary(course.name, className, participants));
                }
            }
        }

        // Layout logical course pages into physical PDF sheets.
#if TEST
        internal
#endif
        List<CoursePageSheet> LayoutSheets(IEnumerable<CourseDesignator> courseDesignators)
        {
            return CoursePageSheetLayout.LayoutSheets(LayoutPages(courseDesignators), coursePdfSettings.PageLayout, coursePdfSettings.Copies);
        }

        // The core printing routine. 
        void DrawPage(IGraphicsTarget graphicsTarget, CoursePage page)
        {
            // Get the course view for the course we are printing.
            CourseView courseView = CourseView.CreatePrintingCourseView(eventDB, page.courseDesignator);

            // Get the correct purple color to print the course in.
            short ocadId;
            float purpleC, purpleM, purpleY, purpleK;
            bool purpleOverprint;
            FindPurple.GetPurpleColor(mapDisplay, appearance, out ocadId, out purpleC, out purpleM, out purpleY, out purpleK, out purpleOverprint);
            ApplyPrintProfileCourseColor(ref ocadId, ref purpleC, ref purpleM, ref purpleY, ref purpleK, ref purpleOverprint);

            // Create a course layout from the view.
            CourseLayout layout = new CourseLayout();
            layout.SetLayerColor(CourseLayer.Descriptions, NormalCourseAppearance.blackColorOcadId, NormalCourseAppearance.blackColorName, NormalCourseAppearance.blackColorC, NormalCourseAppearance.blackColorM, NormalCourseAppearance.blackColorY, NormalCourseAppearance.blackColorK, false);
            layout.SetLayerColor(CourseLayer.MainCourse, ocadId, NormalCourseAppearance.courseColorName, purpleC, purpleM, purpleY, purpleK, purpleOverprint);
            layout.SetLowerLayerColor(CourseLayer.MainCourse, NormalCourseAppearance.lowerPurpleOcadId, NormalCourseAppearance.lowerPurpleColorName, purpleC, purpleM, purpleY, purpleK, purpleOverprint);

            CourseFormatterOptions formatterOptions = new CourseFormatterOptions();
            formatterOptions.showDescriptions = coursePdfSettings.RenderControlDescriptions;
            formatterOptions.trainingExerciseRenderProfile = coursePdfSettings.TrainingExerciseRenderProfile;
            formatterOptions.trainingRenderBounds = courseView.GetViewBounds();
            CourseFormatter.FormatCourseToLayout(symbolDB, courseView, appearance, layout, CourseLayer.MainCourse, formatterOptions);

            // Set the course layout into the map display
            mapDisplay.SetCourse(layout);
            mapDisplay.SetPrintArea(null);

            // Set the transform, and the clip.
            Matrix transform = Geometry.CreateInvertedRectangleTransform(page.mapRectangle, page.printRectangle);
            if (Math.Abs(page.mapRotation) > 0.0001F) {
                PointF center = Geometry.RectCenter(page.mapRectangle);
                // Rotate source map coordinates around the utsnitt center before
                // applying the normal map-to-page transform.
                transform.RotateAt(page.mapRotation, center, MatrixOrder.Prepend);
            }
            PushRectangleClip(graphicsTarget, page.printRectangle);
            graphicsTarget.PushTransform(transform);
            // Determine the resolution in map coordinates.
            Matrix inverseTransform = transform.Clone();
            inverseTransform.Invert();
            float minResolutionPage = 100F / 2400F;  // Assume 2400 DPI as the base resolution, to get very accurate print.
            float minResolutionMap = Geometry.TransformDistance(minResolutionPage, inverseTransform);

            // And draw. Profile colour replacements live only for this render call.
            mapDisplay.MapColorOverrides = printProfileMapColorOverrides;
            try {
                mapDisplay.Draw(graphicsTarget, page.mapRectangle, minResolutionMap, null);
            }
            finally {
                mapDisplay.MapColorOverrides = null;
            }

            graphicsTarget.PopTransform();
            graphicsTarget.PopClip();
        }

        // Draw the optional information-only back of a physical course page.
        // Each logical course page uses its own print rectangle, keeping backsides
        // aligned with their corresponding map when two or four maps share a sheet.
        void DrawBacksideInfo(IGraphicsTarget graphicsTarget, CoursePage page)
        {
            CourseView courseView = CourseView.CreatePrintingCourseView(eventDB, page.courseDesignator);
            List<string> lines = BacksideInfoFormatter.GetLines(eventDB, courseView,
                                                               coursePdfSettings.BacksideText,
                                                               coursePdfSettings.BacksideInfoRecords);
            if (lines.Count == 0)
                return;

            float fontSize = Math.Min(36F, Math.Max(20F, page.printRectangle.Width / 20F));
            float lineHeight = fontSize * 1.45F;
            float contentHeight = lines.Count * lineHeight;
            float left = page.printRectangle.Left + Math.Min(30F, page.printRectangle.Width * 0.08F);
            float top = page.printRectangle.Top + Math.Max(20F, (page.printRectangle.Height - contentHeight) / 2F);
            object fontKey = new object();
            object headingFontKey = new object();
            object brushKey = new object();

            graphicsTarget.CreateFont(fontKey, "Arial", fontSize, TextEffects.Regular);
            graphicsTarget.CreateFont(headingFontKey, "Arial", fontSize * 1.18F, TextEffects.Bold);
            graphicsTarget.CreateSolidBrush(brushKey, CmykColor.FromCmyk(0, 0, 0, 0.42F));

            for (int index = 0; index < lines.Count; ++index) {
                object currentFont = index == 0 && !String.IsNullOrWhiteSpace(coursePdfSettings.BacksideText) ? headingFontKey : fontKey;
                graphicsTarget.DrawText(lines[index], currentFont, brushKey, new PointF(left, top + index * lineHeight));
            }
        }

        /// <summary>
        /// Applies the selected profile's course-colour rule to this export
        /// only. The source map and saved course appearance remain unchanged.
        /// </summary>
        /// <param name="ocadId">The course colour OCAD identifier.</param>
        /// <param name="cyan">The course colour cyan component.</param>
        /// <param name="magenta">The course colour magenta component.</param>
        /// <param name="yellow">The course colour yellow component.</param>
        /// <param name="black">The course colour black component.</param>
        /// <param name="overprint">Whether the course colour should overprint.</param>
        private void ApplyPrintProfileCourseColor(ref short ocadId, ref float cyan, ref float magenta, ref float yellow,
                                                  ref float black, ref bool overprint)
        {
            if (String.IsNullOrEmpty(coursePdfSettings.PrintProfileId))
                return;

            PrintProfile profile = PrintProfileCatalog.FindById(coursePdfSettings.PrintProfileId);
            if (profile == null)
                return;

            PrintProfileColorRule courseRule = BuiltInPrintProfiles.GetCourseColorRule(profile);
            if (courseRule == null)
                return;

            PrintProfileCmyk color = courseRule.EffectiveCmyk;
            cyan = color.Cyan / 100F;
            magenta = color.Magenta / 100F;
            yellow = color.Yellow / 100F;
            black = color.Black / 100F;
            overprint = courseRule.OverprintIntent == PrintProfileOverprintIntent.Overprint;
            if (courseRule.Identifier.OcadIds.Count == 1)
                ocadId = courseRule.Identifier.OcadIds[0];
        }

        /// <summary>Builds non-mutating base-map colour replacements from confirmed profile matches.</summary>
        /// <returns>Overrides keyed by the open vector map's concrete colour objects.</returns>
        private IDictionary<SymColor, MapColorOverride> CreatePrintProfileMapColorOverrides()
        {
            Dictionary<SymColor, MapColorOverride> overrides = new Dictionary<SymColor, MapColorOverride>();
            if (mapDisplay.MapType != MapType.OCAD || String.IsNullOrEmpty(coursePdfSettings.PrintProfileId))
                return overrides;

            PrintProfile profile = PrintProfileCatalog.FindById(coursePdfSettings.PrintProfileId);
            if (profile == null)
                return overrides;

            List<SymColor> mapColors = mapDisplay.GetMapColors();
            List<SourceMapColor> sourceColors = new List<SourceMapColor>();
            for (int index = 0; index < mapColors.Count; ++index)
                sourceColors.Add(SourceMapColor.FromSymColor(mapColors[index], index));

            ColorPreflightReport report = ColorPreflightReport.Analyze(profile, sourceColors,
                PdfExportCapabilities.CreateCurrentImplementation(), coursePdfSettings.ConfirmedPrintProfileRuleIds,
                coursePdfSettings.PrintProfileColorMappings);
            foreach (ColorPreflightRuleResult result in report.RuleResults) {
                if (!result.HasConfirmedMatch)
                    continue;

                SourceMapColor sourceColor = result.CandidateColors[0];
                int mapColorIndex = sourceColors.FindIndex(candidate => candidate.OcadId == sourceColor.OcadId
                    && candidate.DrawOrder == sourceColor.DrawOrder
                    && String.Equals(candidate.Name, sourceColor.Name, StringComparison.Ordinal));
                if (mapColorIndex < 0)
                    continue;

                PrintProfileCmyk replacement = result.Rule.EffectiveCmyk;
                overrides[mapColors[mapColorIndex]] = new MapColorOverride {
                    Color = CmykColor.FromCmyk(replacement.Cyan / 100F, replacement.Magenta / 100F,
                        replacement.Yellow / 100F, replacement.Black / 100F),
                    Overprint = result.Rule.OverprintIntent == PrintProfileOverprintIntent.Overprint,
                };
            }

            return overrides;
        }

        private void PushRectangleClip(IGraphicsTarget graphicsTarget, RectangleF rect)
        {
            object rectanglePath = new object();
            graphicsTarget.CreatePath(rectanglePath, new List<GraphicsPathPart> {
                new GraphicsPathPart(GraphicsPathPartKind.Start, new PointF[] { rect.Location }),
                new GraphicsPathPart(GraphicsPathPartKind.Lines, new PointF[] { new PointF(rect.Right, rect.Top), new PointF(rect.Right, rect.Bottom), new PointF(rect.Left, rect.Bottom), new PointF(rect.Left, rect.Top)}),
                new GraphicsPathPart(GraphicsPathPartKind.Close, new PointF[0])
            }, AreaFillMode.Winding);
            graphicsTarget.PushClip(rectanglePath);
        }
    }
}
