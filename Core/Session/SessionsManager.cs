using System.Collections.Generic;

namespace Grade_Monitor.Core.Session;

internal static class SessionsManager
{
    private static readonly Dictionary<ulong, SessionState> Sessions = [];

    internal static bool TryGetSession(ulong id, out SessionState session) => Sessions.TryGetValue(id, out session!);
    internal static void AddSession(ulong id, SessionState session) => Sessions[id] = session;
    internal static void ClearSessions() => Sessions.Clear();
}