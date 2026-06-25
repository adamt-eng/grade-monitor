using Grade_Monitor.Core.Services;
using Grade_Monitor.Models;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Grade_Monitor.Core.Session;

internal sealed class SessionManager
{
    private readonly AuthService _auth;
    private readonly GradesService _grades;

    internal SessionManager(AuthService authService, GradesService gradesService)
    {
        _auth = authService;
        _grades = gradesService;
    }

    internal async Task InitializeSessionAsync(SessionState state, string? fallbackSemester = null)
    {
        var details = await _auth.PostDataAsync(state, "students/my_details");
        state.CurrentSemester = details["study"]?["en_term"]?.GetValue<string>();

        var results = await _auth.GetDataAsync(state, "students/my_results");
        state.Results = results;

        var semesters = results["results"]?.AsArray() ?? [];
        state.Cgpa = semesters.LastOrDefault()?["grade"]?["cumulative_gpa"]?.GetValue<double>().ToString(CultureInfo.InvariantCulture);

        state.Semesters = [.. semesters.Select(r => r?["en_term"]?.GetValue<string>()).OfType<string>()];
        if (state.CurrentSemester != null)
            state.Semesters.Add(state.CurrentSemester);

        SemesterService.DetermineRequestedSemester(state, fallbackSemester);
    }

    internal Task<IReadOnlyList<CourseGrade>> FetchGradesAsync(SessionState state)
        => _grades.FetchGradesAsync(state);
}
