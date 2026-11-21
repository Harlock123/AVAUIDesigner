using System.Runtime.CompilerServices;
using AVAUIDesigner.Shell.Agents;

namespace AVAUIDesigner.Shell.Tests;

/// <summary>
/// Before any test runs: the AI side's settings, keys, token, connections and saved chats go to a temporary folder,
/// so no test reads or writes the person's own (and none ever finds a real API key).
/// </summary>
internal static class SettingsIsolation
{
    [ModuleInitializer]
    internal static void Isolate() =>
        AgentSettings.FolderOverride = Directory.CreateTempSubdirectory("avauidesigner-agents-").FullName;
}
