using Discord;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Grade_Monitor;

internal partial class Session(string studentId, string password)
{
    [GeneratedRegex(@"\b(Fall|Spring|Summer) \d{4}\b")] private static partial Regex SemestersRegex();

    internal string StudentId = studentId;
    internal string Cgpa;
    internal string RequestedSemester;

    private string _studentCourses;
    private string _currentSemester;

    internal HashSet<string> Semesters;

    internal int Fails;

    internal bool HeavyLoad;

    private static readonly CookieContainer CookieContainer = new();
    private readonly HttpClient _httpClient = new(new HttpClientHandler { CookieContainer = CookieContainer })
    {
        DefaultRequestHeaders =
        {
            {
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36"
            }
        }
    };

    internal async Task InitializeMembers(IUserMessage message)
    {
        _studentCourses = await Program.FetchPage("https://eng.asu.edu.eg/study/studies/student_courses", _httpClient).ConfigureAwait(false);
        _currentSemester = _studentCourses.ExtractBetween("<strong>Term</strong>: ", "<", lastIndexOf: false).Trim();

        // Initialize Semesters HashSet
        Semesters = [];
        foreach (Match semester in SemestersRegex().Matches(_studentCourses))
        {
            Semesters.Add(semester.Value);
        }

        if (RequestedSemester == null && message == null)
        {
            RequestedSemester = _currentSemester;
        }

        if (message != null)
        {
            var actionRows = message.Components.OfType<ActionRowComponent>();
            var selectMenus = actionRows.SelectMany(row => row.Components.OfType<SelectMenuComponent>()).ToList();

            RequestedSemester ??= selectMenus[0].Options.First(option => option.IsDefault == true).Value;
        }
    }
    internal async Task<bool> Login()
    {
        var html = await Program.FetchPage("https://eng.asu.edu.eg/dashboard", _httpClient).ConfigureAwait(false);

        if (html.Contains("login"))
        {
            Program.WriteLog("No stored session, initiating login..", ConsoleColor.Red);

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "email", $"{StudentId}@eng.asu.edu.eg" },
                { "password", password },
                { "_token", html.ExtractBetween("token\" content=\"", "\"", lastIndexOf: false) } // Extract token
            });

            using var response = await _httpClient.PostAsync("https://eng.asu.edu.eg/login", content).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                html = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (html.Contains("alert alert-danger"))
                {
                    return false;
                }
            }
            else
            {
                throw new Exception($"Login Error: {response.ReasonPhrase}");
            }
        }
        else
        {
            Program.WriteLog("Stored session found, reusing cookies..", ConsoleColor.Magenta);
        }

        Cgpa = html.ExtractBetween("white\">", "<", lastIndexOf: false);
        return true;
    }

    internal async Task<SortedDictionary<string, string>> FetchGradesReport()
    {
        if (!HeavyLoad)
        {
            try
            {
                var courses = new Dictionary<string, string>();

                if (!File.Exists(Constants.CoursesBackup))
                {
                    var fileStream = File.Create(Constants.CoursesBackup);
                    await fileStream.DisposeAsync().ConfigureAwait(false);
                }

                var coursesBackupLines = await File.ReadAllLinesAsync(Constants.CoursesBackup).ConfigureAwait(false);
                var coursesBackup = string.Join("\n", coursesBackupLines);

                if (coursesBackup.Contains(RequestedSemester))
                {
                    foreach (var line in coursesBackupLines)
                    {
                        var data = line.Split("|");

                        var semester = data[0];
                        var courseName = data[1];
                        var courseUrl = data[2];

                        Semesters.Add(semester);

                        if (semester == RequestedSemester)
                        {
                            courses[courseName] = courseUrl;
                        }
                    }
                }
                else
                {
                    if (_currentSemester == RequestedSemester)
                    {
                        var myCourses = await Program.FetchPage("https://eng.asu.edu.eg/dashboard/my_courses", _httpClient).ConfigureAwait(false);

                        foreach (var line in myCourses.Split('\n').Where(line => line.Contains(_currentSemester)))
                        {
                            var courseName = line.ExtractBetween(">", " (");
                            var courseUrl = line.ExtractBetween("\"", "\"");

                            // Add the course to coursesBackup to avoid redundant requests in the future to the faculty server
                            await AddCourseToCoursesBackup(RequestedSemester, courseName, courseUrl).ConfigureAwait(false);

                            courses[courseName] = courseUrl;
                        }
                    }
                    else
                    {
                        var studentCourses = _studentCourses.Split("\n");

                        var i = 0;

                        while (i < studentCourses.Length)
                        {
                            var line = studentCourses[i];

                            if (line.Contains("\"https://eng.asu.edu.eg/dashboard/"))
                            {
                                var courseCode = studentCourses[i - 3].ExtractBetween(">", "<");
                                var courseName = $"{courseCode}: {line.ExtractBetween(">", "<")}";
                                var courseSemester = studentCourses[i + 6].ExtractBetween(">", "<").Trim();
                                var courseUrl = line.ExtractBetween("\"", "\"");

                                // Add the course to coursesBackup to avoid redundant requests in the future to the faculty server
                                await AddCourseToCoursesBackup(courseSemester, courseName, courseUrl).ConfigureAwait(false);

                                if (courseSemester == RequestedSemester)
                                {
                                    courses[courseName] = courseUrl;
                                }

                                i += 40; // The distance between each line we need to read is 40 lines
                            }
                            else
                            {
                                i++;
                            }
                        }
                    }
                }

                var grades = new SortedDictionary<string, string>();

                await Task.WhenAll(courses.Select(Selector)).ConfigureAwait(false);

                return grades;

                async Task AddCourseToCoursesBackup(string courseSemester, string courseName, string courseUrl)
                {
                    if (!coursesBackup.Contains(courseName))
                    {
                        await File.AppendAllTextAsync(Constants.CoursesBackup, $"{courseSemester}|{courseName}|{courseUrl}\n").ConfigureAwait(false);
                    }
                }

                async Task Selector(KeyValuePair<string, string> course)
                {
                    var gradeDetails = new List<string>();

                    var htmlLines = (await Program.FetchPage(course.Value, _httpClient).ConfigureAwait(false)).Split('\n');
                    var filteredHtmlLines = htmlLines.Where(line => line.Contains(Constants.GradeIdentifier1) || line.Contains(Constants.GradeIdentifier2)).ToList();

                    for (var i = 0; i < filteredHtmlLines.Count; i++)
                    {
                        var line = filteredHtmlLines[i];

                        // If line contains:

                        // right: this indicates that the line contains a grade value,
                        // and since we don't loop on section-specific grade values, this has to be the final course grade

                        // left: this indicates that the line contains a section-specific grade title

                        if (line.Contains("right"))
                        {
                            var courseGrade = line.ExtractBetween(">", "<").Trim();

                            gradeDetails.Add($"||**__Course Grade: {courseGrade}__**||");
                        }
                        else if (line.Contains("left"))
                        {
                            // Incrementing is needed to read the gradeValue which is in the line immediately after
                            // Incrementing is also useful here to avoid redundant loops

                            i++;

                            var gradeTitle = line.ExtractBetween(">", "<");
                            var gradeValue = filteredHtmlLines[i].ExtractBetween(">", "<").Replace(" ", string.Empty);

                            gradeDetails.Add($"{gradeTitle}: {gradeValue}");
                        }
                    }

                    grades[course.Key] = string.Join("\n", gradeDetails).Trim();
                }
            }
            catch
            {
                // FetchFinalGrades will get called
            }
        }

        return FetchFinalGrades();
    }
    private SortedDictionary<string, string> FetchFinalGrades()
    {
        var grades = new SortedDictionary<string, string>();

        var studentCourses = _studentCourses.Split("\n");

        var i = 0;

        while (i < studentCourses.Length)
        {
            if (studentCourses[i].Contains("<tr >") && studentCourses[i + 3].ExtractBetween(">", " <") == RequestedSemester)
            {
                var courseCode = studentCourses[i + 1].ExtractBetween(">", "<");
                var courseName = studentCourses[i + 2].ExtractBetween(">", "<");
                var courseGrade = studentCourses[i + 4].ExtractBetween(">", "<");

                grades[$"{courseCode}: {courseName}"] = $"||**__Course Grade: {courseGrade}__**||";

                i += 40;
            }
            else
            {
                i++;
            }
        }

        return grades;
    }
}