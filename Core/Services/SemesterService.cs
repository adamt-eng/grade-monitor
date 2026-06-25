using Grade_Monitor.Core.Session;

namespace Grade_Monitor.Core.Services;

internal static class SemesterService
{
    /// <summary>
    /// Picks the semester to display when the session has none selected yet. Falls back to the value the
    /// frontend remembered for the user (e.g. the semester shown in the existing Discord message), and then
    /// to the current term.
    /// </summary>
    internal static void DetermineRequestedSemester(SessionState state, string? fallbackSemester)
    {
        if (state.RequestedSemester != null)
            return;

        state.RequestedSemester = fallbackSemester ?? state.CurrentSemester;
    }
}
