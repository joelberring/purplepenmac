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
using System.Text;
using System.Xml;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Linq;
using PurplePen.Graphics2D;

namespace PurplePen
{
    public sealed class Reports : IDisposable
    {
        StringWriter stringWriter;
        XmlTextWriter xmlTextWriter;
        string[] tableColumnClasses;  // classes to apply to each column in the table; set by BeginTable.
        int currentColumn; // current column in the current row, used to apply column classes to cells.

        // Dispose managed resources.
        public void Dispose()
        {
            xmlTextWriter?.Dispose();
            xmlTextWriter = null;
            stringWriter?.Dispose();
            stringWriter = null;
        }

        // Initialize the xml text writer for creating a new report.
        void InitReport()
        {
            stringWriter = new StringWriter();
            xmlTextWriter = new XmlTextWriter(stringWriter);
            xmlTextWriter.Formatting = Formatting.Indented;

            xmlTextWriter.WriteStartDocument(true);
            xmlTextWriter.WriteStartElement("body");
        }

        // Finishes the report and returns the string.
        string FinishReport()
        {
            xmlTextWriter.WriteFullEndElement();
            xmlTextWriter.WriteEndDocument();
            xmlTextWriter.Close();
            string report = stringWriter.ToString();
            int bodyStart = report.IndexOf("<body>", StringComparison.InvariantCulture) + 6;
            int bodyEnd = report.LastIndexOf("</body>", StringComparison.InvariantCulture);
            return report.Substring(bodyStart, bodyEnd - bodyStart) + "\r\n";
        }

        void WriteClassAttribute(string kind)
        {
            if (! string.IsNullOrEmpty(kind))
                xmlTextWriter.WriteAttributeString("class", kind.Trim());
        }

        void WriteH1(string content)
        {
            xmlTextWriter.WriteElementString("h1", content);
        }

        void WriteH2(string content)
        {
            xmlTextWriter.WriteElementString("h2", content);
        }

        void WriteH3(string content)
        {
            xmlTextWriter.WriteElementString("h3", content);
        }

        void StartPara(string kind)
        {
            xmlTextWriter.WriteStartElement("p");
            WriteClassAttribute(kind);
        }

        void StartPara()
        {
            StartPara("");
        }

        private void WriteText(string content)
        {
            xmlTextWriter.WriteString(content);
        }

        private void WriteStyledText(string content, TextEffects textEffects)
        {
            int closeElements = 0;         // number of close elements needed.

            if ((textEffects & TextEffects.Bold) != 0) {
                xmlTextWriter.WriteStartElement("strong");
                ++closeElements;
            }

            if ((textEffects & TextEffects.Italic) != 0) {
                xmlTextWriter.WriteStartElement("em");
                ++closeElements;
            }

            if ((textEffects & TextEffects.Underline) != 0) {
                xmlTextWriter.WriteStartElement("u");
                ++closeElements;
            }

            if ((textEffects & TextEffects.Strikeout) != 0) {
                xmlTextWriter.WriteStartElement("strike");
                ++closeElements;
            }

            xmlTextWriter.WriteString(content);

            for (int i = 0; i < closeElements; ++i)
                xmlTextWriter.WriteEndElement();
        }
            

        void EndPara()
        {
            xmlTextWriter.WriteEndElement();
        }

        void WritePara(string kind, string content)
        {
            StartPara(kind);
            WriteText(content);
            EndPara();
        }

        void WritePara(string content)
        {
            WritePara("", content);
        }

        void BeginTable(string kind, int cols, params string[] colKinds)
        {
            xmlTextWriter.WriteStartElement("table");
            WriteClassAttribute(kind);

            // We used to use "col" elements to set classes for table columns, but that was only
            // supported by Internet Explorer. For modern browsers, we set the column classes on each cell instead,
            // and save them here in tableColumnClasses to apply to each cell in the column.

            tableColumnClasses = new string[cols];

            for (int col = 0; col < cols; ++col) {
                string colClass;

                // The leftcol/rightcol/middlecol class is set automatically from the column position in the table.
                if (col == 0)
                    colClass = "leftcol";
                else if (col == cols - 1)
                    colClass = "rightcol";
                else
                    colClass = "middlecol";

                if (col < colKinds.Length)
                    colClass += " " + colKinds[col];
                
                tableColumnClasses[col] = colClass;
            }

            currentColumn = 0;
        }

        void BeginTable(int cols)
        {
            BeginTable("", cols);
        }

        void EndTable()
        {
            xmlTextWriter.WriteEndElement();
        }

        // Write thead tag.
        void BeginTableHead()
        {
            xmlTextWriter.WriteStartElement("thead");
        }

        // End thead element
        void EndTableHead()
        {
            xmlTextWriter.WriteFullEndElement();
        }

        // Write tbody tag.
        void BeginTableBody()
        {
            xmlTextWriter.WriteStartElement("tbody");
        }

        // End tbody element
        void EndTableBody()
        {
            xmlTextWriter.WriteFullEndElement();
        }

        void BeginTableRow(string kind)
        {
            xmlTextWriter.WriteStartElement("tr");
            WriteClassAttribute(kind);
            currentColumn = 0;
        }

        void BeginTableRow()
        {
            BeginTableRow("");
        }

        void EndTableRow()
        {
            xmlTextWriter.WriteEndElement();
            currentColumn = 0;
        }

        void WriteTableHeaderCell(string cellContent)
        {
            WriteTableHeaderCell("", cellContent);
        }

        void WriteTableHeaderCell(string kind, string cellContent)
        {
            Debug.Assert(currentColumn < tableColumnClasses.Length, "Wrote more columns than indicated in BeginTable");

            string cellClass = kind + " " + ((currentColumn < tableColumnClasses.Length) ? tableColumnClasses[currentColumn] : "");

            xmlTextWriter.WriteStartElement("th");
            WriteClassAttribute(cellClass);
            xmlTextWriter.WriteString(cellContent);
            xmlTextWriter.WriteEndElement();

            ++currentColumn;
        }

        void WriteTableCell(string cellContent)
        {
            WriteTableCell("", cellContent);
        }

        void WriteTableCell(string kind, string cellContent)
        {
            string cellClass = kind + " " + tableColumnClasses[currentColumn];

            xmlTextWriter.WriteStartElement("td");
            WriteClassAttribute(cellClass);
            xmlTextWriter.WriteString(cellContent);
            xmlTextWriter.WriteEndElement();

            ++currentColumn;
        }

        void WriteSpannedTableCell(string kind, int cellsAcross, string cellContent)
        {
            string cellClass = kind + " " + tableColumnClasses[currentColumn];

            xmlTextWriter.WriteStartElement("td");
            WriteClassAttribute(cellClass);
            xmlTextWriter.WriteAttributeString("colspan", XmlConvert.ToString(cellsAcross));
            xmlTextWriter.WriteString(cellContent);
            xmlTextWriter.WriteEndElement();

            currentColumn += cellsAcross;
        }

        void WriteSpannedTableCell(int cellsAcross, string cellContent)
        {
            WriteSpannedTableCell("", cellsAcross, cellContent);
        }
            
        void WriteTableRow(params string[] cells)
        {
            BeginTableRow();

            foreach (string cell in cells)
                WriteTableCell(cell);

            EndTableRow();
        }

        void WriteTableHeaderRow(params string[] cells)
        {
            BeginTableHead();
            BeginTableRow();

            foreach (string cell in cells)
                WriteTableHeaderCell(cell);

            EndTableRow();
            EndTableHead();
        }

        public string CreateCourseSummaryReport(EventDB eventDB)
        {
            InitReport();

            // Header.
            WriteH1(string.Format(ReportText.CourseSummary_Title, QueryEvent.GetEventTitle(eventDB, " ")));

            // Table of all courses.
            BeginTable("", 4, "leftalign", "rightalign", "rightalign", "rightalign");
            WriteTableHeaderRow(ReportText.ColumnHeader_Course, ReportText.ColumnHeader_Controls, ReportText.ColumnHeader_Length, ReportText.ColumnHeader_Climb);

            BeginTableBody();

            // Enumerate all courses.
            Id<Course>[] courseIds = QueryEvent.SortedCourseIds(eventDB, false);

            // Write row for each course.
            foreach (Id<Course> courseId in courseIds) {
                CreateSummaryRow(eventDB, new CourseDesignator(courseId));

                if (QueryEvent.HasVariations(eventDB, courseId)) {
                    foreach (VariationInfo variationInfo in QueryEvent.GetAllVariations(eventDB, courseId)) {
                        CreateSummaryRow(eventDB, new CourseDesignator(courseId, variationInfo));
                    }
                }
            }

            EndTableBody();

            EndTable();

            return FinishReport();
        }

        void CreateSummaryRow(EventDB eventDB, CourseDesignator designator)
        {
            BeginTableRow();
            CourseView courseView = CourseView.CreateViewingCourseView(eventDB, designator);

            // Get the name, indenting variations.
            string name;
            if (designator.IsVariation) {
                name = "\u00a0\u00a0\u00a0\u00a0" + designator.VariationInfo.CodeString;
            }
            else {
                name = courseView.CourseName;
            }

            // Course name
            WriteTableCell(name);

            // # of normal controls
            WriteTableCell(Util.RangeIfNeeded(courseView.MinNormalControls, courseView.MaxNormalControls));

            // Length (empty for score course)
            if (courseView.Kind == CourseView.CourseViewKind.Score)
                WriteTableCell("");
            else
                WriteTableCell(Util.GetLengthInKm(courseView.MinTotalLength, courseView.MaxTotalLength, 1));

            // Climb (empty for score course or no climb defined)
            if (courseView.Kind == CourseView.CourseViewKind.Score || courseView.TotalClimb < 0)
                WriteTableCell("");
            else
                WriteTableCell(Convert.ToString(Math.Round(courseView.TotalClimb / 5, MidpointRounding.AwayFromZero) * 5.0) + " m");

            EndTableRow();
        }

        public string CreateLoadReport(EventDB eventDB)
        {
            InitReport();
            GatherControlLoads(eventDB);

            // Header.
            WriteH1(string.Format(ReportText.Load_Title, QueryEvent.GetEventTitle(eventDB, " ")));

            if (! QueryEvent.AllCoursesHaveLoads(eventDB)) {
                // Some or all courses don't have loads set. Warn.
                StartPara();
                WriteStyledText(ReportText.Load_Warning, TextEffects.Bold);
                WriteText(" ");
                WriteText(ReportText.Load_MissingLoads);
                EndPara();
            }

            if (QueryEvent.AnyCourseHasVariations(eventDB)) {
                // Some courses have variations. Give note.
                StartPara();
                WriteStyledText(ReportText.Note, TextEffects.Bold);
                WriteText(" ");
                WriteText(ReportText.Load_VariationsExist);
                EndPara();
            }

            bool multiVisit = loadInfos.Any(li => li.visits > li.load);

            if (multiVisit) {
                // Some courses have multi-visit controls.
                StartPara();
                WriteStyledText(ReportText.Note, TextEffects.Bold);
                WriteText(" ");
                WriteText(ReportText.Load_ButterflyExists);
                EndPara();
            }

            // Section 1: Control load
            WriteH2(ReportText.Load_ControlLoadSection);
            WriteControlLoadSection(eventDB, multiVisit);

            // Section 2: Leg load
            WriteH2(ReportText.Load_LegLoadSection);
            WriteLegLoadSection(eventDB);

            return FinishReport();
        }

        struct ControlLoadInfo
        {
            public Id<ControlPoint> controlId;
            public string controlName;
            public int numCourses;
            public int load;
            public int visits;
        }

        List<ControlLoadInfo> loadInfos;

        void GatherControlLoads(EventDB eventDB)
        {
            loadInfos = new List<ControlLoadInfo>();

            // Get load information about each control.
            foreach (Id<ControlPoint> controlId in eventDB.AllControlPointIds) {
                ControlPoint control = eventDB.GetControl(controlId);

                if (control.kind != ControlPointKind.Normal)
                    continue;     // only list normal controls.

                ControlLoadInfo loadInfo = new ControlLoadInfo();

                loadInfo.controlId = controlId;
                loadInfo.controlName = Util.ControlPointName(eventDB, controlId, NameStyle.Medium);
                loadInfo.numCourses = QueryEvent.CoursesUsingControl(eventDB, controlId, false).Length;
                loadInfo.load = QueryEvent.GetControlLoad(eventDB, controlId);
                loadInfo.visits = QueryEvent.GetControlVisitLoad(eventDB, controlId);

                loadInfos.Add(loadInfo);
            }

            // Sort the load information, first by load, then by number of courses, then by name.
            loadInfos.Sort(delegate (ControlLoadInfo loadInfo1, ControlLoadInfo loadInfo2) {
                if (loadInfo1.load < loadInfo2.load) return 1;
                else if (loadInfo1.load > loadInfo2.load) return -1;

                if (loadInfo1.visits < loadInfo2.visits) return 1;
                else if (loadInfo1.visits > loadInfo2.visits) return -1;

                if (loadInfo1.numCourses < loadInfo2.numCourses) return 1;
                else if (loadInfo1.numCourses > loadInfo2.numCourses) return -1;

                int result = Util.CompareCodes(loadInfo1.controlName, loadInfo2.controlName);
                if (result != 0)
                    return result;

                return loadInfo1.controlId.id.CompareTo(loadInfo2.controlId.id);
            });


        }

        void WriteControlLoadSection(EventDB eventDB, bool multiVisits)
        {
            // Write the table.
            if (multiVisits) {
                BeginTable("", 4, "leftalign", "rightalign", "rightalign", "rightalign");
                WriteTableHeaderRow(ReportText.ColumnHeader_Control, ReportText.ColumnHeader_NumberOfCourses, ReportText.ColumnHeader_Load, ReportText.ColumnHeader_Visits);
            }
            else {
                BeginTable("", 3, "leftalign", "rightalign", "rightalign");
                WriteTableHeaderRow(ReportText.ColumnHeader_Control, ReportText.ColumnHeader_NumberOfCourses, ReportText.ColumnHeader_Load);
            }

            BeginTableBody();

            foreach (ControlLoadInfo loadInfo in loadInfos) {
                string loadString = loadInfo.load >= 0 ? Convert.ToString(loadInfo.load) : "";
                string visitString = loadInfo.visits >= 0 ? Convert.ToString(loadInfo.visits) : "";

                if (multiVisits) {
                    WriteTableRow(loadInfo.controlName, Convert.ToString(loadInfo.numCourses), loadString, visitString);
                }
                else {
                    WriteTableRow(loadInfo.controlName, Convert.ToString(loadInfo.numCourses), loadString);
                }
            }

            EndTableBody();
            EndTable();
        }

        struct LegLoadInfo
        {
            public Id<ControlPoint> controlId1, controlId2;
            public string text;
            public int numCourses;
            public int load;
        }

        void WriteLegLoadSection(EventDB eventDB)
        {
            // Maps legs to load infos, so we only process each leg once.
            Dictionary<Pair<Id<ControlPoint>, Id<ControlPoint>>, LegLoadInfo> loadInfos = new Dictionary<Pair<Id<ControlPoint>, Id<ControlPoint>>, LegLoadInfo>();

            // Get load information about each leg. To enumerate all legs, just enumerate all courses and all legs on each course.
            foreach (Id<Course> courseId in eventDB.AllCourseIds) {
                foreach (QueryEvent.LegInfo leg in QueryEvent.EnumLegs(eventDB, new CourseDesignator(courseId))) {
                    Id<ControlPoint> controlId1 = eventDB.GetCourseControl(leg.courseControlId1).control;
                    Id<ControlPoint> controlId2 = eventDB.GetCourseControl(leg.courseControlId2).control;
                    Pair<Id<ControlPoint>, Id<ControlPoint>> key = new Pair<Id<ControlPoint>, Id<ControlPoint>>(controlId1, controlId2);

                    if (!loadInfos.ContainsKey(key)) {
                        // This leg hasn't been processed yet. Process it.
                        LegLoadInfo loadInfo = new LegLoadInfo();
                        loadInfo.controlId1 = controlId1;
                        loadInfo.controlId2 = controlId2;
                        loadInfo.text = string.Format("{0}\u2013{1}", Util.ControlPointName(eventDB, controlId1, NameStyle.Medium), Util.ControlPointName(eventDB, controlId2, NameStyle.Medium));
                        loadInfo.numCourses = QueryEvent.CoursesUsingLeg(eventDB, controlId1, controlId2, false).Length;
                        loadInfo.load = QueryEvent.GetLegLoad(eventDB, controlId1, controlId2);

                        loadInfos.Add(key, loadInfo);
                    }
                }
            }

            // Remove legs used only once.
            List<LegLoadInfo> loadInfoList = new List<LegLoadInfo>(loadInfos.Values);
            loadInfoList = loadInfoList.FindAll(delegate(LegLoadInfo loadInfo) { return loadInfo.numCourses > 1; });

            // Sort the list of legs, first by load, then by number of courses, then by text (so tests are consistent).
            loadInfoList.Sort(delegate(LegLoadInfo loadInfo1, LegLoadInfo loadInfo2) {
                if (loadInfo1.load < loadInfo2.load) return 1;
                else if (loadInfo1.load > loadInfo2.load) return -1;

                if (loadInfo1.numCourses < loadInfo2.numCourses) return 1;
                else if (loadInfo1.numCourses > loadInfo2.numCourses) return -1;

                return loadInfo1.text.CompareTo(loadInfo2.text);
            });

            // Write the table.
            WritePara(ReportText.Load_OnlyLegsMoreThanOnce);

            BeginTable("", 3, "leftalign", "rightalign", "rightalign");
            WriteTableHeaderRow(ReportText.ColumnHeader_Leg, ReportText.ColumnHeader_NumberOfCourses, ReportText.ColumnHeader_Load);

            BeginTableBody();

            foreach (LegLoadInfo loadInfo in loadInfoList) {
                WriteTableRow(loadInfo.text,
                                        Convert.ToString(loadInfo.numCourses),
                                        loadInfo.load >= 0 ? Convert.ToString(loadInfo.load) : "");
            }

            EndTableBody();
            EndTable();
        }

        public string CreateCrossReferenceReport(EventDB eventDB)
        {
            InitReport();

            // Header.
            WriteH1(string.Format(ReportText.CrossRef_Title, QueryEvent.GetEventTitle(eventDB, " ")));

            Id<ControlPoint>[] controlsToXref = GetControlIdsToXref(eventDB);
            Id<Course>[] coursesToXref = QueryEvent.SortedCourseIds(eventDB, false);
            string[,] xref = CreateXref(eventDB, controlsToXref, coursesToXref);

            string[] classes = new string[coursesToXref.Length + 1];
            classes[0] = "leftalign";
            for (int i = 1; i < classes.Length; ++i)
                classes[i] = "rightalign";

            BeginTable("", classes.Length, classes);

            // Write the header row.
            BeginTableHead();
            BeginTableRow();
            WriteTableHeaderCell(ReportText.ColumnHeader_Control);
            for (int i = 0; i < coursesToXref.Length; ++i)
                WriteTableHeaderCell(eventDB.GetCourse(coursesToXref[i]).name);
            EndTableRow();
            EndTableHead();

            // Write the cross-reference rows. Table rule after every 3rd line
            BeginTableBody();
            for (int row = 0; row < controlsToXref.Length; ++row) {
                bool tablerule = (row % 3 == 2);
                BeginTableRow();
                WriteTableCell(tablerule ? "tablerule" : "", eventDB.GetControl(controlsToXref[row]).code);
                for (int col = 0; col < coursesToXref.Length; ++col)
                    WriteTableCell(tablerule ? "tablerule" : "", xref[row, col]);
                EndTableRow();
            }
            EndTableBody();

            EndTable();

            return FinishReport();
        }

        // Create the cross-reference between controls and courses.
        private string[,] CreateXref(EventDB eventDB, Id<ControlPoint>[] controlsToXref, Id<Course>[] coursesToXref)
        {
            SortedSet<int>[,] xref = new SortedSet<int>[controlsToXref.Length, coursesToXref.Length];

            // Go through each course, each variation of the course, and cross-reference.
            for (int col = 0; col < coursesToXref.Length; ++col) {
                foreach (CourseDesignator courseDesignator in AllDesignatorsForCourse(eventDB, coursesToXref[col])) {
                    CourseView view = CourseView.CreateViewingCourseView(eventDB, courseDesignator);

                    foreach (CourseView.ControlView controlView in view.ControlViews) {
                        int row = Array.IndexOf(controlsToXref, controlView.controlId);

                        if (row >= 0) {
                            if (xref[row, col] == null)
                                xref[row, col] = new SortedSet<int>();
                            xref[row, col].Add(controlView.ordinal);
                        }
                    }
                }
            }

            string[,] xrefString = new string[controlsToXref.Length, coursesToXref.Length];

            for (int col = 0; col < coursesToXref.Length; ++col) {
                for (int row = 0; row < controlsToXref.Length; ++row) {
                    xrefString[row, col] = XrefString(xref[row, col]);
                }
            }

            return xrefString;
        }

        // Convert set of ordinals to command seperated string. negative goes to "*".
        private string XrefString(SortedSet<int> set)
        {
            if (set == null || set.Count == 0)
                return "";
            else
                return String.Join(",", set.Select(i => (i < 0) ? "*" : i.ToString()));
        }

        private List<CourseDesignator> AllDesignatorsForCourse(EventDB eventDB, Id<Course> courseId)
        {
            List<CourseDesignator> result = new List<CourseDesignator>();

            if (QueryEvent.HasVariations(eventDB, courseId)) {
                foreach (VariationInfo variationInfo in QueryEvent.GetAllVariations(eventDB, courseId)) {
                    result.Add(new CourseDesignator(courseId, variationInfo));
                }
            }
            else {
                result.Add(new CourseDesignator(courseId));
            }

            return result;
        }


        // Get all the control IDs to cross-ref, in the correct order.
        private Id<ControlPoint>[] GetControlIdsToXref(EventDB eventDB)
        {
            // Only cross-ref regular controls. Then sort by code.
            List<Id<ControlPoint>> list = new List<Id<ControlPoint>>();

            foreach (Id<ControlPoint> controlId in eventDB.AllControlPointIds) {
                if (eventDB.GetControl(controlId).kind == ControlPointKind.Normal)
                    list.Add(controlId);
            }

            list.Sort(delegate(Id<ControlPoint> id1, Id<ControlPoint> id2) {
                ControlPoint control1 = eventDB.GetControl(id1), control2 = eventDB.GetControl(id2);
                return Util.CompareCodes(control1.code, control2.code);
            });

            return list.ToArray();
        }

        private float WriteLegLengthRow(EventDB eventDB, CourseView courseView, int controlIndex, int legIndex, int legNumber)
        {
            float distance = 0;

            int firstIndex = controlIndex;
            int nextIndex;

            for (;;) {
                CourseView.ControlView controlView = courseView.ControlViews[controlIndex];
                ControlPointKind kind = eventDB.GetControl(controlView.controlId).kind;
                nextIndex = controlView.legTo[legIndex];
                distance += controlView.legLength[legIndex];

                // Continue through crossing points that have a unique next control.
                if (nextIndex < 0)
                    break;
                if (eventDB.GetControl(courseView.ControlViews[nextIndex].controlId).kind != ControlPointKind.CrossingPoint)
                    break;
                if (courseView.ControlViews[nextIndex].legTo == null)
                    break;
                if (courseView.ControlViews[nextIndex].legTo.Length != 1)
                    break;

                controlIndex = nextIndex;
                legIndex = 0;
            }

            if (distance > 0 && nextIndex >= 0) {
                Id<ControlPoint> firstControlId = courseView.ControlViews[firstIndex].controlId;
                Id<ControlPoint> secondControlId = courseView.ControlViews[nextIndex].controlId;

                string legText = string.Format("{0}\u2013{1}", Util.ControlPointName(eventDB, firstControlId, NameStyle.Medium), Util.ControlPointName(eventDB, secondControlId, NameStyle.Medium));
                string legNumberText = courseView.Kind == CourseView.CourseViewKind.Normal ? Convert.ToString(legNumber) : "";
                WriteTableRow(legNumberText, legText, string.Format("{0} m", Math.Round(distance)));

            }
            return distance;
        }

        private void WriteLegLengthTable(EventDB eventDB, CourseView courseView)
        {
            BeginTable("", 3, "leftalign", "leftalign", "rightalign");
            WriteTableHeaderRow(ReportText.ColumnHeader_Leg, ReportText.ColumnHeader_Controls, ReportText.ColumnHeader_Length);

            BeginTableBody();

            int legNumber = 1;
            float totalLength = 0;

            for (int controlIndex = 0; controlIndex < courseView.ControlViews.Count; ++controlIndex) {
                CourseView.ControlView controlView = courseView.ControlViews[controlIndex];
                if (controlView.legTo != null) {
                    for (int legIndex = 0; legIndex < controlView.legTo.Length; ++legIndex) {
                        if (eventDB.GetControl(controlView.controlId).kind != ControlPointKind.CrossingPoint ||
                            controlView.legTo.Length > 1) 
                        {
                            float distance = WriteLegLengthRow(eventDB, courseView, controlIndex, legIndex, legNumber);
                            if (distance > 0) {
                                totalLength += distance;
                                legNumber += 1;
                            }
                        }
                    }
                }
            }

            // Write average row
            if (legNumber > 1) {
                BeginTableRow("summaryrow");
                WriteSpannedTableCell(2, ReportText.LegLength_Average);
                WriteTableCell(string.Format("{0} m", Convert.ToString(Math.Round(totalLength / (float) (legNumber - 1)))));
                EndTableRow();
            }

            EndTableBody();

            EndTable();
        }

        public string CreateLegLengthReport(EventDB eventDB)
        {
            InitReport();

            // Header.
            WriteH1(string.Format(ReportText.LegLength_Title, QueryEvent.GetEventTitle(eventDB, " ")));

            // Enumerate all courses.
            Id<Course>[] courseIds = QueryEvent.SortedCourseIds(eventDB, false);

            // Write row for each course.
            foreach (Id<Course> courseId in courseIds) {
                CourseView courseView = CourseView.CreateViewingCourseView(eventDB, new CourseDesignator(courseId));

                // Don't include score courses in the leg length report.
                if (courseView.Kind == CourseView.CourseViewKind.Score)
                    continue;

                // Heading string for course
                string headerLine;
                if (courseView.TotalClimb < 0)
                    headerLine = string.Format(ReportText.LegLength_CourseInfoNoClimb, courseView.CourseName, Util.RangeIfNeeded(courseView.MinNormalControls, courseView.MaxNormalControls), Util.GetLengthInKm(courseView.MinTotalLength, courseView.MaxTotalLength, 1));
                else
                    headerLine = string.Format(ReportText.LegLength_CourseInfo,  courseView.CourseName, Util.RangeIfNeeded(courseView.MinNormalControls, courseView.MaxNormalControls), Util.GetLengthInKm(courseView.MinTotalLength, courseView.MaxTotalLength, 1), Math.Round(courseView.TotalClimb / 5, MidpointRounding.AwayFromZero) * 5.0);
                WriteH2(headerLine);

                WriteLegLengthTable(eventDB, courseView);
            }

            return FinishReport();
        }

        // A struct to list nearby controls.
        private struct NearbyControls {
            public Id<ControlPoint> controlId1, controlId2;
            public float distance;        // straight line distance between controls
            public bool sameSymbol;     // same symbol in column D?
        }

        // Return the symbol id in column D for a control, or null if none.
        private string SymbolOfControl(ControlPoint control)
        {
            string symbol = null;
            if (control.symbolIds != null && control.symbolIds.Length >= 2)
                symbol = control.symbolIds[1];
            if (symbol == "")
                symbol = null;
            return symbol;
        }

        // Return a list of all controls that are nearby each other. It is sorted in order of distance.
        private List<NearbyControls> FindNearbyControls(EventDB eventDB, float distanceLimit)
        {
            ICollection<Id<ControlPoint>> allPoints = eventDB.AllControlPointIds;
            List<NearbyControls> list = new List<NearbyControls>();

            // Go through every pair of normal controls. If the distance between them is less than the distance limit, add to the list.
            foreach (Id<ControlPoint> controlId1 in allPoints) {
                ControlPoint control1 = eventDB.GetControl(controlId1);
                if (control1.kind != ControlPointKind.Normal)
                    continue;     // only deal with normal points.
                string symbol1 = SymbolOfControl(control1);

                // Check all other controls with greater ids (so each pair considered only once)
                foreach (Id<ControlPoint> controlId2 in allPoints) {
                    ControlPoint control2 = eventDB.GetControl(controlId2);
                    if (control2.kind != ControlPointKind.Normal || controlId2.id <= controlId1.id)
                        continue;     // only deal with normal points with greater id.
                    string symbol2 = SymbolOfControl(control2);

                    float distance = QueryEvent.ComputeStraightLineControlDistance(eventDB, controlId1, controlId2);

                    if (distance < distanceLimit) {
                        NearbyControls nearbyControls;
                        nearbyControls.controlId1 = controlId1;
                        nearbyControls.controlId2 = controlId2;
                        nearbyControls.distance = distance;
                        nearbyControls.sameSymbol = (symbol1 != null && symbol2 != null && symbol1 == symbol2); // only same symbol if both have symbols!
                        list.Add(nearbyControls);
                    }
                }
            }

            // Sort the list by distance.
            list.Sort((x1, x2) => x1.distance.CompareTo(x2.distance));

            return list;
        }

        // Get list of unused controls, sorted
        List<Id<ControlPoint>> SortedUnusedControls(EventDB eventDB)
        {
            List<Id<ControlPoint>> unusedControls = QueryEvent.ControlsUnusedInCourses(eventDB, false);
            unusedControls.Sort((id1, id2) => QueryEvent.CompareControlIds(eventDB, id1, id2));

            return unusedControls;
        }

        // Describes a missing thing. Not every field is used in every kind.
        struct MissingThing
        {
            public Id<ControlPoint> controlId;
            public Id<Course> courseId;
            public string what;
            public string why;

            public MissingThing(Id<ControlPoint> controlId, string what, string why)
            {
                this.controlId = controlId;
                this.courseId = Id<Course>.None;
                this.what = what;
                this.why = why;
            }

            public MissingThing(Id<Course> courseId, string what, string why)
            {
                this.controlId = Id<ControlPoint>.None;
                this.courseId = courseId;
                this.what = what;
                this.why = why;
            }

            public MissingThing(Id<ControlPoint> controlId, string what)
            {
                this.controlId = controlId;
                this.courseId = Id<Course>.None;
                this.what = what;
                this.why = null;
            }

            public MissingThing(Id<Course> courseId, Id<ControlPoint> controlId, string what)
            {
                this.courseId = courseId;
                this.controlId = controlId;
                this.what = what;
                this.why = null;
            }
        }

        // Add missing things from a regular course.
        void AddMissingThingsInRegularCourse(EventDB eventDB, Id<Course> courseId, List<MissingThing> list)
        {
            if (! QueryEvent.HasStartControl(eventDB, courseId))
                list.Add(new MissingThing(courseId, "Start", ReportText.EventAudit_MissingStart)); // UNDONE: need resource for this text

            if (!QueryEvent.HasFinishControl(eventDB, courseId))
                list.Add(new MissingThing(courseId, "Finish", ReportText.EventAudit_MissingFinish)); // UNDONE: need resource for this text

            if (eventDB.GetCourse(courseId).climb < 0)
                list.Add(new MissingThing(courseId, ReportText.ColumnHeader_Climb, ReportText.EventAudit_MissingClimb));
        }

        // Add missing things from a score course.
        void AddMissingThingsInScoreCourse(EventDB eventDB, Id<Course> courseId, List<MissingThing> list)
        {
            if (!QueryEvent.HasStartControl(eventDB, courseId))
                list.Add(new MissingThing(courseId, "Start", ReportText.EventAudit_MissingStartScore));  // UNDONE: need resource for this text
        }

        // Get missing things in courses. Sorted in correct course sort order.
        List<MissingThing> MissingCourseThings(EventDB eventDB)
        {
            List<MissingThing> list = new List<MissingThing>();

            bool checkLoad = QueryEvent.AnyCoursesHaveLoads(eventDB);       // only check load if some courses have it.

            foreach (Id<Course> courseId in QueryEvent.SortedCourseIds(eventDB, false)) {
                Course course = eventDB.GetCourse(courseId);
                if (course.kind == CourseKind.Normal)
                    AddMissingThingsInRegularCourse(eventDB, courseId, list);
                else if (course.kind == CourseKind.Score)
                    AddMissingThingsInScoreCourse(eventDB, courseId, list);
                else
                    Debug.Fail("bad course kind");

                if (checkLoad && eventDB.GetCourse(courseId).load < 0)
                    list.Add(new MissingThing(courseId, ReportText.ColumnHeader_Load, ReportText.EventAudit_MissingLoad));
            }

            return list;
        }

        // Get missing description boxes.
        List<MissingThing> MissingDescriptionBoxes(EventDB eventDB, List<Id<ControlPoint>> unusedControls)
        {
            List<MissingThing> missingBoxes = new List<MissingThing>();

            foreach (Id<ControlPoint> controlId in eventDB.AllControlPointIds) {
                // Go through all regular or start controls that are in use.
                if (unusedControls.Contains(controlId))
                    continue;

                ControlPoint control = eventDB.GetControl(controlId);
                if (control.kind != ControlPointKind.Normal && control.kind != ControlPointKind.Start)
                    continue;

                // If a start control is completely empty, don't process it.
                if (control.kind == ControlPointKind.Start) {
                    if (! Array.Exists(control.symbolIds, id => !string.IsNullOrEmpty(id)))
                        continue;
                }

                // Each start or normal control has 6 boxes. C==0, D==1, E==2, F==3, G==4, H==5
                Debug.Assert(control.symbolIds.Length == 6);

                if (string.IsNullOrEmpty(control.symbolIds[1])) {
                    missingBoxes.Add(new MissingThing(controlId, "D", ReportText.EventAudit_MissingD));
                }
                else if ((control.symbolIds[3] == "10.1" || control.symbolIds[3] == "10.2") &&
                         string.IsNullOrEmpty(control.symbolIds[2])) 
                {
                    missingBoxes.Add(new MissingThing(controlId, "E", ReportText.EventAudit_MissingEJunction));
                }
                else if (control.symbolIds[4] == "11.15" && string.IsNullOrEmpty(control.symbolIds[2])) {
                    missingBoxes.Add(new MissingThing(controlId, "E", ReportText.EventAudit_MissingEBetween));
                }
            }

            missingBoxes.Sort(((thing1, thing2) => QueryEvent.CompareControlIds(eventDB, thing1.controlId, thing2.controlId)));
            return missingBoxes;
        }

        // Get missing punches.
        List<MissingThing> MissingPunches(EventDB eventDB, List<Id<ControlPoint>> unusedControls)
        {
            List<MissingThing> missingPunches = new List<MissingThing>();

            bool anyPunches = false;    // Keep track if any controls have non-empty punch pattersn.
            foreach (Id<ControlPoint> controlId in eventDB.AllControlPointIds) {
                if (unusedControls.Contains(controlId))
                    continue;

                ControlPoint control = eventDB.GetControl(controlId);
                if (control.kind != ControlPointKind.Normal)
                    continue;

                if (control.punches == null || control.punches.IsEmpty) 
                    missingPunches.Add(new MissingThing(controlId, ReportText.EventAudit_MissingPunch));
                else
                    anyPunches = true;
            }

            if (anyPunches) {
                missingPunches.Sort((thing1, thing2) => QueryEvent.CompareControlIds(eventDB, thing1.controlId, thing2.controlId));
                return missingPunches;
            }
            else {
                // No controls had punch patterns defined. This event clearly is not using punches.
                return new List<MissingThing>();
            }
        }

        // Get missing points.
        List<MissingThing> MissingScores(EventDB eventDB)
        {
            List<MissingThing> missingScores = new List<MissingThing>();

            foreach (Id<Course> courseId in QueryEvent.SortedCourseIds(eventDB, false)) {
                Course course = eventDB.GetCourse(courseId);
                bool anyScores = false;
                List<MissingThing> missingScoresThisCourse = new List<MissingThing>();

                if (course.kind == CourseKind.Score) {
                    for (Id<CourseControl> courseControlId = course.firstCourseControl; 
                          courseControlId.IsNotNone; 
                          courseControlId = eventDB.GetCourseControl(courseControlId).nextCourseControl) 
                    {
                        CourseControl courseControl = eventDB.GetCourseControl(courseControlId);

                        if (eventDB.GetControl(courseControl.control).kind == ControlPointKind.Normal) {
                            if (courseControl.points <= 0)
                                missingScoresThisCourse.Add(new MissingThing(courseId, courseControl.control, ReportText.EventAudit_MissingScore));
                            else
                                anyScores = true;
                        }
                    }

                    if (anyScores)
                        missingScores.AddRange(missingScoresThisCourse);  // only report missing scores if some control in this course has a score.
                }
            }

            return missingScores;
        }

        class RepeatControl
        {
            public CourseDesignator courseDesignator;
            public Id<ControlPoint> controlId;
            public bool scoreCourse;

            public RepeatControl(CourseDesignator courseDesignator, Id<ControlPoint> controlId, bool scoreCourse)
            {
                this.courseDesignator = courseDesignator;
                this.controlId = controlId;
                this.scoreCourse = scoreCourse;
            }
        }

        List<RepeatControl> RepeatedControls(EventDB eventDB)
        {
            List<RepeatControl> result = new List<RepeatControl>();

            foreach (Id<Course> courseId in QueryEvent.SortedCourseIds(eventDB, false)) {
                foreach (CourseDesignator designator in AllDesignatorsForCourse(eventDB, courseId)) {
                    List<Id<CourseControl>> courseControls = QueryEvent.EnumCourseControlIds(eventDB, designator).ToList();
                    bool score = eventDB.GetCourse(courseId).kind == CourseKind.Score;

                    if (score) {
                        // Check for any repeated control.
                        HashSet<Id<ControlPoint>> controls = new HashSet<Id<ControlPoint>>();
                        foreach (Id<CourseControl> courseControlId in courseControls) {
                            Id<ControlPoint> controlId = eventDB.GetCourseControl(courseControlId).control;
                            if (controls.Contains(controlId))
                                AddRepeatedControl(result, designator, controlId, score);
                            controls.Add(controlId);
                        }
                    }
                    else {
                        for (int i = 1; i < courseControls.Count; ++i) {
                            if (eventDB.GetCourseControl(courseControls[i]).control == 
                                eventDB.GetCourseControl(courseControls[i-1]).control) {
                                AddRepeatedControl(result, designator, eventDB.GetCourseControl(courseControls[i]).control, score);
                            }
                        }
                    }
                }
            }

            return result;
        }

        void AddRepeatedControl(List<RepeatControl> result, CourseDesignator designator, Id<ControlPoint> controlId, bool score)
        {
            // Check the result for the same course and control already.
            foreach (RepeatControl repeat in result) {
                if (repeat.courseDesignator.CourseId == designator.CourseId && repeat.controlId == controlId)
                    return;
            }

            result.Add(new RepeatControl(designator, controlId, score));
        }

        class CourseLeg: IEquatable<CourseLeg>
        {
            public readonly Id<ControlPoint> controlId1, controlId2;

            public CourseLeg(Id<ControlPoint> controlId1, Id<ControlPoint> controlId2)
            {
                this.controlId1 = controlId1;
                this.controlId2 = controlId2;
            }

            public bool Equals(CourseLeg other)
            {
                return controlId1 == other.controlId1 && controlId2 == other.controlId2;
            }

            public override int GetHashCode()
            {
                return 31 * controlId1.GetHashCode() + controlId2.GetHashCode();
            }
        }

        class BothDirectionsLeg
        {
            public readonly CourseLeg courseLeg;
            public readonly List<Id<Course>> forwardCourses;
            public readonly List<Id<Course>> backwardCourses;

            public BothDirectionsLeg(CourseLeg courseLeg, List<Id<Course>> forwardCourses, List<Id<Course>> backwardCourses)
            {
                this.courseLeg = courseLeg;
                this.forwardCourses = forwardCourses;
                this.backwardCourses = backwardCourses;
            }
        }

        List<BothDirectionsLeg> BothDirectionsLegs(EventDB eventDB)
        {
            List<BothDirectionsLeg> result = new List<BothDirectionsLeg>();

            Dictionary<CourseLeg, List<Id<Course>>> forwardLegs = new Dictionary<CourseLeg, List<Id<Course>>>();

            // Accumulate all the legs in the event into the above dictionaries.
            foreach (Id<Course> courseId in QueryEvent.SortedCourseIds(eventDB, false)) {
                if (eventDB.GetCourse(courseId).kind == CourseKind.Score)
                    continue;

                foreach (QueryEvent.LegInfo legInfo in QueryEvent.EnumLegs(eventDB, new CourseDesignator(courseId))) {
                    CourseLeg forward = new CourseLeg(eventDB.GetCourseControl(legInfo.courseControlId1).control, eventDB.GetCourseControl(legInfo.courseControlId2).control);

                    if (!forwardLegs.ContainsKey(forward))
                        forwardLegs[forward] = new List<Id<Course>>();
                    forwardLegs[forward].Add(courseId);
                }
            }

            foreach (CourseLeg forward in forwardLegs.Keys) {
                // The below prevent us from putting each pair of both directions legs in twice,
                // and also filters out when same control is twice in a row (which is handled elsewhere in event audit).
                if (forward.controlId1.id >= forward.controlId2.id)
                    continue;

                CourseLeg backward = new CourseLeg(forward.controlId2, forward.controlId1);
                if (forwardLegs.ContainsKey(backward)) {
                    result.Add(new BothDirectionsLeg(forward, forwardLegs[forward], forwardLegs[backward]));
                }
            }

            return result;
        }

        string CourseList(EventDB eventDB, List<Id<Course>> courseList)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < courseList.Count; ++i) {
                if (i != 0)
                    builder.Append(", ");
                builder.Append(Util.CourseName(eventDB, courseList[i]));
            }

            return builder.ToString();
        }


        // Create a report showing missing things
        /// <summary>
        /// Creates a concise event-readiness report that groups the detailed audit
        /// checks into blockers and warnings for a final competition review.
        /// </summary>
        /// <param name="eventDB">The event to analyze.</param>
        /// <returns>An HTML report body.</returns>
        public string CreateEventReadinessReport(EventDB eventDB, CoursePdfSettings pdfSettings = null)
        {
            List<MissingThing> missingCourseThings = MissingCourseThings(eventDB);
            List<RepeatControl> repeatedControls = RepeatedControls(eventDB);
            List<NearbyControls> nearbyControls = FindNearbyControls(eventDB, 100.4999F);
            List<BothDirectionsLeg> bothDirectionsLegs = BothDirectionsLegs(eventDB);
            List<Id<ControlPoint>> unusedControls = SortedUnusedControls(eventDB);
            List<MissingThing> missingBoxes = MissingDescriptionBoxes(eventDB, unusedControls);
            List<MissingThing> missingPunches = MissingPunches(eventDB, unusedControls);
            List<MissingThing> missingScores = MissingScores(eventDB);
            int relayWarnings = CountRelayBranchWarnings(eventDB);
            List<ShortLeg> shortLegs = FindShortLegs(eventDB, 100.0F);
            List<SharpTurn> sharpTurns = FindSharpTurns(eventDB, 45.0F);
            List<CrossedLeg> crossedLegs = FindCrossedLegs(eventDB);
            List<NumberPlacementIssue> numberIssues = FindNumberPlacementIssues(eventDB);
            List<PrintReadinessIssue> printIssues = FindPrintReadinessIssues(eventDB);
            List<ClassReadinessIssue> classIssues = FindClassReadinessIssues(eventDB);
            List<BacksideReadinessIssue> backsideIssues = FindBacksideReadinessIssues(eventDB, pdfSettings);

            int blockers = missingCourseThings.Count + repeatedControls.Count + missingScores.Count;
            int warnings = nearbyControls.Count + bothDirectionsLegs.Count + unusedControls.Count + missingBoxes.Count + missingPunches.Count + relayWarnings + shortLegs.Count + sharpTurns.Count + crossedLegs.Count + numberIssues.Count + printIssues.Count + classIssues.Count + backsideIssues.Count;

            InitReport();
            WriteH1(String.Format(ReadinessText("EventReadiness_Title"), QueryEvent.GetEventTitle(eventDB, " ")));
            WritePara(blockers == 0
                ? ReadinessText("EventReadiness_NoBlockers")
                : String.Format(ReadinessText("EventReadiness_BlockerSummary"), blockers));
            WritePara(warnings == 0
                ? ReadinessText("EventReadiness_NoWarnings")
                : String.Format(ReadinessText("EventReadiness_WarningSummary"), warnings));

            WriteH2(ReadinessText("EventReadiness_Checks"));
            BeginTable("", 4, "leftalign", "leftalign", "rightalign", "leftalign");
            WriteTableHeaderRow(ReadinessText("EventReadiness_Status"), ReadinessText("EventReadiness_Area"),
                                ReadinessText("EventReadiness_Count"), ReadinessText("EventReadiness_Action"));
            BeginTableBody();
            WriteReadinessRow(ReadinessText("EventReadiness_Blocker"), ReadinessText("EventReadiness_CourseStructure"),
                              missingCourseThings.Count + repeatedControls.Count + missingScores.Count, ReadinessText("EventReadiness_CourseAction"));
            WriteReadinessRow(ReadinessText("EventReadiness_Warning"), ReadinessText("EventReadiness_ControlSafety"),
                              nearbyControls.Count + bothDirectionsLegs.Count, ReadinessText("EventReadiness_ControlAction"));
            WriteReadinessRow(ReadinessText("EventReadiness_Warning"), ReadinessText("EventReadiness_ShortLegs"),
                              shortLegs.Count, ReadinessText("EventReadiness_ShortLegsAction"));
            WriteReadinessRow(ReadinessText("EventReadiness_Warning"), ReadinessText("EventReadiness_SharpTurns"),
                              sharpTurns.Count, ReadinessText("EventReadiness_SharpTurnsAction"));
            WriteReadinessRow(ReadinessText("EventReadiness_Warning"), ReadinessText("EventReadiness_LegCrossings"),
                              crossedLegs.Count, ReadinessText("EventReadiness_LegCrossingsAction"));
            WriteReadinessRow(ReadinessText("EventReadiness_Warning"), ReadinessText("EventReadiness_ControlData"),
                              unusedControls.Count + missingBoxes.Count + missingPunches.Count, ReadinessText("EventReadiness_DataAction"));
            WriteReadinessRow(ReadinessText("EventReadiness_Warning"), ReadinessText("EventReadiness_Relay"),
                              relayWarnings, ReadinessText("EventReadiness_RelayAction"));
            WriteReadinessRow(ReadinessText("EventReadiness_Warning"), ReadinessText("EventReadiness_Numbering"),
                              numberIssues.Count, ReadinessText("EventReadiness_NumberingAction"));
            WriteReadinessRow(ReadinessText("EventReadiness_Warning"), ReadinessText("EventReadiness_Print"),
                              printIssues.Count, ReadinessText("EventReadiness_PrintAction"));
            WriteReadinessRow(ReadinessText("EventReadiness_Warning"), ReadinessText("EventReadiness_Classes"),
                              classIssues.Count, ReadinessText("EventReadiness_ClassesAction"));
            WriteReadinessRow(ReadinessText("EventReadiness_Warning"), ReadinessText("EventReadiness_Backside"),
                              backsideIssues.Count, ReadinessText("EventReadiness_BacksideAction"));
            EndTableBody();
            EndTable();

            WriteReadinessGeometryDetails(eventDB, shortLegs, sharpTurns, crossedLegs);
            WriteReadinessNumberDetails(eventDB, numberIssues);
            WriteReadinessPrintDetails(eventDB, printIssues);
            WriteReadinessClassDetails(eventDB, classIssues);
            WriteReadinessBacksideDetails(eventDB, backsideIssues);

            WritePara(ReadinessText("EventReadiness_Details"));
            return FinishReport();
        }

        private sealed class NumberPlacementIssue
        {
            public Id<Course> courseId;
            public Id<CourseControl> firstCourseControlId;
            public Id<CourseControl> secondCourseControlId;
            public float distance;
            public bool firstCustom;
            public bool secondCustom;
        }

        private sealed class PrintReadinessIssue
        {
            public Id<Course> courseId;
            public string reason;
        }

        private sealed class ClassReadinessIssue
        {
            public Id<Course> courseId;
            public string className;
            public bool duplicate;
        }

        private sealed class BacksideReadinessIssue
        {
            public string courseName;
            public string reason;
        }

        private static List<NumberPlacementIssue> FindNumberPlacementIssues(EventDB eventDB)
        {
            const float numberCollisionDistance = 18.0F;
            List<NumberPlacementIssue> result = new List<NumberPlacementIssue>();
            foreach (Id<Course> courseId in QueryEvent.SortedCourseIds(eventDB, false)) {
                List<NumberPlacementIssue> layoutIssues = FindLayoutNumberPlacementIssues(eventDB, courseId);
                if (layoutIssues != null) {
                    result.AddRange(layoutIssues);
                    continue;
                }
                List<Id<CourseControl>> ids = QueryEvent.EnumCourseControlIds(eventDB, new CourseDesignator(courseId)).ToList();
                List<Tuple<Id<CourseControl>, PointF, bool>> placements = new List<Tuple<Id<CourseControl>, PointF, bool>>();
                foreach (Id<CourseControl> id in ids) {
                    CourseControl cc = eventDB.GetCourseControl(id);
                    ControlPoint cp = eventDB.GetControl(cc.control);
                    PointF location = cp.location;
                    if (cc.customNumberPlacement)
                        location = new PointF(location.X + cc.numberDeltaX, location.Y + cc.numberDeltaY);
                    placements.Add(Tuple.Create(id, location, cc.customNumberPlacement));
                }
                for (int first = 0; first < placements.Count; ++first) {
                    for (int second = first + 1; second < placements.Count; ++second) {
                        float distance = (float) Geometry.Distance(placements[first].Item2, placements[second].Item2);
                        if (distance < numberCollisionDistance) {
                            result.Add(new NumberPlacementIssue {
                                courseId = courseId,
                                firstCourseControlId = placements[first].Item1,
                                secondCourseControlId = placements[second].Item1,
                                distance = distance,
                                firstCustom = placements[first].Item3,
                                secondCustom = placements[second].Item3,
                            });
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>Uses print-scale CourseLayout bounds as the primary numbering collision evidence.</summary>
        /// <returns>Issues, or null when the read-only layout cannot be created.</returns>
        private static List<NumberPlacementIssue> FindLayoutNumberPlacementIssues(EventDB eventDB, Id<Course> courseId)
        {
            try {
                CourseView view = CourseView.CreatePrintingCourseView(eventDB, new CourseDesignator(courseId));
                CourseLayout layout = new CourseLayout();
                SymbolDB symbols = new SymbolDB(Util.GetFileInAppDirectory("symbols.xml"));
                CourseFormatterOptions options = new CourseFormatterOptions { showDescriptions = false, showControlNumbers = true };
                CourseFormatter.FormatCourseToLayout(symbols, view, new CourseAppearance(), layout, CourseLayer.MainCourse, options);
                List<CourseObj> numbers = layout.Where(obj => obj is ControlNumberCourseObj).ToList();
                List<CourseObj> others = layout.Where(obj => !(obj is ControlNumberCourseObj) && obj.courseControlId.IsNotNone).ToList();
                HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
                List<NumberPlacementIssue> result = new List<NumberPlacementIssue>();
                foreach (ControlNumberCourseObj number in numbers) {
                    RectangleF numberBounds = number.GetHighlightBounds();
                    foreach (CourseObj other in others) {
                        if (!numberBounds.IntersectsWith(other.GetHighlightBounds()))
                            continue;
                        string key = number.courseControlId.id + ":" + other.courseControlId.id + ":" + other.GetType().Name;
                        if (!seen.Add(key))
                            continue;
                        result.Add(new NumberPlacementIssue { courseId = courseId, firstCourseControlId = number.courseControlId,
                            secondCourseControlId = other.courseControlId, distance = 0, firstCustom = false, secondCustom = false });
                    }
                }
                for (int first = 0; first < numbers.Count; ++first)
                    for (int second = first + 1; second < numbers.Count; ++second)
                        if (numbers[first].GetHighlightBounds().IntersectsWith(numbers[second].GetHighlightBounds()))
                            result.Add(new NumberPlacementIssue { courseId = courseId, firstCourseControlId = numbers[first].courseControlId,
                                secondCourseControlId = numbers[second].courseControlId, distance = 0, firstCustom = false, secondCustom = false });
                return result;
            }
            catch (Exception) {
                return null;
            }
        }

        private static List<PrintReadinessIssue> FindPrintReadinessIssues(EventDB eventDB)
        {
            List<PrintReadinessIssue> result = new List<PrintReadinessIssue>();
            foreach (Id<Course> courseId in QueryEvent.SortedCourseIds(eventDB, false)) {
                Course course = eventDB.GetCourse(courseId);
                if (float.IsNaN(course.printScale) || float.IsInfinity(course.printScale) || course.printScale < 100 || course.printScale > 100000)
                    result.Add(new PrintReadinessIssue { courseId = courseId, reason = ReadinessText("EventReadiness_InvalidScale") });
                AddPrintAreaIssues(result, courseId, course.printArea);
                foreach (KeyValuePair<int, PrintArea> part in course.partPrintAreas)
                    AddPrintAreaIssues(result, courseId, part.Value);
            }
            return result;
        }

        private static void AddPrintAreaIssues(List<PrintReadinessIssue> result, Id<Course> courseId, PrintArea area)
        {
            if (area == null) {
                result.Add(new PrintReadinessIssue { courseId = courseId, reason = ReadinessText("EventReadiness_MissingPrintArea") });
                return;
            }
            if (!area.autoPrintArea && (area.pageWidth <= 0 || area.pageHeight <= 0))
                result.Add(new PrintReadinessIssue { courseId = courseId, reason = ReadinessText("EventReadiness_InvalidPaper") });
            else if (!area.autoPrintArea && (area.pageMargins < 0 || area.pageMargins * 2 >= Math.Min(area.pageWidth, area.pageHeight)))
                result.Add(new PrintReadinessIssue { courseId = courseId, reason = ReadinessText("EventReadiness_InvalidMargins") });
            if (!area.autoPrintArea && (area.printAreaRectangle.IsEmpty || area.printAreaRectangle.Width <= 0 || area.printAreaRectangle.Height <= 0))
                result.Add(new PrintReadinessIssue { courseId = courseId, reason = ReadinessText("EventReadiness_InvalidPrintArea") });
        }

        private static List<ClassReadinessIssue> FindClassReadinessIssues(EventDB eventDB)
        {
            List<ClassReadinessIssue> result = new List<ClassReadinessIssue>();
            Dictionary<string, List<Id<Course>>> coursesByClass = new Dictionary<string, List<Id<Course>>>(StringComparer.OrdinalIgnoreCase);
            Dictionary<Id<Course>, List<EventClass>> classesByCourse = eventDB.AllCourseIds.ToDictionary(id => id, id => new List<EventClass>());
            foreach (EventClass eventClass in eventDB.AllEventClasses) {
                if (classesByCourse.ContainsKey(eventClass.CourseId))
                    classesByCourse[eventClass.CourseId].Add(eventClass);
            }
            foreach (Id<Course> courseId in QueryEvent.SortedCourseIds(eventDB, false)) {
                List<EventClass> classes = classesByCourse[courseId];
                if (classes.Count == 0) {
                    result.Add(new ClassReadinessIssue { courseId = courseId, className = "", duplicate = false });
                }
                else {
                    foreach (EventClass eventClass in classes) {
                        string className = eventClass.Name == null ? "" : eventClass.Name.Trim();
                        if (className.Length == 0) { result.Add(new ClassReadinessIssue { courseId = courseId, className = "", duplicate = false }); continue; }
                        if (!coursesByClass.ContainsKey(className)) coursesByClass.Add(className, new List<Id<Course>>());
                        coursesByClass[className].Add(courseId);
                    }
                }
            }
            foreach (KeyValuePair<string, List<Id<Course>>> pair in coursesByClass) {
                if (pair.Value.Count > 1)
                    foreach (Id<Course> courseId in pair.Value)
                        result.Add(new ClassReadinessIssue { courseId = courseId, className = pair.Key, duplicate = true });
            }
            return result;
        }

        private static List<BacksideReadinessIssue> FindBacksideReadinessIssues(EventDB eventDB, CoursePdfSettings pdfSettings)
        {
            List<BacksideReadinessIssue> result = new List<BacksideReadinessIssue>();
            if (pdfSettings == null)
                return result;
            if (!pdfSettings.IncludeBacksideInfo)
                return result;
            if (pdfSettings.BacksideInfoRecords == null || pdfSettings.BacksideInfoRecords.Count == 0) {
                result.Add(new BacksideReadinessIssue { courseName = "", reason = ReadinessText("EventReadiness_BacksideNoRecords") });
                return result;
            }
            foreach (Id<Course> courseId in QueryEvent.SortedCourseIds(eventDB, false)) {
                Course course = eventDB.GetCourse(courseId);
                bool hasEventClass = EventClassSupport.GetClasses(eventDB, courseId).Any();
                if (!hasEventClass && course.relaySettings.relayTeams <= 0)
                    result.Add(new BacksideReadinessIssue { courseName = course.name, reason = ReadinessText("EventReadiness_BacksideMissingClass") });
            }
            return result;
        }

        /// <summary>Writes the actionable geometry warnings for a final course review.</summary>
        private void WriteReadinessGeometryDetails(EventDB eventDB, List<ShortLeg> shortLegs, List<SharpTurn> sharpTurns, List<CrossedLeg> crossedLegs)
        {
            if (shortLegs.Count == 0 && sharpTurns.Count == 0 && crossedLegs.Count == 0)
                return;

            WriteH2(ReadinessText("EventReadiness_GeometryDetails"));
            BeginTable("", 4, "leftalign", "leftalign", "leftalign", "rightalign");
            WriteTableHeaderRow(ReadinessText("EventReadiness_GeometryType"), ReportText.ColumnHeader_Course,
                                ReportText.ColumnHeader_Leg, ReadinessText("EventReadiness_GeometryMeasure"));
            BeginTableBody();
            foreach (ShortLeg shortLeg in shortLegs) {
                WriteTableRow(ReadinessText("EventReadiness_ShortLeg"), eventDB.GetCourse(shortLeg.courseId).name,
                              FormatLeg(eventDB, shortLeg.controlId1, shortLeg.controlId2),
                              String.Format("{0} m", Math.Round(shortLeg.length)));
            }
            foreach (SharpTurn sharpTurn in sharpTurns) {
                WriteTableRow(ReadinessText("EventReadiness_SharpTurn"), eventDB.GetCourse(sharpTurn.courseId).name,
                              FormatLeg(eventDB, sharpTurn.controlId1, sharpTurn.controlId2) + " – " +
                              Util.ControlPointName(eventDB, sharpTurn.controlId3, NameStyle.Medium),
                              String.Format("{0}°", Math.Round(sharpTurn.angle)));
            }
            foreach (CrossedLeg crossedLeg in crossedLegs) {
                WriteTableRow(ReadinessText("EventReadiness_LegCrossing"), eventDB.GetCourse(crossedLeg.courseId).name,
                              FormatLeg(eventDB, crossedLeg.controlId1, crossedLeg.controlId2) + " / " +
                              FormatLeg(eventDB, crossedLeg.controlId3, crossedLeg.controlId4), "");
            }
            EndTableBody();
            EndTable();
        }

        private void WriteReadinessNumberDetails(EventDB eventDB, List<NumberPlacementIssue> issues)
        {
            if (issues.Count == 0)
                return;
            WriteH2(ReadinessText("EventReadiness_NumberingDetails"));
            BeginTable("", 4, "leftalign", "leftalign", "rightalign", "leftalign");
            WriteTableHeaderRow(ReportText.ColumnHeader_Course, ReadinessText("EventReadiness_NumberPair"),
                                ReadinessText("EventReadiness_GeometryMeasure"), ReadinessText("EventReadiness_NumberPlacement"));
            BeginTableBody();
            foreach (NumberPlacementIssue issue in issues) {
                CourseControl first = eventDB.GetCourseControl(issue.firstCourseControlId);
                CourseControl second = eventDB.GetCourseControl(issue.secondCourseControlId);
                string placement = (issue.firstCustom || issue.secondCustom) ? ReadinessText("EventReadiness_CustomPlacement") : ReadinessText("EventReadiness_AutomaticPlacement");
                WriteTableRow(eventDB.GetCourse(issue.courseId).name,
                              eventDB.GetControl(first.control).code + " / " + eventDB.GetControl(second.control).code,
                              String.Format("{0} map units", Math.Round(issue.distance, 1)), placement);
            }
            EndTableBody();
            EndTable();
        }

        private void WriteReadinessPrintDetails(EventDB eventDB, List<PrintReadinessIssue> issues)
        {
            if (issues.Count == 0)
                return;
            WriteH2(ReadinessText("EventReadiness_PrintDetails"));
            BeginTable("", 2, "leftalign", "leftalign");
            WriteTableHeaderRow(ReportText.ColumnHeader_Course, ReadinessText("EventReadiness_Reason"));
            BeginTableBody();
            foreach (PrintReadinessIssue issue in issues)
                WriteTableRow(eventDB.GetCourse(issue.courseId).name, issue.reason);
            EndTableBody();
            EndTable();
        }

        private void WriteReadinessClassDetails(EventDB eventDB, List<ClassReadinessIssue> issues)
        {
            if (issues.Count == 0)
                return;
            WriteH2(ReadinessText("EventReadiness_ClassDetails"));
            BeginTable("", 3, "leftalign", "leftalign", "leftalign");
            WriteTableHeaderRow(ReportText.ColumnHeader_Course, ReadinessText("EventReadiness_Class"), ReadinessText("EventReadiness_Reason"));
            BeginTableBody();
            foreach (ClassReadinessIssue issue in issues)
                WriteTableRow(eventDB.GetCourse(issue.courseId).name, issue.className,
                              ReadinessText(issue.duplicate ? "EventReadiness_DuplicateClass" : "EventReadiness_MissingClass"));
            EndTableBody();
            EndTable();
        }

        private void WriteReadinessBacksideDetails(EventDB eventDB, List<BacksideReadinessIssue> issues)
        {
            if (issues.Count == 0)
                return;
            WriteH2(ReadinessText("EventReadiness_BacksideDetails"));
            BeginTable("", 2, "leftalign", "leftalign");
            WriteTableHeaderRow(ReportText.ColumnHeader_Course, ReadinessText("EventReadiness_Reason"));
            BeginTableBody();
            foreach (BacksideReadinessIssue issue in issues)
                WriteTableRow(issue.courseName, issue.reason);
            EndTableBody();
            EndTable();
        }

        /// <summary>Formats one course leg for a readiness-report table.</summary>
        private static string FormatLeg(EventDB eventDB, Id<ControlPoint> controlId1, Id<ControlPoint> controlId2)
        {
            return Util.ControlPointName(eventDB, controlId1, NameStyle.Medium) + " – " +
                   Util.ControlPointName(eventDB, controlId2, NameStyle.Medium);
        }

        private static int CountRelayBranchWarnings(EventDB eventDB)
        {
            int warnings = 0;
            foreach (KeyValuePair<Id<Course>, Course> coursePair in eventDB.AllCoursePairs) {
                if (coursePair.Value.relaySettings != null)
                    warnings += new RelayVariations(eventDB, coursePair.Key, coursePair.Value.relaySettings).GetBranchWarnings().Count();
            }
            return warnings;
        }

        /// <summary>One course leg whose measured length merits a publication review.</summary>
        private struct ShortLeg
        {
            public Id<Course> courseId;
            public Id<ControlPoint> controlId1;
            public Id<ControlPoint> controlId2;
            public float length;
        }

        /// <summary>One potentially difficult acute turn at a normal control.</summary>
        private struct SharpTurn
        {
            public Id<Course> courseId;
            public Id<ControlPoint> controlId1;
            public Id<ControlPoint> controlId2;
            public Id<ControlPoint> controlId3;
            public float angle;
        }

        /// <summary>Two non-adjacent legs that cross at an interior point.</summary>
        private struct CrossedLeg
        {
            public Id<Course> courseId;
            public Id<ControlPoint> controlId1;
            public Id<ControlPoint> controlId2;
            public Id<ControlPoint> controlId3;
            public Id<ControlPoint> controlId4;
        }

        /// <summary>
        /// Finds physical course legs shorter than the supplied limit. Crossing and map-issue
        /// helper points are excluded because they are layout mechanics, not runner legs.
        /// </summary>
        private static List<ShortLeg> FindShortLegs(EventDB eventDB, float distanceLimit)
        {
            List<ShortLeg> shortLegs = new List<ShortLeg>();
            HashSet<string> seenLegs = new HashSet<string>();

            foreach (Id<Course> courseId in QueryEvent.SortedCourseIds(eventDB, false)) {
                Course course = eventDB.GetCourse(courseId);
                if (course.kind == CourseKind.Score)
                    continue;

                foreach (QueryEvent.LegInfo legInfo in QueryEvent.EnumLegs(eventDB, new CourseDesignator(courseId))) {
                    Id<ControlPoint> controlId1 = eventDB.GetCourseControl(legInfo.courseControlId1).control;
                    Id<ControlPoint> controlId2 = eventDB.GetCourseControl(legInfo.courseControlId2).control;
                    ControlPointKind kind1 = eventDB.GetControl(controlId1).kind;
                    ControlPointKind kind2 = eventDB.GetControl(controlId2).kind;
                    if (kind1 == ControlPointKind.CrossingPoint || kind2 == ControlPointKind.CrossingPoint ||
                        kind1 == ControlPointKind.MapIssue || kind2 == ControlPointKind.MapIssue)
                        continue;

                    string identity = String.Format("{0}:{1}:{2}", courseId.id, controlId1.id, controlId2.id);
                    if (!seenLegs.Add(identity))
                        continue;

                    float length = QueryEvent.ComputeLegLength(eventDB, controlId1, controlId2,
                        QueryEvent.FindLeg(eventDB, controlId1, controlId2));
                    if (length < distanceLimit) {
                        shortLegs.Add(new ShortLeg {
                            courseId = courseId,
                            controlId1 = controlId1,
                            controlId2 = controlId2,
                            length = length,
                        });
                    }
                }
            }

            return shortLegs;
        }

        /// <summary>
        /// Finds successive legs that make an acute turn at a normal control. These
        /// are review warnings only: an intentional dogleg can be correct on site.
        /// </summary>
        private static List<SharpTurn> FindSharpTurns(EventDB eventDB, float angleLimit)
        {
            List<SharpTurn> sharpTurns = new List<SharpTurn>();
            HashSet<string> seenTurns = new HashSet<string>();

            foreach (Id<Course> courseId in QueryEvent.SortedCourseIds(eventDB, false)) {
                if (eventDB.GetCourse(courseId).kind == CourseKind.Score)
                    continue;

                List<QueryEvent.LegInfo> legs = QueryEvent.EnumLegs(eventDB, new CourseDesignator(courseId)).ToList();
                foreach (QueryEvent.LegInfo incomingLeg in legs) {
                    foreach (QueryEvent.LegInfo outgoingLeg in legs) {
                        if (incomingLeg.courseControlId2 != outgoingLeg.courseControlId1)
                            continue;

                        Id<ControlPoint> controlId1 = eventDB.GetCourseControl(incomingLeg.courseControlId1).control;
                        Id<ControlPoint> controlId2 = eventDB.GetCourseControl(incomingLeg.courseControlId2).control;
                        Id<ControlPoint> controlId3 = eventDB.GetCourseControl(outgoingLeg.courseControlId2).control;
                        if (eventDB.GetControl(controlId2).kind != ControlPointKind.Normal)
                            continue;

                        float angle = Geometry.Angle(eventDB.GetControl(controlId1).location,
                                                     eventDB.GetControl(controlId2).location,
                                                     eventDB.GetControl(controlId3).location);
                        if (angle >= angleLimit)
                            continue;

                        string identity = String.Format("{0}:{1}:{2}:{3}", courseId.id, controlId1.id, controlId2.id, controlId3.id);
                        if (seenTurns.Add(identity)) {
                            sharpTurns.Add(new SharpTurn {
                                courseId = courseId,
                                controlId1 = controlId1,
                                controlId2 = controlId2,
                                controlId3 = controlId3,
                                angle = angle,
                            });
                        }
                    }
                }
            }

            return sharpTurns;
        }

        /// <summary>
        /// Finds non-adjacent legs in the same normal course whose rendered center
        /// lines cross. Intersections at an endpoint are ignored: they are normal
        /// at controls, and can also occur where a line reaches a finish symbol.
        /// </summary>
        private static List<CrossedLeg> FindCrossedLegs(EventDB eventDB)
        {
            List<CrossedLeg> crossedLegs = new List<CrossedLeg>();

            foreach (Id<Course> courseId in QueryEvent.SortedCourseIds(eventDB, false)) {
                if (eventDB.GetCourse(courseId).kind == CourseKind.Score)
                    continue;

                List<CourseLeg> courseLegs = new List<CourseLeg>();
                HashSet<string> seenLegs = new HashSet<string>();
                foreach (QueryEvent.LegInfo legInfo in QueryEvent.EnumLegs(eventDB, new CourseDesignator(courseId))) {
                    Id<ControlPoint> controlId1 = eventDB.GetCourseControl(legInfo.courseControlId1).control;
                    Id<ControlPoint> controlId2 = eventDB.GetCourseControl(legInfo.courseControlId2).control;
                    ControlPointKind kind1 = eventDB.GetControl(controlId1).kind;
                    ControlPointKind kind2 = eventDB.GetControl(controlId2).kind;
                    if (kind1 == ControlPointKind.CrossingPoint || kind2 == ControlPointKind.CrossingPoint ||
                        kind1 == ControlPointKind.MapIssue || kind2 == ControlPointKind.MapIssue)
                        continue;

                    string identity = String.Format("{0}:{1}", controlId1.id, controlId2.id);
                    if (seenLegs.Add(identity))
                        courseLegs.Add(new CourseLeg(controlId1, controlId2));
                }

                for (int first = 0; first < courseLegs.Count; ++first) {
                    for (int second = first + 1; second < courseLegs.Count; ++second) {
                        CourseLeg firstLeg = courseLegs[first];
                        CourseLeg secondLeg = courseLegs[second];
                        if (LegsShareControl(firstLeg, secondLeg) || !LegsCross(eventDB, firstLeg, secondLeg))
                            continue;

                        crossedLegs.Add(new CrossedLeg {
                            courseId = courseId,
                            controlId1 = firstLeg.controlId1,
                            controlId2 = firstLeg.controlId2,
                            controlId3 = secondLeg.controlId1,
                            controlId4 = secondLeg.controlId2,
                        });
                    }
                }
            }

            return crossedLegs;
        }

        /// <summary>Returns true if two logical legs have a common control point.</summary>
        private static bool LegsShareControl(CourseLeg firstLeg, CourseLeg secondLeg)
        {
            return firstLeg.controlId1 == secondLeg.controlId1 || firstLeg.controlId1 == secondLeg.controlId2 ||
                   firstLeg.controlId2 == secondLeg.controlId1 || firstLeg.controlId2 == secondLeg.controlId2;
        }

        /// <summary>Returns true when any two path segments have a proper intersection.</summary>
        private static bool LegsCross(EventDB eventDB, CourseLeg firstLeg, CourseLeg secondLeg)
        {
            PointF[] firstPath = QueryEvent.GetLegPath(eventDB, firstLeg.controlId1, firstLeg.controlId2).Points;
            PointF[] secondPath = QueryEvent.GetLegPath(eventDB, secondLeg.controlId1, secondLeg.controlId2).Points;

            for (int firstSegment = 1; firstSegment < firstPath.Length; ++firstSegment) {
                for (int secondSegment = 1; secondSegment < secondPath.Length; ++secondSegment) {
                    PointF intersection;
                    if (Geometry.LineSegmentsIntersect(firstPath[firstSegment - 1], firstPath[firstSegment],
                                                       secondPath[secondSegment - 1], secondPath[secondSegment], out intersection) &&
                        IsInteriorIntersection(intersection, firstPath[firstSegment - 1], firstPath[firstSegment],
                                               secondPath[secondSegment - 1], secondPath[secondSegment]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Returns true if an intersection is not one of either segment's endpoints.</summary>
        private static bool IsInteriorIntersection(PointF intersection, PointF firstStart, PointF firstEnd, PointF secondStart, PointF secondEnd)
        {
            const float endpointTolerance = 0.01F;
            return !float.IsNaN(intersection.X) &&
                   Geometry.Distance(intersection, firstStart) > endpointTolerance &&
                   Geometry.Distance(intersection, firstEnd) > endpointTolerance &&
                   Geometry.Distance(intersection, secondStart) > endpointTolerance &&
                   Geometry.Distance(intersection, secondEnd) > endpointTolerance;
        }

        private static string ReadinessText(string key)
        {
            return ReportText.ResourceManager.GetString(key) ?? key;
        }

        private void WriteReadinessRow(string problemStatus, string area, int count, string action)
        {
            string status = count == 0 ? ReadinessText("EventReadiness_Ready") : problemStatus;
            WriteTableRow(status, area, Convert.ToString(count), count == 0 ? ReadinessText("EventReadiness_NoAction") : action);
        }

        public string CreateEventAuditReport(EventDB eventDB)
        {
            bool problemFound = false;

            // Initialize the report
            InitReport();

            // Header.
            WriteH1(string.Format(ReportText.EventAudit_Title, QueryEvent.GetEventTitle(eventDB, " ")));

            // Courses missing things. Climb (not score course), start, finish (not score course), competitor load.
            List<MissingThing> missingCourseThings = MissingCourseThings(eventDB);
            if (missingCourseThings.Count > 0) {
                problemFound = true;

                WriteH2(ReportText.EventAudit_MissingItems);
                BeginTable("", 3, "leftalign", "leftalign", "leftalign");
                WriteTableHeaderRow(ReportText.ColumnHeader_Course, ReportText.ColumnHeader_Item, ReportText.ColumnHeader_Reason);

                BeginTableBody();
                foreach (MissingThing thing in missingCourseThings) {
                    WriteTableRow(eventDB.GetCourse(thing.courseId).name, thing.what, thing.why);
                }
                EndTableBody();

                EndTable();
            }

            // Courses with repeated controls.
            List<RepeatControl> repeatedControls = RepeatedControls(eventDB);
            if (repeatedControls.Count > 0) {
                problemFound = true;

                WriteH2(ReportText.EventAudit_RepeatedControls);
                foreach (RepeatControl repeatControl in repeatedControls) {
                    if (repeatControl.scoreCourse) {
                        WritePara(string.Format(ReportText.EventAudit_ScoreDuplicateControl,
                                  Util.CourseName(eventDB, repeatControl.courseDesignator.CourseId),
                                  Util.ControlPointName(eventDB, repeatControl.controlId, NameStyle.Medium)));
                    }
                    else {
                        WritePara(string.Format(ReportText.EventAudit_RepeatControl,
                                  Util.CourseName(eventDB, repeatControl.courseDesignator.CourseId),
                                  Util.ControlPointName(eventDB, repeatControl.controlId, NameStyle.Medium)));
                    }
                }
            }

            // Close together controls.
            float DISTANCE_LIMIT = 100.4999F;       // limit of distance for close controls (100m, when rounded).
            List<NearbyControls> nearbyList = FindNearbyControls(eventDB, DISTANCE_LIMIT);
            if (nearbyList.Count > 0) {
                problemFound = true;

                // Informational text.
                WriteH2(ReportText.EventAudit_CloseTogetherControls);
                StartPara();
                WriteText(string.Format(ReportText.EventAudit_CloseTogetherExplanation, Math.Round(DISTANCE_LIMIT)));
                EndPara();

                // The report.
                BeginTable("", 3, "leftalign", "rightalign", "leftalign");
                WriteTableHeaderRow(ReportText.ColumnHeader_ControlCodes, ReportText.ColumnHeader_Distance, ReportText.ColumnHeader_SameSymbol);

                BeginTableBody();

                foreach (NearbyControls nearby in nearbyList) {
                    string code1 = eventDB.GetControl(nearby.controlId1).code;
                    string code2 = eventDB.GetControl(nearby.controlId2).code;
                    if (Util.CompareCodes(code1, code2) > 0) {
                        // swap code1 and code 2 so they always appear in order.
                        string temp = code1;
                        code1 = code2;
                        code2 = temp;
                    }

                    WriteTableRow(string.Format("{0}, {1}", code1, code2), string.Format("{0} m", Math.Round(nearby.distance)), nearby.sameSymbol ? ReportText.EventAudit_Yes : ReportText.EventAudit_No);
                }

                EndTableBody();
                EndTable();
            }

            // Courses with legs run in opposite directions.
            List<BothDirectionsLeg> bothDirectionsLegs = BothDirectionsLegs(eventDB);
            if (bothDirectionsLegs.Count > 0) {
                problemFound = true;

                WriteH2(ReportText.EventAudit_BothDirectionsLegs);
                BeginTable("", 2, "leftalign", "leftalign");
                WriteTableHeaderRow(ReportText.ColumnHeader_Leg, ReportText.ColumnHeader_Courses);

                BeginTableBody();

                bool first = true;
                foreach (BothDirectionsLeg bothDirectionsLeg in bothDirectionsLegs) {
                    if (!first)
                        WriteTableRow("\u00a0");

                    WriteTableRow(Util.ControlPointName(eventDB, bothDirectionsLeg.courseLeg.controlId1, NameStyle.Medium) +
                                  " \u2192 " +
                                  Util.ControlPointName(eventDB, bothDirectionsLeg.courseLeg.controlId2, NameStyle.Medium),
                                  CourseList(eventDB, bothDirectionsLeg.forwardCourses));

                    WriteTableRow(Util.ControlPointName(eventDB, bothDirectionsLeg.courseLeg.controlId2, NameStyle.Medium) +
                                  " \u2192 " +
                                  Util.ControlPointName(eventDB, bothDirectionsLeg.courseLeg.controlId1, NameStyle.Medium),
                                  CourseList(eventDB, bothDirectionsLeg.backwardCourses));

                    first = false;
                }

                EndTableBody();
                EndTable();
            }

            // Unused controls.
            List<Id<ControlPoint>> unusedControls = SortedUnusedControls(eventDB);
            if (unusedControls.Count > 0) {
                problemFound = true;

                WriteH2(ReportText.EventAudit_UnusedControls);
                StartPara();
                WriteText(ReportText.EventAudit_UnusedControlsExplanation);
                EndPara();
                BeginTable("", 2, "leftalign", "leftalign");
                WriteTableHeaderRow(ReportText.ColumnHeader_Code, ReportText.ColumnHeader_Location);

                BeginTableBody();
                foreach (Id<ControlPoint> controlId in unusedControls) {
                    ControlPoint control = eventDB.GetControl(controlId);
                    WriteTableRow(Util.ControlPointName(eventDB, controlId, NameStyle.Medium), string.Format("({0}, {1})", Math.Round(control.location.X), Math.Round(control.location.Y)));
                }
                EndTableBody();

                EndTable();
            }

            // Missing descriptions boxes (regular controls only). Missing column D. Missing column E when between/junction/crossing used. Missing between/junction/crossing when column E is a symbol.
            List<MissingThing> missingBoxes = MissingDescriptionBoxes(eventDB, unusedControls);
            if (missingBoxes.Count > 0) {
                problemFound = true;

                WriteH2(ReportText.EventAudit_MissingBoxes);
                BeginTable("", 3, "leftalign", "leftalign", "leftalign");
                WriteTableHeaderRow(ReportText.ColumnHeader_Code, ReportText.ColumnHeader_Column, ReportText.ColumnHeader_Reason);
                BeginTableBody();
                foreach (MissingThing thing in missingBoxes) {
                    WriteTableRow(Util.ControlPointName(eventDB, thing.controlId, NameStyle.Medium), thing.what, thing.why);
                }
                EndTableBody();
                EndTable();
            }

            // Missing punches.
            List<MissingThing> missingPunches = MissingPunches(eventDB, unusedControls);
            if (missingPunches.Count > 0) {
                problemFound = true;

                WriteH2(ReportText.EventAudit_MissingPunchPatterns);
                BeginTable("", 2, "leftalign", "leftalign");
                WriteTableHeaderRow(ReportText.ColumnHeader_Code, ReportText.ColumnHeader_Reason);
                BeginTableBody();
                foreach (MissingThing thing in missingPunches) {
                    WriteTableRow(Util.ControlPointName(eventDB, thing.controlId, NameStyle.Medium), thing.what);
                }
                EndTableBody();
                EndTable();
            }

            // Missing points (score course only)
            List<MissingThing> missingScores = MissingScores(eventDB);
            if (missingScores.Count > 0) {
                problemFound = true;

                WriteH2(ReportText.EventAudit_MissingScores);
                BeginTable("", 3, "leftalign", "leftalign", "leftalign");
                WriteTableHeaderRow(ReportText.ColumnHeader_Course, ReportText.ColumnHeader_Control, ReportText.ColumnHeader_Reason);
                BeginTableBody();
                foreach (MissingThing thing in missingScores) {
                    WriteTableRow(eventDB.GetCourse(thing.courseId).name, Util.ControlPointName(eventDB, thing.controlId, NameStyle.Medium), thing.what);
                }
                EndTableBody();
                EndTable();
            }

            // If none of the above, then "no problems found".
            if (!problemFound) {
                StartPara();
                WriteText(ReportText.EventAudit_NoProblems);
                EndPara();
            }

            return FinishReport();
        }

        public string CreateRelayVariationReport(VariationReportData variationReportData)
        {
            string courseName = variationReportData.CourseName;
            RelayVariations relayVariations = variationReportData.RelayVariations;

            InitReport();

            WriteH1(string.Format(ReportText.RelayVariation_Title, courseName));

            foreach (RelayVariations.BranchWarning branchWarning in variationReportData.RelayVariations.GetBranchWarnings()) {
                string codesMore = string.Join(", ", branchWarning.codeMore);
                string codesLess = string.Join(", ", branchWarning.codeLess);
                WritePara(String.Format(ReportText.RelayVariation_BranchWarning, branchWarning.ControlCode, branchWarning.numMore, codesMore, branchWarning.numLess, codesLess));
            }

            IEnumerable<RelayVariations.ForkKeyEntry> forkKey = relayVariations.GetForkKey();
            if (forkKey.Any()) {
                WriteH2(ReportText.RelayVariation_ForkKey);
                BeginTable("", 2, "leftalign", "leftalign");
                WriteTableHeaderRow(ReportText.ColumnHeader_Control, ReportText.ColumnHeader_Code);
                BeginTableBody();
                foreach (RelayVariations.ForkKeyEntry entry in forkKey) {
                    WriteTableRow(entry.ControlCode, string.Join(", ", entry.BranchCodes));
                }
                EndTableBody();
                EndTable();
            }

            string[] classes = new string[relayVariations.NumberOfLegs + 1];
            classes[0] = "leftalign";
            for (int i = 1; i < classes.Length; ++i)
                classes[i] = "rightalign";

            BeginTable("", classes.Length, classes);

            // Write the header row.
            BeginTableHead();
            BeginTableRow();
            WriteTableHeaderCell("");
            for (int i = 1; i <= relayVariations.NumberOfLegs; ++i)
                WriteTableHeaderCell(string.Format(ReportText.RelayVariation_LegHeader, i));
            EndTableRow();
            EndTableHead();

            BeginTableBody();
            // Write the relay variation rows. Table rule after every 3rd line
            for (int teamNumber = relayVariations.FirstTeamNumber; teamNumber <= relayVariations.LastTeamNumber; ++teamNumber) {
                bool tablerule = (teamNumber % 3 == 0);
                BeginTableRow();
                WriteTableCell(tablerule ? "tablerule" : "", string.Format(ReportText.RelayVariation_TeamNumber, teamNumber));
                for (int legNumber = 1; legNumber <= relayVariations.NumberOfLegs; ++legNumber)
                    WriteTableCell(tablerule ? "tablerule" : "", relayVariations.GetVariation(teamNumber, legNumber).CodeString);
                EndTableRow();
            }
            EndTableBody();

            EndTable();

            return FinishReport();    
        }

        public string CreateRelayVariationNotCreated()
        {
            InitReport();

            WritePara(ReportText.RelayVariation_NoTeams);

            return FinishReport();
        }



        // Create a test report.
        public string CreateTestReport(EventDB eventDB)
        {
            InitReport();

            WriteH1("Test Report");
            WriteH2("Heading & cool stuph 2");
            WritePara("The first paragraph: x+3 < 4");
            WritePara("coolclass", "The second paragraph");

            StartPara("coolclass");
            WriteText("This is the start of paragraph, with ");
            WriteStyledText("bold", TextEffects.Bold);
            WriteText(" text and ");
            WriteStyledText("italic", TextEffects.Italic);
            WriteText(" text and ");
            WriteStyledText("underline", TextEffects.Underline);
            WriteText(" text and ");
            WriteStyledText("strikeout", TextEffects.Strikeout);
            WriteText(" text and ");
            WriteStyledText("combo", TextEffects.Bold | TextEffects.Underline);
            WriteText(" text. ");
            EndPara();

            WritePara("paraclass", "This is a paragraph with style paraclass");

            BeginTable(3);
            WriteTableHeaderRow("Column 1", "Column 2", "Column 3");
            BeginTableBody();
            WriteTableRow("row1col1", "row1col2", "row1col3");
            WriteTableRow("", "row2col2", "row2col3");
            BeginTableRow();
            WriteTableCell("row3col1");
            WriteTableCell("row3col2");
            WriteTableCell("row3col3");
            EndTableRow();
            BeginTableRow();
            WriteSpannedTableCell(2, "row3col1and2");
            WriteTableCell("row3col3");
            EndTableRow();
            EndTableBody();
            EndTable();

            BeginTable("tableClass", 5, "col1Class", "col2Class");
            BeginTableHead();
            BeginTableRow();
            WriteTableHeaderCell(null);
            WriteTableHeaderCell("myklass", "row1col2");
            WriteTableHeaderCell("row1col3");
            WriteTableHeaderCell("");
            WriteTableHeaderCell("anotherclass", "row1col5");
            EndTableRow();
            EndTableHead();

            BeginTableBody();
            BeginTableRow();
            WriteTableCell("row2col1");
            WriteTableCell("myklass", "row2col2");
            WriteTableCell("row2col3");
            WriteTableCell("");
            WriteTableCell("row2col5");
            EndTableRow();
            EndTableBody();

            EndTable();

            return FinishReport();
        }
    }
}
