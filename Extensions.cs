using Discord;
using System;
using System.Linq;

namespace Grade_Monitor;

internal static class Extensions
{
    internal static bool IdenticalTo(this EmbedBuilder embed1, EmbedBuilder embed2) => embed1.Fields.Count == embed2.Fields.Count && embed1.Fields.All(field1 => field1.Value.ToString() == embed2.Fields.First(field2 => field2.Name == field1.Name).Value.ToString());
    internal static string ExtractBetween(this string source, string start, string end, bool lastIndexOf = true)
    {
        int startIndex, endIndex;

        if (lastIndexOf)
        {

            if (start == end)
            {
                endIndex = source.LastIndexOf(start, StringComparison.Ordinal);
                startIndex = source.LastIndexOf(start, endIndex - 1, StringComparison.Ordinal);
            }
            else
            {
                endIndex = source.LastIndexOf(end, StringComparison.Ordinal);
                startIndex = source.LastIndexOf(start, endIndex, StringComparison.Ordinal);
            }

            return source.Substring(startIndex + start.Length, endIndex - startIndex - start.Length);
        }

        startIndex = source.IndexOf(start, StringComparison.Ordinal) + start.Length;
        endIndex = source.IndexOf(end, startIndex, StringComparison.Ordinal);
        return source[startIndex..endIndex];
    }
}