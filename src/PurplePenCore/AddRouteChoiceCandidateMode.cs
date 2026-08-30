/* Copyright (c) 2026, Purple Pen contributors. */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using PurplePen.Graphics2D;
using PurplePen.MapModel;

namespace PurplePen
{
    /// <summary>Interactive map mode for drawing a manual route-choice candidate.</summary>
    internal sealed class AddRouteChoiceCandidateMode : BaseMode
    {
        private readonly Controller controller;
        private readonly SelectionMgr selectionMgr;
        private readonly UndoMgr undoMgr;
        private readonly EventDB eventDB;
        private readonly CourseDesignator courseDesignator;
        private readonly Id<CourseControl> legStartCourseControlId;
        private readonly float courseObjRatio;
        private readonly CourseAppearance appearance;
        private readonly List<PointF> points = new List<PointF>();
        private BoundaryCourseObj highlight;
        private int fixedPointCount;

        /// <summary>Creates a drawing mode for one concrete course leg.</summary>
        public AddRouteChoiceCandidateMode(Controller controller, SelectionMgr selectionMgr, UndoMgr undoMgr,
                                           EventDB eventDB, CourseDesignator courseDesignator,
                                           Id<CourseControl> legStartCourseControlId)
        {
            this.controller = controller;
            this.selectionMgr = selectionMgr;
            this.undoMgr = undoMgr;
            this.eventDB = eventDB;
            this.courseDesignator = courseDesignator.Clone();
            this.legStartCourseControlId = legStartCourseControlId;
            appearance = controller.GetCourseAppearance();
            courseObjRatio = selectionMgr.ActiveCourseView.CourseObjRatio(appearance);
        }

        public override string StatusText { get { return "Adding route-choice candidate"; } }
        public override MousePointerShape GetMouseCursor(Pane pane, PointF location, float pixelSize) { return pane == Pane.Map ? MousePointerShape.Cross : MousePointerShape.Arrow; }
        public override IMapViewerHighlight[] GetHighlights(Pane pane) { return pane == Pane.Map && highlight != null ? new IMapViewerHighlight[] { highlight } : null; }

        public override DragAction LeftButtonDown(Pane pane, PointF location, float pixelSize, ref bool displayUpdateNeeded)
        {
            if (pane != Pane.Map) return DragAction.None;
            if (fixedPointCount == 0) AddFixedPoint(location);
            displayUpdateNeeded = true;
            return DragAction.DelayedDrag;
        }

        public override void LeftButtonDrag(Pane pane, PointF location, PointF locationStart, float pixelSize, ref bool displayUpdateNeeded)
        {
            if (pane == Pane.Map) { AddUnfixedPoint(location); displayUpdateNeeded = true; }
        }

        public override async Task<bool> LeftButtonEndDrag(Pane pane, PointF location, PointF locationStart, float pixelSize)
        {
            if (pane != Pane.Map) return false;
            AddFixedPoint(location);
            return true;
        }

        public override async Task<bool> LeftButtonClick(Pane pane, PointF location, float pixelSize)
        {
            if (pane != Pane.Map) return false;
            if (fixedPointCount >= 2) {
                Create();
                controller.DefaultCommandMode();
            }
            else AddFixedPoint(location);
            return true;
        }

        private void AddFixedPoint(PointF point) { AddUnfixedPoint(point); ++fixedPointCount; }

        private void AddUnfixedPoint(PointF point)
        {
            if (fixedPointCount > points.Count - 1) points.Add(point); else points[fixedPointCount] = point;
            if (points.Count >= 2) highlight = new BoundaryCourseObj(Id<Special>.None, courseObjRatio, appearance, new SymPath(points.ToArray()));
        }

        private void Create()
        {
            CourseControl startCourseControl = eventDB.GetCourseControl(legStartCourseControlId);
            CourseControl endCourseControl = eventDB.GetCourseControl(startCourseControl.nextCourseControl);
            List<PointF> routePoints = new List<PointF>(fixedPointCount + 2) {
                eventDB.GetControl(startCourseControl.control).location
            };
            routePoints.AddRange(points.GetRange(0, fixedPointCount));
            routePoints.Add(eventDB.GetControl(endCourseControl.control).location);
            PointF[] route = routePoints.ToArray();
            undoMgr.BeginCommand(1329, "Add route-choice candidate");
            ChangeEvent.AddRouteChoiceCandidate(eventDB, courseDesignator, legStartCourseControlId, "Candidate", "Manual", route, "");
            undoMgr.EndCommand(1329);
        }
    }
}
