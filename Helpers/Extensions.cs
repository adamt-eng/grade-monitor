using System;

namespace Grade_Monitor.Helpers;

internal static class Extensions
{
    internal static string ExtractBetween(this string source, string start, string end, bool lastIndexOf = true)
    {
        int startIndex;
        int endIndex;

        if (lastIndexOf)
        {
            // Locate final end marker
            endIndex = source.LastIndexOf(end, StringComparison.Ordinal);

            // Locate start marker immediately before section ends
            startIndex = start == end ? source.LastIndexOf(start, endIndex - 1, StringComparison.Ordinal) : source.LastIndexOf(start, endIndex, StringComparison.Ordinal);
            startIndex += start.Length;
        }
        else
        {
            // First occurrence mode
            startIndex = source.IndexOf(start, StringComparison.Ordinal) + start.Length;
            endIndex = source.IndexOf(end, startIndex, StringComparison.Ordinal);
        }

        return source[startIndex..endIndex];
    }
}