using Grade_Monitor.Configuration;
using Grade_Monitor.Core.Session;
using Grade_Monitor.Helpers;
using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Grade_Monitor.Core.Services;

internal sealed class AuthService
{
    private static readonly object EmptyBody = new();

    private readonly ApiClient _api;

    internal AuthService(ApiClient api) => _api = api;

    internal Task<JsonNode> GetDataAsync(SessionState state, string path) =>
        AuthorizedAsync(state, token => _api.GetAsync(path, token, state.User.DiscordUserId));

    internal Task<JsonNode> PostDataAsync(SessionState state, string path) =>
        AuthorizedAsync(state, token => _api.PostAsync(path, EmptyBody, token, state.User.DiscordUserId));

    private async Task<JsonNode> AuthorizedAsync(SessionState state, Func<string, Task<JsonNode>> send)
    {
        if (string.IsNullOrEmpty(state.User.AccessToken))
            await LoginAsync(state);

        var root = await send(state.User.AccessToken!);
        if (IsExpired(root))
        {
            await LoginAsync(state);
            root = await send(state.User.AccessToken!);
        }

        return Unwrap(root);
    }

    private async Task LoginAsync(SessionState state)
    {
        var body = new { email = $"{state.User.StudentId}@eng.asu.edu.eg", password = state.User.Password };
        var root = await _api.PostAsync("login", body, null, state.User.DiscordUserId);

        var token = Unwrap(root)["security"]?["accessToken"]?.GetValue<string>();
        if (string.IsNullOrEmpty(token))
            throw new Exception("Login failed: no access token returned.");

        state.User.AccessToken = token;
        ConfigurationManager.Save(App.Config);
    }

    private static bool IsExpired(JsonNode root) => root["code"]?.GetValue<int>() == 401;

    private static JsonNode Unwrap(JsonNode root)
    {
        if (root["code"]?.GetValue<int>() == 200 && root["data"] is { } data)
            return data;

        var message = root["message"]?.GetValue<string>();
        var error = (root["error"] as JsonArray)?.FirstOrDefault()?["error"]?.GetValue<string>();
        throw new Exception($"API error: {message ?? error ?? "request failed"}");
    }
}
