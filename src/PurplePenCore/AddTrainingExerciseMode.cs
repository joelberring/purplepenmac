/* Copyright (c) 2026, Purple Pen contributors. */
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using PurplePen.Graphics2D;
using PurplePen.MapModel;

namespace PurplePen
{
    /// <summary>Interactive map mode for adding a line or point training exercise.</summary>
    internal sealed class AddTrainingExerciseMode : BaseMode
    {
        private const float CloseDistance = 5F;
        private const float DefaultCorridorWidth = 3F;
        private const float AttackPointControlCircleRatio = 0.6F;
        private readonly Controller controller;
        private readonly SelectionMgr selectionMgr;
        private readonly UndoMgr undoMgr;
        private readonly EventDB eventDB;
        private readonly CourseDesignator courseDesignator;
        private readonly TrainingExerciseKind kind;
        private readonly CourseAppearance appearance;
        private readonly float courseObjRatio;
        private readonly List<PointF> points = new List<PointF>();
        private BoundaryCourseObj highlight;
        private int numberFixedPoints;
        private bool objectCreated;

        /// <summary>Creates an exercise drawing mode for a concrete course.</summary>
        public AddTrainingExerciseMode(Controller controller, SelectionMgr selectionMgr, UndoMgr undoMgr, EventDB eventDB, CourseDesignator courseDesignator, TrainingExerciseKind kind)
        {
            this.controller = controller;
            this.selectionMgr = selectionMgr;
            this.undoMgr = undoMgr;
            this.eventDB = eventDB;
            this.courseDesignator = courseDesignator.Clone();
            this.kind = kind;
            appearance = controller.GetCourseAppearance();
            courseObjRatio = selectionMgr.ActiveCourseView.CourseObjRatio(appearance);
        }

        public override string StatusText { get { return kind == TrainingExerciseKind.AttackPoint ? StatusBarText.AddingObject : StatusBarText.AddingLineArea; } }
        public override MousePointerShape GetMouseCursor(Pane pane, PointF location, float pixelSize) { return pane == Pane.Map ? MousePointerShape.Cross : MousePointerShape.Arrow; }
        public override IMapViewerHighlight[] GetHighlights(Pane pane) { return pane == Pane.Map && highlight != null ? new IMapViewerHighlight[] { highlight } : null; }

        public override DragAction LeftButtonDown(Pane pane, PointF location, float pixelSize, ref bool displayUpdateNeeded)
        {
            if (pane != Pane.Map) return DragAction.None;
            if (kind == TrainingExerciseKind.AttackPoint) return DragAction.DelayedMapPan;
            if (numberFixedPoints == 0) AddFixedPoint(location);
            displayUpdateNeeded = true;
            return DragAction.DelayedDrag;
        }

        public override void LeftButtonDrag(Pane pane, PointF location, PointF locationStart, float pixelSize, ref bool displayUpdateNeeded)
        {
            if (pane == Pane.Map && kind != TrainingExerciseKind.AttackPoint) { AddUnfixedPoint(location); displayUpdateNeeded = true; }
        }

        public override async Task<bool> LeftButtonEndDrag(Pane pane, PointF location, PointF locationStart, float pixelSize)
        {
            if (pane != Pane.Map || kind == TrainingExerciseKind.AttackPoint) return false;
            if (numberFixedPoints >= 2 && Geometry.Distance(location, points[0]) < pixelSize * CloseDistance) {
                if (CreateObject()) controller.DefaultCommandMode();
                return true;
            }
            AddFixedPoint(location);
            return true;
        }

        public override async Task<bool> LeftButtonClick(Pane pane, PointF location, float pixelSize)
        {
            if (pane != Pane.Map) return false;
            if (kind == TrainingExerciseKind.AttackPoint) {
                Create(new PointF[] { location });
                controller.DefaultCommandMode();
                return true;
            }
            if (CreateObject()) controller.DefaultCommandMode();
            return true;
        }

        private void AddFixedPoint(PointF point) { AddUnfixedPoint(point); ++numberFixedPoints; }
        private void AddUnfixedPoint(PointF point)
        {
            if (numberFixedPoints > points.Count - 1) points.Add(point); else points[numberFixedPoints] = point;
            if (points.Count >= 2) highlight = new BoundaryCourseObj(Id<Special>.None, courseObjRatio, appearance, new SymPath(points.ToArray()));
        }
        private bool CreateObject() { if (numberFixedPoints < 2) return false; Create(points.GetRange(0, numberFixedPoints).ToArray()); return true; }
        private void Create(PointF[] locations)
        {
            // Avalonia can report both click and double-click completion callbacks for one
            // physical gesture. Persist at most one exercise from this command-mode instance.
            if (objectCreated)
                return;
            objectCreated = true;

            undoMgr.BeginCommand(1328, CommandNameText.AddObject);
            try {
                float width = kind == TrainingExerciseKind.AttackPoint ?
                              appearance.ControlCircleOutsideDiameter * courseObjRatio * AttackPointControlCircleRatio :
                              kind == TrainingExerciseKind.Corridor ? DefaultCorridorWidth : 0F;
                ChangeEvent.AddTrainingExercise(eventDB, courseDesignator, kind, locations, width, "");
                undoMgr.EndCommand(1328);
            }
            catch {
                objectCreated = false;
                if (undoMgr.CommandInProgress)
                    undoMgr.Rollback();
                throw;
            }
        }
    }
}
