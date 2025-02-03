using System;
using System.Net.Http;
using System.Threading.Tasks;
using Grade_Monitor.Core;

namespace Grade_Monitor.Utilities;

internal static class HttpHelper
{
    internal static async Task<string> FetchPage(string url, HttpClient httpClient)
    {
        var retryCount = 10; // Will retry up to 10 times
        var retryDelay = 3000; // Initial delay (in ms) before trying again (increases linearily with each attempt; 3000, 6000, 9000)
        var attempt = 0;

        while (attempt < retryCount)
        {
            try
            {
                Program.WriteLog(url, ConsoleColor.DarkGreen);
                using var response = await httpClient.GetAsync(url).ConfigureAwait(false);
                return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync().ConfigureAwait(false) : throw new Exception(response.ReasonPhrase);
            }
            catch (Exception ex)
            {
                Program.WriteLog($"Exception (FetchPage): {ex.Message}", ConsoleColor.Red);

                ++attempt;

                // Once attempts limit is reached, throw the exception
                if (attempt == retryCount)
                {
                    throw new Exception($"Failed to fetch page after {attempt} attempts. Last error: {ex.Message}", ex);
                }

                await Task.Delay(retryDelay * attempt).ConfigureAwait(false);
            }
        }

        throw new Exception("Unreachable Code.");
    }
}