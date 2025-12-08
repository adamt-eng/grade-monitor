using Grade_Monitor.Core.Services;
using Grade_Monitor.Helpers;

namespace Grade_Monitor.Core.Session;

internal static class ServiceFactory
{
    internal static SessionManager CreateSessionManager()
    {
        var http = new HttpHelper();

        var auth = new AuthService(http);
        var studentCourses = new StudentCoursesService(http);
        var courseUrlService = new CourseUrlService(http);
        var grades = new GradesService(http, courseUrlService);

        return new SessionManager(
            auth,
            studentCourses,
            grades
        );
    }
}