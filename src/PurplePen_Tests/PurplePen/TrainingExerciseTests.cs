/* Copyright (c) 2026, Purple Pen contributors.
 * All rights reserved.
 */

#if TEST
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestingUtils;

namespace PurplePen.Tests
{
    /// <summary>Tests persistent training-overlay data without involving rendering or UI.</summary>
    [TestClass]
    public class TrainingExerciseTests
    {
        /// <summary>Exercises save/load, validation, copied geometry, and the display profile.</summary>
        [TestMethod]
        public void RoundTripTrainingExercises()
        {
            UndoMgr undoMgr = new UndoMgr(5);
            EventDB eventDB = new EventDB(undoMgr);
            Id<Course> courseId = eventDB.AddCourse(new Course(CourseKind.Normal, "Training", 15000, 1));

            TrainingExercise corridor = new TrainingExercise(TrainingExerciseKind.Corridor, new CourseDesignator(courseId), new PointF[] { new PointF(1, 2), new PointF(3, 4), new PointF(5, 6) });
            corridor.width = 25;
            corridor.maskOpacity = 0.35F;
            corridor.instruction = "Follow the corridor";
            corridor.visibility = TrainingExerciseVisibility.Runner | TrainingExerciseVisibility.Coach;

            TrainingExercise corridorClone = (TrainingExercise)corridor.Clone();
            Assert.AreEqual(0.35F, corridorClone.maskOpacity);
            Assert.AreEqual(corridor, corridorClone);

            TrainingExercise attackPoint = new TrainingExercise(TrainingExerciseKind.AttackPoint, new CourseDesignator(courseId), new PointF[] { new PointF(8, 9) });
            attackPoint.width = 12;
            attackPoint.instruction = "Last safe feature";
            attackPoint.visibility = TrainingExerciseVisibility.Coach | TrainingExerciseVisibility.Answer;

            TrainingExercise line = new TrainingExercise(TrainingExerciseKind.Line, new CourseDesignator(courseId), new PointF[] { new PointF(-1, 0), new PointF(0, 1) });
            line.instruction = "Keep to the line";

            TrainingExercise contourOnly = new TrainingExercise(TrainingExerciseKind.ContourOnly, new CourseDesignator(courseId), new PointF[] { new PointF(20, 20), new PointF(40, 20), new PointF(40, 40), new PointF(20, 40) });
            contourOnly.maskOpacity = 0.6F;
            contourOnly.instruction = "Read the contours";
            contourOnly.allowedSymbolIds = new string[] { "101.0", "102.0", "103.0" };

            TrainingExercise contourOnlyClone = (TrainingExercise)contourOnly.Clone();
            Assert.AreEqual(contourOnly, contourOnlyClone);
            Assert.AreEqual(contourOnly.GetHashCode(), contourOnlyClone.GetHashCode());
            Assert.AreNotSame(contourOnly.allowedSymbolIds, contourOnlyClone.allowedSymbolIds);

            eventDB.AddTrainingExercise(corridor);
            eventDB.AddTrainingExercise(attackPoint);
            eventDB.AddTrainingExercise(line);
            eventDB.AddTrainingExercise(contourOnly);
            eventDB.Validate();

            string fileName = TestUtil.GetTestFile("eventdb\\training_exercises_temp.xml");
            eventDB.Save(fileName);
            EventDB loadedEventDB = new EventDB(new UndoMgr(5));
            loadedEventDB.Load(fileName);
            loadedEventDB.Validate();

            TestUtil.TestEnumerableAnyOrder(loadedEventDB.AllTrainingExercisePairs,
                new KeyValuePair<Id<TrainingExercise>, TrainingExercise>[] {
                    new KeyValuePair<Id<TrainingExercise>, TrainingExercise>(new Id<TrainingExercise>(1), corridor),
                    new KeyValuePair<Id<TrainingExercise>, TrainingExercise>(new Id<TrainingExercise>(2), attackPoint),
                    new KeyValuePair<Id<TrainingExercise>, TrainingExercise>(new Id<TrainingExercise>(3), line),
                    new KeyValuePair<Id<TrainingExercise>, TrainingExercise>(new Id<TrainingExercise>(4), contourOnly)
                });
        }

        /// <summary>Exercises from files predating the contour-symbol selection receive the empty automatic selection.</summary>
        [TestMethod]
        public void ContourOnlyReadsOldFilesWithAutomaticSymbolSelection()
        {
            const string xml = "<training-exercise kind=\"line\" course=\"1\" mask-opacity=\"0.5\"><instruction>Follow</instruction><location x=\"0\" y=\"0\"/><location x=\"10\" y=\"0\"/></training-exercise>";
            TrainingExercise exercise = new TrainingExercise();

            using (XmlInput xmlInput = new XmlInput(new StringReader(xml), "training-exercise-test")) {
                exercise.ReadAttributesAndContent(xmlInput);
            }

            Assert.AreEqual(TrainingExerciseKind.Line, exercise.kind);
            Assert.AreEqual(0.5F, exercise.maskOpacity);
            CollectionAssert.AreEqual(new string[0], exercise.allowedSymbolIds);
        }

        /// <summary>Contour-only areas require a polygon and a distinct, non-empty selection when symbols are explicit.</summary>
        [TestMethod]
        public void ContourOnlyValidation()
        {
            EventDB eventDB = new EventDB(new UndoMgr(5));
            Id<Course> courseId = eventDB.AddCourse(new Course(CourseKind.Normal, "Training", 15000, 1));
            TrainingExercise exercise = new TrainingExercise(TrainingExerciseKind.ContourOnly, new CourseDesignator(courseId), new PointF[] { new PointF(0, 0), new PointF(10, 0) });
            eventDB.AddTrainingExercise(exercise);

            Assert.ThrowsException<System.ApplicationException>(() => eventDB.Validate());

            exercise.locations = new PointF[] { new PointF(0, 0), new PointF(10, 0), new PointF(10, 10) };
            exercise.allowedSymbolIds = new string[] { "101.0", "101.0" };
            eventDB.ReplaceTrainingExercise(new Id<TrainingExercise>(1), exercise);
            Assert.ThrowsException<System.ApplicationException>(() => eventDB.Validate());

            exercise.allowedSymbolIds = new string[] { "101.0", "102.0" };
            eventDB.ReplaceTrainingExercise(new Id<TrainingExercise>(1), exercise);
            eventDB.Validate();
        }

        /// <summary>Exercises participate in the standard undo and redo store actions.</summary>
        [TestMethod]
        public void TrainingExerciseUndoRedo()
        {
            UndoMgr undoMgr = new UndoMgr(5);
            EventDB eventDB = new EventDB(undoMgr);
            Id<Course> courseId = eventDB.AddCourse(new Course(CourseKind.Normal, "Training", 15000, 1));
            undoMgr.Clear();

            undoMgr.BeginCommand(1, "Add training exercise");
            TrainingExercise exercise = new TrainingExercise(TrainingExerciseKind.Line, new CourseDesignator(courseId), new PointF[] { new PointF(1, 1), new PointF(2, 2) });
            Id<TrainingExercise> exerciseId = eventDB.AddTrainingExercise(exercise);
            undoMgr.EndCommand(1);

            Assert.IsTrue(eventDB.IsTrainingExercisePresent(exerciseId));
            undoMgr.Undo();
            Assert.IsFalse(eventDB.IsTrainingExercisePresent(exerciseId));
            undoMgr.Redo();
            Assert.IsTrue(eventDB.IsTrainingExercisePresent(exerciseId));
        }

        /// <summary>Contour-only editing uses the controller API and remains undoable as one command.</summary>
        [TestMethod]
        public void ContourOnlyControllerApiIsUndoable()
        {
            UndoMgr undoMgr = new UndoMgr(5);
            EventDB eventDB = new EventDB(undoMgr);
            Id<Course> courseId = eventDB.AddCourse(new Course(CourseKind.Normal, "Training", 15000, 1));
            undoMgr.Clear();

            PointF[] polygon = new PointF[] { new PointF(0, 0), new PointF(20, 0), new PointF(20, 20) };
            undoMgr.BeginCommand(1, "Add contour-only training area");
            Id<TrainingExercise> exerciseId = ChangeEvent.AddContourOnlyTrainingExercise(eventDB, new CourseDesignator(courseId), polygon, 0.4F, new string[] { "101.0" });
            undoMgr.EndCommand(1);

            Assert.IsTrue(eventDB.IsTrainingExercisePresent(exerciseId));
            Assert.AreEqual(0.4F, eventDB.GetTrainingExercise(exerciseId).maskOpacity);

            undoMgr.BeginCommand(2, "Edit contour-only training area");
            ChangeEvent.ChangeContourOnlyTrainingExercise(eventDB, exerciseId, new CourseDesignator(courseId), new PointF[] { new PointF(1, 1), new PointF(30, 1), new PointF(30, 30), new PointF(1, 30) }, 0.7F);
            undoMgr.EndCommand(2);
            Assert.AreEqual(0.7F, eventDB.GetTrainingExercise(exerciseId).maskOpacity);

            undoMgr.Undo();
            Assert.AreEqual(0.4F, eventDB.GetTrainingExercise(exerciseId).maskOpacity);
            undoMgr.Undo();
            Assert.IsFalse(eventDB.IsTrainingExercisePresent(exerciseId));
        }

        /// <summary>Contour-only creation rejects polygons that cannot define an area.</summary>
        [TestMethod]
        public void ContourOnlyControllerApiRequiresThreePoints()
        {
            EventDB eventDB = new EventDB(new UndoMgr(5));
            Id<Course> courseId = eventDB.AddCourse(new Course(CourseKind.Normal, "Training", 15000, 1));
            Assert.ThrowsException<System.ArgumentException>(() => ChangeEvent.AddContourOnlyTrainingExercise(eventDB, new CourseDesignator(courseId), new PointF[] { new PointF(0, 0), new PointF(1, 1) }));
        }

        /// <summary>Exercises only affect a layout when a training production profile is explicitly selected.</summary>
        [TestMethod]
        public void FormatterRendersTrainingProfiles()
        {
            EventDB eventDB = new EventDB(new UndoMgr(5));
            Id<Course> courseId = eventDB.AddCourse(new Course(CourseKind.Normal, "Training", 15000, 1));
            CourseDesignator courseDesignator = new CourseDesignator(courseId);

            TrainingExercise corridor = new TrainingExercise(TrainingExerciseKind.Corridor, courseDesignator, new PointF[] { new PointF(10, 10), new PointF(30, 10) });
            corridor.width = 6;
            corridor.instruction = "Follow the corridor";
            corridor.visibility = TrainingExerciseVisibility.Runner | TrainingExerciseVisibility.Coach;
            eventDB.AddTrainingExercise(corridor);

            TrainingExercise attackPoint = new TrainingExercise(TrainingExerciseKind.AttackPoint, courseDesignator, new PointF[] { new PointF(20, 20) });
            attackPoint.width = 4;
            attackPoint.instruction = "Last safe";
            attackPoint.visibility = TrainingExerciseVisibility.Coach | TrainingExerciseVisibility.Answer;
            eventDB.AddTrainingExercise(attackPoint);

            TrainingExercise line = new TrainingExercise(TrainingExerciseKind.Line, courseDesignator, new PointF[] { new PointF(5, 5), new PointF(5, 25) });
            line.instruction = "Follow the line";
            eventDB.AddTrainingExercise(line);

            CourseView courseView = CourseView.CreateViewingCourseView(eventDB, courseDesignator);
            CourseAppearance appearance = new CourseAppearance();

            CourseLayout ordinaryLayout = new CourseLayout();
            CourseFormatter.FormatCourseToLayout(null, courseView, appearance, ordinaryLayout, CourseLayer.MainCourse);
            Assert.AreEqual(0, ordinaryLayout.Count);

            CourseLayout runnerLayout = new CourseLayout();
            CourseFormatterOptions runnerOptions = new CourseFormatterOptions() {
                trainingExerciseRenderProfile = TrainingExerciseRenderProfile.Runner,
                trainingRenderBounds = new RectangleF(0, 0, 50, 50)
            };
            CourseFormatter.FormatCourseToLayout(null, courseView, appearance, runnerLayout, CourseLayer.MainCourse, runnerOptions);
            Assert.AreEqual(1, runnerLayout.CorridorMaskOverlays.Count);
            Assert.AreEqual(2, runnerLayout.OfType<LineSpecialCourseObj>().Count());
            Assert.AreEqual(0, runnerLayout.OfType<RectSpecialCourseObj>().Count());

            CourseLayout coachLayout = new CourseLayout();
            CourseFormatterOptions coachOptions = new CourseFormatterOptions() { trainingExerciseRenderProfile = TrainingExerciseRenderProfile.Coach };
            CourseFormatter.FormatCourseToLayout(null, courseView, appearance, coachLayout, CourseLayer.MainCourse, coachOptions);
            Assert.AreEqual(0, coachLayout.CorridorMaskOverlays.Count);
            Assert.AreEqual(2, coachLayout.OfType<LineSpecialCourseObj>().Count());
            Assert.AreEqual(1, coachLayout.OfType<RectSpecialCourseObj>().Count());
        }

        /// <summary>Runner corridor output requires an explicit mask extent so no implicit map area is hidden.</summary>
        [TestMethod]
        public void RunnerCorridorRequiresRenderBounds()
        {
            EventDB eventDB = new EventDB(new UndoMgr(5));
            Id<Course> courseId = eventDB.AddCourse(new Course(CourseKind.Normal, "Training", 15000, 1));
            TrainingExercise corridor = new TrainingExercise(TrainingExerciseKind.Corridor, new CourseDesignator(courseId), new PointF[] { new PointF(0, 0), new PointF(20, 0) });
            corridor.width = 4;
            corridor.instruction = "Follow";
            eventDB.AddTrainingExercise(corridor);

            CourseLayout layout = new CourseLayout();
            CourseFormatterOptions options = new CourseFormatterOptions() { trainingExerciseRenderProfile = TrainingExerciseRenderProfile.Runner };
            Assert.ThrowsException<System.InvalidOperationException>(() => CourseFormatter.FormatCourseToLayout(null, CourseView.CreateViewingCourseView(eventDB, new CourseDesignator(courseId)), new CourseAppearance(), layout, CourseLayer.MainCourse, options));
        }
    }
}
#endif
