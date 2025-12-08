using System.Text.RegularExpressions;

namespace Grade_Monitor.Helpers;

internal static partial class RegexHelper
{
    [GeneratedRegex(@"\b(Fall|Spring|Summer) \d{4}\b")]
    internal static partial Regex SemesterNamePattern();
}