namespace SwitchSaveAssistant.Core.Models;

public sealed record RemoteFile(
    string RelativePath,
    string RemotePath,
    ulong LengthBytes
);
