using Discord;
using Grade_Monitor.Core.Services;
using Grade_Monitor.Helpers;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Grade_Monitor.Core.Session;

internal sealed class SessionManager
{
    private readonly AuthService _auth;
    private readonly StudentCoursesService _coursesService;
    private readonly GradesService _grades;

    internal SessionManager(
        AuthService authService,
        StudentCoursesService studentCoursesService,
        GradesService gradesService)
    {
        _auth = authService;
        _coursesService = studentCoursesService;
        _grades = gradesService;
    }

    internal async Task InitializeSessionAsync(SessionState state, IUserMessage? message)
    {
        // 1. Authenticate + load dashboard
        var dashboardHtml = await _auth.LoginAndGetDashboardHtmlAsync(state);

        // 2. Extract CGPA
        state.Cgpa = dashboardHtml.ExtractBetween("\"text-white\">", "<", lastIndexOf: false);

        // 3. Load student_courses page
        await _coursesService.LoadAsync(state);

        // 4. Extract current semester
        state.CurrentSemester = SemesterService.ExtractCurrentSemester(state.StudentCoursesHtml!);

        // 5. Populate semester list
        SemesterService.PopulateSemesters(state);

        // 6. Determine RequestedSemester
        SemesterService.DetermineRequestedSemester(state, message);
    }

    internal Task<SortedDictionary<string, string>> FetchGradesAsync(SessionState state)
        => _grades.FetchGradesAsync(state);
}