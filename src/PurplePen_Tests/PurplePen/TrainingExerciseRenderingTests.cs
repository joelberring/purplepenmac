/* Copyright (c) 2026, Purple Pen contributors.
 * All rights reserved.
 */

#if TEST
using System.Drawing;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using PurplePen.MapModel;

namespace PurplePen.Tests
{
    /// <summary>Tests the course-layout rendering choices for training overlays.</summary>
    [TestClass]
    public class TrainingExerciseRenderingTests
    {
        /// <summary>Renders a runner corridor as a transparent-capable map overlay plus guides.</summary>
        [TestMethod]
        public void RunnerProfileAddsCorridorMaskAndGuides()
        {
            EventDB eventDB;
            Id<Course> courseId;
            CreateTrainingCourse(out eventDB, out courseId);

            TrainingExercise corridor = new TrainingExercise(TrainingExerciseKind.Corridor, new CourseDesignator(courseId), new PointF[] { new PointF(10, 10), new PointF(30, 10) });
            corridor.width = 8;
            eventDB.AddTrainingExercise(corridor);
            TrainingExercise attackPoint = new TrainingExercise(TrainingExerciseKind.AttackPoint, new CourseDesignator(courseId), new PointF[] { new PointF(30, 10) });
            attackPoint.width = 6;
            eventDB.AddTrainingExercise(attackPoint);

            CourseLayout layout = FormatTrainingCourse(eventDB, courseId, TrainingExerciseRenderProfile.Runner, new RectangleF(0, 0, 50, 50));

            Assert.AreEqual(1, layout.CorridorMaskOverlays.Count);
            Assert.AreEqual(1.0F, layout.CorridorMaskOverlays[0].MaskOpacity);
            Assert.IsTrue(layout.Any(courseObject => courseObject is RectSpecialCourseObj));
            Assert.IsTrue(layout.Count(courseObject => courseObject is LineSpecialCourseObj) >= 1);
        }

        /// <summary>Preserves the configured opacity for direct vector-map corridor masking.</summary>
        [TestMethod]
        public void RunnerCorridorMaskPreservesOpacity()
        {
            EventDB eventDB;
            Id<Course> courseId;
            CreateTrainingCourse(out eventDB, out courseId);

            TrainingExercise corridor = new TrainingExercise(TrainingExerciseKind.Corridor, new CourseDesignator(courseId), new PointF[] { new PointF(10, 10), new PointF(30, 10) });
            corridor.width = 8;
            corridor.whiteMargin = 2;
            corridor.maskOpacity = 0.35F;
            eventDB.AddTrainingExercise(corridor);

            CourseLayout layout = FormatTrainingCourse(eventDB, courseId, TrainingExerciseRenderProfile.Runner, new RectangleF(0, 0, 50, 50));

            Assert.AreEqual(1, layout.CorridorMaskOverlays.Count);
            Assert.AreEqual(0.35F, layout.CorridorMaskOverlays[0].MaskOpacity);
            Assert.AreEqual(4, layout.CorridorMaskOverlays[0].CorridorPolygon.Length);
            Assert.AreEqual(4, layout.CorridorMaskOverlays[0].OuterPolygon.Length);
            Assert.AreEqual(8F, layout.CorridorMaskOverlays[0].CorridorPolygon.Max(point => point.Y) - layout.CorridorMaskOverlays[0].CorridorPolygon.Min(point => point.Y));
            Assert.AreEqual(12F, layout.CorridorMaskOverlays[0].OuterPolygon.Max(point => point.Y) - layout.CorridorMaskOverlays[0].OuterPolygon.Min(point => point.Y));
        }

        /// <summary>Renders coach guides without applying the runner-only corridor mask.</summary>
        [TestMethod]
        public void CoachProfileShowsGuidesWithoutCorridorMask()
        {
            EventDB eventDB;
            Id<Course> courseId;
            CreateTrainingCourse(out eventDB, out courseId);

            TrainingExercise corridor = new TrainingExercise(TrainingExerciseKind.Corridor, new CourseDesignator(courseId), new PointF[] { new PointF(10, 10), new PointF(30, 10) });
            corridor.width = 8;
            eventDB.AddTrainingExercise(corridor);

            CourseLayout layout = FormatTrainingCourse(eventDB, courseId, TrainingExerciseRenderProfile.Coach, null);

            Assert.AreEqual(0, layout.CorridorMaskOverlays.Count);
            Assert.IsFalse(layout.Any(courseObject => courseObject is TrainingCorridorMaskCourseObj));
            Assert.IsTrue(layout.Any(courseObject => courseObject is LineSpecialCourseObj));
        }

        /// <summary>Requires a real render boundary for runner corridor masking.</summary>
        [TestMethod]
        public void RunnerCorridorRequiresExplicitRenderBounds()
        {
            EventDB eventDB;
            Id<Course> courseId;
            CreateTrainingCourse(out eventDB, out courseId);

            TrainingExercise corridor = new TrainingExercise(TrainingExerciseKind.Corridor, new CourseDesignator(courseId), new PointF[] { new PointF(10, 10), new PointF(30, 10) });
            corridor.width = 8;
            eventDB.AddTrainingExercise(corridor);

            Assert.ThrowsException<System.InvalidOperationException>(() => FormatTrainingCourse(eventDB, courseId, TrainingExerciseRenderProfile.Runner, null));
        }

        /// <summary>Passes runner contour-only areas to MapDisplay without adding a purple course object.</summary>
        [TestMethod]
        public void RunnerProfileAddsContourOnlyBaseMapOverlay()
        {
            EventDB eventDB;
            Id<Course> courseId;
            CreateTrainingCourse(out eventDB, out courseId);

            TrainingExercise contours = new TrainingExercise(TrainingExerciseKind.ContourOnly, new CourseDesignator(courseId), new PointF[] {
                new PointF(5, 5), new PointF(35, 5), new PointF(35, 25), new PointF(5, 25)
            });
            contours.maskOpacity = 0.65F;
            contours.allowedSymbolIds = new string[] { "101.0", "102.0" };
            eventDB.AddTrainingExercise(contours);

            CourseLayout runnerLayout = FormatTrainingCourse(eventDB, courseId, TrainingExerciseRenderProfile.Runner, new RectangleF(0, 0, 50, 50));
            CourseLayout coachLayout = FormatTrainingCourse(eventDB, courseId, TrainingExerciseRenderProfile.Coach, null);

            Assert.AreEqual(1, runnerLayout.ContourOnlyOverlays.Count);
            Assert.AreEqual(0.65F, runnerLayout.ContourOnlyOverlays[0].MaskOpacity);
            CollectionAssert.AreEqual(contours.allowedSymbolIds, runnerLayout.ContourOnlyOverlays[0].AllowedSymbolIds);
            Assert.AreEqual(0, coachLayout.ContourOnlyOverlays.Count);
        }

        /// <summary>Creates a small normal course with a valid start, control, and finish.</summary>
        private static void CreateTrainingCourse(out EventDB eventDB, out Id<Course> courseId)
        {
            eventDB = new EventDB(new UndoMgr(5));
            Id<ControlPoint> start = eventDB.AddControlPoint(new ControlPoint(ControlPointKind.Start, null, new PointF(0, 0)));
            Id<ControlPoint> control = eventDB.AddControlPoint(new ControlPoint(ControlPointKind.Normal, "31", new PointF(30, 10)));
            Id<ControlPoint> finish = eventDB.AddControlPoint(new ControlPoint(ControlPointKind.Finish, null, new PointF(40, 10)));
            Id<CourseControl> finishCourseControl = eventDB.AddCourseControl(new CourseControl(finish, Id<CourseControl>.None));
            Id<CourseControl> controlCourseControl = eventDB.AddCourseControl(new CourseControl(control, finishCourseControl));
            Id<CourseControl> startCourseControl = eventDB.AddCourseControl(new CourseControl(start, controlCourseControl));
            Course course = new Course(CourseKind.Normal, "Training", 10000, 1);
            course.firstCourseControl = startCourseControl;
            courseId = eventDB.AddCourse(course);
        }

        /// <summary>Formats a training course with the selected training rendering profile.</summary>
        private static CourseLayout FormatTrainingCourse(EventDB eventDB, Id<Course> courseId, TrainingExerciseRenderProfile profile, RectangleF? bounds)
        {
            SymbolDB symbolDB = new SymbolDB(Util.GetFileInAppDirectory("symbols.xml"));
            CourseLayout layout = new CourseLayout();
            CourseView courseView = CourseView.CreateViewingCourseView(eventDB, new CourseDesignator(courseId));
            CourseFormatterOptions options = new CourseFormatterOptions {
                trainingExerciseRenderProfile = profile,
                trainingRenderBounds = bounds
            };
            CourseFormatter.FormatCourseToLayout(symbolDB, courseView, eventDB.GetEvent().courseAppearance, layout, CourseLayer.MainCourse, options);
            return layout;
        }
    }
}
#endif
