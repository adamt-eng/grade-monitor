using Grade_Monitor.Core.Parsing;
using Grade_Monitor.Core.Session;
using Grade_Monitor.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Grade_Monitor.Core.Services;

internal sealed class GradesService
{
    private readonly HttpHelper _http;
    private readonly CourseUrlService _urlService;

    internal GradesService(HttpHelper http, CourseUrlService urlService)
    {
        _http = http;
        _urlService = urlService;
    }

    internal async Task<SortedDictionary<string, string>> FetchGradesAsync(SessionState state)
    {
        if (state is { FetchFinalGrades: true, FetchedFinalGradesOnce: false })
            return await FetchFinalGradesAsync(state);

        state.FetchedFinalGradesOnce = false;

        var courses = await _urlService.ResolveAsync(state);
        var results = new SortedDictionary<string, string>();

        await Task.WhenAll(courses.Select(async kv =>
        {
            var html = await _http.FetchPage(kv.Value, state.User.DiscordUserId);
            results[kv.Key] = ExtractGradeDetails(html);
        }));

        return results;
    }

    internal async Task<SortedDictionary<string, string>> FetchFinalGradesAsync(SessionState state)
    {
        var results = new SortedDictionary<string, string>();

        foreach (var c in CourseParser.Parse(state.StudentCoursesHtml!)
                                 .Where(c => c.Semester == state.RequestedSemester && !string.IsNullOrEmpty(c.Grade)))
        {
            results[c.FullName] = $"||**__Course Grade: {c.Grade}__**||";
        }

        if (results.Count == 0)
        {
            state.FetchedFinalGradesOnce = true;
            return await FetchGradesAsync(state);
        }

        return results;
    }

    private static string ExtractGradeDetails(string html)
    {
        var lines = html.Split('\n').Where(ContainsGradeIdentifier).ToList();
        var details = new List<string>();

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];

            if (line.Contains("right"))
            {
                var grade = line.ExtractBetween(">", "<").Trim();
                details.Add($"||**__Course Grade: {grade}__**||");
            }
            else if (line.Contains("left"))
            {
                var title = line.ExtractBetween(">", "<");
                var value = lines[++i].ExtractBetween(">", "<").Replace(" ", "");
                details.Add($"{title}: {value}");
            }
        }

        return string.Join(Environment.NewLine, details).Trim();
    }

    private static bool ContainsGradeIdentifier(string s) =>
        s.Contains("text-align: right;") || s.Contains("7 col");
}
