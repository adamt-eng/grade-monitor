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
        AppConfiguration? config;

        if (!File.Exists(AppPaths.Config))
        {
            config = ConfigurationBootstrap.AskUserForConfiguration();
            Save(config);
            return config;
        }

        var json = File.ReadAllText(AppPaths.Config);

        config = JsonConvert.DeserializeObject<AppConfiguration>(json, JsonSettings);

        return config ?? throw new JsonException("Deserialized configuration is null.");
    }

    internal static void Save(AppConfiguration appConfiguration) => File.WriteAllText(AppPaths.Config, JsonConvert.SerializeObject(appConfiguration, JsonSettings));
}
