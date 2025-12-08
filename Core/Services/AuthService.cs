using Grade_Monitor.Configuration;
using Grade_Monitor.Core.Session;
using Grade_Monitor.Discord_App;
using Grade_Monitor.Helpers;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Grade_Monitor.Core.Services;

internal sealed class AuthService
{
    private readonly HttpHelper _http;

    internal AuthService(HttpHelper httpHelper)
    {
        _http = httpHelper;
    }

    internal async Task<string> LoginAndGetDashboardHtmlAsync(SessionState state)
    {
        // Use stored cookies first
        if (state.User.LaravelSession != null)
        {
            _http.SetCookie("https://eng.asu.edu.eg", $"laravel_session={state.User.LaravelSession}");
            _http.SetCookie("https://eng.asu.edu.eg", $"asueng_web={state.User.LaravelSession}");
        }

        var html = await _http.FetchPage("https://eng.asu.edu.eg/dashboard", state.User.DiscordUserId);
        if (html.Contains("my_courses"))
            return html;

        if (html.Contains("Questionnaire", StringComparison.OrdinalIgnoreCase))
            throw new Exception("Unable to fetch grades: Please complete the mandatory questionnaire.");

        // Detect login variant
        var emailField = html.Contains("email1") ? "email1" : "email";
        var passwordField = html.Contains("password1") ? "password1" : "password";
        var pageName = emailField == "email1" ? "log1n" : "login";

        // Navigate to login page
        html = await SeleniumHelper.FetchPage($"https://eng.asu.edu.eg/{pageName}", state.User.DiscordUserId);

        SeleniumHelper.FillTextField(emailField, $"{state.User.StudentId}@eng.asu.edu.eg");
        SeleniumHelper.FillTextField(passwordField, state.User.Password);

        // Solve CAPTCHA if detected
        if (html.Contains("recaptcha"))
        {
            var token = await CaptchaSolver.SolveRecaptchaAsync(pageName);
            SeleniumHelper.SendRecaptchaToken(token);
        }

        // Submit login
        html = SeleniumHelper.SubmitLoginForm();
        if (!html.Contains("my_courses"))
        {
            if (html.Contains("Questionnaire", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Unable to fetch grades: Please complete the mandatory questionnaire.");

            throw new Exception("Login failed: CAPTCHA invalid or credentials incorrect.");
        }

        // Save cookies for HttpClient usage
        var cookies = SeleniumHelper.GetCookies();
        _http.SetCookies(cookies);

        // Store session token
        var laravelSession = cookies.First(c => c.Name is "asueng_web" or "laravel_session").Value;
        state.User.LaravelSession = laravelSession;

        ConfigurationManager.Save(DiscordApp.AppConfig);

        return html;
    }
}
