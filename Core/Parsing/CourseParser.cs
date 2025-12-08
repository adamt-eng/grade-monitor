using Grade_Monitor.Helpers;
using Grade_Monitor.Models;
using System;
using System.Collections.Generic;

namespace Grade_Monitor.Core.Parsing;

internal static class CourseParser
{
    internal static IEnumerable<Course> Parse(string studentCoursesHtml)
    {
        var lines = studentCoursesHtml.Split(Environment.NewLine);
        var i = 0;

        while (i < lines.Length)
        {
            if (lines[i].Contains("\"https://eng.asu.edu.eg/dashboard/"))
            {
                var url = lines[i].ExtractBetween("\"", "\"");

                i += 24;

                var code = lines[i].ExtractBetween(">", "<");
                var name = lines[i + 1].ExtractBetween(">", "<");
                var semester = lines[i + 2].ExtractBetween(">", "<").Trim();
                var grade = lines[i + 3].ExtractBetween(">", "<");

                yield return new Course($"{code}: {name}", semester, url, grade);

                i += 16;
            }
            else
            {
                i++;
            }
        }
    }
}
