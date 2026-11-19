namespace SwitchSaveAssistant.Core.Models;

public sealed record BackupResult(
    string DestinationPath,
    string PayloadPath,
    string ManifestPath,
    int FileCount,
    ulong TotalBytes,
    IReadOnlyList<ManifestEntry> Files
);
