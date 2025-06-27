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

internal partial class Session(User user)
{
    [GeneratedRegex(@"\b(Fall|Spring|Summer) \d{4}\b")] private static partial Regex SemesterRegex(); // Regular expression to identify semester names

    internal int Timer;

    internal User User = user;

    internal string Cgpa; // User's CGPA, fetched from the dashboard
    internal string RequestedSemester; // The semester the user has selected

    private string _studentCourses; // The 'Student Courses' page source
    private string _currentSemester; // The name of the current semester

    internal int Fails; // Fail counter

    internal bool HeavyLoad; // Indicates load type to determine if it will fetch detailed grades or only final grades for faster retrieval
    private bool _checkedFinalGrades; // Indicates whether we have checked for the final grades or not

    private readonly CookieContainer _cookieContainer = new(); // Use CookieContainer to avoid repeated login requests 
    private HttpClient _httpClient;

    internal async Task<bool> Login()
    {
        // Initialize _httpClient if it didn't get initialized before
        _httpClient ??= new HttpClient(new HttpClientHandler { UseProxy = false, CookieContainer = _cookieContainer });
        
        // Attempt to visit dashboard
        var html = await HttpHelper.FetchPage("https://eng.asu.edu.eg/dashboard", _httpClient, User.DiscordUserId).ConfigureAwait(false);

        // If my_courses is accessible; user is already logged in
        if (html.Contains("my_courses"))
        {
            Program.WriteLog($"{User.DiscordUserId}: Stored session found, reusing cookies.", ConsoleColor.Magenta);
        }
        else
        {
            Program.WriteLog($"{User.DiscordUserId}: No stored session, initiating login.", ConsoleColor.Red);

            var isAlternateVersion = html.Contains("email1");
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "_token", html.ExtractBetween("token\" content=\"", "\"", lastIndexOf: false) },
                { isAlternateVersion ? "email1" : "email", $"{User.StudentId}@eng.asu.edu.eg" },
                { isAlternateVersion ? "password1" : "password", User.Password }
            });

            using var response = await _httpClient.PostAsync(isAlternateVersion ? "https://eng.asu.edu.eg/log1n" : "https://eng.asu.edu.eg/login", content).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Faculty server is currently down.");
            }

            html = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            // If my_courses is still not accessible
            if (!html.Contains("my_courses"))
            {
                // Filling questionnaire is required.
                if (html.Contains("Questionnaire", StringComparison.OrdinalIgnoreCase))
                {
                    // Prompts user to visit faculty site to fill the questionnaire.
                    throw new Exception("Unable to fetch grades: A mandatory questionnaire must be completed first. Please visit the faculty website to fill it out and try again.");
                }

                // Incorrect email, password, or solving CAPTCHA is required.
                if (html.Contains("alert alert-danger"))
                {
                    // Incorrect email or password.
                    if (!html.Contains("recaptcha"))
                    {
                        // Incorrect email or password.
                        throw new Exception("Incorrect Student ID or password. Please check your credentials and try again.");
                    }

                    // Solving CAPTCHA is required.
                    Program.WriteLog($"{User.DiscordUserId}: CAPTCHA required; attempting login with 'laravel_session' cookie.", ConsoleColor.Yellow);

                    if (User.LaravelSession == null)
                    {
                        // Prompts user to use the get-grades-using-session-cookie
                        throw new Exception("Login attempt using email and password has failed due to a CAPTCHA. Please use the `/get-grades-using-session-cookie` command.");
                    }

                    _cookieContainer.SetCookies(new Uri("https://eng.asu.edu.eg"), $"laravel_session={User.LaravelSession}");
                    _cookieContainer.SetCookies(new Uri("https://eng.asu.edu.eg"), $"asueng_web={User.LaravelSession}");

                    html = await HttpHelper.FetchPage("https://eng.asu.edu.eg/dashboard", _httpClient, User.DiscordUserId);

                    // If my_courses is still not accessible
                    if (!html.Contains("my_courses"))
                    {
                        // Filling questionnaire is required.
                        if (html.Contains("Questionnaire", StringComparison.OrdinalIgnoreCase))
                        {
                            // Prompts user to visit faculty site to fill the questionnaire.
                            throw new Exception("Unable to fetch grades: A mandatory questionnaire must be completed first. Please visit the faculty website to fill it out and try again.");
                        }

                        throw new Exception("The 'laravel_session' cookie has expired. Please update it's value using the `/get-grades-using-session-cookie` command.");
                    }
                }
            }

            Program.WriteLog($"{User.DiscordUserId}: Logged in successfully.", ConsoleColor.Magenta);
        }

        // Extract CGPA from dashboard
        Cgpa = html.ExtractBetween("\"text-white\">", "<", lastIndexOf: false);
        return true;
    }

    internal async Task InitializeMembers(IUserMessage message)
    {
        _studentCourses = await HttpHelper.FetchPage("https://eng.asu.edu.eg/study/studies/student_courses", _httpClient, User.DiscordUserId).ConfigureAwait(false);

        // Filling questionnaire is required.
        if (_studentCourses.Contains("Questionnaire", StringComparison.OrdinalIgnoreCase))
        {
            // Prompts user to visit faculty site to fill the questionnaire.
            throw new Exception("Unable to fetch grades: A mandatory questionnaire must be completed first. Please visit the faculty website to fill it out and try again.");
        }

        _currentSemester = _studentCourses.ExtractBetween("<strong>Term</strong>: ", "<", lastIndexOf: false).Trim();

        if (User.StudentId == null)
        {
            User.StudentId = _studentCourses.ExtractBetween("<strong>", "</strong>", lastIndexOf: false).Trim();

            // Update config.json
            Program.ConfigurationManager.Save(Program.Configuration);
        }

        // Store the name of each semester the student took a course during
        foreach (Match semester in SemesterRegex().Matches(_studentCourses))
        {
            var semesterName = semester.Value;

            // Add semester to user's semesters
            if (!User.Semesters.ContainsKey(semesterName))
            {
                User.Semesters[semesterName] = [];
            }
        }

        // If semester not specified; it is only specified when it's changed with select-load option
        // Otherwise it's fetched with the following:
        if (RequestedSemester == null)
        {
            // If no message found, set the requested semester to the current semester
            if (message == null)
            {
                RequestedSemester = _currentSemester;
            }
            else
            {
                // Else set it to the semester selected by the user on the message

                var actionRows = message.Components.OfType<ActionRowComponent>();
                var selectMenus = actionRows.SelectMany(row => row.Components.OfType<SelectMenuComponent>()).ToList();

                RequestedSemester = selectMenus[0].Options.First(option => option.IsDefault == true).Value;
            }
        }
    }

    internal async Task<SortedDictionary<string, string>> FetchGradesReport()
    {
        if (HeavyLoad && !_checkedFinalGrades)
        {
            return await FetchOnlyFinalGradesAsync().ConfigureAwait(false);
        }
        else
        {
            _checkedFinalGrades = false;
        }

        try
        {
            // Check first if the semester's courses were already saved before
            // If user has withdrawn/dropped/added courses, they must use the slash command again
            var courses = new Dictionary<string, string>();
            var refreshRequired = false;
            foreach (var semester in User.Semesters)
            {
                if (semester.Key == RequestedSemester)
                {
                    if (semester.Value.Count == 0) break;

                    foreach (var course in semester.Value)
                    {
                        if (!Program.Configuration.Courses.TryGetValue(course, out var result))
                        {
                            refreshRequired = true;
                            break;
                        }

                        courses[course] = result;
                    }

                    if (refreshRequired) break;
                }
            }

            // If no courses stored, fetch them
            if (courses.Count == 0 || refreshRequired)
            {
                await RefreshCoursesUrls(courses).ConfigureAwait(false);
            }

            var grades = new SortedDictionary<string, string>();

            await Task.WhenAll(courses.Select(Selector)).ConfigureAwait(false);

            return grades;

            async Task Selector(KeyValuePair<string, string> course)
            {
                var gradeDetails = new List<string>();

                var htmlLines = (await HttpHelper.FetchPage(course.Value, _httpClient, User.DiscordUserId).ConfigureAwait(false)).Split('\n');
                var filteredHtmlLines = htmlLines.Where(line => line.Contains(Constants.GradeIdentifier1) || line.Contains(Constants.GradeIdentifier2)).ToList();

                // This case can occur when the link for the course is updated and the saved one now redirects
                // to an error page that does not contain the keywords used for identifying the grades
                // If this occurs, it will refetch all the courses urls using RefreshUrls() to get the updated links
                if (filteredHtmlLines.Count == 0)
                {
                    await RefreshCoursesUrls(courses).ConfigureAwait(false);
                    htmlLines = (await HttpHelper.FetchPage(courses[course.Key], _httpClient, User.DiscordUserId).ConfigureAwait(false)).Split('\n');
                    filteredHtmlLines = htmlLines.Where(line => line.Contains(Constants.GradeIdentifier1) || line.Contains(Constants.GradeIdentifier2)).ToList();
                }

                for (var i = 0; i < filteredHtmlLines.Count; ++i)
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

                        ++i;

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

        return await FetchOnlyFinalGradesAsync().ConfigureAwait(false);
    }

    private async Task<SortedDictionary<string, string>> FetchOnlyFinalGradesAsync()
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

                i += 40; // The distance between each line we need to read is 40 lines
            }
            else
            {
                ++i;
            }
        }

        switch (grades.Count)
        {
            case 0:
                _checkedFinalGrades = true;
                return await FetchGradesReport().ConfigureAwait(false);
            default:
                return grades;
        }
    }

    private async Task RefreshCoursesUrls(Dictionary<string, string> courses)
    {
        courses.Clear();

        // The current semester's courses' URLs are fetched from the my_courses page, this is because they are loaded with AJAX
        // and therefore not instantly available on the student_courses page as HttpClient can not execute client scripts
        if (_currentSemester == RequestedSemester)
        {
            // Read my_courses page HTML and transform the page into an array with each line as an element
            var myCourses = (await HttpHelper.FetchPage("https://eng.asu.edu.eg/dashboard/my_courses", _httpClient, User.DiscordUserId).ConfigureAwait(false)).Split('\n');

            // Filter the array to only contain the relevant courses
            myCourses = myCourses.Where(line => line.Contains(_currentSemester)).ToArray();

            // Get the course's name and url and add it to the configuration
            foreach (var line in myCourses)
            {
                var courseName = line.ExtractBetween(">", " (");
                var courseUrl = line.ExtractBetween("\"", "?");

                AddCourseToConfiguration(_currentSemester, courseName, courseUrl);

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

                    AddCourseToConfiguration(courseSemester, courseName, courseUrl);

                    // The program gets the grades for all the courses in the courses dictionary
                    // And so we have to do the following condition to make sure only the requested ones are fetched
                    if (courseSemester == RequestedSemester)
                    {
                        courses[courseName] = courseUrl;
                    }

                    i += 40; // The distance between each line we need to read is 40 lines
                }
                else
                {
                    ++i;
                }
            }
        }

        void AddCourseToConfiguration(string courseSemester, string courseName, string courseUrl)
        {
            // Add course's name to the specified semester in the user's data
            User.Semesters[courseSemester].Add(courseName);
            
            // Add the course and it's URL to the Courses dictionary
            Program.Configuration.Courses[courseName] = courseUrl;
        }

        // Update config.json
        Program.ConfigurationManager.Save(Program.Configuration);
    }
}