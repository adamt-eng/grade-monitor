using Grade_Monitor.Configuration;
using Grade_Monitor.Models;

namespace Grade_Monitor.Core;

/// <summary>How the application is running.</summary>
internal enum RunMode
{
    Terminal,
    Discord
}

/// <summary>
/// Shared application state, independent of the active frontend (Discord or terminal).
/// Holds the single in-memory configuration instance both frontends read and persist.
/// </summary>
internal static class App
{
    internal static AppConfiguration Config { get; set; } = ConfigurationManager.Load();
}
