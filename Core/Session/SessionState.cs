using Grade_Monitor.Models;

namespace Grade_Monitor.Core.Session;

internal sealed class SessionState
{
    internal User User { get; }
    internal int Offset;
    internal int Fails;

    internal bool FetchFinalGrades;
    internal bool FetchedFinalGradesOnce;

    internal string? RequestedSemester;
    internal string? Cgpa;

    internal string? StudentCoursesHtml;
    internal string? CurrentSemester { get; set; }

    internal SessionState(User user) => User = user;
}
