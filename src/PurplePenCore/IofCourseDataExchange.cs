using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace PurplePen
{
    /// <summary>Neutral course-data exchange model used by import/review workflows.</summary>
    public sealed class IofCourseDataModel
    {
        public List<IofCourse> Courses { get; } = new List<IofCourse>();
        public Dictionary<string, IofControl> Controls { get; } = new Dictionary<string, IofControl>(StringComparer.OrdinalIgnoreCase);
    }
    public sealed class IofCourse { public string Name = ""; public string CourseFamily = ""; public List<string> Controls { get; } = new List<string>(); public List<IofCourseControl> CourseControls { get; } = new List<IofCourseControl>(); }
    public sealed class IofCourseControl { public string Code = ""; public string Type = ""; }
    public sealed class IofControl { public string Code = ""; public string Type = ""; public double? X; public double? Y; }
    public sealed class IofValidationError { public int Line; public int Column; public string Message = ""; public override string ToString() => $"{Line}:{Column}: {Message}"; }
    public sealed class IofCourseDataValidationResult { public bool IsValid => Errors.Count == 0; public List<IofValidationError> Errors { get; } = new List<IofValidationError>(); }
    public sealed class IofCourseDataDifference { public string Kind = ""; public string Course = ""; public string Detail = ""; }

    /// <summary>Validates and compares IOF CourseData 3.0 without changing EventDB.</summary>
    public static class IofCourseDataExchange
    {
        private const string Namespace = "http://www.orienteering.org/datastandard/3.0";
        private static XmlSchema LoadSchema()
        {
            Assembly assembly = typeof(IofCourseDataExchange).Assembly;
            using Stream stream = assembly.GetManifestResourceStream("PurplePen.iof.xsd");
            if (stream == null) throw new InvalidOperationException("The embedded IOF CourseData schema is missing.");
            return XmlSchema.Read(stream, null);
        }
        public static IofCourseDataValidationResult Validate(string xml)
        {
            IofCourseDataValidationResult result = new IofCourseDataValidationResult();
            try {
                XmlSchemaSet schemas = new XmlSchemaSet(); schemas.Add(LoadSchema()); schemas.Compile();
                XmlReaderSettings settings = new XmlReaderSettings { ValidationType = ValidationType.Schema, Schemas = schemas, DtdProcessing = DtdProcessing.Prohibit };
                settings.ValidationEventHandler += (sender, args) => { IXmlLineInfo info = sender as IXmlLineInfo; result.Errors.Add(new IofValidationError { Line = info != null && info.HasLineInfo() ? info.LineNumber : 0, Column = info != null && info.HasLineInfo() ? info.LinePosition : 0, Message = args.Message }); };
                using XmlReader reader = XmlReader.Create(new StringReader(xml ?? ""), settings); while (reader.Read()) { }
            }
            catch (Exception ex) when (ex is XmlException || ex is XmlSchemaException || ex is InvalidOperationException) { result.Errors.Add(new IofValidationError { Message = ex.Message }); }
            return result;
        }
        public static IofCourseDataModel Parse(string xml)
        {
            IofCourseDataValidationResult validation = Validate(xml);
            if (!validation.IsValid) throw new FormatException(validation.Errors[0].ToString());
            XNamespace ns = Namespace; XDocument document = XDocument.Parse(xml, LoadOptions.SetLineInfo); IofCourseDataModel model = new IofCourseDataModel();
            foreach (XElement control in document.Descendants(ns + "Control")) {
                string code = (string)control.Element(ns + "Id") ?? ""; if (code.Length == 0) continue;
                XElement pos = control.Element(ns + "MapPosition"); IofControl value = new IofControl { Code = code, Type = (string)control.Attribute("type") ?? "" };
                if (pos != null && Double.TryParse((string)pos.Attribute("x"), NumberStyles.Float, CultureInfo.InvariantCulture, out double x) && Double.TryParse((string)pos.Attribute("y"), NumberStyles.Float, CultureInfo.InvariantCulture, out double y)) { value.X = x; value.Y = y; }
                model.Controls[code] = value;
            }
            foreach (XElement course in document.Descendants(ns + "Course")) {
                IofCourse value = new IofCourse {
                    Name = (string)course.Element(ns + "Name") ?? "",
                    CourseFamily = (string)course.Element(ns + "CourseFamily") ?? "",
                };
                foreach (XElement cc in course.Elements(ns + "CourseControl")) {
                    string code = (string)cc.Element(ns + "Control");
                    if (!String.IsNullOrWhiteSpace(code)) {
                        string trimmedCode = code.Trim();
                        value.Controls.Add(trimmedCode);
                        value.CourseControls.Add(new IofCourseControl { Code = trimmedCode, Type = (string)cc.Attribute("type") ?? "" });
                    }
                }
                model.Courses.Add(value);
            }
            return model;
        }
        public static List<IofCourseDataDifference> Compare(IofCourseDataModel source, EventDB eventDB)
        {
            List<IofCourseDataDifference> differences = new List<IofCourseDataDifference>(); Dictionary<string, KeyValuePair<Id<Course>, Course>> courses = eventDB.AllCoursePairs.ToDictionary(pair => pair.Value.name, pair => pair, StringComparer.OrdinalIgnoreCase);
            HashSet<Id<Course>> matchedCourses = new HashSet<Id<Course>>();
            foreach (IofCourse course in source.Courses) {
                KeyValuePair<Id<Course>, Course> target;
                CourseDesignator targetDesignator;
                if (!TryResolveCourse(eventDB, courses, course, out target, out targetDesignator)) {
                    differences.Add(new IofCourseDataDifference { Kind = "MissingCourse", Course = course.Name, Detail = "Course is not present in EventDB." });
                    continue;
                }

                matchedCourses.Add(target.Key);
                List<IofCourseControl> expected = QueryEvent.EnumCourseControlIds(eventDB, targetDesignator)
                    .Select(id => CreateEventControl(eventDB.GetControl(eventDB.GetCourseControl(id).control)))
                    .Where(control => control != null).ToList();
                List<IofCourseControl> importedControls = GetCourseControls(source, course);
                for (int index = 0; index < Math.Max(expected.Count, importedControls.Count); ++index) {
                    IofCourseControl actual = index < expected.Count ? expected[index] : null;
                    IofCourseControl imported = index < importedControls.Count ? importedControls[index] : null;
                    if (!ControlsMatch(actual, imported))
                        differences.Add(new IofCourseDataDifference { Kind = actual != null && imported != null ? "ControlSequenceChanged" : actual != null ? "MissingControl" : "ExtraControl", Course = course.Name, Detail = String.Format(CultureInfo.InvariantCulture, "Position {0}: EventDB '{1}', IOF '{2}'.", index + 1, DisplayControl(actual), DisplayControl(imported)) });
                }
                foreach (string code in course.Controls.Distinct(StringComparer.OrdinalIgnoreCase)) {
                    if (!modelControlPosition(source, code, out double x, out double y)) continue;
                    ControlPoint point = eventDB.AllControlPoints.FirstOrDefault(item => String.Equals(item.code, code, StringComparison.OrdinalIgnoreCase));
                    if (point != null && (Math.Abs(point.location.X - x) > 0.1 || Math.Abs(point.location.Y - y) > 0.1))
                        differences.Add(new IofCourseDataDifference { Kind = "ControlPositionChanged", Course = course.Name, Detail = String.Format(CultureInfo.InvariantCulture, "Control {0}: EventDB ({1:0.##},{2:0.##}), IOF ({3:0.##},{4:0.##}).", code, point.location.X, point.location.Y, x, y) });
                }
            }
            foreach (KeyValuePair<Id<Course>, Course> target in eventDB.AllCoursePairs) {
                if (!matchedCourses.Contains(target.Key))
                    differences.Add(new IofCourseDataDifference { Kind = "ExtraCourse", Course = target.Value.name, Detail = "Course is not present in IOF data." });
            }
            return differences;
        }

        /// <summary>Resolves an IOF course name or course family to an EventDB course and variation.</summary>
        private static bool TryResolveCourse(EventDB eventDB, Dictionary<string, KeyValuePair<Id<Course>, Course>> courses,
                                             IofCourse imported, out KeyValuePair<Id<Course>, Course> target,
                                             out CourseDesignator targetDesignator)
        {
            target = new KeyValuePair<Id<Course>, Course>();
            targetDesignator = null;

            if (!String.IsNullOrWhiteSpace(imported.CourseFamily) && courses.TryGetValue(imported.CourseFamily, out target)) {
                string variationCode = GetVariationCode(imported.Name, imported.CourseFamily);
                if (!String.IsNullOrEmpty(variationCode)) {
                    VariationInfo variation = QueryEvent.GetAllVariations(eventDB, target.Key)
                        .FirstOrDefault(value => String.Equals(value.CodeString, variationCode, StringComparison.OrdinalIgnoreCase));
                    if (variation != null) {
                        targetDesignator = new CourseDesignator(target.Key, variation);
                        return true;
                    }

                    // Purple Pen's IOF exporter names a variation as
                    // "CourseFamily variation-code". Do not compare that
                    // export shape against the unvaried base course when its
                    // variation code cannot be resolved.
                    return false;
                }

                // A CourseFamily without an explicit variation remains a valid reference
                // to the base course, as permitted by the IOF format.
                targetDesignator = new CourseDesignator(target.Key);
                return true;
            }

            if (courses.TryGetValue(imported.Name, out target)) {
                targetDesignator = new CourseDesignator(target.Key);
                return true;
            }

            return false;
        }

        /// <summary>Gets the variation suffix from an exported IOF course name.</summary>
        private static string GetVariationCode(string courseName, string courseFamily)
        {
            if (String.IsNullOrWhiteSpace(courseName) || String.IsNullOrWhiteSpace(courseFamily))
                return String.Empty;

            string prefix = courseFamily.Trim() + " ";
            return courseName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? courseName.Substring(prefix.Length).Trim()
                : String.Empty;
        }

        private static bool modelControlPosition(IofCourseDataModel model, string code, out double x, out double y)
        {
            x = y = 0;
            return model.Controls.TryGetValue(code, out IofControl control) && control.X.HasValue && control.Y.HasValue && ((x = control.X.Value) == x) && ((y = control.Y.Value) == y);
        }

        /// <summary>Creates the semantic identity used for a CourseControl in EventDB.</summary>
        private static IofCourseControl CreateEventControl(ControlPoint control)
        {
            switch (control.kind) {
                case ControlPointKind.Normal:
                    return new IofCourseControl { Code = control.code ?? "", Type = "Control" };
                case ControlPointKind.Start:
                case ControlPointKind.MapExchange:
                    return new IofCourseControl { Type = "Start" };
                case ControlPointKind.Finish:
                    return new IofCourseControl { Type = "Finish" };
                case ControlPointKind.CrossingPoint:
                    return new IofCourseControl { Type = "CrossingPoint" };
                default:
                    return null;
            }
        }

        /// <summary>Gets typed controls, retaining compatibility with manually-created exchange models.</summary>
        private static List<IofCourseControl> GetCourseControls(IofCourseDataModel source, IofCourse course)
        {
            if (course.CourseControls.Count > 0)
                return course.CourseControls;

            return course.Controls.Select(code => new IofCourseControl {
                Code = code,
                Type = source.Controls.TryGetValue(code, out IofControl control) ? control.Type : "",
            }).ToList();
        }

        /// <summary>Matches special points by type and ordinary controls by their code.</summary>
        private static bool ControlsMatch(IofCourseControl expected, IofCourseControl imported)
        {
            if (expected == null || imported == null)
                return expected == imported;

            string importedType = imported.Type;
            if (String.IsNullOrEmpty(importedType))
                return String.Equals(expected.Code, imported.Code, StringComparison.OrdinalIgnoreCase);
            if (!String.Equals(expected.Type, importedType, StringComparison.OrdinalIgnoreCase))
                return false;
            return String.Equals(expected.Type, "Control", StringComparison.OrdinalIgnoreCase)
                ? String.Equals(expected.Code, imported.Code, StringComparison.OrdinalIgnoreCase)
                : true;
        }

        /// <summary>Returns a readable course-control identity for comparison diagnostics.</summary>
        private static string DisplayControl(IofCourseControl control)
        {
            if (control == null)
                return "<none>";
            return String.IsNullOrEmpty(control.Code) ? control.Type : control.Code;
        }
    }
}
