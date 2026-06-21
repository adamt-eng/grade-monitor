using Grade_Monitor.Core.Session;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Grade_Monitor.Core.Services;

internal sealed class GradesService
{
    private const string NoGrades = "No grades available yet.";

    private readonly AuthService _auth;

    internal GradesService(AuthService auth) => _auth = auth;

    internal async Task<SortedDictionary<string, string>> FetchGradesAsync(SessionState state)
    {
        if (state.RequestedSemester != null && state.RequestedSemester != state.CurrentSemester)
            return FetchPastSemester(state);

        return await FetchCurrentSemesterAsync(state);
    }

    private async Task<SortedDictionary<string, string>> FetchCurrentSemesterAsync(SessionState state)
    {
        var data = await _auth.GetDataAsync(state, "students/my_courses");
        var completed = CompletedCodes(state.Results);
        var results = new SortedDictionary<string, string>();

        foreach (var study in data["studies"]?.AsArray() ?? [])
        {
            if (study == null)
                continue;

            var code = study["code"]?.GetValue<string>();
            if (code != null && completed.Contains(code))
                continue;

            var name = $"{code}: {study["en_name"]?.GetValue<string>()}";
            results[name] = FormatCurrent(study, state.FetchFinalGrades);
        }

        return results;
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

    private static SortedDictionary<string, string> FetchPastSemester(SessionState state)
    {
        var results = new SortedDictionary<string, string>();

        var semester = state.Results?["results"]?.AsArray()
            .FirstOrDefault(r => r?["en_term"]?.GetValue<string>() == state.RequestedSemester);

        foreach (var course in semester?["grades"]?.AsArray() ?? [])
        {
            if (course == null)
                continue;

            var name = $"{course["code"]?.GetValue<string>()}: {course["en_name"]?.GetValue<string>()}";
            results[name] = FinalGrade(course["grade"]?["grade"]?.GetValue<string>());
        }

        return results;
    }

    private static string FormatCurrent(JsonNode study, bool finalOnly)
    {
        var finalLetter = study["grade"]?["grade"]?.GetValue<string>();
        if (finalOnly && !string.IsNullOrEmpty(finalLetter))
            return FinalGrade(finalLetter);

        var lines = (study["grades_detailes"]?.AsArray() ?? [])
            .Where(g => g != null)
            .Select(g => $"{g!["en_name"]?.GetValue<string>()}: {Degree(g)}/{g["max_degree"]?.GetValue<int>()}")
            .ToList();

        return lines.Count > 0 ? string.Join("\n", lines) : NoGrades;
    }

    private static string Degree(JsonNode component) =>
        component["degree"] is { } d ? d.GetValue<int>().ToString() : "—";

    private static string FinalGrade(string? letter) => $"||**__Course Grade: {letter}__**||";
}
