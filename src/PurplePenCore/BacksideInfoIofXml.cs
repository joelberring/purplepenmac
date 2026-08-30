// BacksideInfoIofXml.cs
// Imports the portable portions of IOF XML 3.0 start lists for PDF production.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace PurplePen
{
    /// <summary>Imports class and competitor information from an IOF XML 3.0 start list.</summary>
    public static class BacksideInfoIofXml
    {
        /// <summary>
        /// Imports person-start records. Course information is optional in IOF start lists,
        /// so an empty course is retained for class-only production matching.
        /// </summary>
        /// <param name="reader">Reader containing an IOF StartList document.</param>
        /// <returns>Imported participant records.</returns>
        public static List<BacksideInfoRecord> Import(TextReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            XDocument document = XDocument.Load(reader, LoadOptions.None);
            XElement root = document.Root;
            if (root == null || root.Name.LocalName != "StartList")
                throw new FormatException("The XML file is not an IOF StartList.");

            List<BacksideInfoRecord> records = new List<BacksideInfoRecord>();
            foreach (XElement personStart in root.Descendants().Where(element => element.Name.LocalName == "PersonStart")) {
                XElement classStart = personStart.Ancestors().FirstOrDefault(element => element.Name.LocalName == "ClassStart");
                string className = ReadNestedName(classStart == null ? null : classStart.Elements().FirstOrDefault(element => element.Name.LocalName == "Class"));
                if (String.IsNullOrWhiteSpace(className))
                    className = ReadNestedName(personStart.Elements().FirstOrDefault(element => element.Name.LocalName == "Class"));

                XElement person = personStart.Elements().FirstOrDefault(element => element.Name.LocalName == "Person");
                string name = ReadPersonName(person);
                XElement teamStart = personStart.Ancestors().FirstOrDefault(element => element.Name.LocalName == "TeamStart");
                string teamName = ReadNestedName(teamStart == null ? null : teamStart.Elements().FirstOrDefault(element => element.Name.LocalName == "Team"));
                if (String.IsNullOrWhiteSpace(teamName))
                    teamName = ReadDirectValue(teamStart, "TeamName");

                records.Add(new BacksideInfoRecord {
                    Team = ReadPositiveInteger(teamStart, "BibNumber", "StartNumber", "TeamNumber"),
                    Leg = ReadPositiveInteger(personStart, "TeamSequence", "Leg", "LegNumber"),
                    Course = ReadNestedName(personStart.Elements().FirstOrDefault(element => element.Name.LocalName == "Course")),
                    Name = name,
                    ClassName = className,
                    TeamName = teamName,
                });
            }

            if (records.Count == 0)
                throw new FormatException("The IOF StartList contains no PersonStart records.");

            return records;
        }

        /// <summary>Reads a display name from an element whose nested Name element may be structured.</summary>
        private static string ReadNestedName(XElement element)
        {
            if (element == null)
                return String.Empty;

            XElement name = element.Elements().FirstOrDefault(child => child.Name.LocalName == "Name");
            return name == null ? String.Empty : name.Value.Trim();
        }

        /// <summary>Reads an IOF person name as given name followed by family name.</summary>
        private static string ReadPersonName(XElement person)
        {
            if (person == null)
                return String.Empty;

            XElement name = person.Elements().FirstOrDefault(child => child.Name.LocalName == "Name");
            if (name == null)
                return String.Empty;

            string given = ReadDirectValue(name, "Given");
            string family = ReadDirectValue(name, "Family");
            return String.Join(" ", new string[] { given, family }.Where(value => !String.IsNullOrWhiteSpace(value)));
        }

        /// <summary>Reads a direct named child value from an XML element.</summary>
        private static string ReadDirectValue(XElement element, string localName)
        {
            if (element == null)
                return String.Empty;

            XElement child = element.Elements().FirstOrDefault(candidate => candidate.Name.LocalName == localName);
            return child == null ? String.Empty : child.Value.Trim();
        }

        /// <summary>Reads the first positive integer from the named descendants of an element.</summary>
        private static int ReadPositiveInteger(XElement element, params string[] localNames)
        {
            if (element == null)
                return 0;

            foreach (string localName in localNames) {
                XElement candidate = element.Descendants().FirstOrDefault(child => child.Name.LocalName == localName);
                if (candidate != null && Int32.TryParse(candidate.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value > 0)
                    return value;
            }

            return 0;
        }
    }
}
