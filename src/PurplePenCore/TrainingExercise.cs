/* Copyright (c) 2026, Purple Pen contributors.
 * All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Xml;

namespace PurplePen
{
    /// <summary>The visual training activities supported by the initial training-course model.</summary>
    public enum TrainingExerciseKind { Corridor, AttackPoint, Line, ContourOnly }

    /// <summary>Specifies which production views include a training exercise.</summary>
    [Flags]
    public enum TrainingExerciseVisibility { Runner = 1, Coach = 2, Answer = 4, All = Runner | Coach | Answer }

    /// <summary>
    /// An immutable contour-only rendering instruction carried from course formatting to map display.
    /// It deliberately contains no reference to the event database, so rendering never changes event data.
    /// </summary>
    public sealed class TrainingContourOnlyOverlay : IEquatable<TrainingContourOnlyOverlay>
    {
        private readonly PointF[] locations;
        private readonly string[] allowedSymbolIds;

        public PointF[] Locations { get { return (PointF[])locations.Clone(); } }
        public float MaskOpacity { get; private set; }
        public string[] AllowedSymbolIds { get { return (string[])allowedSymbolIds.Clone(); } }

        internal PointF[] LocationsForRendering { get { return locations; } }
        internal string[] AllowedSymbolIdsForRendering { get { return allowedSymbolIds; } }

        /// <summary>Creates an isolated rendering instruction from the persisted exercise data.</summary>
        public TrainingContourOnlyOverlay(PointF[] locations, float maskOpacity, string[] allowedSymbolIds)
        {
            this.locations = locations == null ? new PointF[0] : (PointF[])locations.Clone();
            MaskOpacity = maskOpacity;
            this.allowedSymbolIds = allowedSymbolIds == null ? new string[0] : (string[])allowedSymbolIds.Clone();
        }

        /// <summary>Compares the complete immutable overlay data.</summary>
        public bool Equals(TrainingContourOnlyOverlay other)
        {
            return other != null && MaskOpacity == other.MaskOpacity && locations.SequenceEqual(other.locations) && allowedSymbolIds.SequenceEqual(other.allowedSymbolIds);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as TrainingContourOnlyOverlay);
        }

        public override int GetHashCode()
        {
            int hash = MaskOpacity.GetHashCode();
            foreach (PointF location in locations)
                hash ^= location.GetHashCode();
            foreach (string symbolId in allowedSymbolIds)
                hash ^= symbolId.GetHashCode();
            return hash;
        }
    }

    /// <summary>
    /// An immutable white separation band for a runner corridor. The inner polygon remains
    /// transparent so the map is visible in the runnable corridor, while the outer polygon
    /// determines how far the white band extends on each side.
    /// </summary>
    public sealed class TrainingCorridorMaskOverlay
    {
        private readonly RectangleF renderBounds;
        private readonly PointF[] corridorPolygon;
        private readonly PointF[] outerPolygon;

        /// <summary>Gets the outer map extent covered by the mask.</summary>
        public RectangleF RenderBounds { get { return renderBounds; } }

        /// <summary>Gets the corridor polygon left unmasked.</summary>
        public PointF[] CorridorPolygon { get { return (PointF[])corridorPolygon.Clone(); } }

        /// <summary>Gets the outside edge of the white separation band.</summary>
        public PointF[] OuterPolygon { get { return (PointF[])outerPolygon.Clone(); } }

        /// <summary>Gets the mask opacity, from transparent (0) to opaque (1).</summary>
        public float MaskOpacity { get; private set; }

        internal PointF[] CorridorPolygonForRendering { get { return corridorPolygon; } }
        internal PointF[] OuterPolygonForRendering { get { return outerPolygon; } }

        /// <summary>Creates an isolated rendering instruction from formatter data.</summary>
        public TrainingCorridorMaskOverlay(RectangleF renderBounds, PointF[] corridorPolygon, PointF[] outerPolygon, float maskOpacity)
        {
            this.renderBounds = renderBounds;
            this.corridorPolygon = corridorPolygon == null ? new PointF[0] : (PointF[])corridorPolygon.Clone();
            this.outerPolygon = outerPolygon == null ? new PointF[0] : (PointF[])outerPolygon.Clone();
            MaskOpacity = maskOpacity;
        }
    }

    /// <summary>
    /// A persistent, course-specific training overlay. Rendering is deliberately handled separately
    /// so course and competition logic remains unchanged.
    /// </summary>
    public class TrainingExercise : StorableObject
    {
        public TrainingExerciseKind kind;
        public CourseDesignator courseDesignator;
        public Id<CourseControl> legStartCourseControlId;
        public PointF[] locations;
        public float width;
        /// <summary>The width of the white separation band outside each corridor edge, in map millimetres.</summary>
        public float whiteMargin;
        /// <summary>The opacity of the corridor mask, from transparent (0) to opaque (1).</summary>
        public float maskOpacity;
        /// <summary>
        /// The map-symbol identifiers that remain visible in a contour-only area. An empty array
        /// requests the standard contour symbol selection, which is resolved by a later rendering layer.
        /// </summary>
        public string[] allowedSymbolIds = new string[0];
        public string instruction;
        public TrainingExerciseVisibility visibility;

        public TrainingExercise() { }

        /// <summary>Creates a training overlay with copied geometry and the standard production visibility.</summary>
        public TrainingExercise(TrainingExerciseKind kind, CourseDesignator courseDesignator, PointF[] locations)
        {
            this.kind = kind;
            this.courseDesignator = courseDesignator == null ? null : courseDesignator.Clone();
            this.locations = locations == null ? null : (PointF[])locations.Clone();
            maskOpacity = 1;
            whiteMargin = kind == TrainingExerciseKind.Corridor ? 1.5F : 0;
            instruction = "";
            visibility = TrainingExerciseVisibility.All;
        }

        /// <summary>Validates the association, geometry, and display data of this training overlay.</summary>
        public void Validate(Id<TrainingExercise> id, EventDB.ValidateInfo validateInfo)
        {
            if (kind != TrainingExerciseKind.Corridor && kind != TrainingExerciseKind.AttackPoint && kind != TrainingExerciseKind.Line && kind != TrainingExerciseKind.ContourOnly)
                throw new ApplicationException(string.Format("Training exercise {0} has an invalid kind", id));
            if (courseDesignator == null || courseDesignator.IsAllControls)
                throw new ApplicationException(string.Format("Training exercise {0} must be assigned to a course", id));
            if (courseDesignator.IsVariation)
                throw new ApplicationException(string.Format("Training exercise {0} cannot be assigned to a relay variation", id));

            validateInfo.eventDB.CheckCourseId(courseDesignator.CourseId);
            if (!courseDesignator.AllParts && courseDesignator.Part >= QueryEvent.CountCourseParts(validateInfo.eventDB, new CourseDesignator(courseDesignator.CourseId)))
                throw new ApplicationException(string.Format("Training exercise {0} has an invalid course part", id));

            if (locations == null)
                throw new ApplicationException(string.Format("Training exercise {0} must have locations", id));
            if ((kind == TrainingExerciseKind.Corridor || kind == TrainingExerciseKind.Line) && locations.Length < 2)
                throw new ApplicationException(string.Format("Training exercise {0} must have two or more locations", id));
            if (kind == TrainingExerciseKind.AttackPoint && locations.Length != 1)
                throw new ApplicationException(string.Format("Training exercise {0} must have one location", id));
            if (kind == TrainingExerciseKind.ContourOnly && locations.Length < 3)
                throw new ApplicationException(string.Format("Training exercise {0} must have three or more polygon locations", id));
            if ((kind == TrainingExerciseKind.Corridor || kind == TrainingExerciseKind.AttackPoint) && width <= 0)
                throw new ApplicationException(string.Format("Training exercise {0} must have a positive width", id));
            if (kind == TrainingExerciseKind.Corridor && (whiteMargin < 0 || Single.IsNaN(whiteMargin) || Single.IsInfinity(whiteMargin)))
                throw new ApplicationException(string.Format("Training exercise {0} must have a non-negative finite white margin", id));
            if (maskOpacity < 0 || maskOpacity > 1)
                throw new ApplicationException(string.Format("Training exercise {0} must have a mask opacity between zero and one", id));
            if (instruction == null)
                throw new ApplicationException(string.Format("Training exercise {0} must have an instruction", id));
            if (allowedSymbolIds == null)
                throw new ApplicationException(string.Format("Training exercise {0} must have allowed symbol identifiers", id));
            HashSet<string> allowedSymbolIdSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (string symbolId in allowedSymbolIds) {
                if (string.IsNullOrWhiteSpace(symbolId))
                    throw new ApplicationException(string.Format("Training exercise {0} has an empty allowed symbol identifier", id));
                if (!allowedSymbolIdSet.Add(symbolId))
                    throw new ApplicationException(string.Format("Training exercise {0} has a duplicate allowed symbol identifier", id));
            }
            if (visibility == 0 || (visibility & ~TrainingExerciseVisibility.All) != 0)
                throw new ApplicationException(string.Format("Training exercise {0} has an invalid visibility", id));

            if (legStartCourseControlId.IsNotNone) {
                validateInfo.eventDB.CheckCourseControlId(legStartCourseControlId);
                if (!QueryEvent.EnumCourseControlIds(validateInfo.eventDB, courseDesignator).Contains(legStartCourseControlId))
                    throw new ApplicationException(string.Format("Training exercise {0} is assigned to a leg outside its course", id));
            }
        }

        public override StorableObject Clone()
        {
            TrainingExercise clone = (TrainingExercise)base.Clone();
            clone.courseDesignator = courseDesignator == null ? null : courseDesignator.Clone();
            clone.locations = locations == null ? null : (PointF[])locations.Clone();
            clone.allowedSymbolIds = allowedSymbolIds == null ? null : (string[])allowedSymbolIds.Clone();
            return clone;
        }

        public override bool Equals(object obj)
        {
            TrainingExercise other = obj as TrainingExercise;
            if (other == null || other.kind != kind || other.courseDesignator != courseDesignator || other.legStartCourseControlId != legStartCourseControlId || other.width != width || other.whiteMargin != whiteMargin || other.maskOpacity != maskOpacity || other.instruction != instruction || other.visibility != visibility)
                return false;
            if (other.allowedSymbolIds == null || allowedSymbolIds == null) {
                if (other.allowedSymbolIds != allowedSymbolIds)
                    return false;
            }
            else if (!other.allowedSymbolIds.SequenceEqual(allowedSymbolIds))
                return false;
            if (other.locations == null || locations == null)
                return other.locations == locations;
            if (other.locations.Length != locations.Length)
                return false;
            for (int i = 0; i < locations.Length; ++i) {
                if (other.locations[i] != locations[i])
                    return false;
            }
            return true;
        }

        public override int GetHashCode()
        {
            int hash = (int)kind ^ (courseDesignator == null ? 0 : courseDesignator.GetHashCode()) ^ legStartCourseControlId.GetHashCode() ^ width.GetHashCode() ^ whiteMargin.GetHashCode() ^ maskOpacity.GetHashCode() ^ (instruction == null ? 0 : instruction.GetHashCode()) ^ (int)visibility;
            if (locations != null) {
                foreach (PointF location in locations)
                    hash ^= location.GetHashCode();
            }
            if (allowedSymbolIds != null) {
                foreach (string symbolId in allowedSymbolIds)
                    hash ^= symbolId == null ? 0 : symbolId.GetHashCode();
            }
            return hash;
        }

        public override void ReadAttributesAndContent(XmlInput xmlinput)
        {
            switch (xmlinput.GetAttributeString("kind")) {
            case "corridor": kind = TrainingExerciseKind.Corridor; break;
            case "attack-point": kind = TrainingExerciseKind.AttackPoint; break;
            case "line": kind = TrainingExerciseKind.Line; break;
            case "contour-only": kind = TrainingExerciseKind.ContourOnly; break;
            default: xmlinput.BadXml("Invalid training exercise kind '{0}'", xmlinput.GetAttributeString("kind")); break;
            }

            int courseId = xmlinput.GetAttributeInt("course");
            int part = xmlinput.GetAttributeInt("part", -1);
            courseDesignator = part >= 0 ? new CourseDesignator(new Id<Course>(courseId), part) : new CourseDesignator(new Id<Course>(courseId));
            legStartCourseControlId = new Id<CourseControl>(xmlinput.GetAttributeInt("leg-start-course-control", 0));
            width = xmlinput.GetAttributeFloat("width", 0);
            whiteMargin = xmlinput.GetAttributeFloat("white-margin", kind == TrainingExerciseKind.Corridor ? 1.5F : 0);
            // Preserve the historical opaque-mask behavior when opening older event files.
            maskOpacity = xmlinput.GetAttributeFloat("mask-opacity", 1);
            visibility = (TrainingExerciseVisibility)xmlinput.GetAttributeInt("visibility", (int)TrainingExerciseVisibility.All);
            instruction = "";
            List<PointF> locationList = new List<PointF>();
            List<string> allowedSymbolIdList = new List<string>();

            bool first = true;
            while (xmlinput.FindSubElement(first, "instruction", "location", "allowed-symbol")) {
                if (xmlinput.Name == "instruction")
                    instruction = xmlinput.GetContentString();
                else if (xmlinput.Name == "location") {
                    locationList.Add(new PointF(xmlinput.GetAttributeFloat("x"), xmlinput.GetAttributeFloat("y")));
                    xmlinput.Skip();
                }
                else {
                    allowedSymbolIdList.Add(xmlinput.GetAttributeString("id"));
                    xmlinput.Skip();
                }
                first = false;
            }
            locations = locationList.ToArray();
            allowedSymbolIds = allowedSymbolIdList.ToArray();
        }

        public override void WriteAttributesAndContent(XmlTextWriter xmloutput)
        {
            string kindText = kind == TrainingExerciseKind.Corridor ? "corridor" : kind == TrainingExerciseKind.AttackPoint ? "attack-point" : kind == TrainingExerciseKind.Line ? "line" : kind == TrainingExerciseKind.ContourOnly ? "contour-only" : null;
            if (kindText == null)
                throw new ApplicationException("Invalid training exercise kind");
            xmloutput.WriteAttributeString("kind", kindText);
            xmloutput.WriteAttributeString("course", XmlConvert.ToString(courseDesignator.CourseId.id));
            if (!courseDesignator.AllParts)
                xmloutput.WriteAttributeString("part", XmlConvert.ToString(courseDesignator.Part));
            if (legStartCourseControlId.IsNotNone)
                xmloutput.WriteAttributeString("leg-start-course-control", XmlConvert.ToString(legStartCourseControlId.id));
            if (width != 0)
                xmloutput.WriteAttributeString("width", XmlConvert.ToString(width));
            if (whiteMargin != 0)
                xmloutput.WriteAttributeString("white-margin", XmlConvert.ToString(whiteMargin));
            if (maskOpacity != 1)
                xmloutput.WriteAttributeString("mask-opacity", XmlConvert.ToString(maskOpacity));
            if (visibility != TrainingExerciseVisibility.All)
                xmloutput.WriteAttributeString("visibility", XmlConvert.ToString((int)visibility));
            xmloutput.WriteElementString("instruction", instruction);
            foreach (PointF location in locations) {
                xmloutput.WriteStartElement("location");
                xmloutput.WriteAttributeString("x", XmlConvert.ToString(location.X));
                xmloutput.WriteAttributeString("y", XmlConvert.ToString(location.Y));
                xmloutput.WriteEndElement();
            }
            foreach (string symbolId in allowedSymbolIds) {
                xmloutput.WriteStartElement("allowed-symbol");
                xmloutput.WriteAttributeString("id", symbolId);
                xmloutput.WriteEndElement();
            }
        }

        public override string ElementName { get { return "training-exercise"; } }
    }
}
