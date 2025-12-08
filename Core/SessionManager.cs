using System.Collections.Generic;

namespace Grade_Monitor.Core;

internal static class SessionManager
{
    private static readonly Dictionary<ulong, Session> Sessions = [];

    internal static bool TryGetSession(ulong id, out Session session) => Sessions.TryGetValue(id, out session!);
    internal static void AddSession(ulong id, Session session) => Sessions[id] = session;
    internal static void ClearSessions() => Sessions.Clear();
}