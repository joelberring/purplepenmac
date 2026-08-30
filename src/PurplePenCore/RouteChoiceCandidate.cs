/* Copyright (c) 2026, Purple Pen contributors. */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Xml;
using PurplePen.Graphics2D;

namespace PurplePen
{
    /// <summary>A manually drawn alternative route attached to one concrete course leg.</summary>
    public class RouteChoiceCandidate : StorableObject
    {
        public CourseDesignator courseDesignator;
        public Id<CourseControl> legStartCourseControlId;
        public string name;
        public string source;
        public PointF[] locations;
        public string notes;

        public RouteChoiceCandidate() { }

        /// <summary>Creates a candidate route. The first course control identifies the leg start.</summary>
        public RouteChoiceCandidate(CourseDesignator courseDesignator, Id<CourseControl> legStartCourseControlId,
                                    string name, string source, PointF[] locations, string notes)
        {
            this.courseDesignator = courseDesignator == null ? null : courseDesignator.Clone();
            this.legStartCourseControlId = legStartCourseControlId;
            this.name = name ?? "";
            this.source = source ?? "";
            this.locations = locations == null ? new PointF[0] : (PointF[])locations.Clone();
            this.notes = notes ?? "";
        }

        /// <summary>Validates the candidate and its association with a real course leg.</summary>
        public void Validate(Id<RouteChoiceCandidate> id, EventDB.ValidateInfo validateInfo)
        {
            if (courseDesignator == null || courseDesignator.IsAllControls || courseDesignator.IsVariation)
                throw new ApplicationException(string.Format("Route choice candidate {0} must be assigned to a normal course", id));
            validateInfo.eventDB.CheckCourseId(courseDesignator.CourseId);
            if (QueryEvent.HasVariations(validateInfo.eventDB, courseDesignator.CourseId))
                throw new ApplicationException(string.Format("Route choice candidate {0} must be assigned to a course without variations", id));
            if (legStartCourseControlId.IsNone)
                throw new ApplicationException(string.Format("Route choice candidate {0} has no leg start", id));
            validateInfo.eventDB.CheckCourseControlId(legStartCourseControlId);
            if (!QueryEvent.EnumCourseControlIds(validateInfo.eventDB, courseDesignator).Contains(legStartCourseControlId))
                throw new ApplicationException(string.Format("Route choice candidate {0} is assigned outside its course", id));
            CourseControl start = validateInfo.eventDB.GetCourseControl(legStartCourseControlId);
            if (start.nextCourseControl.IsNone)
                throw new ApplicationException(string.Format("Route choice candidate {0} has no leg end", id));
            if (locations == null || locations.Length < 2)
                throw new ApplicationException(string.Format("Route choice candidate {0} must have at least two points", id));
            foreach (PointF point in locations) {
                if (Single.IsNaN(point.X) || Single.IsInfinity(point.X) || Single.IsNaN(point.Y) || Single.IsInfinity(point.Y))
                    throw new ApplicationException(string.Format("Route choice candidate {0} has invalid geometry", id));
            }
            CourseControl end = validateInfo.eventDB.GetCourseControl(start.nextCourseControl);
            PointF expectedStart = validateInfo.eventDB.GetControl(start.control).location;
            PointF expectedEnd = validateInfo.eventDB.GetControl(end.control).location;
            if (Geometry.Distance(locations[0], expectedStart) > 0.001 || Geometry.Distance(locations[locations.Length - 1], expectedEnd) > 0.001)
                throw new ApplicationException(string.Format("Route choice candidate {0} must start and end at its leg controls", id));
        }

        /// <summary>Gets the baseline statistics for this candidate's course leg.</summary>
        public RouteChoiceStatistics GetBaselineStatistics(EventDB eventDB)
        {
            if (eventDB == null) throw new ArgumentNullException(nameof(eventDB));
            CourseControl start = eventDB.GetCourseControl(legStartCourseControlId);
            CourseControl end = eventDB.GetCourseControl(start.nextCourseControl);
            return RouteChoiceAnalysis.AnalyzeLeg(eventDB, start.control, end.control,
                                                  QueryEvent.FindLeg(eventDB, start.control, end.control));
        }

        /// <summary>Gets statistics for the manually supplied route points.</summary>
        public RouteChoiceStatistics GetCandidateStatistics(EventDB eventDB)
        {
            if (eventDB == null) throw new ArgumentNullException(nameof(eventDB));
            return RouteChoiceAnalysis.AnalyzeRoute(locations, eventDB.GetEvent().mapScale);
        }

        public override StorableObject Clone()
        {
            RouteChoiceCandidate clone = (RouteChoiceCandidate)base.Clone();
            clone.courseDesignator = courseDesignator == null ? null : courseDesignator.Clone();
            clone.locations = locations == null ? null : (PointF[])locations.Clone();
            return clone;
        }

        public override bool Equals(object obj)
        {
            RouteChoiceCandidate other = obj as RouteChoiceCandidate;
            return other != null && other.courseDesignator == courseDesignator && other.legStartCourseControlId == legStartCourseControlId &&
                other.name == name && other.source == source && other.notes == notes &&
                ((locations == null && other.locations == null) || (locations != null && other.locations != null && locations.SequenceEqual(other.locations)));
        }

        public override int GetHashCode()
        {
            int hash = (courseDesignator == null ? 0 : courseDesignator.GetHashCode()) ^ legStartCourseControlId.GetHashCode();
            hash ^= (name ?? "").GetHashCode() ^ (source ?? "").GetHashCode() ^ (notes ?? "").GetHashCode();
            if (locations != null) foreach (PointF point in locations) hash ^= point.GetHashCode();
            return hash;
        }

        public override void ReadAttributesAndContent(XmlInput xmlinput)
        {
            int courseId = xmlinput.GetAttributeInt("course");
            int part = xmlinput.GetAttributeInt("part", -1);
            courseDesignator = part >= 0 ? new CourseDesignator(new Id<Course>(courseId), part) : new CourseDesignator(new Id<Course>(courseId));
            legStartCourseControlId = new Id<CourseControl>(xmlinput.GetAttributeInt("leg-start-course-control"));
            name = xmlinput.GetAttributeString("name", "");
            source = xmlinput.GetAttributeString("source", "");
            notes = "";
            List<PointF> points = new List<PointF>();
            bool first = true;
            while (xmlinput.FindSubElement(first, "point", "notes")) {
                if (xmlinput.Name == "notes") notes = xmlinput.GetContentString();
                else {
                    points.Add(new PointF(xmlinput.GetAttributeFloat("x"), xmlinput.GetAttributeFloat("y")));
                    xmlinput.Skip();
                }
                first = false;
            }
            locations = points.ToArray();
        }

        public override void WriteAttributesAndContent(XmlTextWriter xmloutput)
        {
            xmloutput.WriteAttributeString("course", XmlConvert.ToString(courseDesignator.CourseId.id));
            if (!courseDesignator.AllParts) xmloutput.WriteAttributeString("part", XmlConvert.ToString(courseDesignator.Part));
            xmloutput.WriteAttributeString("leg-start-course-control", XmlConvert.ToString(legStartCourseControlId.id));
            xmloutput.WriteAttributeString("name", name ?? "");
            xmloutput.WriteAttributeString("source", source ?? "");
            xmloutput.WriteElementString("notes", notes ?? "");
            if (locations != null) foreach (PointF point in locations) {
                xmloutput.WriteStartElement("point");
                xmloutput.WriteAttributeString("x", XmlConvert.ToString(point.X));
                xmloutput.WriteAttributeString("y", XmlConvert.ToString(point.Y));
                xmloutput.WriteEndElement();
            }
        }

        public override string ElementName { get { return "route-choice-candidate"; } }
    }
}
