using System;
using System.Net.Http;
using System.Threading.Tasks;
using Grade_Monitor.Core;

namespace Grade_Monitor.Utilities;

internal static class HttpHelper
{
    internal static async Task<string> FetchPage(string url, HttpClient httpClient)
    {
        try
        {
            Program.WriteLog(url, ConsoleColor.DarkGreen);
            using var response = await httpClient.GetAsync(url).ConfigureAwait(false);
            return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync().ConfigureAwait(false) : throw new Exception(response.ReasonPhrase);
        }
        catch (Exception)
        {
            Program.WriteLog("Exception (FetchPage)", ConsoleColor.Red);
            throw;
        }
    }
}