using Grade_Monitor.Core;
using Grade_Monitor.Models;
using Newtonsoft.Json;
using System.IO;

namespace Grade_Monitor.Configuration;

internal static class ConfigurationManager
{
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Formatting = Formatting.Indented
    };

    internal static AppConfiguration Load()
    {
        // Start from an empty configuration when no file exists yet. The active frontend prompts for
        // whatever it needs (a bot token for Discord, or student credentials for terminal) and saves.
        if (!File.Exists(AppPaths.Config))
            return new AppConfiguration();

        var json = File.ReadAllText(AppPaths.Config);

        var config = JsonConvert.DeserializeObject<AppConfiguration>(json, JsonSettings);

        return config ?? throw new JsonException("Deserialized configuration is null.");
    }

    internal static void Save(AppConfiguration appConfiguration) => File.WriteAllText(AppPaths.Config, JsonConvert.SerializeObject(appConfiguration, JsonSettings));
}
