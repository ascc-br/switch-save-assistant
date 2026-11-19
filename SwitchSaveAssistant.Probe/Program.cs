using System.Text;
using SwitchSaveAssistant.Core.Backup;
using SwitchSaveAssistant.Core.Mtp;
using SwitchSaveAssistant.Core.Sync;
using SwitchSaveAssistant.Core.Models;
using SwitchSaveAssistant.Core.Restore;

Console.OutputEncoding = Encoding.UTF8;

var command = args.Length > 0
    ? args[0].ToLowerInvariant()
    : "list";

try
{
    switch (command)
    {
        case "list":
        {
            using var dbi = ConnectDbi();

            ListSaves(dbi);
            break;
        }

        case "backup":
        {
            using var dbi = ConnectDbi();

            RunBackup(dbi, args);
            break;
        }

        case "compare":
            RunCompare(args);
            break;

        case "state-init":
            RunStateInit(args);
            break;

        case "state-show":
            RunStateShow(args);
            break;

        case "status":
        {
            using var dbi = ConnectDbi();

            RunStatus(dbi, args);
            break;
        }

        case "restore-preflight":
        {
            using var dbi = ConnectDbi();

            RunRestorePreflight(dbi, args);
            break;
        }

        case "restore-execute":
        {
            RunRestoreExecute(args);
            break;
        }

        case "mtp-write-probe":
        {
            using var dbi = ConnectDbi();

            RunMtpWriteProbe(dbi);
            break;
        }

        case "mtp-replace-probe":
        {
            using var dbi = ConnectDbi();

            RunMtpReplaceProbe(dbi);
            break;
        }

        case "mtp-overwrite-probe":
        {
            using var dbi = ConnectDbi();

            RunMtpOverwriteProbe(dbi);
            break;
        }

        case "mtp-capabilities":
        {
            using var dbi = ConnectDbi();

            RunMtpCapabilities(dbi);
            break;
        }

        case "mtp-shell-replace-probe":
        {
            RunMtpShellReplaceProbe();
            break;
        }

        case "mtp-saves-droptarget-capabilities":
        {
            RunMtpSavesDropTargetCapabilities();
            break;
        }

        case "mtp-saves-drop-negotiate":
        {
            RunMtpSavesDropNegotiate();
            break;
        }

        case "mtp-sd-drop-replace-probe":
        {
            RunMtpSdDropReplaceProbe();
            break;
        }

        case "mtp-sd-drop-create-probe":
        {
            RunMtpSdDropCreateProbe();
            break;
        }

        case "mtp-wpd-raw-create-replace-probe":
        {
            using var dbi = ConnectDbi();

            RunWpdRawCreateReplaceProbe(dbi);
            break;
        }

        case "mtp-sd-async-drop-create-probe":
        {
            RunMtpSdAsyncDropCreateProbe();
            break;
        }

        case "mtp-sd-async-drop-replace-probe":
        {
            RunMtpSdAsyncDropReplaceProbe();
            break;
        }

        case "mtp-sd-async-tree-probe":
        {
            RunMtpSdAsyncTreeProbe();
            break;
        }

        default:
            PrintUsage();
            Environment.ExitCode = 1;
            break;
    }
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine(
        $"ERRO: {ex.GetType().Name}: {ex.Message}"
    );

    Environment.ExitCode = 1;
}


static DbiMtpClient ConnectDbi()
{
    var dbi = new DbiMtpClient();

    dbi.Connect();

    Console.WriteLine(
        $"Switch conectado: {dbi.DeviceDescription}"
    );

    Console.WriteLine();

    return dbi;
}


static void ListSaves(
    DbiMtpClient dbi
)
{
    foreach (var category in new[]
             {
                 "Installed games",
                 "Uninstalled games"
             })
    {
        Console.WriteLine(
            $"=== {category} ==="
        );

        Console.WriteLine();

        var games = dbi.GetGames(category);

        foreach (var game in games)
        {
            Console.WriteLine(
                $"🎮 {game.Name}"
            );

            var saves = dbi.GetSaveEntries(
                game
            );

            if (saves.Count == 0)
            {
                Console.WriteLine(
                    "   (nenhum save)"
                );

                continue;
            }

            foreach (var save in saves)
            {
                Console.WriteLine(
                    $"   └─ [{save.EntryType}] {save.EntryName}"
                );
            }
        }

        Console.WriteLine();
    }
}


static void RunBackup(
    DbiMtpClient dbi,
    string[] args
)
{
    if (args.Length != 4)
    {
        Console.WriteLine(
            "Uso:"
        );

        Console.WriteLine(
            "dotnet run -- backup \"Installed games\" \"Nome do jogo\" \"Nome do save\""
        );

        Environment.ExitCode = 1;
        return;
    }

    var category = args[1];
    var gameName = args[2];
    var saveEntry = args[3];

    var remoteSave = dbi.FindSave(
        category,
        gameName,
        saveEntry
    );

    var backupRoot = Path.GetFullPath(
        Path.Combine(
            Environment.CurrentDirectory,
            "..",
            "TestBackup"
        )
    );

    var timestamp = DateTime.Now.ToString(
        "yyyyMMdd-HHmmss"
    );

    var destinationPath = Path.Combine(
        backupRoot,
        timestamp,
        gameName,
        saveEntry
    );

    Console.WriteLine(
        $"Jogo    : {gameName}"
    );

    Console.WriteLine(
        $"Save    : {saveEntry} ({remoteSave.EntryType})"
    );

    Console.WriteLine(
        $"Origem  : {remoteSave.RemotePath}"
    );

    Console.WriteLine(
        $"Destino : {destinationPath}"
    );

    Console.WriteLine();

    var service = new BackupService();

    var result = service.Backup(
        dbi,
        remoteSave,
        destinationPath,
        entry =>
        {
            Console.WriteLine(
                $"↓ {entry.RelativePath}"
            );

            Console.WriteLine(
                $"  {entry.LengthBytes:N0} bytes"
            );

            Console.WriteLine(
                $"  {entry.Sha256}"
            );
        }
    );

    Console.WriteLine();
    Console.WriteLine("BACKUP CONCLUÍDO");

    Console.WriteLine(
        $"Arquivos : {result.FileCount}"
    );

    Console.WriteLine(
        $"Bytes    : {result.TotalBytes:N0}"
    );

    Console.WriteLine(
        $"Payload  : {result.PayloadPath}"
    );

    Console.WriteLine(
        $"Manifest : {result.ManifestPath}"
    );
}


static void RunCompare(
    string[] args
)
{
    if (args.Length != 3)
    {
        Console.WriteLine(
            "Uso:"
        );

        Console.WriteLine(
            "dotnet run -- compare \"manifest-antigo\" \"manifest-atual\""
        );

        Environment.ExitCode = 1;
        return;
    }

    var baseline = Path.GetFullPath(
        args[1]
    );

    var current = Path.GetFullPath(
        args[2]
    );

    Console.WriteLine(
        $"Baseline : {baseline}"
    );

    Console.WriteLine(
        $"Atual    : {current}"
    );

    Console.WriteLine();

    var service = new ManifestComparisonService();

    var result = service.Compare(
        baseline,
        current
    );

    if (result.AreEqual)
    {
        Console.WriteLine(
            "RESULTADO: manifests idênticos."
        );

        return;
    }

    Console.WriteLine(
        $"RESULTADO: {result.Changes.Count} alteração(ões)."
    );

    Console.WriteLine();

    foreach (var change in result.Changes)
    {
        Console.WriteLine(
            $"[{change.ChangeType}] {change.RelativePath}"
        );

        if (change.BaselineSha256 is not null)
        {
            Console.WriteLine(
                $"  anterior: {change.BaselineSha256}"
            );
        }

        if (change.CurrentSha256 is not null)
        {
            Console.WriteLine(
                $"  atual   : {change.CurrentSha256}"
            );
        }
    }

    Environment.ExitCode = 2;
}


static void PrintUsage()
{
    Console.WriteLine(
        "SwitchSaveAssistant.Probe"
    );

    Console.WriteLine();

    Console.WriteLine(
        "Listar jogos e saves:"
    );

    Console.WriteLine(
        "  dotnet run"
    );

    Console.WriteLine();

    Console.WriteLine(
        "Criar backup:"
    );

    Console.WriteLine(
        "  dotnet run -- backup \"Installed games\" \"Mario Party Superstars\" \"Alfran\""
    );

    Console.WriteLine();

    Console.WriteLine(
        "Comparar manifests:"
    );

    Console.WriteLine(
        "  dotnet run -- compare \"manifest-1\" \"manifest-2\""
    );


    Console.WriteLine();

    Console.WriteLine(
        "Preflight de restore, sem escrita:"
    );

    Console.WriteLine(
        "  dotnet run -- restore-preflight " +
        "\"sync-state.json\" \"payload\" \"manifest\" \"safety-backups\""
    );

    Console.WriteLine();

    Console.WriteLine(
        "Restore real protegido:"
    );

    Console.WriteLine(
        "  dotnet run -- restore-execute " +
        "\"sync-state.json\" \"payload\" \"manifest\" \"safety-backups\" " +
        "--confirm-real-save-write"
    );
}


static void RunStateInit(
    string[] args
)
{
    if (args.Length != 7)
    {
        Console.WriteLine(
            "Uso:"
        );

        Console.WriteLine(
            "dotnet run -- state-init \"manifest\" \"sync-state.json\" \"categoria\" \"jogo\" \"save\" \"tipo\""
        );

        Console.WriteLine(
            "Tipos: User, Device, Bcat, Unknown"
        );

        Environment.ExitCode = 1;
        return;
    }

    var manifestPath = Path.GetFullPath(
        args[1]
    );

    var statePath = Path.GetFullPath(
        args[2]
    );

    var category = args[3];
    var gameName = args[4];
    var entryName = args[5];

    if (!Enum.TryParse<SaveEntryType>(
            args[6],
            ignoreCase: true,
            out var entryType
        ))
    {
        Console.WriteLine(
            $"Tipo de save inválido: '{args[6]}'."
        );

        Environment.ExitCode = 1;
        return;
    }

    var service = new SyncStateService();

    var state = service.CreateFromManifest(
        category,
        gameName,
        entryName,
        entryType,
        manifestPath
    );

    service.Save(
        statePath,
        state
    );

    Console.WriteLine(
        "sync-state.json criado com sucesso."
    );

    Console.WriteLine();

    Console.WriteLine(
        $"Arquivo : {statePath}"
    );

    Console.WriteLine(
        $"Categoria: {state.Category}"
    );

    Console.WriteLine(
        $"Jogo    : {state.GameName}"
    );

    Console.WriteLine(
        $"Save    : {state.EntryName}"
    );

    Console.WriteLine(
        $"Tipo    : {state.EntryType}"
    );

    Console.WriteLine(
        $"Arquivos: {state.BaselineFiles.Count}"
    );

    Console.WriteLine(
        $"UTC     : {state.LastSynchronizedAtUtc:O}"
    );
}

static void RunStateShow(
    string[] args
)
{
    if (args.Length != 2)
    {
        Console.WriteLine(
            "Uso:"
        );

        Console.WriteLine(
            "dotnet run -- state-show \"sync-state.json\""
        );

        Environment.ExitCode = 1;
        return;
    }

    var statePath = Path.GetFullPath(
        args[1]
    );

    var service = new SyncStateService();

    var state = service.Load(
        statePath
    );

    Console.WriteLine(
        $"Schema   : {state.SchemaVersion}"
    );

    Console.WriteLine(
        $"Categoria: {state.Category}"
    );

    Console.WriteLine(
        $"Jogo     : {state.GameName}"
    );

    Console.WriteLine(
        $"Save     : {state.EntryName}"
    );

    Console.WriteLine(
        $"Tipo     : {state.EntryType}"
    );

    Console.WriteLine(
        $"Última sincronização UTC: {state.LastSynchronizedAtUtc:O}"
    );

    Console.WriteLine();

    Console.WriteLine(
        $"Baseline ({state.BaselineFiles.Count} arquivo(s)):"
    );

    foreach (var file in state.BaselineFiles)
    {
        Console.WriteLine(
            $"  {file.Sha256}  {file.RelativePath}"
        );
    }
}


static void RunStatus(
    DbiMtpClient dbi,
    string[] args
)
{
    if (args.Length != 3)
    {
        Console.WriteLine(
            "Uso:"
        );

        Console.WriteLine(
            "dotnet run -- status \"sync-state.json\" \"pasta-local\""
        );

        Environment.ExitCode = 1;
        return;
    }

    var statePath = Path.GetFullPath(
        args[1]
    );

    var localPath = Path.GetFullPath(
        args[2]
    );

    var stateService = new SyncStateService();

    var state = stateService.Load(
        statePath
    );

    Console.WriteLine(
        $"Jogo : {state.GameName}"
    );

    Console.WriteLine(
        $"Save : {state.EntryName} ({state.EntryType})"
    );

    Console.WriteLine();

    Console.WriteLine(
        "Calculando snapshot do PC..."
    );

    var localSnapshotService =
        new LocalSnapshotService();

    var pcSnapshot =
        localSnapshotService.Create(
            localPath
        );

    Console.WriteLine(
        $"PC: {pcSnapshot.Count} arquivo(s)"
    );

    Console.WriteLine();

    var remoteSave = dbi.FindSave(
        state.Category,
        state.GameName,
        state.EntryName
    );

    Console.WriteLine(
        "Calculando snapshot do Switch..."
    );

    var switchSnapshotService =
        new SwitchSnapshotService();

    var switchSnapshot =
        switchSnapshotService.Create(
            dbi,
            remoteSave,
            file =>
            {
                Console.WriteLine(
                    $"  lendo: {file.RelativePath}"
                );
            }
        );

    Console.WriteLine(
        $"Switch: {switchSnapshot.Count} arquivo(s)"
    );

    Console.WriteLine();

    var evaluation =
        new SyncEvaluationService()
            .Evaluate(
                state,
                pcSnapshot,
                switchSnapshot
            );

    var statusText = evaluation.Status switch
    {
        SyncStatus.Synchronized =>
            "SINCRONIZADO",

        SyncStatus.PcChanged =>
            "PC MUDOU",

        SyncStatus.SwitchChanged =>
            "SWITCH MUDOU",

        SyncStatus.Conflict =>
            "CONFLITO",

        _ =>
            evaluation.Status.ToString()
    };

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine(
        $" STATUS: {statusText}"
    );

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine();

    Console.WriteLine(
        $"PC vs baseline     : " +
        $"{(evaluation.PcAgainstBaseline.AreEqual ? "igual" : "diferente")}"
    );

    Console.WriteLine(
        $"Switch vs baseline : " +
        $"{(evaluation.SwitchAgainstBaseline.AreEqual ? "igual" : "diferente")}"
    );

    Console.WriteLine(
        $"PC vs Switch       : " +
        $"{(evaluation.PcAgainstSwitch.AreEqual ? "igual" : "diferente")}"
    );

    Console.WriteLine(
        $"Atualizar baseline : " +
        $"{(evaluation.BaselineNeedsRefresh ? "sim" : "não")}"
    );

    Console.WriteLine();

    switch (evaluation.Status)
    {
        case SyncStatus.Synchronized:
            Console.WriteLine(
                "Nenhuma sincronização de arquivos é necessária."
            );
            break;

        case SyncStatus.PcChanged:
            Console.WriteLine(
                "Somente o PC mudou desde a última sincronização."
            );
            break;

        case SyncStatus.SwitchChanged:
            Console.WriteLine(
                "Somente o Switch mudou desde a última sincronização."
            );
            break;

        case SyncStatus.Conflict:
            Console.WriteLine(
                "PC e Switch divergiram desde a última sincronização."
            );

            Console.WriteLine(
                "Nenhum lado deve ser sobrescrito automaticamente."
            );
            break;
    }
}


static void RunRestorePreflight(
    DbiMtpClient dbi,
    string[] args
)
{
    if (args.Length != 5)
    {
        Console.WriteLine(
            "Uso:"
        );

        Console.WriteLine(
            "dotnet run -- restore-preflight \"sync-state.json\" \"payload\" \"manifest\" \"safety-backups\""
        );

        Environment.ExitCode = 1;
        return;
    }

    var statePath = Path.GetFullPath(
        args[1]
    );

    var payloadPath = Path.GetFullPath(
        args[2]
    );

    var manifestPath = Path.GetFullPath(
        args[3]
    );

    var safetyBackupRoot = Path.GetFullPath(
        args[4]
    );

    var state =
        new SyncStateService()
            .Load(
                statePath
            );

    Console.WriteLine(
        $"Jogo : {state.GameName}"
    );

    Console.WriteLine(
        $"Save : {state.EntryName} ({state.EntryType})"
    );

    Console.WriteLine();

    var service =
        new RestorePreflightService();

    var result = service.Prepare(
        dbi,
        state,
        payloadPath,
        manifestPath,
        safetyBackupRoot,
        message =>
        {
            Console.WriteLine(
                message
            );
        }
    );

    Console.WriteLine();
    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine(
        $" PREFLIGHT: {result.Status}"
    );

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine();

    Console.WriteLine(
        result.Message
    );

    if (result.SafetyBackup is not null)
    {
        Console.WriteLine();

        Console.WriteLine(
            $"Backup de segurança:"
        );

        Console.WriteLine(
            $"  {result.SafetyBackup.DestinationPath}"
        );

        Console.WriteLine(
            $"  arquivos: {result.SafetyBackup.FileCount}"
        );

        Console.WriteLine(
            $"  bytes   : {result.SafetyBackup.TotalBytes:N0}"
        );
    }

    if (result.Plan is not null)
    {
        Console.WriteLine();

        Console.WriteLine(
            "RESTORE PLAN CRIADO"
        );

        Console.WriteLine(
            $"Preparado UTC : {result.Plan.PreparedAtUtc:O}"
        );

        Console.WriteLine(
            $"Payload       : {result.Plan.LocalPayloadPath}"
        );

        Console.WriteLine(
            $"Manifest      : {result.Plan.LocalManifestPath}"
        );

        Console.WriteLine(
            $"Safety backup : {result.Plan.SafetyBackupPath}"
        );

        Console.WriteLine();

        Console.WriteLine(
            "IMPORTANTE: nenhum upload foi realizado."
        );
    }

    if (!result.CanRestore)
    {
        Environment.ExitCode = 3;
    }
}


static void RunRestoreExecute(
    string[] args
)
{
    const string confirmation =
        "--confirm-real-save-write";

    if (args.Length != 6)
    {
        Console.WriteLine(
            "Uso:"
        );

        Console.WriteLine(
            "dotnet run -- restore-execute " +
            "\"sync-state.json\" " +
            "\"payload\" " +
            "\"manifest\" " +
            "\"safety-backups\" " +
            "--confirm-real-save-write"
        );

        Console.WriteLine();

        Console.WriteLine(
            "ATENÇÃO: este comando pode escrever em 7: Saves."
        );

        Environment.ExitCode = 1;
        return;
    }

    if (!string.Equals(
            args[5],
            confirmation,
            StringComparison.Ordinal
        ))
    {
        Console.WriteLine(
            "RESTORE BLOQUEADO."
        );

        Console.WriteLine();

        Console.WriteLine(
            $"Confirmação obrigatória ausente: {confirmation}"
        );

        Environment.ExitCode = 2;
        return;
    }

    var realRestoreTransportEnabled =
        string.Equals(
            Environment.GetEnvironmentVariable(
                "SWITCHSAVEASSISTANT_ENABLE_REAL_RESTORE"
            ),
            "DROP-TRANSPORT-VALIDATED",
            StringComparison.Ordinal
        );

    if (!realRestoreTransportEnabled)
    {
        Console.WriteLine(
            "RESTORE REAL TEMPORARIAMENTE BLOQUEADO."
        );

        Console.WriteLine();

        Console.WriteLine(
            "Motivo: IFileOperation funciona na SD Card, mas 7: Saves retornou ERROR_WRITE_PROTECT."
        );

        Console.WriteLine(
            "Aguardando validação do transporte Shell Drop."
        );

        Environment.ExitCode = 5;
        return;
    }

    var statePath =
        Path.GetFullPath(
            args[1]
        );

    var payloadPath =
        Path.GetFullPath(
            args[2]
        );

    var manifestPath =
        Path.GetFullPath(
            args[3]
        );

    var safetyBackupRoot =
        Path.GetFullPath(
            args[4]
        );

    var state =
        new SyncStateService()
            .Load(
                statePath
            );

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine(
        " RESTORE REAL - DBI 7: SAVES"
    );

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine();

    Console.WriteLine(
        "ATENÇÃO: este comando está autorizado a escrever no save real."
    );

    Console.WriteLine(
        "Nenhum arquivo remoto será apagado pelo SwitchSaveAssistant."
    );

    Console.WriteLine(
        "O baseline NÃO será atualizado automaticamente."
    );

    Console.WriteLine();

    Console.WriteLine(
        $"Categoria : {state.Category}"
    );

    Console.WriteLine(
        $"Jogo      : {state.GameName}"
    );

    Console.WriteLine(
        $"Save      : {state.EntryName} ({state.EntryType})"
    );

    Console.WriteLine(
        $"Payload   : {payloadPath}"
    );

    Console.WriteLine(
        $"Manifest  : {manifestPath}"
    );

    Console.WriteLine(
        $"Safety dir: {safetyBackupRoot}"
    );

    Console.WriteLine();

    Console.WriteLine(
        "FASE 1/2 - PREFLIGHT NOVO"
    );

    Console.WriteLine();

    RestorePreflightResult preflight;

    using (var dbi =
           ConnectDbi())
    {
        preflight =
            new RestorePreflightService()
                .Prepare(
                    dbi,
                    state,
                    payloadPath,
                    manifestPath,
                    safetyBackupRoot,
                    message =>
                    {
                        Console.WriteLine(
                            message
                        );
                    }
                );
    }

    Console.WriteLine();

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine(
        $" PREFLIGHT: {preflight.Status}"
    );

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine();

    Console.WriteLine(
        preflight.Message
    );

    if (preflight.SafetyBackup is not null)
    {
        Console.WriteLine();

        Console.WriteLine(
            "Backup de segurança:"
        );

        Console.WriteLine(
            $"  {preflight.SafetyBackup.DestinationPath}"
        );

        Console.WriteLine(
            $"  arquivos: {preflight.SafetyBackup.FileCount}"
        );

        Console.WriteLine(
            $"  bytes   : {preflight.SafetyBackup.TotalBytes:N0}"
        );
    }

    if (!preflight.CanRestore ||
        preflight.Plan is null)
    {
        Console.WriteLine();

        Console.WriteLine(
            "RESTORE NÃO AUTORIZADO PELO PREFLIGHT."
        );

        Console.WriteLine(
            "Nenhuma escrita em 7: Saves foi iniciada."
        );

        Environment.ExitCode = 3;
        return;
    }

    Console.WriteLine();

    Console.WriteLine(
        "RestorePlan fresco criado."
    );

    Console.WriteLine(
        $"Preparado UTC : {preflight.Plan.PreparedAtUtc:O}"
    );

    Console.WriteLine(
        $"Safety backup : {preflight.Plan.SafetyBackupPath}"
    );

    Console.WriteLine();

    Console.WriteLine(
        "Conexão MediaDevices do preflight encerrada."
    );

    Console.WriteLine();

    Console.WriteLine(
        "FASE 2/2 - EXECUÇÃO PROTEGIDA"
    );

    Console.WriteLine();

    var execution =
        new RestoreExecutor()
            .Execute(
                ConnectDbi,
                preflight.Plan,
                message =>
                {
                    Console.WriteLine(
                        message
                    );
                }
            );

    Console.WriteLine();

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine(
        $" RESTORE: {execution.Status}"
    );

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine();

    Console.WriteLine(
        execution.Message
    );

    Console.WriteLine();

    Console.WriteLine(
        $"Requer novo scan: " +
        $"{(execution.RequiresRescan ? "sim" : "não")}"
    );

    Console.WriteLine(
        $"Safety backup preservado:"
    );

    Console.WriteLine(
        $"  {preflight.Plan.SafetyBackupPath}"
    );

    Console.WriteLine();

    if (execution.Success)
    {
        Console.WriteLine(
            "RESTORE VALIDADO POR SHA-256."
        );

        Console.WriteLine(
            "O sync-state.json permanece inalterado."
        );

        return;
    }

    Console.WriteLine(
        "RESTORE NÃO FOI DECLARADO COMO CONCLUÍDO."
    );

    Console.WriteLine(
        "Não atualize o baseline."
    );

    Environment.ExitCode = 4;
}


static void RunMtpWriteProbe(
    DbiMtpClient dbi
)
{
    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine(
        " MTP WRITE PROBE - SD CARD"
    );

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine();

    Console.WriteLine(
        "ATENÇÃO: este teste escreve somente na SD Card."
    );

    Console.WriteLine(
        "O armazenamento 7: Saves não será utilizado."
    );

    Console.WriteLine();

    var service =
        new SwitchSaveAssistant.Core.Safety
            .MtpWriteProbeService();

    var result = service.Run(
        dbi,
        message =>
        {
            Console.WriteLine(
                message
            );
        }
    );

    Console.WriteLine();
    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine(
        " WRITE PROBE: OK"
    );

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine();

    Console.WriteLine(
        $"Bytes     : {result.Bytes:N0}"
    );

    Console.WriteLine(
        $"SHA origem: {result.SourceSha256}"
    );

    Console.WriteLine(
        $"SHA retorno: {result.DownloadedSha256}"
    );

    Console.WriteLine(
        $"Limpeza   : {(result.RemoteCleanupConfirmed ? "OK" : "FALHOU")}"
    );

    Console.WriteLine();

    Console.WriteLine(
        "Nenhum save foi alterado."
    );
}


static void RunMtpReplaceProbe(
    DbiMtpClient dbi
)
{
    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine(
        " MTP REPLACE PROBE - SD CARD"
    );

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine();

    Console.WriteLine(
        "Este teste ensaia substituição segura somente na SD Card."
    );

    Console.WriteLine(
        "O armazenamento 7: Saves não será utilizado."
    );

    Console.WriteLine();

    var service =
        new SwitchSaveAssistant.Core.Safety
            .MtpReplaceProbeService();

    var result = service.Run(
        dbi,
        message =>
        {
            Console.WriteLine(
                message
            );
        }
    );

    Console.WriteLine();
    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine(
        " REPLACE PROBE: OK"
    );

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine();

    Console.WriteLine(
        $"SHA original : {result.OriginalSha256}"
    );

    Console.WriteLine(
        $"SHA novo     : {result.ReplacementSha256}"
    );

    Console.WriteLine(
        $"SHA final    : {result.FinalSha256}"
    );

    Console.WriteLine(
        $"Limpeza      : {(result.CleanupConfirmed ? "OK" : "FALHOU")}"
    );

    Console.WriteLine();

    Console.WriteLine(
        "Nenhum save foi alterado."
    );
}


static void RunMtpOverwriteProbe(
    DbiMtpClient dbi
)
{
    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine(
        " MTP IN-PLACE OVERWRITE PROBE - SD CARD"
    );

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine();

    Console.WriteLine(
        "Este teste altera somente um arquivo descartável na SD Card."
    );

    Console.WriteLine(
        "7: Saves permanece intocado."
    );

    Console.WriteLine();

    var service =
        new SwitchSaveAssistant.Core.Safety
            .MtpOverwriteProbeService();

    var result =
        service.Run(
            dbi,
            message =>
            {
                Console.WriteLine(
                    message
                );
            }
        );

    Console.WriteLine();
    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine(
        " IN-PLACE OVERWRITE PROBE: OK"
    );

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine();

    Console.WriteLine(
        $"Original    : {result.OriginalBytes:N0} bytes"
    );

    Console.WriteLine(
        $"Substituto  : {result.ReplacementBytes:N0} bytes"
    );

    Console.WriteLine(
        $"SHA original: {result.OriginalSha256}"
    );

    Console.WriteLine(
        $"SHA novo    : {result.ReplacementSha256}"
    );

    Console.WriteLine(
        $"Após crescer: {result.AfterGrowSha256}"
    );

    Console.WriteLine(
        $"Após reduzir: {result.AfterShrinkSha256}"
    );

    Console.WriteLine(
        $"Limpeza     : {(result.CleanupConfirmed ? "OK" : "FALHOU")}"
    );

    Console.WriteLine();

    Console.WriteLine(
        "Nenhum save foi alterado."
    );
}


static void RunMtpCapabilities(
    DbiMtpClient dbi
)
{
    Console.WriteLine();
    Console.WriteLine(
        "=============================================="
    );
    Console.WriteLine(
        " WPD / MTP - COMANDOS ANUNCIADOS PELO DBI"
    );
    Console.WriteLine(
        "=============================================="
    );
    Console.WriteLine();

    var commands =
        dbi.GetSupportedCommandNames();

    foreach (var command in commands)
    {
        Console.WriteLine(
            command
        );
    }

    Console.WriteLine();
    Console.WriteLine(
        $"Total: {commands.Count}"
    );

    Console.WriteLine();

    var interesting =
        commands.Where(name =>
            name.Contains(
                "Update",
                StringComparison.OrdinalIgnoreCase
            ) ||
            name.Contains(
                "Write",
                StringComparison.OrdinalIgnoreCase
            ) ||
            name.Contains(
                "CreateObject",
                StringComparison.OrdinalIgnoreCase
            ) ||
            name.Contains(
                "Delete",
                StringComparison.OrdinalIgnoreCase
            )
        )
        .ToList();

    Console.WriteLine(
        "Operações relevantes:"
    );

    foreach (var command in interesting)
    {
        Console.WriteLine(
            $"  {command}"
        );
    }

    Console.WriteLine();
    Console.WriteLine(
        "Consulta somente leitura; nenhum arquivo foi alterado."
    );
}


static void RunMtpShellReplaceProbe()
{
    Console.WriteLine();

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine(
        " WINDOWS SHELL REPLACE PROBE - SD CARD"
    );

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine();

    Console.WriteLine(
        "Este teste reproduz a substituição feita pelo Explorer."
    );

    Console.WriteLine(
        "Somente 1: SD Card será utilizada."
    );

    Console.WriteLine(
        "7: Saves permanece intocado."
    );

    Console.WriteLine();

    var service =
        new SwitchSaveAssistant.Core.Safety
            .ShellReplaceProbeService();

    var result =
        service.Run(
            ConnectDbi,
            message =>
            {
                Console.WriteLine(
                    message
                );
            }
        );

    Console.WriteLine();

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine(
        " WINDOWS SHELL REPLACE PROBE: OK"
    );

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine();

    Console.WriteLine(
        $"Original   : {result.OriginalBytes:N0} bytes"
    );

    Console.WriteLine(
        $"Substituto : {result.ReplacementBytes:N0} bytes"
    );

    Console.WriteLine();

    Console.WriteLine(
        $"SHA original : {result.OriginalSha256}"
    );

    Console.WriteLine(
        $"SHA novo     : {result.ReplacementSha256}"
    );

    Console.WriteLine(
        $"Após crescer : {result.AfterGrowSha256}"
    );

    Console.WriteLine(
        $"Após reduzir : {result.AfterShrinkSha256}"
    );

    Console.WriteLine();

    Console.WriteLine(
        $"Limpeza      : {(result.CleanupConfirmed ? "OK" : "FALHOU")}"
    );

    Console.WriteLine();

    Console.WriteLine(
        "Nenhum save foi alterado."
    );
}


static void RunMtpSavesDropTargetCapabilities()
{
    Console.WriteLine();

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine(
        " SHELL DROP CAPABILITIES - 7: SAVES"
    );

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine();

    Console.WriteLine(
        "Probe somente de interfaces COM."
    );

    Console.WriteLine(
        "Nenhum DragEnter, DragOver ou Drop será executado."
    );

    Console.WriteLine(
        "Nenhum arquivo será enviado ao Switch."
    );

    Console.WriteLine();

    var result =
        new SwitchSaveAssistant.Core.Safety
            .ShellDropCapabilityProbeService()
            .Run(
                "Switch",
                "7: Saves"
            );

    Console.WriteLine(
        $"7: Saves expõe IDropTarget : " +
        $"{(result.DestinationDropTargetAvailable ? "SIM" : "NÃO")}"
    );

    Console.WriteLine(
        $"  HRESULT: 0x{result.DestinationDropTargetHResult:X8}"
    );

    Console.WriteLine();

    Console.WriteLine(
        $"Staging local expõe IDataObject: " +
        $"{(result.SourceDataObjectAvailable ? "SIM" : "NÃO")}"
    );

    Console.WriteLine(
        $"  HRESULT: 0x{result.SourceDataObjectHResult:X8}"
    );

    Console.WriteLine();

    Console.WriteLine(
        "Drop executado: NÃO"
    );

    Console.WriteLine(
        "Escritas em 7: Saves: 0"
    );

    if (!result.DestinationDropTargetAvailable ||
        !result.SourceDataObjectAvailable)
    {
        Environment.ExitCode = 6;
    }
}


static void RunMtpSavesDropNegotiate()
{
    Console.WriteLine();

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine(
        " SHELL DROP NEGOTIATION - 7: SAVES"
    );

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine();

    Console.WriteLine(
        "Será executado apenas DragEnter + DragLeave."
    );

    Console.WriteLine(
        "IDropTarget.Drop NÃO será chamado."
    );

    Console.WriteLine(
        "Escritas esperadas em 7: Saves: 0"
    );

    Console.WriteLine();

    var result =
        new SwitchSaveAssistant.Core.Safety
            .ShellDropNegotiationProbeService()
            .Run(
                "Switch",
                "7: Saves"
            );

    Console.WriteLine(
        $"DragEnter HRESULT : 0x{result.DragEnterHResult:X8}"
    );

    Console.WriteLine(
        $"Efeito solicitado : 0x{result.RequestedEffect:X8}"
    );

    Console.WriteLine(
        $"Efeito aceito     : 0x{result.AcceptedEffect:X8}"
    );

    Console.WriteLine(
        $"DragLeave HRESULT : 0x{result.DragLeaveHResult:X8}"
    );

    Console.WriteLine();

    Console.WriteLine(
        $"COPY aceito       : " +
        $"{((result.AcceptedEffect & 1) != 0 ? "SIM" : "NÃO")}"
    );

    Console.WriteLine(
        "Drop executado     : NÃO"
    );

    Console.WriteLine(
        "Escritas em Saves  : 0"
    );

    if (result.DragEnterHResult < 0 ||
        result.DragLeaveHResult < 0 ||
        (result.AcceptedEffect & 1) == 0)
    {
        Environment.ExitCode = 7;
    }
}


static void RunMtpSdDropReplaceProbe()
{
    Console.WriteLine();

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine(
        " SHELL DROP REPLACE PROBE - SD CARD"
    );

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine();

    Console.WriteLine(
        "ATENÇÃO: este teste executará IDropTarget.Drop de verdade."
    );

    Console.WriteLine(
        "Somente 1: SD Card será utilizada."
    );

    Console.WriteLine(
        "Uma pasta remota exclusiva e descartável será criada."
    );

    Console.WriteLine(
        "7: Saves NÃO será utilizado."
    );

    Console.WriteLine();

    var result =
        new SwitchSaveAssistant.Core.Safety
            .ShellDropReplaceProbeService()
            .Run(
                ConnectDbi,
                message =>
                {
                    Console.WriteLine(
                        message
                    );
                }
            );

    Console.WriteLine();

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine(
        " SHELL DROP REPLACE PROBE: OK"
    );

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine();

    Console.WriteLine(
        $"Original   : {result.OriginalBytes:N0} bytes"
    );

    Console.WriteLine(
        $"Substituto : {result.ReplacementBytes:N0} bytes"
    );

    Console.WriteLine();

    Console.WriteLine(
        $"SHA original : {result.OriginalSha256}"
    );

    Console.WriteLine(
        $"SHA novo     : {result.ReplacementSha256}"
    );

    Console.WriteLine(
        $"Após crescer : {result.GrownSha256}"
    );

    Console.WriteLine(
        $"Após reduzir : {result.ShrunkSha256}"
    );

    Console.WriteLine();

    Console.WriteLine(
        $"Drop #1      : 0x{result.FirstDropEffect:X8}"
    );

    Console.WriteLine(
        $"Drop #2      : 0x{result.SecondDropEffect:X8}"
    );

    Console.WriteLine();

    Console.WriteLine(
        $"Limpeza      : " +
        $"{(result.CleanupConfirmed ? "OK" : "FALHOU")}"
    );

    Console.WriteLine();

    Console.WriteLine(
        "Nenhum save foi alterado."
    );
}


static void RunMtpSdDropCreateProbe()
{
    Console.WriteLine();

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine(
        " SHELL DROP CREATE PROBE - SD CARD"
    );

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine();

    Console.WriteLine(
        "Este teste executará IDropTarget.Drop de um arquivo NOVO."
    );

    Console.WriteLine(
        "Não haverá colisão nem substituição."
    );

    Console.WriteLine(
        "Somente 1: SD Card será utilizada."
    );

    Console.WriteLine(
        "7: Saves NÃO será utilizado."
    );

    Console.WriteLine();

    var result =
        new SwitchSaveAssistant.Core.Safety
            .ShellDropCreateProbeService()
            .Run(
                ConnectDbi,
                message =>
                {
                    Console.WriteLine(
                        message
                    );
                }
            );

    Console.WriteLine();

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine(
        " SHELL DROP CREATE PROBE: OK"
    );

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine();

    Console.WriteLine(
        $"Bytes       : {result.Bytes:N0}"
    );

    Console.WriteLine(
        $"SHA origem  : {result.SourceSha256}"
    );

    Console.WriteLine(
        $"SHA remoto  : {result.RemoteSha256}"
    );

    Console.WriteLine(
        $"Drop effect : 0x{result.DropEffect:X8}"
    );

    Console.WriteLine(
        $"Limpeza     : " +
        $"{(result.CleanupConfirmed ? "OK" : "FALHOU")}"
    );

    Console.WriteLine();

    Console.WriteLine(
        "Nenhum save foi alterado."
    );
}


static void RunWpdRawCreateReplaceProbe(
    DbiMtpClient dbi
)
{
    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine(
        " WPD RAW CREATE/REPLACE PROBE - SD CARD"
    );

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine();

    Console.WriteLine(
        "Este teste ignora SOMENTE a trava de nome existente do MediaDevices."
    );

    Console.WriteLine(
        "O WPD/DBI receberá CreateObjectWithPropertiesAndData normalmente."
    );

    Console.WriteLine(
        "Somente 1: SD Card será utilizada."
    );

    Console.WriteLine(
        "7: Saves está bloqueado pelo próprio método de probe."
    );

    Console.WriteLine();

    var result =
        new SwitchSaveAssistant.Core.Safety
            .WpdRawCreateReplaceProbeService()
            .Run(
                dbi,
                message =>
                {
                    Console.WriteLine(
                        message
                    );
                }
            );

    Console.WriteLine();

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine(
        " WPD RAW CREATE/REPLACE PROBE: OK"
    );

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine();

    Console.WriteLine(
        $"Original   : {result.OriginalBytes:N0} bytes"
    );

    Console.WriteLine(
        $"Substituto : {result.ReplacementBytes:N0} bytes"
    );

    Console.WriteLine();

    Console.WriteLine(
        $"SHA original : {result.OriginalSha256}"
    );

    Console.WriteLine(
        $"SHA novo     : {result.ReplacementSha256}"
    );

    Console.WriteLine(
        $"Após crescer : {result.AfterGrowSha256}"
    );

    Console.WriteLine(
        $"Após reduzir : {result.AfterShrinkSha256}"
    );

    Console.WriteLine();

    Console.WriteLine(
        $"Objetos após crescer : {result.MatchingFilesAfterGrow}"
    );

    Console.WriteLine(
        $"Objetos após reduzir : {result.MatchingFilesAfterShrink}"
    );

    Console.WriteLine(
        $"Limpeza              : " +
        $"{(result.CleanupConfirmed ? "OK" : "FALHOU")}"
    );

    Console.WriteLine();

    Console.WriteLine(
        "Nenhum save foi alterado."
    );
}


static void RunMtpSdAsyncDropCreateProbe()
{
    Console.WriteLine();

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine(
        " ASYNC SHELL DROP CREATE PROBE - SD CARD"
    );

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine();

    Console.WriteLine(
        "IDataObjectAsyncCapability será habilitado quando disponível."
    );

    Console.WriteLine(
        "O data object permanecerá vivo durante a operação assíncrona."
    );

    Console.WriteLine(
        "Somente 1: SD Card será utilizada."
    );

    Console.WriteLine(
        "7: Saves NÃO será utilizado."
    );

    Console.WriteLine();

    var result =
        new SwitchSaveAssistant.Core.Safety
            .ShellAsyncDropCreateProbeService()
            .Run(
                ConnectDbi,
                Console.WriteLine
            );

    Console.WriteLine();

    Console.WriteLine(
        "ASYNC SHELL DROP CREATE PROBE: OK"
    );

    Console.WriteLine(
        $"Bytes      : {result.Bytes:N0}"
    );

    Console.WriteLine(
        $"SHA origem : {result.SourceSha256}"
    );

    Console.WriteLine(
        $"SHA remoto : {result.RemoteSha256}"
    );

    Console.WriteLine(
        $"Limpeza    : {(result.CleanupConfirmed ? "OK" : "FALHOU")}"
    );

    Console.WriteLine();

    Console.WriteLine(
        "Nenhum save foi alterado."
    );
}


static void RunMtpSdAsyncDropReplaceProbe()
{
    Console.WriteLine();

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine(
        " ASYNC SHELL DROP REPLACE PROBE - SD CARD"
    );

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine();

    Console.WriteLine(
        "Este teste substituirá um arquivo descartável existente."
    );

    Console.WriteLine(
        "Teste 1: arquivo menor → arquivo maior."
    );

    Console.WriteLine(
        "Teste 2: arquivo maior → arquivo menor."
    );

    Console.WriteLine(
        "Somente 1: SD Card será utilizada."
    );

    Console.WriteLine(
        "7: Saves NÃO será utilizado."
    );

    Console.WriteLine();

    var result =
        new SwitchSaveAssistant.Core.Safety
            .ShellAsyncDropReplaceProbeService()
            .Run(
                ConnectDbi,
                Console.WriteLine
            );

    Console.WriteLine();

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine(
        " ASYNC SHELL DROP REPLACE PROBE: OK"
    );

    Console.WriteLine(
        "=============================================="
    );

    Console.WriteLine();

    Console.WriteLine(
        $"Original   : {result.OriginalBytes:N0} bytes"
    );

    Console.WriteLine(
        $"Substituto : {result.ReplacementBytes:N0} bytes"
    );

    Console.WriteLine();

    Console.WriteLine(
        $"SHA original : {result.OriginalSha256}"
    );

    Console.WriteLine(
        $"SHA novo     : {result.ReplacementSha256}"
    );

    Console.WriteLine(
        $"Após crescer : {result.GrownSha256}"
    );

    Console.WriteLine(
        $"Após reduzir : {result.ShrunkSha256}"
    );

    Console.WriteLine();

    Console.WriteLine(
        $"Drop #1 : 0x{result.FirstDropEffect:X8}"
    );

    Console.WriteLine(
        $"Drop #2 : 0x{result.SecondDropEffect:X8}"
    );

    Console.WriteLine(
        $"Limpeza : {(result.CleanupConfirmed ? "OK" : "FALHOU")}"
    );

    Console.WriteLine();

    Console.WriteLine(
        "Nenhum save foi alterado."
    );
}


static void RunMtpSdAsyncTreeProbe()
{
    Console.WriteLine();

    Console.WriteLine(
        "=================================================="
    );

    Console.WriteLine(
        " ASYNC SHELL DROP TREE PROBE - SD CARD"
    );

    Console.WriteLine(
        "=================================================="
    );

    Console.WriteLine();

    Console.WriteLine(
        "Este teste imita a estrutura de um restore real."
    );

    Console.WriteLine();

    Console.WriteLine(
        "Será testado:"
    );

    Console.WriteLine(
        "  - merge da pasta payload;"
    );

    Console.WriteLine(
        "  - overwrite com crescimento;"
    );

    Console.WriteLine(
        "  - overwrite com redução;"
    );

    Console.WriteLine(
        "  - criação de novo arquivo;"
    );

    Console.WriteLine(
        "  - criação de novo subdiretório;"
    );

    Console.WriteLine(
        "  - verificação EXATA por tamanho + SHA-256."
    );

    Console.WriteLine();

    Console.WriteLine(
        "Somente 1: SD Card será utilizada."
    );

    Console.WriteLine(
        "7: Saves NÃO será utilizado."
    );

    Console.WriteLine();

    Console.WriteLine(
        "ATENÇÃO:"
    );

    Console.WriteLine(
        "Se o Windows perguntar sobre merge/substituição,"
    );

    Console.WriteLine(
        "confirme SOMENTE para a pasta payload deste probe."
    );

    Console.WriteLine();

    var result =
        new SwitchSaveAssistant.Core.Safety
            .ShellAsyncTreeDropProbeService()
            .Run(
                ConnectDbi,
                Console.WriteLine
            );

    Console.WriteLine();

    Console.WriteLine(
        "=================================================="
    );

    Console.WriteLine(
        " ASYNC SHELL DROP TREE PROBE: OK"
    );

    Console.WriteLine(
        "=================================================="
    );

    Console.WriteLine();

    Console.WriteLine(
        $"Arquivos : {result.FileCount}"
    );

    Console.WriteLine(
        $"Bytes    : {result.TotalBytes:N0}"
    );

    Console.WriteLine(
        $"Drop     : 0x{result.DropEffect:X8}"
    );

    Console.WriteLine(
        $"Limpeza  : {(result.CleanupConfirmed ? "OK" : "FALHOU")}"
    );

    Console.WriteLine();

    foreach (var file
             in result.Files)
    {
        Console.WriteLine(
            $"  {file.Sha256}  {file.RelativePath}"
        );
    }

    Console.WriteLine();

    Console.WriteLine(
        "Nenhum save foi alterado."
    );
}
