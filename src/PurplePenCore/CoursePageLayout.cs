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
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Diagnostics;


namespace PurplePen
{
    using PurplePen.Graphics2D;
    using PurplePen.MapModel;

    // Layout of a single page that is being printed. Might be all of a course or just part.
    public class CoursePage
    {
        public CourseDesignator courseDesignator;             // course to print
        public string description;           // description of course
        public RectangleF mapRectangle;      // rectangle to print in map coordinates
        public RectangleF printRectangle;     // rectangle to print to on page, in hundredth of inch.
        public float mapRotation;             // rotation of map rectangle in degrees.
        public bool landscape;                       // true if page should be printed in landscape orientation
        public PrintingPaperSize paperSize;            // the paper size for that page.
        public bool lastPageOfCourseOrPart;    // true if last page of a course or part of course (used for pausing printing)
    }

    // A physical output page containing one or more independently laid-out course pages.
    public class CoursePageSheet
    {
        public List<CoursePage> pages = new List<CoursePage>();
        public bool landscape;
        public PrintingPaperSize paperSize;
    }

    // Arranges independently laid-out course pages on physical output pages.
    public static class CoursePageSheetLayout
    {
        /// <summary>Returns the physical slot counts for a logical page set.</summary>
        /// <remarks>This lightweight planner is shared by the paper workshop
        /// preview so its sheet boundaries stay aligned with PDF production.</remarks>
        public static List<int> GetSheetSlotCounts(int logicalPageCount, CoursePdfSettings.PdfPageLayout pageLayout, int copies)
        {
            int pagesPerSheet = (int)pageLayout;
            if (pagesPerSheet != 1 && pagesPerSheet != 2 && pagesPerSheet != 4)
                throw new ArgumentOutOfRangeException(nameof(pageLayout));
            if (logicalPageCount < 0)
                throw new ArgumentOutOfRangeException(nameof(logicalPageCount));
            if (copies < 1)
                throw new ArgumentOutOfRangeException(nameof(copies));

            List<int> result = new List<int>();
            for (int copy = 0; copy < copies; ++copy) {
                for (int offset = 0; offset < logicalPageCount; offset += pagesPerSheet)
                    result.Add(Math.Min(pagesPerSheet, logicalPageCount - offset));
            }
            return result;
        }

        // Arrange pages in one-up, two-up, or four-up sheets while retaining
        // the configured map scale. Multi-up layouts crop map extents instead
        // of reducing the map artwork.
        public static List<CoursePageSheet> LayoutSheets(IEnumerable<CoursePage> pages, CoursePdfSettings.PdfPageLayout pageLayout)
        {
            return LayoutSheets(pages, pageLayout, 1);
        }

        // Arrange complete page sets repeatedly in one-up, two-up, or four-up sheets.
        public static List<CoursePageSheet> LayoutSheets(IEnumerable<CoursePage> pages, CoursePdfSettings.PdfPageLayout pageLayout, int copies)
        {
            int pagesPerSheet = (int)pageLayout;
            if (pagesPerSheet != 1 && pagesPerSheet != 2 && pagesPerSheet != 4)
                throw new ArgumentOutOfRangeException(nameof(pageLayout));
            if (copies < 1)
                throw new ArgumentOutOfRangeException(nameof(copies));

            List<CoursePageSheet> sheets = new List<CoursePageSheet>();
            List<CoursePage> logicalPages = new List<CoursePage>(pages);

            for (int copy = 0; copy < copies; ++copy) {
                // A copy is a complete logical page set. Do not let a partial
                // sheet from its predecessor absorb pages from this copy.
                CoursePageSheet currentSheet = null;
                foreach (CoursePage page in logicalPages) {
                    if (currentSheet == null || currentSheet.pages.Count == pagesPerSheet || !SamePaper(currentSheet, page)) {
                        currentSheet = new CoursePageSheet {
                            landscape = page.landscape,
                            paperSize = page.paperSize,
                        };
                        sheets.Add(currentSheet);
                    }

                    int slotIndex = currentSheet.pages.Count;
                    currentSheet.pages.Add(CreateSheetPage(page, pageLayout, slotIndex));
                }
            }

            return sheets;
        }

        // Test whether a page can share a physical sheet with existing pages.
        static bool SamePaper(CoursePageSheet sheet, CoursePage page)
        {
            return sheet.landscape == page.landscape &&
                   sheet.paperSize.SizeInHundreths.Width == page.paperSize.SizeInHundreths.Width &&
                   sheet.paperSize.SizeInHundreths.Height == page.paperSize.SizeInHundreths.Height;
        }

        // Copy a logical page into the requested sheet slot. The print area and
        // corresponding map area are reduced by the same amount, preserving the
        // map scale while automatically centering the cropped map view.
        static CoursePage CreateSheetPage(CoursePage page, CoursePdfSettings.PdfPageLayout pageLayout, int slotIndex)
        {
            SizeF pageSize = page.paperSize.SizeInHundreths;
            float sheetWidth = page.landscape ? pageSize.Height : pageSize.Width;
            float sheetHeight = page.landscape ? pageSize.Width : pageSize.Height;
            int columns, rows;
            GetGridDimensions(pageLayout, out columns, out rows);
            int column = slotIndex % columns;
            int row = slotIndex / columns;
            float horizontalScale = 1F / columns;
            float verticalScale = 1F / rows;

            CoursePage sheetPage = new CoursePage();
            sheetPage.courseDesignator = page.courseDesignator;
            sheetPage.description = page.description;
            sheetPage.mapRectangle = CropMapRectangle(page.mapRectangle, horizontalScale, verticalScale);
            sheetPage.landscape = page.landscape;
            sheetPage.paperSize = page.paperSize;
            sheetPage.lastPageOfCourseOrPart = page.lastPageOfCourseOrPart;
            sheetPage.printRectangle = new RectangleF(
                column * sheetWidth * horizontalScale + page.printRectangle.Left * horizontalScale,
                row * sheetHeight * verticalScale + page.printRectangle.Top * verticalScale,
                page.printRectangle.Width * horizontalScale,
                page.printRectangle.Height * verticalScale);
            sheetPage.mapRotation = page.mapRotation;
            return sheetPage;
        }

        // Gets the grid used by a requested multi-up layout. Two views occupy
        // the top and bottom halves of an A4 sheet (two A5 portrait fields).
        static void GetGridDimensions(CoursePdfSettings.PdfPageLayout pageLayout, out int columns, out int rows)
        {
            switch (pageLayout) {
                case CoursePdfSettings.PdfPageLayout.OnePerPage:
                    columns = 1;
                    rows = 1;
                    break;

                case CoursePdfSettings.PdfPageLayout.TwoPerPage:
                    columns = 1;
                    rows = 2;
                    break;

                case CoursePdfSettings.PdfPageLayout.FourPerPage:
                    columns = 2;
                    rows = 2;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(pageLayout));
            }
        }

        // Crops a map rectangle around its centre by the supplied proportions.
        static RectangleF CropMapRectangle(RectangleF mapRectangle, float horizontalScale, float verticalScale)
        {
            float width = mapRectangle.Width * horizontalScale;
            float height = mapRectangle.Height * verticalScale;
            return new RectangleF(mapRectangle.Left + (mapRectangle.Width - width) / 2F,
                                  mapRectangle.Top + (mapRectangle.Height - height) / 2F,
                                  width,
                                  height);
        }
    }

    // Class to layout the printing onto pages. Used for both printing and PDF creation.
    public class CoursePageLayout
    {
        // Encapsulate the layout of one dimension of a page layout.
#if TEST
        internal
#endif
        struct DimensionLayout
        {
            public float startMap;                   // start and length in the map in map coords.
            public float lengthMap;
            public float startPage;                 // start and length on the printed page, in hundreths of a inch
            public float lengthPage;

            public DimensionLayout(float startMap, float lengthMap, float startPage, float lengthPage)
            {
                this.startMap = startMap;
                this.lengthMap = lengthMap;
                this.startPage = startPage;
                this.lengthPage = lengthPage;
            }
        }

        private EventDB eventDB;
        private SymbolDB symbolDB;
        private Controller controller;
        private CourseAppearance appearance;
        private bool cropLargePrintArea;
        private float scaleCalibrationFactor;

        // mapDisplay is a MapDisplay that contains the correct map. All other features of the map display need to be customized.
        public CoursePageLayout(EventDB eventDB, SymbolDB symbolDB, Controller controller,  
                                CourseAppearance appearance, bool cropLargePrintArea)
            : this(eventDB, symbolDB, controller, appearance, cropLargePrintArea, 1.0F)
        {
        }

        /// <summary>Creates a layout with an optional printer calibration factor.</summary>
        public CoursePageLayout(EventDB eventDB, SymbolDB symbolDB, Controller controller,
                                CourseAppearance appearance, bool cropLargePrintArea, float scaleCalibrationFactor)
        {
            this.eventDB = eventDB;
            this.symbolDB = symbolDB;
            this.controller = controller;
            this.appearance = appearance;
            this.cropLargePrintArea = cropLargePrintArea;
            this.scaleCalibrationFactor = scaleCalibrationFactor > 0 ? scaleCalibrationFactor : 1.0F;
        }

        // Layout all the pages, return the total number of pages.
        public List<CoursePage> LayoutPages(IEnumerable<CourseDesignator> courseDesignators)
        {
            List<CoursePage> pages = new List<CoursePage>();

            // Go through each course and lay it out, then add to the page list.
            foreach (CourseDesignator courseDesignator in courseDesignators) {
                pages.AddRange(LayoutOptimizedCourse(courseDesignator));
                pages[pages.Count - 1].lastPageOfCourseOrPart = true;
            }

            return pages;
        }

        // Layout a single course onto one or more pages.
        // This used to automatically choose landscape or portrait, but no longer.
        List<CoursePage> LayoutOptimizedCourse(CourseDesignator courseDesignator)
        {
            return LayoutCourse(courseDesignator);
        }

        // Return the printable page area in 1/100 of an inch, given the page size, margins, and landscape.
        RectangleF GetPrintablePageArea(bool landscape, PrintingPaperSize paperSize, int margins)
        {
            if (landscape) {
                return new RectangleF(margins, margins, paperSize.SizeInHundreths.Height - (2 * margins), paperSize.SizeInHundreths.Width - (2 * margins));
            }
            else {
                return new RectangleF(margins, margins, paperSize.SizeInHundreths.Width - (2 * margins), paperSize.SizeInHundreths.Height - (2 * margins));
            }
        }

        // Layout a course onto one or more pages.
        List<CoursePage> LayoutCourse(CourseDesignator courseDesignator)
        {
            List<CoursePage> pageList = new List<CoursePage>();

            // Get the area of the map we want to print, in map coordinates, and the ratio between print scale and map scale.
            float scaleRatio;
            bool landscape;
            PrintingPaperSize paperSize;
            string description;
            int margins;
            float mapRotation;
            RectangleF mapArea = GetPrintAreaForCourse(courseDesignator, out landscape, out paperSize, out margins, out scaleRatio, out description, out mapRotation);

            // Get the available page size on the page.
            RectangleF printableArea = GetPrintablePageArea(landscape, paperSize, margins);
            SizeF pageSizeAvailable = printableArea.Size;

            // Layout both page dimensions, iterate through them to get all the pages we have.
            foreach (DimensionLayout verticalLayout in LayoutPageDimension(mapArea.Top, mapArea.Height, printableArea.Top, printableArea.Height, scaleRatio))
                foreach (DimensionLayout horizontalLayout in LayoutPageDimension(mapArea.Left, mapArea.Width, printableArea.Left, printableArea.Width, scaleRatio)) {
                    CoursePage page = new CoursePage();
                    page.courseDesignator = courseDesignator;
                    page.description = description;
                    page.landscape = landscape;
                    page.paperSize = paperSize;
                    page.mapRectangle = new RectangleF(horizontalLayout.startMap, verticalLayout.startMap, horizontalLayout.lengthMap, verticalLayout.lengthMap);
                    page.printRectangle = new RectangleF(horizontalLayout.startPage, verticalLayout.startPage, horizontalLayout.lengthPage, verticalLayout.lengthPage);
                    page.mapRotation = mapRotation;
                    pageList.Add(page);
                }

            return pageList;
        }

        // Lays out a page in one dimension, determine if it fits on one page or more than one page, and how the mapping goes.
        // Yields an enumeration of the page layouts in that dimensions.
#if TEST
        internal
#endif
 static IEnumerable<DimensionLayout> LayoutPageDimension(float mapStart, float mapLength, float printableAreaStart, float printableAreaLength, float scaleRatio)
        {
            // Map coordinates are in mm, so there are 0.2544 map units per page unit.
            float mmPerPageUnit = (0.254F * scaleRatio);

            // Figure out the length this map part will need on the page, in 1/100 of an inch, given the scale ratio.
            float pageLengthNeeded = mapLength / mmPerPageUnit;

            // If it fits in the printable area, just center it and a single page suffices. 1/1000 of an inch slop allowed for roundoff issues.
            if (pageLengthNeeded <= printableAreaLength + 0.1F) {
                float borderAmount = (printableAreaLength - pageLengthNeeded) / 2F;
                yield return new DimensionLayout(mapStart, mapLength, printableAreaStart + borderAmount, pageLengthNeeded);
            }
            else {
                // Doesn't fit on one page. How many pages will be needed?

                // The minimum amount of overlap is either 1 inch, or 1/6th of the printable area.
                float minOverlap = Math.Min(100F, printableAreaLength / 6);

                // How many pages?
                int numberOfPages = (int)Math.Ceiling((pageLengthNeeded - minOverlap) / (printableAreaLength - minOverlap));
                Debug.Assert(numberOfPages >= 2);

                // How much overlap will there be between pages (in page units)?
                float overlapPage = (numberOfPages * printableAreaLength - pageLengthNeeded) / (numberOfPages - 1);

                // And create the pages.
                float mapAdvance = (printableAreaLength - overlapPage) * mmPerPageUnit;
                for (int i = 0; i < numberOfPages; ++i) {
                    yield return new DimensionLayout(mapStart + i * mapAdvance, printableAreaLength * mmPerPageUnit, printableAreaStart, printableAreaLength);
                }
            }
        }

        // Get the printable size, scaled to map units(mm) and taking scaleRatio into account. To avoid some round-off problems, the size
        // is reduced by 0.001mm in each direction
        SizeF GetScaledPrintableSizeInMapUnits(RectangleF printableArea, float scaleRatio)
        {
            float mmPerPageUnit = (0.254F * scaleRatio);
            return new SizeF(printableArea.Width * mmPerPageUnit - 0.001F, printableArea.Height * mmPerPageUnit - 0.001F);
        }


        // Get the area of the map we want to print, in map coordinates, and the print scale.
        // if the courseId is None, do all controls.
        // If asked for, crop to a single page size.
        RectangleF GetPrintAreaForCourse(CourseDesignator courseDesignator, out bool landscape, out PrintingPaperSize paperSize, out int margins, out float scaleRatio, out string description, out float mapRotation)
        {
            // Get the course view to get the scale ratio.
            CourseView courseView = CourseView.CreatePositioningCourseView(eventDB, courseDesignator);
            scaleRatio = courseView.ScaleRatio;
            scaleRatio *= scaleCalibrationFactor;
            description = courseView.CourseFullName;

            RectangleF printRectangle = controller.GetCurrentPrintAreaRectangle(courseDesignator);
            PrintArea printArea = controller.GetCurrentPrintArea(courseDesignator);
            landscape = printArea.pageLandscape;
            paperSize = new PrintingPaperSize("Custom", printArea.pageWidth, printArea.pageHeight);
            margins = printArea.pageMargins;
            mapRotation = printArea.rotation;

            if (cropLargePrintArea) {
                // Crop the print area to a single page, portrait or landscape.
                // Try to keep CourseObjects in view as much as possible.
                CourseLayout layout = new CourseLayout();
                CourseFormatter.FormatCourseToLayout(symbolDB, courseView, appearance, layout, 0);
                RectangleF courseObjectsArea = layout.BoundingRect();
                courseObjectsArea.Intersect(printRectangle);

                // We may need to crop the print area to fit. 
                float areaCovered;
                RectangleF printableArea = GetPrintablePageArea(landscape, paperSize, margins);
                RectangleF croppedRectangle = CropPrintArea(printRectangle, courseObjectsArea, GetScaledPrintableSizeInMapUnits(printableArea, scaleRatio), out areaCovered);

                return croppedRectangle;
            }
            else {
                return printRectangle;
            }
        }

        // Find a crop of printArea that includes as much of courseObjectsArea as possible, that is of the printableSize;
#if TEST
        internal
#endif
 static RectangleF CropPrintArea(RectangleF printArea, RectangleF courseObjectsArea, SizeF printableSize, out float areaCovered)
        {
            float left, top, right, bottom;

            if (printableSize.Width >= printArea.Width) {
                left = printArea.Left; right = printArea.Right;
            }
            else {
                // Center on courseObjectsArea as much as possible, while staying fully inside printArea.
                left = (courseObjectsArea.Left + courseObjectsArea.Right) / 2 - printableSize.Width / 2;
                right = (courseObjectsArea.Left + courseObjectsArea.Right) / 2 + printableSize.Width / 2;
                if (left < printArea.Left) {
                    right += (printArea.Left - left); left = printArea.Left;
                }
                else if (right > printArea.Right) {
                    left -= (right - printArea.Right); right = printArea.Right;
                }
            }

            if (printableSize.Height >= printArea.Height) {
                top = printArea.Top; bottom = printArea.Bottom;
            }
            else {
                // Center on courseObjectsArea as much as possible, while staying fully inside printArea.
                top = (courseObjectsArea.Top + courseObjectsArea.Bottom) / 2 - printableSize.Height / 2;
                bottom = (courseObjectsArea.Top + courseObjectsArea.Bottom) / 2 + printableSize.Height / 2;
                if (top < printArea.Top) {
                    bottom += (printArea.Top - top); top = printArea.Top;
                }
                else if (bottom > printArea.Bottom) {
                    top -= (bottom - printArea.Bottom); bottom = printArea.Bottom;
                }
            }

            // Create the rectangle.
            RectangleF result = RectangleF.FromLTRB(left, top, right, bottom);

            // Calculate the intersected area.
            RectangleF intersect = result;
            intersect.Intersect(courseObjectsArea);
            areaCovered = intersect.Height * intersect.Width;

            return result;
        }
    }
}
