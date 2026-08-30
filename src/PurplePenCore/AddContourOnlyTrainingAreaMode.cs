/* Copyright (c) 2026, Purple Pen contributors.
 * All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using PurplePen.MapModel;
using PurplePen.Graphics2D;

namespace PurplePen
{
    /// <summary>
    /// Interactive map mode for drawing a contour-only training area.
    /// </summary>
    internal sealed class AddContourOnlyTrainingAreaMode: BaseMode
    {
        private const float CloseDistance = 5F;

        private readonly Controller controller;
        private readonly SelectionMgr selectionMgr;
        private readonly UndoMgr undoMgr;
        private readonly EventDB eventDB;
        private readonly CourseDesignator courseDesignator;
        private readonly float courseObjRatio;
        private readonly CourseAppearance appearance;
        private readonly List<PointF> points = new List<PointF>();

        private int numberFixedPoints;
        private BoundaryCourseObj highlight;

        /// <summary>Creates the drawing mode for one concrete course view.</summary>
        public AddContourOnlyTrainingAreaMode(Controller controller, SelectionMgr selectionMgr, UndoMgr undoMgr, EventDB eventDB, CourseDesignator courseDesignator)
        {
            this.controller = controller;
            this.selectionMgr = selectionMgr;
            this.undoMgr = undoMgr;
            this.eventDB = eventDB;
            this.courseDesignator = courseDesignator.Clone();
            this.appearance = controller.GetCourseAppearance();
            this.courseObjRatio = selectionMgr.ActiveCourseView.CourseObjRatio(appearance);
        }

        public override string StatusText
        {
            get { return StatusBarText.AddingLineArea; }
        }

        public override MousePointerShape GetMouseCursor(Pane pane, PointF location, float pixelSize)
        {
            return pane == Pane.Map ? MousePointerShape.Cross : MousePointerShape.Arrow;
        }

        public override IMapViewerHighlight[] GetHighlights(Pane pane)
        {
            return pane == Pane.Map && highlight != null ? new IMapViewerHighlight[] { highlight } : null;
        }

        public override DragAction LeftButtonDown(Pane pane, PointF location, float pixelSize, ref bool displayUpdateNeeded)
        {
            if (pane != Pane.Map)
                return DragAction.None;

            if (numberFixedPoints == 0)
                AddFixedPoint(location);

            displayUpdateNeeded = true;
            return DragAction.DelayedDrag;
        }

        public override void LeftButtonDrag(Pane pane, PointF location, PointF locationStart, float pixelSize, ref bool displayUpdateNeeded)
        {
            AddUnfixedPoint(location);
            displayUpdateNeeded = true;
        }

        public override async Task<bool> LeftButtonEndDrag(Pane pane, PointF location, PointF locationStart, float pixelSize)
        {
            if (pane != Pane.Map)
                return false;

            if (numberFixedPoints >= 3 && Geometry.Distance(location, points[0]) < pixelSize * CloseDistance) {
                CreateObject();
                controller.DefaultCommandMode();
                return true;
            }

            AddFixedPoint(location);
            return true;
        }

        public override async Task<bool> LeftButtonClick(Pane pane, PointF location, float pixelSize)
        {
            if (pane != Pane.Map)
                return false;

            // A click completes the polygon, matching the line/area special tool.
            if (CreateObject())
                controller.DefaultCommandMode();
            return true;
        }

        private void AddFixedPoint(PointF point)
        {
            AddUnfixedPoint(point);
            ++numberFixedPoints;
        }

        private void AddUnfixedPoint(PointF point)
        {
            if (numberFixedPoints > points.Count - 1)
                points.Add(point);
            else
                points[numberFixedPoints] = point;

            if (points.Count >= 2)
                highlight = new BoundaryCourseObj(Id<Special>.None, courseObjRatio, appearance, new SymPath(points.ToArray()));
        }

        private bool CreateObject()
        {
            if (numberFixedPoints < 3)
                return false;

            undoMgr.BeginCommand(1327, CommandNameText.AddObject);
            ChangeEvent.AddContourOnlyTrainingExercise(eventDB, courseDesignator, points.GetRange(0, numberFixedPoints).ToArray());
            undoMgr.EndCommand(1327);
            return true;
        }
    }
}
