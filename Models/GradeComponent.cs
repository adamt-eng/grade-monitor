namespace Grade_Monitor.Models;

/// <summary>
/// One graded component of a course (midterm, activities, practical, etc.).
/// </summary>
internal sealed class GradeComponent
{
    internal required string Name { get; init; }

    /// <summary>The achieved degree, or <c>null</c> when it has not been graded yet.</summary>
    internal int? Degree { get; init; }

    internal int MaxDegree { get; init; }
}
