using NUnit.Framework;
using PurplePen;
using PurplePen.MapModel;
using PurplePen.ViewModels;
using System;
using System.Drawing;
using System.IO;
using System.Linq;

namespace PurplePenViewModels.Tests
{
    [TestFixture]
    public class EventClassTests
    {
        [Test]
        public void IofCourseDataFixtureValidatesAndParses()
        {
            string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../TestFiles/exportxml/marymoor_expected_v3.xml"));
            if (!File.Exists(path)) Assert.Ignore("IOF fixture is unavailable in this test deployment.");
            string xml = File.ReadAllText(path);
            IofCourseDataValidationResult validation = IofCourseDataExchange.Validate(xml);
            Assert.That(validation.IsValid, Is.True, String.Join("\n", validation.Errors));
            Assert.That(IofCourseDataExchange.Parse(xml).Courses.Count, Is.GreaterThan(0));
        }

        [Test]
        public void IofCourseDataInvalidXmlReportsLineAndColumn()
        {
            IofCourseDataValidationResult validation = IofCourseDataExchange.Validate("<CourseData xmlns=\"http://www.orienteering.org/datastandard/3.0\" iofVersion=\"3.0\">\n  <Broken />\n</CourseData>");
            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Errors.Any(item => item.Line > 0 && item.Column > 0), Is.True);
        }

        /// <summary>IOF's generated codes for unnumbered special points do not cause false course differences.</summary>
        [Test]
        public void IofCourseDataComparison_UsesSpecialPointTypesButDetectsControlChanges()
        {
            UndoMgr undo = new UndoMgr(20);
            EventDB eventDB = new EventDB(undo);
            undo.BeginCommand(4, "Create test course");
            Id<Course> courseId = eventDB.AddCourse(new Course(CourseKind.Normal, "Blue", 15000, 1));
            Id<ControlPoint> start = eventDB.AddControlPoint(new ControlPoint(ControlPointKind.Start, null, new PointF(0, 0)));
            Id<ControlPoint> control31 = eventDB.AddControlPoint(new ControlPoint(ControlPointKind.Normal, "31", new PointF(10, 10)));
            Id<ControlPoint> crossing = eventDB.AddControlPoint(new ControlPoint(ControlPointKind.CrossingPoint, null, new PointF(20, 20)));
            Id<ControlPoint> finish = eventDB.AddControlPoint(new ControlPoint(ControlPointKind.Finish, null, new PointF(30, 30)));
            Id<CourseControl> finishCourseControl = eventDB.AddCourseControl(new CourseControl(finish, Id<CourseControl>.None));
            Id<CourseControl> crossingCourseControl = eventDB.AddCourseControl(new CourseControl(crossing, finishCourseControl));
            Id<CourseControl> controlCourseControl = eventDB.AddCourseControl(new CourseControl(control31, crossingCourseControl));
            Id<CourseControl> startCourseControl = eventDB.AddCourseControl(new CourseControl(start, controlCourseControl));
            eventDB.GetCourse(courseId).firstCourseControl = startCourseControl;
            undo.EndCommand(4);

            IofCourseDataModel model = new IofCourseDataModel();
            IofCourse importedCourse = new IofCourse { Name = "Blue", CourseFamily = "Blue" };
            importedCourse.CourseControls.Add(new IofCourseControl { Code = "STA1", Type = "Start" });
            importedCourse.CourseControls.Add(new IofCourseControl { Code = "31", Type = "Control" });
            importedCourse.CourseControls.Add(new IofCourseControl { Code = "CROSS1", Type = "CrossingPoint" });
            importedCourse.CourseControls.Add(new IofCourseControl { Code = "FIN1", Type = "Finish" });
            model.Courses.Add(importedCourse);

            Assert.That(IofCourseDataExchange.Compare(model, eventDB), Is.Empty);

            importedCourse.CourseControls[1].Code = "32";
            Assert.That(IofCourseDataExchange.Compare(model, eventDB).Select(difference => difference.Kind), Does.Contain("ControlSequenceChanged"));
        }

        [Test]
        public void EventClassDialogValidatesAndAppliesSuggestions()
        {
            EventClassDialogViewModel vm = new EventClassDialogViewModel();
            vm.Load(Array.Empty<Controller.EventClassInfo>(), new[] { new CourseChoice { Id = new Id<Course>(1), Name = "Blue" } });
            vm.AddCommand.Execute(null);
            Assert.That(vm.ValidateRows(), Is.False);
            EventClassRow row = vm.Rows[0];
            row.Name = "H21"; row.Course = vm.Courses[0]; row.ParticipantCount = 1;
            Assert.That(vm.ValidateRows(), Is.True);
            vm.SetImportedStartList(new[] { new BacksideInfoRecord { ClassName = "H21", Team = 1, Leg = 1 } });
            Assert.That(vm.Suggestions.Single().ImportedCount, Is.EqualTo(1));
            vm.ApplySuggestionsCommand.Execute(null);
            Assert.That(row.ParticipantCount, Is.EqualTo(1));
            Assert.That(vm.TotalParticipants, Is.EqualTo(1));
        }

        [Test]
        public void EventClassSuggestions_UseCurrentRowsInsteadOfPersistedClasses()
        {
            UndoMgr undo = new UndoMgr(20);
            EventDB eventDB = new EventDB(undo);
            undo.BeginCommand(1, "persisted class");
            Id<Course> courseId = eventDB.AddCourse(new Course(CourseKind.Normal, "Blue", 15000, 1));
            eventDB.AddEventClass(new EventClass { Name = "Old name", CourseId = courseId });
            undo.EndCommand(1);
            EventClassDialogViewModel vm = new EventClassDialogViewModel { EventDB = eventDB };
            vm.Load(Array.Empty<Controller.EventClassInfo>(), new[] { new CourseChoice { Id = courseId, Name = "Blue" } });
            vm.AddCommand.Execute(null);
            vm.Rows[0].Name = "Renamed class";
            vm.Rows[0].Course = vm.Courses[0];

            vm.SetImportedStartList(new[] { new BacksideInfoRecord { ClassName = "Renamed class", Team = 1, Leg = 1 } });

            Assert.That(vm.Suggestions.Single().IsConflict, Is.False);
            Assert.That(vm.Suggestions.Single().ExistingCount, Is.EqualTo(1));
        }

        [Test]
        public void EventClassRequiredMapsAndXmlRoundTrip()
        {
            UndoMgr undo = new UndoMgr(20);
            EventDB source = new EventDB(undo);
            undo.BeginCommand(1, "test");
            Id<Course> courseId = source.AddCourse(new Course(CourseKind.Normal, "Blue", 15000, 1));
            Id<EventClass> classId = source.AddEventClass(new EventClass {
                Name = "H21", CourseId = courseId, ParticipantCount = 42, MapCount = 1, ReserveCount = 3,
                StartInterval = 2, BibNumberStart = 1, BibNumberEnd = 42
            });
            undo.EndCommand(1);
            Assert.That(source.GetEventClass(classId).RequiredMapCount, Is.EqualTo(45));

            string path = Path.Combine(Path.GetTempPath(), "purplepen-event-class-" + Guid.NewGuid().ToString("N") + ".ppen");
            try {
                source.Save(path);
                EventDB restored = new EventDB(new UndoMgr(20));
                restored.Load(path);
                EventClass result = restored.AllEventClasses.Single();
                Assert.That(result.Name, Is.EqualTo("H21"));
                Assert.That(result.CourseId, Is.EqualTo(courseId));
                Assert.That(result.RequiredMapCount, Is.EqualTo(45));
            }
            finally {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Test]
        public void LegacyCourseClassNameMigratesWithoutRemovingCourseData()
        {
            UndoMgr undo = new UndoMgr(20);
            EventDB source = new EventDB(undo);
            undo.BeginCommand(2, "test");
            Id<Course> courseId = source.AddCourse(new Course(CourseKind.Normal, "Blue", 15000, 1));
            ChangeEvent.ChangeCourseClassName(source, courseId, "D21");
            ChangeEvent.ChangeCourseLoad(source, courseId, 18);
            undo.EndCommand(2);

            string path = Path.Combine(Path.GetTempPath(), "purplepen-legacy-class-" + Guid.NewGuid().ToString("N") + ".ppen");
            try {
                string legacyXml = source.SaveToString().Replace(" event-classes=\"true\"", String.Empty);
                File.WriteAllText(path, legacyXml);
                EventDB restored = new EventDB(new UndoMgr(20));
                restored.Load(path);
                EventClass result = restored.AllEventClasses.Single();
                Assert.That(result.Name, Is.EqualTo("D21"));
                Assert.That(result.CourseId, Is.EqualTo(courseId));
                Assert.That(result.ParticipantCount, Is.EqualTo(18));
                Assert.That(restored.GetCourse(courseId).className, Is.EqualTo("D21"));
            }
            finally {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Test]
        public void DeletingLastEventClassDoesNotRecreateLegacyClassOnReload()
        {
            UndoMgr undo = new UndoMgr(20);
            EventDB source = new EventDB(undo);
            undo.BeginCommand(4, "test");
            Id<Course> courseId = source.AddCourse(new Course(CourseKind.Normal, "Blue", 15000, 1));
            ChangeEvent.ChangeCourseClassName(source, courseId, "D21");
            Id<EventClass> classId = source.AddEventClass(new EventClass { Name = "H21", CourseId = courseId, ParticipantCount = 12 });
            undo.EndCommand(4);

            undo.BeginCommand(5, "delete class");
            source.RemoveEventClass(classId);
            undo.EndCommand(5);
            source.Validate();

            string path = Path.Combine(Path.GetTempPath(), "purplepen-empty-class-" + Guid.NewGuid().ToString("N") + ".ppen");
            try {
                source.Save(path);
                EventDB restored = new EventDB(new UndoMgr(20));
                restored.Load(path);
                restored.Validate();

                Assert.That(restored.AllEventClasses, Is.Empty);
                Assert.That(restored.GetCourse(courseId).className, Is.EqualTo("D21"));
            }
            finally {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Test]
        public void DeletingCourseCascadesEventClassesThroughUndoAndRedo()
        {
            UndoMgr undo = new UndoMgr(20);
            EventDB eventDB = new EventDB(undo);
            undo.BeginCommand(6, "add courses and classes");
            Id<Course> deletedCourseId = eventDB.AddCourse(new Course(CourseKind.Normal, "Blue", 15000, 1));
            Id<Course> retainedCourseId = eventDB.AddCourse(new Course(CourseKind.Normal, "Green", 15000, 2));
            Id<EventClass> deletedClassOneId = eventDB.AddEventClass(new EventClass { Name = "H21", CourseId = deletedCourseId });
            Id<EventClass> deletedClassTwoId = eventDB.AddEventClass(new EventClass { Name = "D21", CourseId = deletedCourseId });
            Id<EventClass> retainedClassId = eventDB.AddEventClass(new EventClass { Name = "H45", CourseId = retainedCourseId });
            undo.EndCommand(6);
            eventDB.Validate();

            undo.BeginCommand(7, "delete course");
            ChangeEvent.DeleteCourse(eventDB, deletedCourseId);
            undo.EndCommand(7);
            eventDB.Validate();
            Assert.That(eventDB.IsCoursePresent(deletedCourseId), Is.False);
            Assert.That(eventDB.AllEventClassIds.Contains(deletedClassOneId), Is.False);
            Assert.That(eventDB.AllEventClassIds.Contains(deletedClassTwoId), Is.False);
            Assert.That(eventDB.AllEventClassIds.Contains(retainedClassId), Is.True);

            undo.Undo();
            eventDB.Validate();
            Assert.That(eventDB.IsCoursePresent(deletedCourseId), Is.True);
            Assert.That(eventDB.AllEventClassIds.Contains(deletedClassOneId), Is.True);
            Assert.That(eventDB.AllEventClassIds.Contains(deletedClassTwoId), Is.True);

            undo.Redo();
            eventDB.Validate();
            Assert.That(eventDB.IsCoursePresent(deletedCourseId), Is.False);
            Assert.That(eventDB.AllEventClassIds.Contains(deletedClassOneId), Is.False);
            Assert.That(eventDB.AllEventClassIds.Contains(deletedClassTwoId), Is.False);
            Assert.That(eventDB.AllEventClassIds.Contains(retainedClassId), Is.True);
        }

        [Test]
        public void ClassAggregationUsesClassesAndSuggestsStartListCounts()
        {
            UndoMgr undo = new UndoMgr(20);
            EventDB eventDB = new EventDB(undo);
            undo.BeginCommand(3, "test");
            Id<Course> courseId = eventDB.AddCourse(new Course(CourseKind.Normal, "Blue", 15000, 1));
            eventDB.AddEventClass(new EventClass { Name = "H21", CourseId = courseId, ParticipantCount = 12 });
            eventDB.AddEventClass(new EventClass { Name = "D21", CourseId = courseId, ParticipantCount = 8, ReserveCount = 2 });
            undo.EndCommand(3);

            Assert.That(EventClassSupport.GetCourseParticipantCount(eventDB, courseId), Is.EqualTo(20));
            Assert.That(EventClassSupport.GetCourseRequiredMapCount(eventDB, courseId), Is.EqualTo(22));
            Assert.That(EventClassSupport.SuggestParticipantCounts(new[] { "H21", "h21", "D21" })["H21"], Is.EqualTo(2));
        }

        /// <summary>Control, visit, and leg reports use event-class participants before legacy course loads.</summary>
        [Test]
        public void QueryLoadsUseEventClassParticipantsAndKeepLegacyFallback()
        {
            UndoMgr undo = new UndoMgr(20);
            EventDB eventDB = new EventDB(undo);
            undo.BeginCommand(9, "Create event-class load test");
            Id<Course> classCourseId = eventDB.AddCourse(new Course(CourseKind.Normal, "Blue", 15000, 1));
            Id<ControlPoint> first = eventDB.AddControlPoint(new ControlPoint(ControlPointKind.Normal, "31", new PointF(0, 0)));
            Id<ControlPoint> second = eventDB.AddControlPoint(new ControlPoint(ControlPointKind.Normal, "32", new PointF(10, 0)));
            Id<CourseControl> secondCourseControl = eventDB.AddCourseControl(new CourseControl(second, Id<CourseControl>.None));
            Id<CourseControl> firstCourseControl = eventDB.AddCourseControl(new CourseControl(first, secondCourseControl));
            eventDB.GetCourse(classCourseId).firstCourseControl = firstCourseControl;
            eventDB.GetCourse(classCourseId).load = 3;
            eventDB.AddEventClass(new EventClass { Name = "H21", CourseId = classCourseId, ParticipantCount = 12 });
            eventDB.AddEventClass(new EventClass { Name = "D21", CourseId = classCourseId, ParticipantCount = 8 });

            Id<Course> legacyCourseId = eventDB.AddCourse(new Course(CourseKind.Normal, "Green", 15000, 2));
            eventDB.GetCourse(legacyCourseId).load = 7;
            undo.EndCommand(9);

            Assert.That(QueryEvent.GetCourseLoad(eventDB, classCourseId), Is.EqualTo(20));
            Assert.That(QueryEvent.GetControlLoad(eventDB, first), Is.EqualTo(20));
            Assert.That(QueryEvent.GetControlVisitLoad(eventDB, first), Is.EqualTo(20));
            Assert.That(QueryEvent.GetLegLoad(eventDB, first, second), Is.EqualTo(20));
            Assert.That(QueryEvent.GetCourseLoad(eventDB, legacyCourseId), Is.EqualTo(7));
            Assert.That(EventClassSupport.GetCourseClassNames(eventDB, classCourseId), Is.EqualTo("D21, H21"));
        }

        [Test]
        public void ClassAggregation_UsesZeroTotalsWhenClassesExist()
        {
            UndoMgr undo = new UndoMgr(20);
            EventDB eventDB = new EventDB(undo);
            undo.BeginCommand(4, "zero class totals");
            Id<Course> courseId = eventDB.AddCourse(new Course(CourseKind.Normal, "Blue", 15000, 1));
            eventDB.GetCourse(courseId).load = 25;
            eventDB.AddEventClass(new EventClass { Name = "D21", CourseId = courseId, ParticipantCount = 0, MapCount = 1, ReserveCount = 0 });
            undo.EndCommand(4);

            Assert.That(EventClassSupport.GetCourseParticipantCount(eventDB, courseId), Is.EqualTo(0));
            Assert.That(EventClassSupport.GetCourseRequiredMapCount(eventDB, courseId), Is.EqualTo(0));
        }

        [Test]
        public void ExportXmlUsesEventClassesBeforeLegacyCourseClassName()
        {
            UndoMgr undo = new UndoMgr(20);
            EventDB eventDB = new EventDB(undo);
            undo.BeginCommand(8, "Create export class test");
            Id<Course> courseId = AddMinimalExportableCourse(eventDB, "Blue", "Legacy");
            eventDB.AddEventClass(new EventClass { Name = "Event class", CourseId = courseId });
            undo.EndCommand(8);
            string path = Path.Combine(Path.GetTempPath(), "purplepen-event-class-export-" + Guid.NewGuid().ToString("N") + ".xml");

            try {
                new ExportXmlVersion3().WriteXml(path, eventDB, new RectangleF(0, 0, 100, 100), null);
                string xml = File.ReadAllText(path);
                Assert.That(xml, Does.Contain("<ClassName>Event class</ClassName>"));
                Assert.That(xml, Does.Not.Contain("<ClassName>Legacy</ClassName>"));
            }
            finally {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Test]
        public void ExportXmlFallsBackToLegacyCourseClassNameWithoutEventClass()
        {
            UndoMgr undo = new UndoMgr(20);
            EventDB eventDB = new EventDB(undo);
            undo.BeginCommand(9, "Create legacy export test");
            AddMinimalExportableCourse(eventDB, "Blue", "Legacy");
            undo.EndCommand(9);
            string path = Path.Combine(Path.GetTempPath(), "purplepen-legacy-class-export-" + Guid.NewGuid().ToString("N") + ".xml");

            try {
                new ExportXmlVersion3().WriteXml(path, eventDB, new RectangleF(0, 0, 100, 100), null);
                Assert.That(File.ReadAllText(path), Does.Contain("<ClassName>Legacy</ClassName>"));
            }
            finally {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        private static Id<Course> AddMinimalExportableCourse(EventDB eventDB, string courseName, string legacyClassName)
        {
            Id<Course> courseId = eventDB.AddCourse(new Course(CourseKind.Normal, courseName, 15000, 1));
            ChangeEvent.ChangeCourseClassName(eventDB, courseId, legacyClassName);
            Id<ControlPoint> startId = eventDB.AddControlPoint(new ControlPoint(ControlPointKind.Start, null, new PointF(0, 0)));
            Id<ControlPoint> finishId = eventDB.AddControlPoint(new ControlPoint(ControlPointKind.Finish, null, new PointF(10, 0)));
            Id<CourseControl> finishCourseControlId = eventDB.AddCourseControl(new CourseControl(finishId, Id<CourseControl>.None));
            Id<CourseControl> startCourseControlId = eventDB.AddCourseControl(new CourseControl(startId, finishCourseControlId));
            Course course = (Course)eventDB.GetCourse(courseId).Clone();
            course.firstCourseControl = startCourseControlId;
            eventDB.ReplaceCourse(courseId, course);
            return courseId;
        }
    }
}
