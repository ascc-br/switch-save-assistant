using SwitchSaveAssistant.Core.Models;

namespace SwitchSaveAssistant.Core.Restore;

public sealed class RestorePolicyService
{
    public RestorePreflightStatus Decide(
        SyncEvaluationResult evaluation
    )
    {
        ArgumentNullException.ThrowIfNull(
            evaluation
        );

        return evaluation.Status switch
        {
            SyncStatus.PcChanged =>
                RestorePreflightStatus.Ready,

            SyncStatus.Synchronized =>
                RestorePreflightStatus.NoChanges,

            SyncStatus.SwitchChanged =>
                RestorePreflightStatus.SwitchChanged,

            SyncStatus.Conflict =>
                RestorePreflightStatus.Conflict,

            _ =>
                throw new InvalidOperationException(
                    $"Estado de sincronização não suportado: {evaluation.Status}."
                )
        };
    }
}
