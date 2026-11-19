namespace SwitchSaveAssistant.Core.Models;

public sealed record ManifestEntry(
    string RelativePath,
    ulong LengthBytes,
    string Sha256
);
