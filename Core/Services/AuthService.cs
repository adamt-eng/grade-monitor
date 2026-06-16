using Grade_Monitor.Configuration;
using Grade_Monitor.Core.Session;
using Grade_Monitor.Discord_App;
using Grade_Monitor.Helpers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Grade_Monitor.Core.Services;

internal sealed class AuthService
{
    private const string BaseUrl = "https://eng.asu.edu.eg";

    // The login endpoint is throttled (observed X-RateLimit-Limit: 3 per email + IP),
    // so we get only a handful of real submissions before being blocked for a while.
    private const int MaxLoginSubmits = 3;

    // Fetching and reading a CAPTCHA is free and unlimited, so we keep regenerating until
    // we get a confident six-digit read before spending one of the scarce login submits.
    private const int MaxCaptchaReads = 12;

    // The faculty validates the CAPTCHA before the credentials, so this exact message means
    // our answer was wrong (regenerate + retry). Any other alert is a credential/account
    // problem where retrying only burns the limited login attempts.
    private const string CaptchaErrorText = "Server error, invalid request.";

    private readonly HttpHelper _http;

    internal AuthService(HttpHelper httpHelper)
    {
        _http = httpHelper;
    }

    internal async Task<string> LoginAndGetDashboardHtmlAsync(SessionState state)
    {
        var discordUserId = state.User.DiscordUserId;

        // Use stored cookies first
        if (!string.IsNullOrEmpty(state.User.LaravelSession))
        {
            _http.SetCookie(BaseUrl, $"laravel_session={state.User.LaravelSession}");
            _http.SetCookie(BaseUrl, $"asueng_web={state.User.LaravelSession}");

            var cached = await _http.FetchPage($"{BaseUrl}/dashboard", discordUserId);
            if (cached.Contains("my_courses"))
                return cached;

            if (cached.Contains("Questionnaire", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Unable to fetch grades: Please complete the mandatory questionnaire.");
        }

        var html = await PerformLoginAsync(state);

        // Store session token for reuse
        var session = _http.GetCookieValue("asueng_web") ?? _http.GetCookieValue("laravel_session");
        if (session != null)
        {
            state.User.LaravelSession = session;
            ConfigurationManager.Save(DiscordApp.AppConfig);
        }

        return html;
    }

    private async Task<string> PerformLoginAsync(SessionState state)
    {
        var discordUserId = state.User.DiscordUserId;

        // CSRF token (stays valid across failed attempts within the session)
        var loginPage = await _http.FetchPage($"{BaseUrl}/login", discordUserId);
        var csrfToken = loginPage.ExtractBetween("name=\"_token\" value=\"", "\"", lastIndexOf: false);

        for (var submit = 1; submit <= MaxLoginSubmits; submit++)
        {
            var (captchaToken, answer) = await SolveCaptchaAsync(discordUserId);

            var fields = new[]
            {
                new KeyValuePair<string, string>("_token", csrfToken),
                new KeyValuePair<string, string>("email", $"{state.User.StudentId}@eng.asu.edu.eg"),
                new KeyValuePair<string, string>("password", state.User.Password),
                new KeyValuePair<string, string>("captcha_token", captchaToken),
                new KeyValuePair<string, string>("captcha_answer", answer)
            };

            LoggingService.WriteLog($"{discordUserId}: Submitting login {submit}/{MaxLoginSubmits}", ConsoleColor.DarkGreen);
            var result = await _http.PostForm($"{BaseUrl}/login", fields, discordUserId, referer: $"{BaseUrl}/login");

            if (result.Contains("my_courses"))
                return result;

            if (result.Contains("Questionnaire", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Unable to fetch grades: Please complete the mandatory questionnaire.");

            var alert = ExtractAlert(result);
            if (alert == CaptchaErrorText)
            {
                LoggingService.WriteLog($"{discordUserId}: CAPTCHA rejected by server, retrying with a new one", ConsoleColor.DarkYellow);
                continue;
            }

            // Not a CAPTCHA issue - retrying won't help and would waste the limited login attempts.
            throw new Exception(alert != null
                ? $"Login failed: {alert}"
                : "Login failed: credentials incorrect.");
        }

        throw new Exception("Login failed: could not solve the CAPTCHA after multiple attempts.");
    }

    // Generates fresh CAPTCHAs (free and unlimited) until one is read confidently as six digits.
    private async Task<(string token, string answer)> SolveCaptchaAsync(ulong discordUserId)
    {
        for (var attempt = 1; attempt <= MaxCaptchaReads; attempt++)
        {
            var (token, imageUrl) = await GetCaptchaAsync(discordUserId);
            var image = await _http.FetchBytes(imageUrl, discordUserId);

            var answer = await ImageCaptchaSolver.SolveAsync(image, discordUserId);
            if (answer != null)
                return (token, answer);
        }

        throw new Exception("Unable to read a CAPTCHA after several attempts.");
    }

    private async Task<(string token, string imageUrl)> GetCaptchaAsync(ulong discordUserId)
    {
        var json = await _http.FetchPage($"{BaseUrl}/captcha/generate", discordUserId);
        var token = json.ExtractBetween("\"token\":\"", "\"", lastIndexOf: false);
        var imageUrl = json.ExtractBetween("\"image_url\":\"", "\"", lastIndexOf: false).Replace("\\/", "/");
        return (token, imageUrl);
    }

    private static string? ExtractAlert(string html)
    {
        const string marker = "alert alert-danger\" style=\"color: darkred\">";

        var start = html.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
            return null;

        start += marker.Length;
        var end = html.IndexOf("</div>", start, StringComparison.Ordinal);

        return end < 0 ? null : html[start..end].Trim();
    }
}
