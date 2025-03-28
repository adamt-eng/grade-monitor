using System;
using System.IO;
using Grade_Monitor.Core;
using Newtonsoft.Json;

namespace Grade_Monitor.Configuration;

public class ConfigurationManager(string configFilePath)
{
    private readonly JsonSerializerSettings _jsonSettings = new() { Formatting = Formatting.Indented };
    public Configuration Load()
    {
        if (!File.Exists(configFilePath))
        {
            Program.WriteLog("Please enter your Discord bot's authorization token: ", ConsoleColor.Yellow);
            Save(new Configuration { BotToken = Console.ReadLine() });
            Console.Clear();
        }

        return JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(configFilePath), _jsonSettings);
    }
    public void Save(Configuration configuration)
    {
        File.WriteAllText(configFilePath, JsonConvert.SerializeObject(configuration, _jsonSettings));
    }
}