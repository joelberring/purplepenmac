/* Copyright (c) 2026, Purple Pen contributors.
 * All rights reserved.
 */

#if TEST
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;

namespace PurplePen.Tests
{
    using PurplePen.MapModel;

    /// <summary>Tests read-only event and undo history snapshots.</summary>
    [TestClass]
    public class HistoryReviewTests
    {
        /// <summary>Reports object totals and the next Undo command after a completed change.</summary>
        [TestMethod]
        public void CreateStatusReportsObjectsAndNextUndoCommand()
        {
            UndoMgr undoMgr = new UndoMgr(5);
            EventDB eventDB = new EventDB(undoMgr);
            long initialChangeNumber = eventDB.ChangeNum;

            undoMgr.BeginCommand(1, "Add review control");
            eventDB.AddControlPoint(new ControlPoint(ControlPointKind.Normal, "31", new PointF(10, 20)));
            undoMgr.EndCommand(1);

            HistoryReviewStatus status = HistoryReview.CreateStatus(eventDB, undoMgr);

            Assert.AreNotEqual(initialChangeNumber, status.ChangeNumber);
            Assert.IsTrue(status.IsDirty);
            Assert.IsFalse(status.IsCommandInProgress);
            Assert.IsTrue(status.CanUndo);
            Assert.IsFalse(status.CanRedo);
            Assert.AreEqual("Add review control", status.UndoName);
            Assert.IsNull(status.RedoName);
            Assert.AreEqual(1, status.ObjectCounts.ControlPointCount);
            Assert.AreEqual(0, status.ObjectCounts.CourseCount);
            Assert.AreEqual(1, status.ObjectCounts.TotalCount);
        }

        /// <summary>Counts event classes and route-choice candidates in the review total.</summary>
        [TestMethod]
        public void CreateStatusCountsNewEventObjectTypes()
        {
            UndoMgr undoMgr = new UndoMgr(5);
            EventDB eventDB = new EventDB(undoMgr);
            eventDB.AddEventClass(new EventClass { Name = "D21" });
            eventDB.AddRouteChoiceCandidate(new RouteChoiceCandidate());

            HistoryReviewStatus status = HistoryReview.CreateStatus(eventDB, undoMgr);

            Assert.AreEqual(1, status.ObjectCounts.EventClassCount);
            Assert.AreEqual(1, status.ObjectCounts.RouteChoiceCandidateCount);
            Assert.AreEqual(2, status.ObjectCounts.TotalCount);
        }

        /// <summary>Reports the next Redo command after undoing and does not mutate the undo state while reading.</summary>
        [TestMethod]
        public void CreateStatusReportsRedoWithoutChangingHistory()
        {
            UndoMgr undoMgr = new UndoMgr(5);
            EventDB eventDB = new EventDB(undoMgr);
            undoMgr.BeginCommand(1, "Add review control");
            eventDB.AddControlPoint(new ControlPoint(ControlPointKind.Normal, "31", new PointF(10, 20)));
            undoMgr.EndCommand(1);
            undoMgr.Undo();
            long undoneChangeNumber = eventDB.ChangeNum;

            HistoryReviewStatus firstStatus = HistoryReview.CreateStatus(eventDB, undoMgr);
            HistoryReviewStatus secondStatus = HistoryReview.CreateStatus(eventDB, undoMgr);

            Assert.IsFalse(firstStatus.CanUndo);
            Assert.IsTrue(firstStatus.CanRedo);
            Assert.IsNull(firstStatus.UndoName);
            Assert.AreEqual("Add review control", firstStatus.RedoName);
            Assert.AreEqual(0, firstStatus.ObjectCounts.ControlPointCount);
            Assert.AreEqual(undoneChangeNumber, secondStatus.ChangeNumber);
            Assert.IsFalse(undoMgr.CanUndo);
            Assert.IsTrue(undoMgr.CanRedo);
        }

        /// <summary>Disables undo and redo names while a command is in progress.</summary>
        [TestMethod]
        public void CreateStatusReportsInProgressCommandWithoutReadingUnavailableNames()
        {
            UndoMgr undoMgr = new UndoMgr(5);
            EventDB eventDB = new EventDB(undoMgr);
            undoMgr.BeginCommand(1, "Add review control");

            HistoryReviewStatus status = HistoryReview.CreateStatus(eventDB, undoMgr);

            Assert.IsTrue(status.IsCommandInProgress);
            Assert.IsFalse(status.CanUndo);
            Assert.IsFalse(status.CanRedo);
            Assert.IsNull(status.UndoName);
            Assert.IsNull(status.RedoName);

            undoMgr.Rollback();
        }
    }
}
#endif
