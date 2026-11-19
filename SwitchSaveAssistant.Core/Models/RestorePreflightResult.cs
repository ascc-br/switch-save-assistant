namespace SwitchSaveAssistant.Core.Models;

public sealed record RestorePreflightResult(
    RestorePreflightStatus Status,
    string Message,
    SyncEvaluationResult? Evaluation,
    ManifestComparisonResult LocalPayloadValidation,
    ManifestComparisonResult? SafetyBackupValidation,
    BackupResult? SafetyBackup,
    RestorePlan? Plan
)
{
    public bool CanRestore =>
        Status == RestorePreflightStatus.Ready;
}
