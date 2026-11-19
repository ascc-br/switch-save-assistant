using SwitchSaveAssistant.Core.Models;
using SwitchSaveAssistant.Core.Mtp;
using SwitchSaveAssistant.Core.Providers;
using SwitchSaveAssistant.Core.Sync;

namespace SwitchSaveAssistant.Core.Restore;

public sealed class RestoreExecutor
{
    private readonly PayloadValidationService
        _payloadValidationService = new();

    private readonly SwitchSnapshotService
        _switchSnapshotService = new();

    private readonly ManifestComparisonService
        _manifestComparisonService = new();

    private readonly RestorePlanGuardService
        _guardService = new();

    private readonly ShellMtpCopyProvider
        _shellProvider = new();

    public RestoreExecutionResult Execute(
        Func<DbiMtpClient> clientFactory,
        RestorePlan plan,
        Action<string>? progress = null
    )
    {
        ArgumentNullException.ThrowIfNull(
            clientFactory
        );

        ArgumentNullException.ThrowIfNull(
            plan
        );

        try
        {
            _guardService.Validate(
                plan
            );
        }
        catch (Exception ex)
        {
            return Result(
                RestoreExecutionStatus.PlanInvalid,
                $"RestorePlan inválido: {ex.Message}",
                false
            );
        }

        progress?.Invoke(
            "Revalidando payload local..."
        );

        var localValidation =
            _payloadValidationService.Validate(
                plan.LocalPayloadPath,
                plan.LocalManifestPath
            );

        if (!localValidation.AreEqual)
        {
            return Result(
                RestoreExecutionStatus.LocalPayloadInvalid,
                "O payload local mudou desde o preflight. Restore abortado.",
                false
            );
        }

        var safetyPayload =
            Path.Combine(
                plan.SafetyBackupPath,
                "payload"
            );

        progress?.Invoke(
            "Revalidando backup de segurança..."
        );

        var safetyValidation =
            _payloadValidationService.Validate(
                safetyPayload,
                plan.SafetyBackupManifestPath
            );

        if (!safetyValidation.AreEqual)
        {
            return Result(
                RestoreExecutionStatus.SafetyBackupInvalid,
                "O backup de segurança não está íntegro. Restore abortado.",
                false
            );
        }

        var targetManifest =
            _manifestComparisonService.ReadManifest(
                plan.LocalManifestPath
            );

        var expectedSwitch =
            _guardService.CreateExpectedSwitchSnapshot(
                plan
            );

        if (!_guardService.HasSameFileSet(
                targetManifest,
                expectedSwitch
            ))
        {
            return Result(
                RestoreExecutionStatus.FileSetMismatch,
                "O payload desejado e o save atual possuem conjuntos de arquivos diferentes. " +
                "Este executor não realiza adições nem remoções.",
                false
            );
        }

        progress?.Invoke(
            "Verificando novamente o Switch imediatamente antes da escrita..."
        );

        try
        {
            using var client =
                OpenClient(
                    clientFactory
                );

            var remoteSave =
                FindAndValidateRemoteSave(
                    client,
                    plan
                );

            var liveSnapshot =
                _switchSnapshotService.Create(
                    client,
                    remoteSave,
                    file =>
                    {
                        progress?.Invoke(
                            $"  pré-write {file.RelativePath}"
                        );
                    }
                );

            var stability =
                _manifestComparisonService.Compare(
                    expectedSwitch,
                    liveSnapshot
                );

            if (!stability.AreEqual)
            {
                return Result(
                    RestoreExecutionStatus.SwitchChangedSincePreflight,
                    "O save do Switch mudou desde a criação do RestorePlan. Restore abortado.",
                    false
                );
            }
        }
        catch (Exception ex)
        {
            return Result(
                RestoreExecutionStatus.RemoteIdentityMismatch,
                $"Falha na validação pré-write do save remoto: {ex.Message}",
                false
            );
        }

        var stagingRoot =
            Directory
                .CreateTempSubdirectory(
                    "SwitchSaveAssistant-Restore-"
                )
                .FullName;

        try
        {
            var categoryDirectory =
                Path.Combine(
                    stagingRoot,
                    plan.Category
                );

            var stagedPayload =
                Path.Combine(
                    categoryDirectory,
                    plan.GameName,
                    plan.EntryName
                );

            progress?.Invoke(
                "Criando staging local protegido..."
            );

            CopyPayloadToStaging(
                plan.LocalPayloadPath,
                stagedPayload
            );

            var stagedValidation =
                _payloadValidationService.Validate(
                    stagedPayload,
                    plan.LocalManifestPath
                );

            if (!stagedValidation.AreEqual)
            {
                return Result(
                    RestoreExecutionStatus.LocalPayloadInvalid,
                    "O staging não corresponde ao manifest. Restore abortado.",
                    false
                );
            }

            progress?.Invoke(
                "Staging validado."
            );

            progress?.Invoke(
                "Executando verificação TOCTOU final do Switch..."
            );

            try
            {
                using var client =
                    OpenClient(
                        clientFactory
                    );

                var remoteSave =
                    FindAndValidateRemoteSave(
                        client,
                        plan
                    );

                var finalPreWriteSnapshot =
                    _switchSnapshotService.Create(
                        client,
                        remoteSave,
                        file =>
                        {
                            progress?.Invoke(
                                $"  TOCTOU {file.RelativePath}"
                            );
                        }
                    );

                var finalStability =
                    _manifestComparisonService.Compare(
                        expectedSwitch,
                        finalPreWriteSnapshot
                    );

                if (!finalStability.AreEqual)
                {
                    return Result(
                        RestoreExecutionStatus.SwitchChangedSincePreflight,
                        "O save do Switch mudou imediatamente antes da escrita. Restore abortado.",
                        false
                    );
                }
            }
            catch (Exception ex)
            {
                return Result(
                    RestoreExecutionStatus.RemoteIdentityMismatch,
                    $"Falha na verificação TOCTOU final: {ex.Message}",
                    false
                );
            }

            progress?.Invoke(
                "TOCTOU final confirmado."
            );

            progress?.Invoke(
                "Executando cópia Windows Shell → DBI 7: Saves..."
            );

            try
            {
                _shellProvider.CopyDirectoryToStorage(
                    "Switch",
                    "7: Saves",
                    categoryDirectory
                );
            }
            catch (Exception ex)
            {
                return Result(
                    RestoreExecutionStatus.ShellCopyFailed,
                    $"A operação Windows Shell falhou: {ex.Message}",
                    true
                );
            }

            progress?.Invoke(
                "Cópia concluída. Verificando todo o save por SHA-256..."
            );

            try
            {
                using var client =
                    OpenClient(
                        clientFactory
                    );

                var remoteSave =
                    FindAndValidateRemoteSave(
                        client,
                        plan
                    );

                var postWriteSnapshot =
                    _switchSnapshotService.Create(
                        client,
                        remoteSave,
                        file =>
                        {
                            progress?.Invoke(
                                $"  pós-write {file.RelativePath}"
                            );
                        }
                    );

                var verification =
                    _manifestComparisonService.Compare(
                        targetManifest,
                        postWriteSnapshot
                    );

                if (!verification.AreEqual)
                {
                    return Result(
                        RestoreExecutionStatus.PostWriteVerificationFailed,
                        "A cópia terminou, mas o snapshot remoto não corresponde ao manifest desejado. " +
                        "O baseline NÃO foi atualizado e o backup de segurança deve ser preservado.",
                        true
                    );
                }
            }
            catch (Exception ex)
            {
                return Result(
                    RestoreExecutionStatus.PostWriteVerificationFailed,
                    $"Não foi possível confirmar o estado remoto após a escrita: {ex.Message}. " +
                    "O baseline NÃO foi atualizado.",
                    true
                );
            }

            return Result(
                RestoreExecutionStatus.Completed,
                "Restore concluído e validado por SHA-256. " +
                "O baseline ainda NÃO foi atualizado.",
                false
            );
        }
        finally
        {
            try
            {
                if (Directory.Exists(
                        stagingRoot
                    ))
                {
                    Directory.Delete(
                        stagingRoot,
                        recursive: true
                    );
                }
            }
            catch
            {
                // Staging local é descartável.
            }
        }
    }

    private static DbiMtpClient OpenClient(
        Func<DbiMtpClient> factory
    )
    {
        var client =
            factory();

        try
        {
            client.Connect();
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private static RemoteSave FindAndValidateRemoteSave(
        DbiMtpClient client,
        RestorePlan plan
    )
    {
        var remoteSave =
            client.FindSave(
                plan.Category,
                plan.GameName,
                plan.EntryName
            );

        if (remoteSave.EntryType !=
            plan.EntryType)
        {
            throw new InvalidOperationException(
                $"EntryType divergente. " +
                $"Plano={plan.EntryType}; " +
                $"Switch={remoteSave.EntryType}."
            );
        }

        var savesRoot =
            client.GetDriveRootPath(
                "7: Saves"
            );

        var expectedPath =
            CombineRemotePath(
                savesRoot,
                plan.Category,
                plan.GameName,
                plan.EntryName
            );

        if (!string.Equals(
                NormalizeRemotePath(
                    remoteSave.RemotePath
                ),
                NormalizeRemotePath(
                    expectedPath
                ),
                StringComparison.OrdinalIgnoreCase
            ))
        {
            throw new InvalidOperationException(
                "O caminho remoto não corresponde exatamente ao save autorizado pelo RestorePlan."
            );
        }

        return remoteSave;
    }

    private static void CopyPayloadToStaging(
        string sourceRoot,
        string destinationRoot
    )
    {
        sourceRoot =
            Path.GetFullPath(
                sourceRoot
            );

        destinationRoot =
            Path.GetFullPath(
                destinationRoot
            );

        var sourceInfo =
            new DirectoryInfo(
                sourceRoot
            );

        if (!sourceInfo.Exists)
        {
            throw new DirectoryNotFoundException(
                sourceRoot
            );
        }

        RejectReparsePoint(
            sourceInfo
        );

        Directory.CreateDirectory(
            destinationRoot
        );

        CopyDirectoryTree(
            sourceInfo,
            sourceRoot,
            destinationRoot
        );
    }

    private static void CopyDirectoryTree(
        DirectoryInfo currentDirectory,
        string sourceRoot,
        string destinationRoot
    )
    {
        foreach (var file
                 in currentDirectory.EnumerateFiles())
        {
            RejectReparsePoint(
                file
            );

            var relative =
                Path.GetRelativePath(
                    sourceRoot,
                    file.FullName
                );

            var destination =
                ResolveInsideRoot(
                    destinationRoot,
                    relative
                );

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    destination
                )!
            );

            file.CopyTo(
                destination,
                overwrite: false
            );
        }

        foreach (var directory
                 in currentDirectory.EnumerateDirectories())
        {
            // Importante: examinamos o diretório
            // ANTES de entrar nele recursivamente.
            RejectReparsePoint(
                directory
            );

            var relative =
                Path.GetRelativePath(
                    sourceRoot,
                    directory.FullName
                );

            var destination =
                ResolveInsideRoot(
                    destinationRoot,
                    relative
                );

            Directory.CreateDirectory(
                destination
            );

            CopyDirectoryTree(
                directory,
                sourceRoot,
                destinationRoot
            );
        }
    }

    private static void RejectReparsePoint(
        FileSystemInfo entry
    )
    {
        if (entry.Attributes.HasFlag(
                FileAttributes.ReparsePoint
            ))
        {
            throw new IOException(
                $"Reparse point recusado no payload: '{entry.FullName}'."
            );
        }
    }

    private static string ResolveInsideRoot(
        string root,
        string relative
    )
    {
        var fullRoot =
            Path.GetFullPath(
                root
            )
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar
            );

        var candidate =
            Path.GetFullPath(
                Path.Combine(
                    fullRoot,
                    relative
                )
            );

        var requiredPrefix =
            fullRoot +
            Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(
                requiredPrefix,
                StringComparison.OrdinalIgnoreCase
            ))
        {
            throw new IOException(
                $"Caminho escapou do staging: '{relative}'."
            );
        }

        return candidate;
    }

    private static string CombineRemotePath(
        params string[] parts
    )
    {
        var result =
            parts[0].TrimEnd(
                '\\',
                '/'
            );

        foreach (var part
                 in parts.Skip(1))
        {
            result +=
                "\\" +
                part.Trim(
                    '\\',
                    '/'
                );
        }

        return result;
    }

    private static string NormalizeRemotePath(
        string value
    )
    {
        return value
            .Replace(
                '/',
                '\\'
            )
            .TrimEnd(
                '\\'
            );
    }

    private static RestoreExecutionResult Result(
        RestoreExecutionStatus status,
        string message,
        bool requiresRescan
    )
    {
        return new RestoreExecutionResult(
            status,
            message,
            requiresRescan
        );
    }
}
