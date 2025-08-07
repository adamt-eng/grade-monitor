using Grade_Monitor.Core;
using System;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Grade_Monitor.Utilities;

internal class HttpHelper
{
    private readonly CookieContainer _cookieContainer = new();
    private readonly HttpClient _httpClient;
    
    internal HttpHelper() => _httpClient = new HttpClient(new HttpClientHandler { UseProxy = false, CookieContainer = _cookieContainer });

    internal async Task<string> FetchPage(string url, ulong discordUserId)
    {
        try
        {
            Program.WriteLog($"{discordUserId}: {url}", ConsoleColor.DarkGreen);
            using var response = await _httpClient.GetAsync(url).ConfigureAwait(false);
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            Program.WriteLog($"{discordUserId}: Exception (FetchPage - HttpClient)", ConsoleColor.Red);
            throw;
        }
    }

    internal void SetCookie(string uri, string cookieHeader) => _cookieContainer.SetCookies(new Uri(uri), cookieHeader);

    internal void SetCookies(ReadOnlyCollection<OpenQA.Selenium.Cookie> cookies)
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