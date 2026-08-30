/* Copyright (c) 2026, Purple Pen contributors.
 *
 * A persisted competition class and its map/participant settings.
 */

using System;
using System.Linq;
using System.Xml;

namespace PurplePen
{
    /// <summary>Stores the competition class associated with one course.</summary>
    public class EventClass : StorableObject
    {
        public string Name = String.Empty;
        public Id<Course> CourseId = Id<Course>.None;
        public int ParticipantCount;
        public int StartInterval;
        public int BibNumberStart;
        public int BibNumberEnd;
        public int MapCount = 1;
        public int ReserveCount;

        /// <summary>Number of maps required for this class, including reserves.</summary>
        public int RequiredMapCount
        {
            get { return Math.Max(0, ParticipantCount) * Math.Max(0, MapCount) + Math.Max(0, ReserveCount); }
        }

        public override string ElementName { get { return "event-class"; } }

        public override void ReadAttributesAndContent(XmlInput xmlinput)
        {
            Name = xmlinput.GetAttributeString("name", String.Empty);
            CourseId = new Id<Course>(xmlinput.GetAttributeInt("course-id", 0));
            ParticipantCount = xmlinput.GetAttributeInt("participants", 0);
            StartInterval = xmlinput.GetAttributeInt("start-interval", 0);
            BibNumberStart = xmlinput.GetAttributeInt("bib-start", 0);
            BibNumberEnd = xmlinput.GetAttributeInt("bib-end", 0);
            MapCount = xmlinput.GetAttributeInt("maps", 1);
            ReserveCount = xmlinput.GetAttributeInt("reserves", 0);
            xmlinput.Skip();
        }

        public override void WriteAttributesAndContent(XmlTextWriter xmloutput)
        {
            if (!String.IsNullOrEmpty(Name))
                xmloutput.WriteAttributeString("name", Name);
            if (CourseId.IsNotNone)
                xmloutput.WriteAttributeString("course-id", XmlConvert.ToString(CourseId.id));
            if (ParticipantCount != 0)
                xmloutput.WriteAttributeString("participants", XmlConvert.ToString(ParticipantCount));
            if (StartInterval != 0)
                xmloutput.WriteAttributeString("start-interval", XmlConvert.ToString(StartInterval));
            if (BibNumberStart != 0)
                xmloutput.WriteAttributeString("bib-start", XmlConvert.ToString(BibNumberStart));
            if (BibNumberEnd != 0)
                xmloutput.WriteAttributeString("bib-end", XmlConvert.ToString(BibNumberEnd));
            if (MapCount != 1)
                xmloutput.WriteAttributeString("maps", XmlConvert.ToString(MapCount));
            if (ReserveCount != 0)
                xmloutput.WriteAttributeString("reserves", XmlConvert.ToString(ReserveCount));
        }

        public void Validate(Id<EventClass> id, EventDB.ValidateInfo validateInfo)
        {
            if (String.IsNullOrWhiteSpace(Name))
                throw new ApplicationException(String.Format("Event class '{0}' should have a name", id));
            if (CourseId.IsNone || !validateInfo.eventDB.AllCourseIds.Contains(CourseId))
                throw new ApplicationException(String.Format("Event class '{0}' has an invalid course", id));
            if (ParticipantCount < 0 || StartInterval < 0 || BibNumberStart < 0 || BibNumberEnd < 0 || MapCount < 0 || ReserveCount < 0)
                throw new ApplicationException(String.Format("Event class '{0}' has a negative setting", id));
            if (BibNumberEnd != 0 && BibNumberStart != 0 && BibNumberEnd < BibNumberStart)
                throw new ApplicationException(String.Format("Event class '{0}' has an invalid bib range", id));
        }
    }
}
