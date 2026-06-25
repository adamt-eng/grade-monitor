using Grade_Monitor.Core.Session;
using Grade_Monitor.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Grade_Monitor.Core.Services;

internal sealed class GradesService
{
    private readonly AuthService _auth;

    internal GradesService(AuthService auth) => _auth = auth;

    internal async Task<IReadOnlyList<CourseGrade>> FetchGradesAsync(SessionState state)
    {
        if (state.RequestedSemester != null && state.RequestedSemester != state.CurrentSemester)
            return FetchPastSemester(state);

        return await FetchCurrentSemesterAsync(state);
    }

    private async Task<IReadOnlyList<CourseGrade>> FetchCurrentSemesterAsync(SessionState state)
    {
        var data = await _auth.GetDataAsync(state, "students/my_courses");
        var completed = CompletedCodes(state.Results);

        // Keyed by "CODE: Name" so the output keeps a stable, code-ordered sequence.
        var results = new SortedDictionary<string, CourseGrade>(StringComparer.Ordinal);

        foreach (var study in data["studies"]?.AsArray() ?? [])
        {
            if (study == null)
                continue;

            var code = study["code"]?.GetValue<string>();
            if (code != null && completed.Contains(code))
                continue;

            var course = BuildCurrentCourse(study, code, state.FetchFinalGrades);
            results[$"{course.Code}: {course.Name}"] = course;
        }

        return [.. results.Values];
    }

    private static CourseGrade BuildCurrentCourse(JsonNode study, string? code, bool finalOnly)
    {
        var name = study["en_name"]?.GetValue<string>() ?? string.Empty;
        var finalLetter = study["grade"]?["grade"]?.GetValue<string>();

        // In "final grades only" mode, show the released letter as soon as it exists; otherwise fall through
        // to the per-component breakdown.
        if (finalOnly && !string.IsNullOrEmpty(finalLetter))
            return new CourseGrade { Code = code ?? string.Empty, Name = name, FinalGrade = finalLetter };

        var components = (study["grades_detailes"]?.AsArray() ?? [])
            .Where(g => g != null)
            .Select(g => new GradeComponent
            {
                Name = g!["en_name"]?.GetValue<string>() ?? string.Empty,
                Degree = g["degree"] is { } d ? d.GetValue<int>() : null,
                MaxDegree = g["max_degree"]?.GetValue<int>() ?? 0
            })
            .ToList();

        return new CourseGrade { Code = code ?? string.Empty, Name = name, Components = components };
    }

    private static HashSet<string> CompletedCodes(JsonNode? results)
    {
        var codes = new HashSet<string>();

        foreach (var semester in results?["results"]?.AsArray() ?? [])
            foreach (var course in semester?["grades"]?.AsArray() ?? [])
                if (course?["code"]?.GetValue<string>() is { } code)
                    codes.Add(code);

        return codes;
    }

    private static IReadOnlyList<CourseGrade> FetchPastSemester(SessionState state)
    {
        var results = new SortedDictionary<string, CourseGrade>(StringComparer.Ordinal);

        var semester = state.Results?["results"]?.AsArray()
            .FirstOrDefault(r => r?["en_term"]?.GetValue<string>() == state.RequestedSemester);

        foreach (var course in semester?["grades"]?.AsArray() ?? [])
        {
            if (course == null)
                continue;

            // A past semester always renders as a final letter grade (empty string while unpublished).
            var grade = new CourseGrade
            {
                Code = course["code"]?.GetValue<string>() ?? string.Empty,
                Name = course["en_name"]?.GetValue<string>() ?? string.Empty,
                FinalGrade = course["grade"]?["grade"]?.GetValue<string>() ?? string.Empty
            };

            results[$"{grade.Code}: {grade.Name}"] = grade;
        }

        return [.. results.Values];
    }
}
