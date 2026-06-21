using Grade_Monitor.Models;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Grade_Monitor.Core.Session;

internal sealed class SessionState
{
    internal User User { get; }
    internal int Offset;
    internal int Fails;

    internal bool FetchFinalGrades;

    internal string? RequestedSemester;
    internal string? Cgpa;

    internal string? CurrentSemester { get; set; }
    internal HashSet<string> Semesters { get; set; } = [];
    internal JsonNode? Results { get; set; }

    internal SessionState(User user) => User = user;
}
