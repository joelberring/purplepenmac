using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PurplePen
{
    /// <summary>Builds localized backside information consistently for PDF and preview output.</summary>
    public static class BacksideInfoFormatter
    {
        /// <summary>Returns the localized lines for one printable course variation.</summary>
        public static List<string> GetLines(EventDB eventDB, CourseView courseView, string backsideText, IEnumerable<BacksideInfoRecord> records)
        {
            List<string> lines = new List<string>();
            if (!String.IsNullOrWhiteSpace(backsideText))
                lines.Add(backsideText.Trim());

            BacksideInfoRecord imported = FindImportedRecord(eventDB, courseView, records);

            if (imported != null) {
                AddValue(lines, "BacksideName", imported.Name);
                AddValue(lines, "BacksideClass", imported.ClassName);
                AddValue(lines, "BacksideTeamName", imported.TeamName);
            }

            AddValue(lines, "BacksideCourse", courseView.CourseNameAndPart);
            if (courseView.RelayTeam.HasValue)
                AddValue(lines, "BacksideTeam", courseView.RelayTeam.Value.ToString(CultureInfo.CurrentCulture));
            if (courseView.RelayLeg.HasValue)
                AddValue(lines, "BacksideLeg", courseView.RelayLeg.Value.ToString(CultureInfo.CurrentCulture));
            if (!String.IsNullOrEmpty(courseView.VariationName))
                AddValue(lines, "BacksideVariationCode", courseView.VariationName);
            return lines;
        }

        /// <summary>Returns the EventClass names assigned to the printed course.</summary>
        public static string[] GetClassNames(EventDB eventDB, CourseView courseView)
        {
            return eventDB == null || courseView.BaseCourseId.IsNotNone == false
                ? Array.Empty<string>()
                : EventClassSupport.GetClasses(eventDB, courseView.BaseCourseId)
                    .Select(pair => pair.Value.Name)
                    .Where(name => !String.IsNullOrWhiteSpace(name))
                    .ToArray();
        }

        /// <summary>Finds a unique imported backside record using all assigned EventClasses.</summary>
        public static BacksideInfoRecord FindImportedRecord(EventDB eventDB, CourseView courseView, IEnumerable<BacksideInfoRecord> records)
        {
            foreach (string className in GetClassNames(eventDB, courseView)) {
                BacksideInfoRecord imported = BacksideInfoRecord.FindUniqueMatch(records, courseView.RelayTeam ?? 0, courseView.RelayLeg ?? 0,
                                                                                  courseView.CourseName, courseView.CourseNameAndPart, className);
                if (imported != null)
                    return imported;
            }
            return BacksideInfoRecord.FindUniqueMatch(records, courseView.RelayTeam ?? 0, courseView.RelayLeg ?? 0,
                                                      courseView.CourseName, courseView.CourseNameAndPart, null);
        }

        private static void AddValue(List<string> lines, string resourceKey, string value)
        {
            if (String.IsNullOrWhiteSpace(value))
                return;
            string label = MiscText.ResourceManager.GetString(resourceKey, CultureInfo.CurrentUICulture) ?? resourceKey;
            lines.Add(String.Format(CultureInfo.CurrentCulture, "{0}: {1}", label, value.Trim()));
        }
    }
}
