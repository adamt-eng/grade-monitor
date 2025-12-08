using Discord;
using Grade_Monitor.Core.Session;
using Grade_Monitor.Helpers;
using System.Linq;
using System.Text.RegularExpressions;

namespace Grade_Monitor.Core.Services;

internal static class SemesterService
{
    internal static string ExtractCurrentSemester(string studentCoursesHtml)
    {
        return studentCoursesHtml
            .ExtractBetween("<strong>Term</strong>: ", "<", lastIndexOf: false)
            .Trim();
    }

    internal static void PopulateSemesters(SessionState state)
    {
        foreach (Match m in RegexHelper.SemesterNamePattern().Matches(state.StudentCoursesHtml!))
        {
            if (!state.User.Semesters.ContainsKey(m.Value))
                state.User.Semesters[m.Value] = [];
        }
    }

    internal static void DetermineRequestedSemester(SessionState state, IUserMessage? message)
    {
        if (state.RequestedSemester != null)
            return;

        if (message == null)
        {
            state.RequestedSemester = state.CurrentSemester;
            return;
        }

        var selectMenus =
            message.Components.OfType<ActionRowComponent>()
                .SelectMany(r => r.Components.OfType<SelectMenuComponent>())
                .ToList();

        state.RequestedSemester =
            selectMenus[0].Options.First(o => o.IsDefault == true).Value;
    }
}
