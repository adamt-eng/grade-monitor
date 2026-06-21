using Grade_Monitor.Core.Services;
using Grade_Monitor.Helpers;

namespace Grade_Monitor.Core.Session;

internal static class ServiceFactory
{
    internal static SessionManager CreateSessionManager()
    {
        var api = new ApiClient();

        var auth = new AuthService(api);
        var grades = new GradesService(auth);

        return new SessionManager(auth, grades);
    }
}
