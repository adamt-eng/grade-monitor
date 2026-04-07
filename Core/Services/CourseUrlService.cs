using Grade_Monitor.Configuration;
using Grade_Monitor.Core.Session;
using Grade_Monitor.Discord_App;
using Grade_Monitor.Helpers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Grade_Monitor.Core.Services;

internal sealed class CourseUrlService
{
    private readonly HttpHelper _http;

    internal CourseUrlService(HttpHelper http)
    {
        _http = http;
    }

    internal async Task<IDictionary<string, string>> ResolveAsync(SessionState state)
    {
        var result = new Dictionary<string, string>();

        var html = await _http.FetchPage("https://eng.asu.edu.eg/dashboard/my_courses", state.User.DiscordUserId);
        var lines = html.Split('\n')
                        .Where(l => state.RequestedSemester != null && l.Contains(state.RequestedSemester));

        foreach (var line in lines)
        {
            var name = line.ExtractBetween(">", " (");
            var url = line.ExtractBetween("\"", "\"");

            AddToConfig(state, name, url);
            result[name] = url;
        }

        ConfigurationManager.Save(DiscordApp.AppConfig);

        return result;
    }

    private static void AddToConfig(SessionState state, string name, string url)
    {
        state.User.Semesters[state.RequestedSemester!].Add(name);
        DiscordApp.AppConfig.Courses[name] = url;
    }
}
