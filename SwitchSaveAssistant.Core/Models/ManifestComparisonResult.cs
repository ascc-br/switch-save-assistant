namespace SwitchSaveAssistant.Core.Models;

public sealed record ManifestComparisonResult(
    bool AreEqual,
    IReadOnlyList<ManifestChange> Changes
);
