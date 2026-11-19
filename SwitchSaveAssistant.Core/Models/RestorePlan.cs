namespace SwitchSaveAssistant.Core.Models;

public sealed record RestorePlan(
    string Category,
    string GameName,
    string EntryName,
    SaveEntryType EntryType,
    string LocalPayloadPath,
    string LocalManifestPath,
    string SafetyBackupPath,
    string SafetyBackupManifestPath,
    DateTimeOffset PreparedAtUtc,
    IReadOnlyList<SyncFileState> ExpectedSwitchFiles
);
