using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Grade_Monitor.Helpers;

internal sealed class ApiClient
{
    private const string BaseUrl = "https://eng.asu.edu.eg/api/";

    private static readonly HttpClient Http = new(new HttpClientHandler { UseProxy = false })
    {
        BaseAddress = new Uri(BaseUrl)
    };

    internal Task<JsonNode> GetAsync(string path, string? token, ulong discordUserId) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Get, path), token, discordUserId);

    internal Task<JsonNode> PostAsync(string path, object body, string? token, ulong discordUserId) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) }, token, discordUserId);

    private static async Task<JsonNode> SendAsync(HttpRequestMessage request, string? token, ulong discordUserId)
    {
        try
        {
            request.Headers.Add("Accept-Language", "en");
            if (!string.IsNullOrEmpty(token))
                request.Headers.Add("Authorization", $"Bearer {token}");

            LoggingService.WriteLog($"{discordUserId}: {request.Method} {request.RequestUri}", ConsoleColor.DarkGreen);

            using var response = await Http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            return JsonNode.Parse(json) ?? throw new JsonException("Empty API response.");
        }
        catch (Exception)
        {
            LoggingService.WriteLog($"{discordUserId}: Exception (ApiClient)", ConsoleColor.Red);
            throw;
        }
        finally
        {
            request.Dispose();
        }
    }
}
