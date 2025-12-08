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

    private static readonly string ConfigFileName = "config.json";

    internal static AppConfiguration Load()
    {
        AppConfiguration? config;

        if (!File.Exists(ConfigFileName))
        {
            config = ConfigurationBootstrap.AskUserForConfiguration();
            Save(config);
            return config;
        }

        var json = File.ReadAllText(ConfigFileName);

        config = JsonConvert.DeserializeObject<AppConfiguration>(json, JsonSettings);

        return config ?? throw new JsonException("Deserialized configuration is null.");
    }

    internal static void Save(AppConfiguration appConfiguration) => File.WriteAllText(ConfigFileName, JsonConvert.SerializeObject(appConfiguration, JsonSettings));
}
