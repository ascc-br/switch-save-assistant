namespace SwitchSaveAssistant.Core.Models;

public sealed record SyncEvaluationResult(
    SyncStatus Status,
    bool BaselineNeedsRefresh,
    ManifestComparisonResult PcAgainstBaseline,
    ManifestComparisonResult SwitchAgainstBaseline,
    ManifestComparisonResult PcAgainstSwitch
);
