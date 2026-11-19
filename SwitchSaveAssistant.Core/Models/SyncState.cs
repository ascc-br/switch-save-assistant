namespace SwitchSaveAssistant.Core.Models;

public sealed record SyncState(
    int SchemaVersion,
    string Category,
    string GameName,
    string EntryName,
    SaveEntryType EntryType,
    DateTimeOffset LastSynchronizedAtUtc,
    List<SyncFileState> BaselineFiles
);
