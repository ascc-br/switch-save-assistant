namespace SwitchSaveAssistant.Core.Models;

public sealed record SyncFileState(
    string RelativePath,
    string Sha256
);
