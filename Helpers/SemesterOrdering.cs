using System;
using System.Collections.Generic;
using System.Linq;

namespace Grade_Monitor.Helpers;

/// <summary>
/// Chronological ordering for semester labels like "Spring 2024", shared by every frontend.
/// </summary>
internal static class SemesterOrdering
{
    internal static IEnumerable<string> Order(IEnumerable<string> semesters) => semesters.OrderBy(Key);

    private static int Key(string semester)
    {
        var parts = semester.Split(' ');
        var season = parts[0];
        var year = int.Parse(parts[1]);

        var seasonValue = season switch
        {
            "Spring" => 0,
            "Summer" => 1,
            "Fall" => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(semester))
        };

        return year * 10 + seasonValue;
    }
}
