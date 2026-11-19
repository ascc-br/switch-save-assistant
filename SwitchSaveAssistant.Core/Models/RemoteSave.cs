namespace SwitchSaveAssistant.Core.Models;

public sealed record RemoteSave(
    string Category,
    string GameName,
    string EntryName,
    SaveEntryType EntryType,
    string RemotePath
);
