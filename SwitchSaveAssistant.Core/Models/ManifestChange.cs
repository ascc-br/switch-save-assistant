namespace SwitchSaveAssistant.Core.Models;

public sealed record ManifestChange(
    string RelativePath,
    ManifestChangeType ChangeType,
    string? BaselineSha256,
    string? CurrentSha256
);
