using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Cookie = OpenQA.Selenium.Cookie;

namespace Grade_Monitor.Helpers;

internal class HttpHelper
{
    private const string BaseUrl = "https://eng.asu.edu.eg";

    private readonly CookieContainer _cookieContainer = new();
    private readonly HttpClient _httpClient;

    internal HttpHelper()
    {
        _httpClient = new HttpClient(new HttpClientHandler { UseProxy = false, CookieContainer = _cookieContainer });
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
    }

    internal async Task<string> FetchPage(string url, ulong discordUserId)
    {
        try
        {
            LoggingService.WriteLog($"{discordUserId}: {url}", ConsoleColor.DarkGreen);
            using var response = await _httpClient.GetAsync(url);
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception)
        {
            LoggingService.WriteLog($"{discordUserId}: Exception (FetchPage - HttpClient)", ConsoleColor.Red);
            throw;
        }
    }

    internal async Task<byte[]> FetchBytes(string url, ulong discordUserId)
    {
        try
        {
            LoggingService.WriteLog($"{discordUserId}: {url}", ConsoleColor.DarkGreen);
            return await _httpClient.GetByteArrayAsync(url);
        }
        catch (Exception)
        {
            LoggingService.WriteLog($"{discordUserId}: Exception (FetchBytes - HttpClient)", ConsoleColor.Red);
            throw;
        }
    }

    internal async Task<string> PostForm(string url, IEnumerable<KeyValuePair<string, string>> fields, ulong discordUserId, string? referer = null)
    {
        try
        {
            LoggingService.WriteLog($"{discordUserId}: POST {url}", ConsoleColor.DarkGreen);
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new FormUrlEncodedContent(fields);
            if (referer != null)
                request.Headers.Referrer = new Uri(referer);

            using var response = await _httpClient.SendAsync(request);
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception)
        {
            LoggingService.WriteLog($"{discordUserId}: Exception (PostForm - HttpClient)", ConsoleColor.Red);
            throw;
        }
    }

    internal string? GetCookieValue(string name)
    {
        foreach (System.Net.Cookie cookie in _cookieContainer.GetCookies(new Uri(BaseUrl)))
            if (cookie.Name == name)
                return cookie.Value;

        return null;
    }

    internal void SetCookie(string uri, string cookieHeader) => _cookieContainer.SetCookies(new Uri(uri), cookieHeader);

    internal void SetCookies(ReadOnlyCollection<Cookie> cookies)
    {
        foreach (var cookie in cookies)
        {
            var cookieName = cookie.Name;
            if (cookieName == "XSRF-TOKEN")
            {
                continue;
            }

            SetCookie("https://eng.asu.edu.eg", $"{cookieName}={cookie.Value}");
        }
    }
}