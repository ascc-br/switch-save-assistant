namespace SwitchSaveAssistant.Core.Models;

public sealed record ShellTreeDropProbeResult(
    string RemoteDirectory,
    int FileCount,
    ulong TotalBytes,
    uint DropEffect,
    bool CleanupConfirmed,
    IReadOnlyList<ManifestEntry> Files
);
