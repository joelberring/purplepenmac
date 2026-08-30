using NUnit.Framework;
using PurplePen;
using PurplePen.Graphics2D;
using PurplePen.ViewModels;
using System.Drawing;
using System.Linq;

namespace PurplePenViewModels.Tests
{
    /// <summary>Tests for non-destructive training-overlay editing.</summary>
    [TestFixture]
    public class TrainingExercisesDialogViewModelTests
    {
        [Test]
        public void LoadAndCreatePreservesGeometryAndEditsMetadata()
        {
            TrainingExercise exercise = new TrainingExercise(TrainingExerciseKind.AttackPoint, new CourseDesignator(new Id<Course>(3)), new PointF[] { new PointF(4, 5) }) {
                width = 25,
                maskOpacity = 0.4F,
                instruction = "Attack",
                visibility = TrainingExerciseVisibility.Runner | TrainingExerciseVisibility.Answer,
            };
            TrainingExercisesDialogViewModel viewModel = new TrainingExercisesDialogViewModel();
            viewModel.LoadExercises(new[] { exercise });

            viewModel.Exercises[0].Instruction = "Find the last safe";
            viewModel.Exercises[0].MaskOpacityPercent = 65;
            viewModel.Exercises[0].ShowToCoach = true;
            var result = viewModel.CreateExercises();

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].instruction, Is.EqualTo("Find the last safe"));
            Assert.That(result[0].visibility, Is.EqualTo(TrainingExerciseVisibility.All));
            Assert.That(result[0].maskOpacity, Is.EqualTo(0.65F));
            Assert.That(result[0].locations[0], Is.EqualTo(new PointF(4, 5)));
            Assert.That(exercise.instruction, Is.EqualTo("Attack"));
            Assert.That(exercise.maskOpacity, Is.EqualTo(0.4F));
        }

        [Test]
        public void InvalidGeometryDisablesSaveAndRemoveStillWorks()
        {
            TrainingExercisesDialogViewModel viewModel = new TrainingExercisesDialogViewModel();
            TrainingExercise invalid = new TrainingExercise(TrainingExerciseKind.ContourOnly, new CourseDesignator(new Id<Course>(3)), new PointF[] { new PointF(0, 0), new PointF(100, 0) });
            viewModel.LoadExercises(new[] { invalid });
            Assert.That(viewModel.Exercises, Has.Count.EqualTo(1));
            Assert.That(viewModel.IsSaveEnabled, Is.False);
            viewModel.RemoveExerciseCommand.Execute(null);
            Assert.That(viewModel.Exercises, Is.Empty);
            Assert.That(viewModel.IsSaveEnabled, Is.True);
        }

        [Test]
        public void ContourOnlyEditsAllowedSymbolIdsAndUsesEmptyForDefaults()
        {
            TrainingExercise exercise = new TrainingExercise(TrainingExerciseKind.ContourOnly, new CourseDesignator(new Id<Course>(3)), new PointF[] {
                new PointF(0, 0), new PointF(100, 0), new PointF(100, 100)
            });
            TrainingExercisesDialogViewModel viewModel = new TrainingExercisesDialogViewModel();
            viewModel.LoadExercises(new[] { exercise });

            TrainingExerciseEditorItem item = viewModel.Exercises[0];
            Assert.That(item.IsContourOnly, Is.True);
            Assert.That(item.IsMaskOpacityVisible, Is.True);
            item.AllowedSymbolIdsText = "101; 102.1,103";
            TrainingExercise[] result = viewModel.CreateExercises().ToArray();

            Assert.That(result[0].allowedSymbolIds, Is.EqualTo(new[] { "101", "102.1", "103" }));
            item.AllowedSymbolIdsText = "";
            result = viewModel.CreateExercises().ToArray();
            Assert.That(result[0].allowedSymbolIds, Is.Empty);
        }

        [Test]
        public void CorridorObjectUsesDashedBoundariesAndCentreLineHandles()
        {
            PointF[] centreLine = new PointF[] { new PointF(0, 0), new PointF(10, 0) };
            TrainingCorridorCourseObj corridor = new TrainingCorridorCourseObj(new CourseAppearance(), centreLine, 3F);

            Assert.That(corridor.lineKind, Is.EqualTo(LineKind.Dashed));
            Assert.That(corridor.GetHandles(), Is.EqualTo(centreLine));
            Assert.That(corridor.path.BoundingBox.Height, Is.EqualTo(3F).Within(0.001F));

            corridor.MoveHandle(centreLine[1], new PointF(12, 2));
            Assert.That(corridor.GetHandles()[1], Is.EqualTo(new PointF(12, 2)));
        }

        [Test]
        public void CorridorEditsVisibleWidthAndWhiteMarginSeparately()
        {
            TrainingExercise exercise = new TrainingExercise(TrainingExerciseKind.Corridor, new CourseDesignator(new Id<Course>(3)),
                                                              new PointF[] { new PointF(0, 0), new PointF(20, 0) }) {
                width = 4,
                whiteMargin = 1.5F
            };
            TrainingExercisesDialogViewModel viewModel = new TrainingExercisesDialogViewModel();
            viewModel.LoadExercises(new[] { exercise });

            TrainingExerciseEditorItem item = viewModel.Exercises[0];
            item.Width = 5;
            item.WhiteMargin = 2.25M;
            TrainingExercise result = viewModel.CreateExercises().Single();

            Assert.That(result.width, Is.EqualTo(5F));
            Assert.That(result.whiteMargin, Is.EqualTo(2.25F));
            Assert.That(exercise.width, Is.EqualTo(4F));
            Assert.That(exercise.whiteMargin, Is.EqualTo(1.5F));
        }

        [Test]
        public void SharpCorridorBendCannotCreateAGiantMiterSpike()
        {
            PointF corner = new PointF(10, 0);
            TrainingCorridorOutline outline = TrainingCorridorGeometry.CreateOutline(
                new PointF[] { new PointF(0, 0), corner, new PointF(1, 1) }, 2);

            Assert.That(Geometry.Distance(corner, outline.LeftBoundary[1]), Is.LessThanOrEqualTo(2.001F));
            Assert.That(Geometry.Distance(corner, outline.RightBoundary[1]), Is.LessThanOrEqualTo(2.001F));
        }

        [Test]
        public void MultipleAttackPointsReceiveDistinctStableOrdinals()
        {
            CourseDesignator course = new CourseDesignator(new Id<Course>(3));
            TrainingExercise first = new TrainingExercise(TrainingExerciseKind.AttackPoint, course, new PointF[] { new PointF(4, 5) }) { width = 3 };
            TrainingExercise second = new TrainingExercise(TrainingExerciseKind.AttackPoint, course, new PointF[] { new PointF(8, 9) }) { width = 3 };
            TrainingExercisesDialogViewModel viewModel = new TrainingExercisesDialogViewModel();
            viewModel.LoadExercises(new[] { first, second }, course);

            Assert.That(viewModel.Exercises[0].DisplayOrdinal, Is.EqualTo(1));
            Assert.That(viewModel.Exercises[1].DisplayOrdinal, Is.EqualTo(2));

            viewModel.SelectedExercise = viewModel.Exercises[0];
            viewModel.RemoveExerciseCommand.Execute(null);
            Assert.That(viewModel.Exercises.Single().DisplayOrdinal, Is.EqualTo(1));
        }

        [Test]
        public void ContourAreaUsesVisualMapLayerChoices()
        {
            CourseDesignator course = new CourseDesignator(new Id<Course>(3));
            TrainingExercise exercise = new TrainingExercise(TrainingExerciseKind.ContourOnly, course,
                new PointF[] { new PointF(0, 0), new PointF(10, 0), new PointF(10, 10) });
            TrainingMapSymbolInfo[] symbols = new[] {
                new TrainingMapSymbolInfo("101.0", "Contour", CmykColor.FromCmyk(0, 0.55F, 1, 0)),
                new TrainingMapSymbolInfo("201.0", "Cliff", CmykColor.FromCmyk(0, 0, 0, 1))
            };
            TrainingExercisesDialogViewModel viewModel = new TrainingExercisesDialogViewModel();
            viewModel.LoadExercises(new[] { exercise }, course, symbols);

            TrainingExerciseEditorItem item = viewModel.Exercises.Single();
            Assert.That(item.HasSymbolChoices, Is.True);
            Assert.That(item.SymbolChoices.Single(choice => choice.SymbolId == "101.0").IsSelected, Is.True);
            Assert.That(item.SymbolChoices.Single(choice => choice.SymbolId == "201.0").IsSelected, Is.False);

            item.SymbolChoices.Single(choice => choice.SymbolId == "101.0").IsSelected = false;
            item.SymbolChoices.Single(choice => choice.SymbolId == "201.0").IsSelected = true;
            TrainingExercise saved = viewModel.CreateExercises().Single();
            Assert.That(saved.allowedSymbolIds, Is.EqualTo(new[] { "201.0" }));
        }
    }
}
