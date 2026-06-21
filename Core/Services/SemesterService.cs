using Discord;
using Grade_Monitor.Core.Session;
using System.Linq;

namespace Grade_Monitor.Core.Services;

internal static class SemesterService
{
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
