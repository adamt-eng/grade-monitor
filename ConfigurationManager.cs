using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.IO;

namespace Grade_Monitor;

public class ConfigurationManager(string configFilePath)
{
    private readonly JsonSerializerSettings _jsonSettings = new() { ContractResolver = new CamelCasePropertyNamesContractResolver(), Formatting = Formatting.Indented };
    public Configuration LoadSettings()
    {
        if (!File.Exists(configFilePath))
        {
            Program.WriteLog("Please enter your Discord bot's authorization token: ", ConsoleColor.Yellow);
            SaveSettings(new Configuration { BotToken = Console.ReadLine() });
            Console.Clear();
        }

        return JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(configFilePath), _jsonSettings);
    }
    public void SaveSettings(Configuration configuration)
    {
        File.WriteAllText(configFilePath, JsonConvert.SerializeObject(configuration, _jsonSettings));
    }
}