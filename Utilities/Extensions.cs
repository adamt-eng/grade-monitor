using System;

namespace Grade_Monitor.Utilities;

internal static class Extensions
{
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