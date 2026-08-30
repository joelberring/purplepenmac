/* Copyright (c) 2026, Purple Pen contributors.
 * All rights reserved.
 */

using System;

using PurplePen.MapModel;

namespace PurplePen
{
    /// <summary>
    /// Creates read-only snapshots suitable for history and event-review user interfaces.
    /// </summary>
    public static class HistoryReview
    {
        /// <summary>
        /// Reads the current event revision, object totals, and undo state without changing either object.
        /// </summary>
        /// <param name="eventDB">Event database to summarize.</param>
        /// <param name="undoMgr">Undo manager that owns the event changes.</param>
        /// <returns>An immutable status snapshot.</returns>
        public static HistoryReviewStatus CreateStatus(EventDB eventDB, UndoMgr undoMgr)
        {
            if (eventDB == null)
                throw new ArgumentNullException(nameof(eventDB));
            if (undoMgr == null)
                throw new ArgumentNullException(nameof(undoMgr));

            bool canUndo = undoMgr.CanUndo;
            bool canRedo = undoMgr.CanRedo;
            string undoName = canUndo ? undoMgr.UndoName : null;
            string redoName = canRedo ? undoMgr.RedoName : null;
            EventObjectCounts objectCounts = new EventObjectCounts(eventDB.AllControlPointIds.Count,
                                                                     eventDB.AllCourseIds.Count,
                                                                     eventDB.AllCourseControlIds.Count,
                                                                     eventDB.AllLegIds.Count,
                                                                     eventDB.AllSpecialIds.Count,
                                                                     eventDB.AllTrainingExerciseIds.Count,
                                                                     eventDB.AllEventClassIds.Count,
                                                                     eventDB.AllRouteChoiceCandidateIds.Count);

            return new HistoryReviewStatus(eventDB.ChangeNum, objectCounts, undoMgr.IsDirty,
                                           undoMgr.CommandInProgress, canUndo, canRedo, undoName, redoName);
        }
    }

    /// <summary>
    /// Immutable event revision and undo state for a review surface.
    /// </summary>
    public sealed class HistoryReviewStatus
    {
        /// <summary>Initializes a history-review status snapshot.</summary>
        /// <param name="changeNumber">Current EventDB change number.</param>
        /// <param name="objectCounts">Current event object totals.</param>
        /// <param name="isDirty">Whether the undo manager considers the document unsaved.</param>
        /// <param name="isCommandInProgress">Whether an undoable command is currently being built.</param>
        /// <param name="canUndo">Whether Undo is available.</param>
        /// <param name="canRedo">Whether Redo is available.</param>
        /// <param name="undoName">Display name for the next Undo command, when available.</param>
        /// <param name="redoName">Display name for the next Redo command, when available.</param>
        internal HistoryReviewStatus(long changeNumber, EventObjectCounts objectCounts, bool isDirty,
                                     bool isCommandInProgress, bool canUndo, bool canRedo, string undoName, string redoName)
        {
            ChangeNumber = changeNumber;
            ObjectCounts = objectCounts;
            IsDirty = isDirty;
            IsCommandInProgress = isCommandInProgress;
            CanUndo = canUndo;
            CanRedo = canRedo;
            UndoName = undoName;
            RedoName = redoName;
        }

        /// <summary>Gets the EventDB change number, useful for detecting a revision change.</summary>
        public long ChangeNumber { get; private set; }

        /// <summary>Gets the current object totals.</summary>
        public EventObjectCounts ObjectCounts { get; private set; }

        /// <summary>Gets whether the document differs from its last clean state.</summary>
        public bool IsDirty { get; private set; }

        /// <summary>Gets whether a command is still being assembled.</summary>
        public bool IsCommandInProgress { get; private set; }

        /// <summary>Gets whether the next undo command is available.</summary>
        public bool CanUndo { get; private set; }

        /// <summary>Gets whether the next redo command is available.</summary>
        public bool CanRedo { get; private set; }

        /// <summary>Gets the user-visible next undo command, or null when Undo is unavailable.</summary>
        public string UndoName { get; private set; }

        /// <summary>Gets the user-visible next redo command, or null when Redo is unavailable.</summary>
        public string RedoName { get; private set; }
    }

    /// <summary>
    /// Immutable totals of objects currently stored in an event database.
    /// </summary>
    public sealed class EventObjectCounts
    {
        /// <summary>Initializes event object totals.</summary>
        /// <param name="controlPointCount">Number of control points.</param>
        /// <param name="courseCount">Number of courses.</param>
        /// <param name="courseControlCount">Number of course controls.</param>
        /// <param name="legCount">Number of explicitly stored legs.</param>
        /// <param name="specialCount">Number of specials.</param>
        internal EventObjectCounts(int controlPointCount, int courseCount, int courseControlCount, int legCount, int specialCount,
                                   int trainingExerciseCount = 0, int eventClassCount = 0, int routeChoiceCandidateCount = 0)
        {
            ControlPointCount = controlPointCount;
            CourseCount = courseCount;
            CourseControlCount = courseControlCount;
            LegCount = legCount;
            SpecialCount = specialCount;
            TrainingExerciseCount = trainingExerciseCount;
            EventClassCount = eventClassCount;
            RouteChoiceCandidateCount = routeChoiceCandidateCount;
        }

        /// <summary>Gets the number of control points.</summary>
        public int ControlPointCount { get; private set; }

        /// <summary>Gets the number of courses.</summary>
        public int CourseCount { get; private set; }

        /// <summary>Gets the number of course controls.</summary>
        public int CourseControlCount { get; private set; }

        /// <summary>Gets the number of explicit legs.</summary>
        public int LegCount { get; private set; }

        /// <summary>Gets the number of specials.</summary>
        public int SpecialCount { get; private set; }

        /// <summary>Gets the number of training exercises.</summary>
        public int TrainingExerciseCount { get; private set; }

        /// <summary>Gets the number of event class records.</summary>
        public int EventClassCount { get; private set; }

        /// <summary>Gets the number of route-choice candidate records.</summary>
        public int RouteChoiceCandidateCount { get; private set; }

        /// <summary>Gets the total of all counted event objects.</summary>
        public int TotalCount
        {
            get { return ControlPointCount + CourseCount + CourseControlCount + LegCount + SpecialCount + TrainingExerciseCount + EventClassCount + RouteChoiceCandidateCount; }
        }
    }
}
