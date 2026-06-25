using System.Collections.Generic;

namespace Grade_Monitor.Models;

/// <summary>
/// A single course's grade data, independent of how it is rendered (Discord embed or terminal table).
/// </summary>
internal sealed class CourseGrade
{
    internal required string Code { get; init; }
    internal required string Name { get; init; }

    /// <summary>
    /// The released final letter grade to display, or <c>null</c> when the final grade should not be shown
    /// (i.e. the per-component breakdown is shown instead). An empty string means a final grade is expected
    /// but the faculty has not published a letter yet.
    /// </summary>
    internal string? FinalGrade { get; init; }

    internal IReadOnlyList<GradeComponent> Components { get; init; } = [];
}
