using NUnit.Framework;
using PurplePen;
using PurplePen.MapModel;
using PurplePen.ViewModels;
using System.Drawing;
using System.Linq;

namespace PurplePenViewModels.Tests
{
    /// <summary>Tests route-choice candidate association and endpoint geometry.</summary>
    [TestFixture]
    public class RouteChoiceCandidateTests
    {
        [Test]
        public void AddAndChangeAnchorRouteAtLegControls()
        {
            EventDB eventDB = CreateEventWithLeg(out UndoMgr undoMgr, out Id<Course> courseId, out Id<CourseControl> startCourseControlId,
                                                  out PointF startLocation, out PointF endLocation);

            undoMgr.BeginCommand(2, "Add route-choice candidate");
            Id<RouteChoiceCandidate> candidateId = ChangeEvent.AddRouteChoiceCandidate(
                eventDB, new CourseDesignator(courseId), startCourseControlId, "Route", "Manual",
                new PointF[] { new PointF(2, 3), new PointF(7, 8), new PointF(9, 4) }, "");
            undoMgr.EndCommand(2);

            RouteChoiceCandidate added = eventDB.GetRouteChoiceCandidate(candidateId);
            Assert.That(added.locations[0], Is.EqualTo(startLocation));
            Assert.That(added.locations[1], Is.EqualTo(new PointF(7, 8)));
            Assert.That(added.locations[2], Is.EqualTo(endLocation));

            undoMgr.BeginCommand(3, "Change route-choice candidate");
            ChangeEvent.ChangeRouteChoiceCandidate(eventDB, candidateId, "Changed", "Manual",
                new PointF[] { new PointF(-1, -1), new PointF(4, 6), new PointF(99, 99) }, "");
            undoMgr.EndCommand(3);
            RouteChoiceCandidate changed = eventDB.GetRouteChoiceCandidate(candidateId);
            Assert.That(changed.locations[0], Is.EqualTo(startLocation));
            Assert.That(changed.locations[1], Is.EqualTo(new PointF(4, 6)));
            Assert.That(changed.locations[2], Is.EqualTo(endLocation));
            eventDB.Validate();
        }

        [Test]
        public void ValidationRejectsPersistedRouteOutsideLegEndpoints()
        {
            EventDB eventDB = CreateEventWithLeg(out UndoMgr undoMgr, out Id<Course> courseId, out Id<CourseControl> startCourseControlId,
                                                  out PointF startLocation, out PointF endLocation);
            undoMgr.BeginCommand(2, "Add malformed route-choice candidate");
            eventDB.AddRouteChoiceCandidate(new RouteChoiceCandidate(
                new CourseDesignator(courseId), startCourseControlId, "Bad", "Imported",
                new PointF[] { new PointF(startLocation.X + 1, startLocation.Y), endLocation }, ""));
            undoMgr.EndCommand(2);

            Assert.Throws<ApplicationException>(() => eventDB.Validate());
        }

        [Test]
        public void MovingLegEndpointReanchorsRouteAndUndoRestoresIt()
        {
            EventDB eventDB = CreateEventWithLeg(out UndoMgr undoMgr, out Id<Course> courseId, out Id<CourseControl> startCourseControlId,
                                                  out PointF startLocation, out PointF endLocation);
            Id<RouteChoiceCandidate> candidateId = AddCandidate(eventDB, undoMgr, courseId, startCourseControlId);
            Id<ControlPoint> endControlId = eventDB.GetCourseControl(eventDB.GetCourseControl(startCourseControlId).nextCourseControl).control;
            PointF movedEnd = new PointF(50, 60);

            undoMgr.BeginCommand(3, "Move endpoint");
            ChangeEvent.ChangeControlLocation(eventDB, endControlId, movedEnd);
            undoMgr.EndCommand(3);
            Assert.That(eventDB.GetRouteChoiceCandidate(candidateId).locations.Last(), Is.EqualTo(movedEnd));
            eventDB.Validate();

            undoMgr.Undo();
            Assert.That(eventDB.GetRouteChoiceCandidate(candidateId).locations.Last(), Is.EqualTo(endLocation));
            eventDB.Validate();
            undoMgr.Redo();
            Assert.That(eventDB.GetRouteChoiceCandidate(candidateId).locations.Last(), Is.EqualTo(movedEnd));
        }

        [Test]
        public void RemovingLegStartRemovesCandidateAndUndoRestoresIt()
        {
            EventDB eventDB = CreateEventWithLeg(out UndoMgr undoMgr, out Id<Course> courseId, out Id<CourseControl> startCourseControlId,
                                                  out PointF startLocation, out PointF endLocation);
            Id<RouteChoiceCandidate> candidateId = AddCandidate(eventDB, undoMgr, courseId, startCourseControlId);

            undoMgr.BeginCommand(3, "Remove route leg start");
            ChangeEvent.RemoveCourseControl(eventDB, courseId, startCourseControlId);
            undoMgr.EndCommand(3);
            Assert.That(eventDB.IsRouteChoiceCandidatePresent(candidateId), Is.False);
            eventDB.Validate();

            undoMgr.Undo();
            Assert.That(eventDB.IsRouteChoiceCandidatePresent(candidateId), Is.True);
            eventDB.Validate();
        }

        [Test]
        public void RemovingLegEndThatRewritesLegRemovesCandidateAndUndoRestoresIt()
        {
            EventDB eventDB = CreateEventWithLeg(out UndoMgr undoMgr, out Id<Course> courseId, out Id<CourseControl> startCourseControlId,
                                                  out PointF startLocation, out PointF endLocation);
            Id<CourseControl> removedCourseControlId = eventDB.GetCourseControl(startCourseControlId).nextCourseControl;

            undoMgr.BeginCommand(2, "Extend course and add route-choice candidate");
            Id<ControlPoint> finalControlId = eventDB.AddControlPoint(new ControlPoint(ControlPointKind.Normal, "33", new PointF(50, 60)));
            Id<CourseControl> finalCourseControlId = eventDB.AddCourseControl(new CourseControl(finalControlId, Id<CourseControl>.None));
            CourseControl removedCourseControl = (CourseControl)eventDB.GetCourseControl(removedCourseControlId).Clone();
            removedCourseControl.nextCourseControl = finalCourseControlId;
            eventDB.ReplaceCourseControl(removedCourseControlId, removedCourseControl);
            Id<RouteChoiceCandidate> candidateId = ChangeEvent.AddRouteChoiceCandidate(eventDB, new CourseDesignator(courseId), startCourseControlId,
                "Route", "Manual", new PointF[] { startLocation, new PointF(20, 30), endLocation }, "");
            undoMgr.EndCommand(2);

            undoMgr.BeginCommand(3, "Remove route leg end");
            ChangeEvent.RemoveCourseControl(eventDB, courseId, removedCourseControlId);
            undoMgr.EndCommand(3);
            Assert.That(eventDB.IsRouteChoiceCandidatePresent(candidateId), Is.False);
            eventDB.Validate();

            undoMgr.Undo();
            Assert.That(eventDB.IsRouteChoiceCandidatePresent(candidateId), Is.True);
            eventDB.Validate();
        }

        [Test]
        public void InsertingControlIntoCandidateLegRemovesCandidateAndUndoRestoresIt()
        {
            EventDB eventDB = CreateEventWithLeg(out UndoMgr undoMgr, out Id<Course> courseId, out Id<CourseControl> startCourseControlId,
                                                  out PointF startLocation, out PointF endLocation);
            Id<RouteChoiceCandidate> candidateId = AddCandidate(eventDB, undoMgr, courseId, startCourseControlId);
            Id<CourseControl> oldEndCourseControlId = eventDB.GetCourseControl(startCourseControlId).nextCourseControl;

            undoMgr.BeginCommand(3, "Insert control in candidate leg");
            // Match the old end location: coordinate-based reconciliation alone would
            // miss that the candidate now refers to a different directed leg.
            Id<ControlPoint> insertedControlId = eventDB.AddControlPoint(new ControlPoint(ControlPointKind.Normal, "33", endLocation));
            ChangeEvent.AddCourseControl(eventDB, insertedControlId, courseId, startCourseControlId, oldEndCourseControlId, LegInsertionLoc.Normal);
            undoMgr.EndCommand(3);

            Assert.That(eventDB.IsRouteChoiceCandidatePresent(candidateId), Is.False);
            eventDB.Validate();

            undoMgr.Undo();
            Assert.That(eventDB.IsRouteChoiceCandidatePresent(candidateId), Is.True);
            eventDB.Validate();

            undoMgr.Redo();
            Assert.That(eventDB.IsRouteChoiceCandidatePresent(candidateId), Is.False);
            eventDB.Validate();
        }

        [Test]
        public void AddingVariationRemovesCandidatesAndBlocksAllVariationsAnalysis()
        {
            EventDB eventDB = CreateEventWithLeg(out UndoMgr undoMgr, out Id<Course> courseId, out Id<CourseControl> startCourseControlId,
                                                  out PointF startLocation, out PointF endLocation);
            Id<RouteChoiceCandidate> candidateId = AddCandidate(eventDB, undoMgr, courseId, startCourseControlId);

            undoMgr.BeginCommand(3, "Add variation to candidate course");
            Assert.That(ChangeEvent.AddVariation(eventDB, new CourseDesignator(courseId), startCourseControlId, false, 2), Is.True);
            undoMgr.EndCommand(3);

            Assert.That(eventDB.IsRouteChoiceCandidatePresent(candidateId), Is.False);
            Assert.Throws<ArgumentException>(() => new RouteChoiceAnalysisDialogViewModel().Load(eventDB, new CourseDesignator(courseId)));
            eventDB.Validate();

            undoMgr.Undo();
            Assert.That(eventDB.IsRouteChoiceCandidatePresent(candidateId), Is.True);
            eventDB.Validate();
        }

        private static Id<RouteChoiceCandidate> AddCandidate(EventDB eventDB, UndoMgr undoMgr, Id<Course> courseId, Id<CourseControl> startCourseControlId)
        {
            undoMgr.BeginCommand(2, "Add route-choice candidate");
            Id<RouteChoiceCandidate> candidateId = ChangeEvent.AddRouteChoiceCandidate(eventDB, new CourseDesignator(courseId), startCourseControlId,
                "Route", "Manual", new[] { new PointF(1, 2), new PointF(20, 30), new PointF(3, 4) }, "");
            undoMgr.EndCommand(2);
            return candidateId;
        }

        /// <summary>Creates a minimal valid event containing one normal two-control course.</summary>
        private static EventDB CreateEventWithLeg(out UndoMgr undoMgr, out Id<Course> courseId, out Id<CourseControl> startCourseControlId,
                                                   out PointF startLocation, out PointF endLocation)
        {
            undoMgr = new UndoMgr(20);
            EventDB eventDB = new EventDB(undoMgr);
            startLocation = new PointF(10, 20);
            endLocation = new PointF(30, 40);
            undoMgr.BeginCommand(1, "Create route-choice test course");
            Id<ControlPoint> startControlId = eventDB.AddControlPoint(new ControlPoint(ControlPointKind.Normal, "31", startLocation));
            Id<ControlPoint> endControlId = eventDB.AddControlPoint(new ControlPoint(ControlPointKind.Normal, "32", endLocation));
            courseId = eventDB.AddCourse(new Course(CourseKind.Normal, "Blue", 15000, 1));
            Id<CourseControl> endCourseControlId = eventDB.AddCourseControl(new CourseControl(endControlId, Id<CourseControl>.None));
            startCourseControlId = eventDB.AddCourseControl(new CourseControl(startControlId, endCourseControlId));
            Course course = (Course)eventDB.GetCourse(courseId).Clone();
            course.firstCourseControl = startCourseControlId;
            eventDB.ReplaceCourse(courseId, course);
            undoMgr.EndCommand(1);
            return eventDB;
        }
    }
}
