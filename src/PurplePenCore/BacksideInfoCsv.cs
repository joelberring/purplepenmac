// BacksideInfoCsv.cs
//
// Imports relay participant information for PDF information backsides. The
// format deliberately uses a small, portable CSV contract so that start lists
// exported from MeOS can be reviewed or adjusted before map production.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PurplePen
{
    /// <summary>Optional participant and team information for one relay map backside.</summary>
    public class BacksideInfoRecord
    {
        /// <summary>Optional Purple Pen course name used to narrow the match.</summary>
        public string Course = String.Empty;

        /// <summary>Relay team number, matching Purple Pen's team number.</summary>
        public int Team;

        /// <summary>Relay leg number, matching Purple Pen's leg number.</summary>
        public int Leg;

        /// <summary>Competitor name printed on the backside when supplied.</summary>
        public string Name = String.Empty;

        /// <summary>Class name printed on the backside when supplied.</summary>
        public string ClassName = String.Empty;

        /// <summary>Team name printed on the backside when supplied.</summary>
        public string TeamName = String.Empty;

        /// <summary>Checks whether this record belongs to a particular printed relay variation.</summary>
        /// <param name="courseView">The course variation being printed.</param>
        /// <returns>True when the relay and optional course keys agree.</returns>
        public bool Matches(CourseView courseView)
        {
            if (courseView.RelayTeam != Team || courseView.RelayLeg != Leg)
                return false;

            return String.IsNullOrWhiteSpace(Course) ||
                   String.Equals(Course.Trim(), courseView.CourseName, StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(Course.Trim(), courseView.CourseNameAndPart, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Finds the one safe imported record for a relay course. A specific
        /// course and class take precedence over blank values, but duplicate
        /// candidates deliberately produce no result rather than attaching the
        /// first row in an arbitrary import order.
        /// </summary>
        /// <param name="records">Imported start-list records to inspect.</param>
        /// <param name="team">Purple Pen relay team number.</param>
        /// <param name="leg">Purple Pen relay leg number.</param>
        /// <param name="courseName">Purple Pen course name.</param>
        /// <param name="courseNameAndPart">Purple Pen course name including an optional part suffix.</param>
        /// <param name="className">Explicit Purple Pen course class, when assigned.</param>
        /// <returns>The unique compatible record, or null when no unambiguous match exists.</returns>
        public static BacksideInfoRecord FindUniqueMatch(IEnumerable<BacksideInfoRecord> records, int team, int leg, string courseName, string courseNameAndPart, string className)
        {
            if (records == null)
                return null;

            List<BacksideInfoRecord> relayCandidates = records.Where(record => record != null && record.Team == team && record.Leg == leg).ToList();
            List<BacksideInfoRecord> courseCandidates = relayCandidates.Where(record => CourseMatches(record.Course, courseName, courseNameAndPart)).ToList();
            if (courseCandidates.Count == 0)
                courseCandidates = relayCandidates.Where(record => String.IsNullOrWhiteSpace(record.Course)).ToList();

            if (courseCandidates.Count == 0)
                return null;

            if (!String.IsNullOrWhiteSpace(className)) {
                List<BacksideInfoRecord> exactClassCandidates = courseCandidates.Where(record => ClassMatches(record.ClassName, className)).ToList();
                if (exactClassCandidates.Count > 0)
                    return exactClassCandidates.Count == 1 ? exactClassCandidates[0] : null;

                courseCandidates = courseCandidates.Where(record => String.IsNullOrWhiteSpace(record.ClassName)).ToList();
            }

            return courseCandidates.Count == 1 ? courseCandidates[0] : null;
        }

        /// <summary>Checks whether an imported course value is a specific match for a printed course.</summary>
        static bool CourseMatches(string importedCourse, string courseName, string courseNameAndPart)
        {
            return !String.IsNullOrWhiteSpace(importedCourse) &&
                   (String.Equals(importedCourse.Trim(), courseName, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(importedCourse.Trim(), courseNameAndPart, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Checks whether an imported class value agrees with the explicit course class.</summary>
        static bool ClassMatches(string importedClassName, string className)
        {
            return !String.IsNullOrWhiteSpace(importedClassName) &&
                   String.Equals(importedClassName.Trim(), className.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Creates an independent copy suitable for export settings.</summary>
        /// <returns>A copy of this record.</returns>
        public BacksideInfoRecord Clone()
        {
            return (BacksideInfoRecord)MemberwiseClone();
        }
    }

    /// <summary>Imports relay backside information from a CSV start-list export.</summary>
    public static class BacksideInfoCsv
    {
        /// <summary>
        /// Imports records from a comma, semicolon, or tab-delimited CSV file.
        /// Required headings are team/lag and leg/sträcka. Optional headings are
        /// course/bana, name/namn, class/klass, and team name/lagnamn.
        /// </summary>
        /// <param name="reader">The CSV text to import.</param>
        /// <returns>The imported participant records.</returns>
        public static List<BacksideInfoRecord> Import(TextReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            string headerLine = reader.ReadLine();
            if (String.IsNullOrWhiteSpace(headerLine))
                throw new FormatException("The CSV file has no header row.");

            char delimiter = DetectDelimiter(headerLine);
            List<string> headings = ParseLine(headerLine, delimiter);
            int teamColumn = FindColumn(headings, "team", "lag", "teamnumber", "startnumber", "bib");
            int legColumn = FindColumn(headings, "leg", "sträcka", "stracka");
            if (teamColumn < 0 || legColumn < 0)
                throw new FormatException("The CSV file must contain team/lag and leg/sträcka columns.");

            int courseColumn = FindColumn(headings, "course", "bana");
            int nameColumn = FindColumn(headings, "name", "namn", "competitor", "runner");
            int classColumn = FindColumn(headings, "class", "klass");
            int teamNameColumn = FindColumn(headings, "teamname", "lagnamn");
            List<BacksideInfoRecord> records = new List<BacksideInfoRecord>();
            string line;

            while ((line = reader.ReadLine()) != null) {
                if (String.IsNullOrWhiteSpace(line))
                    continue;

                List<string> values = ParseLine(line, delimiter);
                int team = ParseRequiredPositiveInteger(GetValue(values, teamColumn), "team/lag");
                int leg = ParseRequiredPositiveInteger(GetValue(values, legColumn), "leg/sträcka");
                BacksideInfoRecord record = new BacksideInfoRecord {
                    Team = team,
                    Leg = leg,
                    Course = GetValue(values, courseColumn),
                    Name = GetValue(values, nameColumn),
                    ClassName = GetValue(values, classColumn),
                    TeamName = GetValue(values, teamNameColumn),
                };
                records.Add(record);
            }

            if (records.Count == 0)
                throw new FormatException("The CSV file contains no participant rows.");

            return records;
        }

        /// <summary>Finds a supported normalized heading in the CSV header.</summary>
        static int FindColumn(List<string> headings, params string[] aliases)
        {
            for (int index = 0; index < headings.Count; ++index) {
                string heading = NormalizeHeading(headings[index]);
                foreach (string alias in aliases) {
                    if (heading == NormalizeHeading(alias))
                        return index;
                }
            }

            return -1;
        }

        /// <summary>Gets a CSV value or an empty value for optional absent columns.</summary>
        static string GetValue(List<string> values, int column)
        {
            if (column < 0 || column >= values.Count)
                return String.Empty;
            return values[column].Trim();
        }

        /// <summary>Parses a required positive integer from a CSV value.</summary>
        static int ParseRequiredPositiveInteger(string value, string description)
        {
            int number;
            if (!Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number) || number < 1)
                throw new FormatException(string.Format("Invalid {0} value '{1}'.", description, value));
            return number;
        }

        /// <summary>Chooses the most common standard delimiter in a header line.</summary>
        static char DetectDelimiter(string line)
        {
            int commaCount = CountCharacter(line, ',');
            int semicolonCount = CountCharacter(line, ';');
            int tabCount = CountCharacter(line, '\t');
            if (tabCount >= commaCount && tabCount >= semicolonCount && tabCount > 0)
                return '\t';
            if (semicolonCount >= commaCount && semicolonCount > 0)
                return ';';
            return ',';
        }

        /// <summary>Counts a character in a string.</summary>
        static int CountCharacter(string text, char character)
        {
            int count = 0;
            foreach (char current in text) {
                if (current == character)
                    ++count;
            }
            return count;
        }

        /// <summary>Parses one RFC-4180-style CSV row.</summary>
        static List<string> ParseLine(string line, char delimiter)
        {
            List<string> values = new List<string>();
            System.Text.StringBuilder value = new System.Text.StringBuilder();
            bool quoted = false;

            for (int index = 0; index < line.Length; ++index) {
                char current = line[index];
                if (current == '"') {
                    if (quoted && index + 1 < line.Length && line[index + 1] == '"') {
                        value.Append('"');
                        ++index;
                    }
                    else {
                        quoted = !quoted;
                    }
                }
                else if (current == delimiter && !quoted) {
                    values.Add(value.ToString());
                    value.Clear();
                }
                else {
                    value.Append(current);
                }
            }

            if (quoted)
                throw new FormatException("The CSV file has an unclosed quoted value.");

            values.Add(value.ToString());
            return values;
        }

        /// <summary>Normalizes a column heading before comparing aliases.</summary>
        static string NormalizeHeading(string heading)
        {
            return heading.Trim().ToLowerInvariant().Replace(" ", String.Empty).Replace("_", String.Empty).Replace("-", String.Empty);
        }
    }
}
