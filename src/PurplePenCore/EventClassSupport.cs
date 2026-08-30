/* Copyright (c) 2026, Purple Pen contributors. */

using System;
using System.Collections.Generic;
using System.Linq;

namespace PurplePen
{
    /// <summary>Aggregates class settings for course-load and print planning.</summary>
    public static class EventClassSupport
    {
        public sealed class ClassParticipantSuggestion
        {
            public string ClassName = String.Empty;
            public int ImportedCount;
            public int ExistingCount;
            public bool IsConflict;
        }

        /// <summary>Matches imported start-list class names to persisted classes.</summary>
        public static List<ClassParticipantSuggestion> MatchStartList(EventDB eventDB, IEnumerable<BacksideInfoRecord> records)
        {
            Dictionary<string, int> counts = SuggestParticipantCounts(records.Select(record => record.ClassName));
            return counts.Select(pair => {
                EventClass[] matches = eventDB.AllEventClasses.Where(item => String.Equals(item.Name, pair.Key, StringComparison.CurrentCultureIgnoreCase)).ToArray();
                int existing = matches.Length == 1 ? matches[0].ParticipantCount : 0;
                return new ClassParticipantSuggestion { ClassName = pair.Key, ImportedCount = pair.Value, ExistingCount = existing, IsConflict = matches.Length != 1 };
            }).OrderBy(item => item.ClassName, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        /// <summary>Returns classes sorted by name, optionally restricted to a course.</summary>
        public static IEnumerable<KeyValuePair<Id<EventClass>, EventClass>> GetClasses(EventDB eventDB, Id<Course> courseId)
        {
            IEnumerable<KeyValuePair<Id<EventClass>, EventClass>> result = eventDB.AllEventClassPairs;
            if (courseId.IsNotNone)
                result = result.Where(pair => pair.Value.CourseId == courseId);
            return result.OrderBy(pair => pair.Value.Name, StringComparer.CurrentCultureIgnoreCase);
        }

        /// <summary>Returns the total participant load for a course's event classes.</summary>
        public static int GetCourseParticipantCount(EventDB eventDB, Id<Course> courseId)
        {
            List<KeyValuePair<Id<EventClass>, EventClass>> classes = GetClasses(eventDB, courseId).ToList();
            return classes.Count > 0
                ? classes.Sum(pair => pair.Value.ParticipantCount)
                : GetLegacyCourseLoad(eventDB, courseId);
        }

        /// <summary>
        /// Returns the legacy per-course load. This is deliberately private to the
        /// compatibility layer: callers should use <see cref="GetCourseParticipantCount"/>
        /// so event classes remain the authoritative source when present.
        /// </summary>
        private static int GetLegacyCourseLoad(EventDB eventDB, Id<Course> courseId)
        {
            int load = eventDB.GetCourse(courseId).load;
            return load >= 0 ? load : -1;
        }

        /// <summary>Returns the total required map count for a course.</summary>
        public static int GetCourseRequiredMapCount(EventDB eventDB, Id<Course> courseId)
        {
            List<KeyValuePair<Id<EventClass>, EventClass>> classes = GetClasses(eventDB, courseId).ToList();
            return classes.Count > 0
                ? classes.Sum(pair => pair.Value.RequiredMapCount)
                : Math.Max(0, GetLegacyCourseLoad(eventDB, courseId));
        }

        /// <summary>Returns the course's event-class names, or its legacy class name when needed.</summary>
        public static string GetCourseClassNames(EventDB eventDB, Id<Course> courseId)
        {
            List<string> names = GetClasses(eventDB, courseId)
                .Select(pair => pair.Value.Name == null ? String.Empty : pair.Value.Name.Trim())
                .Where(name => name.Length > 0)
                .ToList();
            if (names.Count > 0)
                return String.Join(", ", names);

            return eventDB.GetCourse(courseId).className ?? String.Empty;
        }

        /// <summary>Suggests class participant counts from imported start-list class names.</summary>
        public static Dictionary<string, int> SuggestParticipantCounts(IEnumerable<string> classNames)
        {
            return classNames.Where(name => !String.IsNullOrWhiteSpace(name))
                .GroupBy(name => name.Trim(), StringComparer.CurrentCultureIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.CurrentCultureIgnoreCase);
        }
    }
}
