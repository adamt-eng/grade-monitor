using Discord;
using Grade_Monitor.Configuration;
using Grade_Monitor.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Grade_Monitor.Core;

internal partial class Session
{
    private record Course(string FullName, string Semester, string Url, string Grade);

    [GeneratedRegex(@"\b(Fall|Spring|Summer) \d{4}\b")] private static partial Regex SemesterRegex(); // Regular expression to identify semester names

    internal int Timer; // Timestamp at which this user's grades will be refreshed

    internal User User; // The user associated with this session

    internal string Cgpa; // User's CGPA, fetched from the dashboard
    internal string RequestedSemester; // The semester the user has selected

    private string _studentCourses; // The 'Student Courses' page source

    internal int Fails; // Fail counter

    internal bool FetchFinalGrades;
    private bool _fetchedFinalGrades;

    private readonly HttpHelper _httpHelper;

    // Lazily gets the current semester name from _studentCourses.
    // The value is extracted only on first access and then cached in _currentSemester.
    private string _currentSemester;
    private string CurrentSemester => _currentSemester ??= _studentCourses.ExtractBetween("<strong>Term</strong>: ", "<", lastIndexOf: false).Trim();

    internal Session(User user)
    {
        User = user;
        _httpHelper = new HttpHelper();
    }

    internal async Task<bool> Login()
    {
        if (User.LaravelSession != null)
        {
            _httpHelper.SetCookie("https://eng.asu.edu.eg", $"laravel_session={User.LaravelSession}");
            _httpHelper.SetCookie("https://eng.asu.edu.eg", $"asueng_web={User.LaravelSession}");
        }

        var html = await _httpHelper.FetchPage("https://eng.asu.edu.eg/dashboard", User.DiscordUserId).ConfigureAwait(false);
        if (html.Contains("my_courses"))
        {
            Program.WriteLog($"{User.DiscordUserId}: Stored session found, reusing cookies.", ConsoleColor.Magenta);
        }
        else
        {
            // Filling questionnaire is required.
            if (html.Contains("Questionnaire", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("Unable to fetch grades: A mandatory questionnaire must be completed first. Please visit the faculty website to fill it out and try again.");
            }

            Program.WriteLog($"{User.DiscordUserId}: No stored session, initiating login.", ConsoleColor.Red);

            var pageName = "login";
            var emailField = "email";
            var passwordField = "password";

            // If the page source contains "email1" that indicates it's the alternate version
            // of the login page which has slightly different names
            if (html.Contains("email1"))
            {
                pageName = "log1n";
                emailField = "email1";
                passwordField = "password1";
            }

            html = await SeleniumHelper.FetchPage($"https://eng.asu.edu.eg/{pageName}", User.DiscordUserId);

            SeleniumHelper.FillTextField(emailField, $"{User.StudentId}@eng.asu.edu.eg");
            SeleniumHelper.FillTextField(passwordField, User.Password);

            // If CAPTCHA is detected, solve it before submitting
            if (html.Contains("recaptcha"))
            {
                Program.WriteLog($"{User.DiscordUserId}: CAPTCHA detected. Solving...", ConsoleColor.Yellow);

                CaptchaSolver.SolveRecaptcha(pageName);
            }

            // Submit the login form
            html = SeleniumHelper.SubmitLoginForm();

            // If my_courses is still not accessible
            if (!html.Contains("my_courses"))
            {
                // Filling questionnaire is required.
                if (html.Contains("Questionnaire", StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception("Unable to fetch grades: A mandatory questionnaire must be completed first. Please visit the faculty website to fill it out and try again.");
                }

                throw new Exception("Login failed: CAPTCHA might be invalid or credentials are incorrect.");
            }

            // Get cookies generated from the successful login
            var cookies = SeleniumHelper.GetCookies();

            // Save the cookies to _httpHelper, as HttpClient will be used for the rest of the fetching grades process
            // As Selenium is much slower and is not required for it
            _httpHelper.SetCookies(cookies);

            // The faculty uses asueng_web as an alternative name to the laravel_session cookie
            var laravelSession = cookies.First(cookie => cookie.Name is "asueng_web" or "laravel_session").Value;
            Program.Configuration.Users.First(user => user.DiscordUserId == User.DiscordUserId).LaravelSession = laravelSession;
            Program.ConfigurationManager.Save(Program.Configuration);
        }

        // Extract cumulative GPA
        Cgpa = html.ExtractBetween("\"text-white\">", "<", lastIndexOf: false);

        Program.WriteLog($"{User.DiscordUserId}: Logged in successfully.", ConsoleColor.Magenta);

        return true;
    }

    internal async Task LoadStudentData()
    {
        await LoadStudentCourses().ConfigureAwait(false);

        ExtractAndStoreUserSemesters();
    }

    internal async Task<SortedDictionary<string, string>> FetchGradesReport()
    {
        if (FetchFinalGrades && !_fetchedFinalGrades)
        {
            return await FetchFinalGradesAsync().ConfigureAwait(false);
        }

        // Set _fetchedFinalGrades to false to allow an attempt
        // to FetchOnlyFinalGradesAsync() again the next time user refreshes if FetchFinalGrades remains true
        _fetchedFinalGrades = false;

        // Check first if the semester's courses were already saved before
        var courses = new Dictionary<string, string>();
        var refreshRequired = false;

        // Loops on each semester stored for the user in the configuration file
        foreach (var semester in User.Semesters)
        {
            if (semester.Key == RequestedSemester)
            {
                // If semester has no courses, break
                if (semester.Value.Count == 0)
                {
                    break;
                }

                // Check if all courses are stored in configuration
                // If one course is not, set refreshRequired to true to
                // force a refresh of all course data for the requested semester
                foreach (var course in semester.Value)
                {
                    if (!Program.Configuration.Courses.TryGetValue(course, out var courseUrl))
                    {
                        refreshRequired = true;
                        break;
                    }

                    courses[course] = courseUrl;
                }

                if (refreshRequired)
                {
                    break;
                }
            }
        }

        // If no courses stored in configuration or if refreshRequired is true
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

            var htmlLines = (await _httpHelper.FetchPage(course.Value, User.DiscordUserId).ConfigureAwait(false)).Split('\n');
            var filteredHtmlLines = htmlLines.Where(ContainsGradeIdentifier).ToList();

            // This case can occur when the link for the course is updated and the saved one now redirects
            // to an error page that does not contain the keywords used for identifying the grades
            // If this occurs, it will refetch all course urls
            if (filteredHtmlLines.Count == 0)
            {
                await RefreshCoursesUrls(courses).ConfigureAwait(false);
                htmlLines = (await _httpHelper.FetchPage(courses[course.Key], User.DiscordUserId).ConfigureAwait(false)).Split('\n');
                filteredHtmlLines = [.. htmlLines.Where(ContainsGradeIdentifier)];
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

            grades[course.Key] = string.Join(Environment.NewLine, gradeDetails).Trim();
        }

        static bool ContainsGradeIdentifier(string s) => s.Contains("text-align: right;") || s.Contains("7 col");
    }

    private async Task<SortedDictionary<string, string>> FetchFinalGradesAsync()
    {
        var grades = new SortedDictionary<string, string>();

        foreach (var course in ParseStudentCourses().Where(course => course.Semester == RequestedSemester && !string.IsNullOrEmpty(course.Grade)))
        {
            grades[course.FullName] = $"||**__Course Grade: {course.Grade}__**||";
        }

        // If the count is 0, it indicates that final grades are not released yet for the requested semester
        // So instead, fetch all grades for the requested semester
        // Set _fetchedFinalGradesto true to avoid infinite calls to this function
        if (grades.Count == 0)
        {
            _fetchedFinalGrades = true;
            return await FetchGradesReport().ConfigureAwait(false);
        }

        return grades;
    }

    private async Task RefreshCoursesUrls(Dictionary<string, string> courses)
    {
        courses.Clear();

        // The current semester's course urls are fetched from the my_courses page
        // They are available on student_courses too, but they are loaded with AJAX which will require Selenium which is much slower than HttpClient
        if (CurrentSemester == RequestedSemester)
        {
            // Read my_courses page HTML and transform the page into an array with each line as an element
            var myCourses = (await _httpHelper.FetchPage("https://eng.asu.edu.eg/dashboard/my_courses", User.DiscordUserId).ConfigureAwait(false)).Split('\n');

            // Filter the array to only contain the relevant courses
            myCourses = [.. myCourses.Where(line => line.Contains(CurrentSemester))];

            // Add course data to the configuration file
            foreach (var line in myCourses)
            {
                var courseName = line.ExtractBetween(">", " (");
                var courseUrl = line.ExtractBetween("\"", "?");

                AddCourseToConfiguration(CurrentSemester, courseName, courseUrl);

                // Also add it to the courses Dictionary as it will be used in FetchGradesReport()
                courses[courseName] = courseUrl;
            }
        }
        else
        {
            foreach (var (courseName, semester, url, _) in ParseStudentCourses().Where(course => course.Semester == RequestedSemester))
            {
                AddCourseToConfiguration(semester, courseName, url);
                courses[courseName] = url;
            }
        }

        void AddCourseToConfiguration(string courseSemester, string courseName, string courseUrl)
        {
            // Add course name to the specified semester in the user's data
            User.Semesters[courseSemester].Add(courseName);
            
            // Add the course and it's URL to the global Courses dictionary
            Program.Configuration.Courses[courseName] = courseUrl;
        }

        // Update config.json
        Program.ConfigurationManager.Save(Program.Configuration);
    }

    private IEnumerable<Course> ParseStudentCourses()
    {
        // This function is extremely fragile and prone to bugs if even one line in the student_courses HTML page source changes
        // The alternative is to use Selenium to navigate the page but that is much slower and is still fragile if anything changes on the page
        // So while this approach is not future-proof, the faculty does not update
        // the site frequently so it's the best method at the moment

        var lines = _studentCourses.Split(Environment.NewLine);
        var i = 0;

        while (i < lines.Length)
        {
            if (lines[i].Contains("\"https://eng.asu.edu.eg/dashboard/"))
            {
                var courseUrl = lines[i].ExtractBetween("\"", "\"");

                i += 24; // Jump 24 lines to reach the rest of the course data

                var courseCode = lines[i].ExtractBetween(">", "<");
                var courseName = lines[i + 1].ExtractBetween(">", "<");
                var courseSemester = lines[i + 2].ExtractBetween(">", "<").Trim();
                var courseGrade = lines[i + 3].ExtractBetween(">", "<");

                yield return new Course($"{courseCode}: {courseName}", courseSemester, courseUrl, courseGrade);

                i += 16; // Jump to the next course url
            }
            else
            {
                ++i; // Else keep increasing by 1 to find the needed lines
            }
        }
    }

    private async Task LoadStudentCourses()
    {
        // The student_courses page contains the list of all semesters including the name of the current semester
        // It also contains all previous courses and their final grades
        // It's crucial for the grade fetching process
        _studentCourses = await _httpHelper.FetchPage("https://eng.asu.edu.eg/study/studies/student_courses", User.DiscordUserId).ConfigureAwait(false);

        // Filling questionnaire is required.
        if (_studentCourses.Contains("Questionnaire", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Unable to fetch grades: A mandatory questionnaire must be completed first. Please visit the faculty website to fill it out and try again.");
        }
    }

    private void ExtractAndStoreUserSemesters()
    {
        // Store the name of each semester
        foreach (Match semester in SemesterRegex().Matches(_studentCourses))
        {
            var semesterName = semester.Value;

            // Add semester to user's semesters if not already added
            if (!User.Semesters.ContainsKey(semesterName))
            {
                User.Semesters[semesterName] = [];
            }
        }
    }

    internal void DetermineRequestedSemester(IUserMessage message)
    {
        // RequestedSemester is always == null EXCEPT for the call where
        // the user manually selected a semester using the select-semester menu
        if (RequestedSemester == null)
        {
            if (message == null)
            {
                // If no previous message found, set RequestedSemester to the current semester name
                RequestedSemester = CurrentSemester;
            }
            else
            {
                // Else set it to the semester currently selected from the select-semester menu
                var actionRows = message.Components.OfType<ActionRowComponent>();
                var selectMenus = actionRows.SelectMany(row => row.Components.OfType<SelectMenuComponent>()).ToList();
                RequestedSemester = selectMenus[0].Options.First(option => option.IsDefault == true).Value;
            }
        }
    }
}