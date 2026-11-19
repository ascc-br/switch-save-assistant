using SwitchSaveAssistant.Core.Backup;
using SwitchSaveAssistant.Core.Models;
using SwitchSaveAssistant.Core.Mtp;
using SwitchSaveAssistant.Core.Sync;

namespace SwitchSaveAssistant.Core.Restore;

public sealed class RestorePreflightService
{
    private readonly PayloadValidationService _payloadValidationService;
    private readonly LocalSnapshotService _localSnapshotService;
    private readonly SwitchSnapshotService _switchSnapshotService;
    private readonly SyncEvaluationService _syncEvaluationService;
    private readonly RestorePolicyService _restorePolicyService;
    private readonly BackupService _backupService;
    private readonly ManifestComparisonService _manifestComparisonService;

    public RestorePreflightService()
    {
        _payloadValidationService =
            new PayloadValidationService();

        _localSnapshotService =
            new LocalSnapshotService();

        _switchSnapshotService =
            new SwitchSnapshotService();

        _syncEvaluationService =
            new SyncEvaluationService();

        _restorePolicyService =
            new RestorePolicyService();

        _backupService =
            new BackupService();

        _manifestComparisonService =
            new ManifestComparisonService();
    }

    public RestorePreflightResult Prepare(
        DbiMtpClient client,
        SyncState state,
        string localPayloadPath,
        string localManifestPath,
        string safetyBackupRoot,
        Action<string>? progress = null
    )
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(state);

        progress?.Invoke(
            "Validando payload local..."
        );

        var localValidation =
            _payloadValidationService.Validate(
                localPayloadPath,
                localManifestPath
            );

        if (!localValidation.AreEqual)
        {
            return new RestorePreflightResult(
                RestorePreflightStatus.LocalPayloadInvalid,
                "O payload local não corresponde ao manifest. Restauração bloqueada.",
                null,
                localValidation,
                null,
                null,
                null
            );
        }

        var localSnapshot =
            _localSnapshotService.Create(
                localPayloadPath
            );

        var remoteSave = client.FindSave(
            state.Category,
            state.GameName,
            state.EntryName
        );

        if (remoteSave.EntryType != state.EntryType)
        {
            throw new InvalidOperationException(
                $"Tipo de save divergente. " +
                $"Estado={state.EntryType}, " +
                $"Switch={remoteSave.EntryType}."
            );
        }

        progress?.Invoke(
            "Lendo estado atual do Switch..."
        );

        var switchSnapshot =
            _switchSnapshotService.Create(
                client,
                remoteSave,
                file =>
                {
                    progress?.Invoke(
                        $"  lendo {file.RelativePath}"
                    );
                }
            );

        var evaluation =
            _syncEvaluationService.Evaluate(
                state,
                localSnapshot,
                switchSnapshot
            );

        var policyDecision =
            _restorePolicyService.Decide(
                evaluation
            );

        if (policyDecision != RestorePreflightStatus.Ready)
        {
            var message = policyDecision switch
            {
                RestorePreflightStatus.NoChanges =>
                    "PC e Switch já estão sincronizados. Nenhuma restauração é necessária.",

                RestorePreflightStatus.SwitchChanged =>
                    "O Switch mudou desde a última sincronização. PC → Switch foi bloqueado.",

                RestorePreflightStatus.Conflict =>
                    "PC e Switch mudaram desde a última sincronização. Há conflito e nenhuma restauração automática é permitida.",

                _ =>
                    "Restauração bloqueada."
            };

            return new RestorePreflightResult(
                policyDecision,
                message,
                evaluation,
                localValidation,
                null,
                null,
                null
            );
        }

        progress?.Invoke(
            "Criando backup de segurança do Switch..."
        );

        var backupId =
            DateTime.Now.ToString(
                "yyyyMMdd-HHmmss-fff"
            )
            + "-"
            + Guid.NewGuid()
                .ToString("N")[..8];

        var safetyBackupPath = Path.Combine(
            Path.GetFullPath(
                safetyBackupRoot
            ),
            backupId,
            state.GameName,
            state.EntryName
        );

        var safetyBackup =
            _backupService.Backup(
                client,
                remoteSave,
                safetyBackupPath,
                entry =>
                {
                    progress?.Invoke(
                        $"  backup {entry.RelativePath}"
                    );
                }
            );

        progress?.Invoke(
            "Validando backup de segurança..."
        );

        var safetyValidation =
            _payloadValidationService.Validate(
                safetyBackup.PayloadPath,
                safetyBackup.ManifestPath
            );

        if (!safetyValidation.AreEqual)
        {
            return new RestorePreflightResult(
                RestorePreflightStatus.SafetyBackupInvalid,
                "O backup de segurança do Switch falhou na validação. Restauração bloqueada.",
                evaluation,
                localValidation,
                safetyValidation,
                safetyBackup,
                null
            );
        }

        var safetySnapshot =
            _manifestComparisonService.ReadManifest(
                safetyBackup.ManifestPath
            );

        var switchStability =
            _manifestComparisonService.Compare(
                switchSnapshot,
                safetySnapshot
            );

        if (!switchStability.AreEqual)
        {
            return new RestorePreflightResult(
                RestorePreflightStatus.SwitchChangedDuringPreflight,
                "O Switch mudou durante o preflight. Restauração bloqueada.",
                evaluation,
                localValidation,
                safetyValidation,
                safetyBackup,
                null
            );
        }

        var expectedSwitchFiles =
            safetySnapshot
                .OrderBy(
                    pair => pair.Key,
                    StringComparer.Ordinal
                )
                .Select(pair =>
                    new SyncFileState(
                        pair.Key,
                        pair.Value
                    )
                )
                .ToList();

        var plan = new RestorePlan(
            state.Category,
            state.GameName,
            state.EntryName,
            state.EntryType,
            Path.GetFullPath(
                localPayloadPath
            ),
            Path.GetFullPath(
                localManifestPath
            ),
            safetyBackup.DestinationPath,
            safetyBackup.ManifestPath,
            DateTimeOffset.UtcNow,
            expectedSwitchFiles
        );

        return new RestorePreflightResult(
            RestorePreflightStatus.Ready,
            "Preflight concluído. A restauração está autorizável, mas nenhum arquivo foi enviado ao Switch.",
            evaluation,
            localValidation,
            safetyValidation,
            safetyBackup,
            plan
        );
    }
}
