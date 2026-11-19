using Microsoft.Win32;
using SwitchSaveAssistant.Core.Updates;
using System.IO;
using System.Net.Http;
using System.Reflection;

namespace SwitchSaveAssistant.App.Updates;

internal static class AppUpdates
{
    private static readonly HttpClient Client = new() { Timeout = Timeout.InfiniteTimeSpan };
    internal static GitHubUpdateService Service { get; } = new(Client);
    internal static AvailableUpdate? Latest { get; set; }
    internal static string CurrentVersion => typeof(App).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";

    internal static bool IsInstalled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{4DFD14CF-0D06-4B90-A795-598D698B9824}_is1");
                return key?.GetValue("InstallLocation") is string location &&
                    string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(location)),
                        Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory)), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception) { return false; }
        }
    }

    internal static UpdatePackageKind PackageKind => IsInstalled ? UpdatePackageKind.Installer : UpdatePackageKind.Portable;
}
