using System;
using System.Net.Http;
using System.Threading.Tasks;
using Grade_Monitor.Core;

namespace Grade_Monitor.Utilities;

internal static class HttpHelper
{
    internal static async Task<string> FetchPage(string url, HttpClient httpClient, ulong discordUserId)
    {
        try
        {
            Program.WriteLog($"{discordUserId}: {url}", ConsoleColor.DarkGreen);
            using var response = await httpClient.GetAsync(url).ConfigureAwait(false);
            return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync().ConfigureAwait(false) : throw new Exception(response.ReasonPhrase);
        }
        catch (Exception)
        {
            Program.WriteLog($"{discordUserId}: Exception (FetchPage)", ConsoleColor.Red);
            throw;
        }
    }
}