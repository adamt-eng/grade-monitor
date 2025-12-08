using Grade_Monitor.Core.Session;
using Grade_Monitor.Helpers;
using System;
using System.Threading.Tasks;

namespace Grade_Monitor.Core.Services;

internal sealed class StudentCoursesService
{
    private readonly HttpHelper _http;

    internal StudentCoursesService(HttpHelper httpHelper)
    {
        _http = httpHelper;
    }

    internal async Task LoadAsync(SessionState state)
    {
        var html = await _http.FetchPage("https://eng.asu.edu.eg/study/studies/student_courses", state.User.DiscordUserId);

        if (html.Contains("Questionnaire", StringComparison.OrdinalIgnoreCase))
            throw new Exception("Unable to fetch grades: Please complete the mandatory questionnaire.");

        state.StudentCoursesHtml = html;
    }
}
