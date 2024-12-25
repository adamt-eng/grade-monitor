using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Grade_Monitor.Configuration;
using Grade_Monitor.Utilities;

namespace Grade_Monitor.Core;

internal partial class Session(string studentId, string password)
{
    [GeneratedRegex(@"\b(Fall|Spring|Summer) \d{4}\b")] private static partial Regex SemestersRegex();

    internal string StudentId = studentId;
    internal string Cgpa;
    internal string RequestedSemester;

    private string _studentCourses;
    private string _currentSemester;

    internal HashSet<string> Semesters = [];

    internal int Fails;

    internal bool HeavyLoad;

    private static readonly CookieContainer CookieContainer = new();
    private readonly HttpClient _httpClient = new(new HttpClientHandler { UseProxy = false, CookieContainer = CookieContainer })
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

        // For each semester the student took a course during
        foreach (Match semester in SemestersRegex().Matches(_studentCourses))
        {
            // Add the semester to the configuration
            if (Semesters.Add(semester.Value) && Program.Configuration.Semesters.All(s => s.Name != semester.Value))
            {
                Program.Configuration.Semesters.Add(new Semester { Name = semester.Value, Courses = [] });
            }
        }

        // If semester not specified
        if (RequestedSemester == null)
        {
            // If no message found, set the requested semester to the current semester
            if (message == null)
            {
                RequestedSemester = _currentSemester;
            }
            else
            {
                // Else: set it to the semester selected by the user on the message

                var actionRows = message.Components.OfType<ActionRowComponent>();
                var selectMenus = actionRows.SelectMany(row => row.Components.OfType<SelectMenuComponent>()).ToList();

                RequestedSemester = selectMenus[0].Options.First(option => option.IsDefault == true).Value;
            }
        }
    }

    private async Task RefreshUrls(Dictionary<string, string> courses)
    {
        var coursesHashSetUpdated = false;
        
        if (_currentSemester == RequestedSemester)
        {
            // Read all courses
            var myCourses = await Program.FetchPage("https://eng.asu.edu.eg/dashboard/my_courses", _httpClient).ConfigureAwait(false);
            
            // For each course registered in the current semester
            // Get the course's name and url and add it to the configuration
            foreach (var line in myCourses.Split('\n').Where(line => line.Contains(_currentSemester)))
            {
                var courseName = line.ExtractBetween(">", " (");
                var courseUrl = line.ExtractBetween("\"", "\"");

                AddCourse(_currentSemester, courseName, courseUrl);

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

                    AddCourse(courseSemester, courseName, courseUrl);

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

        void AddCourse(string courseSemester, string courseName, string courseUrl)
        {
            foreach (var semester in Program.Configuration.Semesters)
            {
                if (semester.Name == courseSemester)
                {
                    // Remove course old data
                    foreach (var course in semester.Courses)
                    {
                        if (course.Name == courseName)
                        {
                            semester.Courses.Remove(course);
                            break;
                        }
                    }

                    // Add new course data
                    semester.Courses.Add(new Course { Name = courseName, Url = courseUrl });
                    coursesHashSetUpdated = true;
                    return;
                }
            }
        }

        if (coursesHashSetUpdated)
        {
            Program.ConfigurationManager.Save(Program.Configuration);
        }
    }
    internal async Task<SortedDictionary<string, string>> FetchGradesReport(bool fetchUrls = false)
    {
        if (!HeavyLoad)
        {
            try
            {
                // Check first if the semester's courses were already saved before
                // If user has withdrawn/dropped/added courses, they must use the slash command again
                var courses = new Dictionary<string, string>();
                foreach (var semester in Program.Configuration.Semesters)
                {
                    if (semester.Name == RequestedSemester)
                    {
                        foreach (var course in semester.Courses)
                        {
                            courses[course.Name] = course.Url;
                        }
                    }
                }

                // If no courses stored, fetch them
                if (courses.Count == 0)
                {
                    await RefreshUrls(courses).ConfigureAwait(false);
                }

                var grades = new SortedDictionary<string, string>();

                await Task.WhenAll(courses.Select(Selector)).ConfigureAwait(false);

                return grades;

                async Task Selector(KeyValuePair<string, string> course)
                {
                    var gradeDetails = new List<string>();

                    var htmlLines = (await Program.FetchPage(course.Value, _httpClient).ConfigureAwait(false)).Split('\n');
                    var filteredHtmlLines = htmlLines.Where(line => line.Contains(Constants.GradeIdentifier1) || line.Contains(Constants.GradeIdentifier2)).ToList();

                    // This case can occur when the link for the course is updated and the saved one now redirects
                    // to an error page that does not contain the keywords used for identifying the grades
                    // If this occurs, it will refetch all the courses urls using RefreshUrls() to get the updated links
                    if (filteredHtmlLines.Count == 0)
                    {
                        courses.Clear();
                        await RefreshUrls(courses).ConfigureAwait(false);
                        filteredHtmlLines = (await Program.FetchPage(courses[course.Key], _httpClient).ConfigureAwait(false)).Split('\n').Where(line => line.Contains(Constants.GradeIdentifier1) || line.Contains(Constants.GradeIdentifier2)).ToList();
                    }

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

}