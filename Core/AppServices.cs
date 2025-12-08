using Grade_Monitor.Core.Session;

namespace Grade_Monitor.Core;

internal static class AppServices
{
    internal static readonly SessionManager SessionManager = ServiceFactory.CreateSessionManager();
}