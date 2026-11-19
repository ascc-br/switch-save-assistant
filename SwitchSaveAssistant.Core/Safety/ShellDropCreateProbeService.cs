using SwitchSaveAssistant.Core.Hashing;
using SwitchSaveAssistant.Core.Models;
using SwitchSaveAssistant.Core.Mtp;
using SwitchSaveAssistant.Core.Providers;

namespace SwitchSaveAssistant.Core.Safety;

public sealed class ShellDropCreateProbeService
{
    private readonly Sha256Service
        _sha256Service = new();

    private readonly ShellMtpDropProvider
        _dropProvider = new();

    public ShellDropCreateProbeResult Run(
        Func<DbiMtpClient> connectDbi,
        Action<string>? progress = null
    )
    {
        ArgumentNullException.ThrowIfNull(
            connectDbi
        );

        var probeId =
            DateTime.Now.ToString(
                "yyyyMMdd-HHmmss"
            )
            + "-"
            + Guid.NewGuid()
                .ToString("N")[..8];

        var probeFolderName =
            $"drop-create-{probeId}";

        var localRoot =
            Directory
                .CreateTempSubdirectory(
                    "SwitchSaveAssistant-SdDropCreate-"
                )
                .FullName;

        var localFile =
            Path.Combine(
                localRoot,
                "created.bin"
            );

        var downloadedFile =
            Path.Combine(
                localRoot,
                "downloaded.bin"
            );

        var payload =
            CreatePattern(
                8197,
                47
            );

        File.WriteAllBytes(
            localFile,
            payload
        );

        var sourceSha =
            _sha256Service.CalculateFile(
                localFile
            );

        string remoteDirectory =
            string.Empty;

        string remoteFile =
            string.Empty;

        var completed =
            false;

        try
        {
            progress?.Invoke(
                "Criando pasta remota vazia via MediaDevices..."
            );

            using (var dbi =
                   connectDbi())
            {
                var sdRoot =
                    dbi.GetDriveRootPath(
                        "1: SD Card"
                    );

                remoteDirectory =
                    CombineRemotePath(
                        sdRoot,
                        "SwitchSaveAssistant-Probe",
                        probeFolderName
                    );

                remoteFile =
                    CombineRemotePath(
                        remoteDirectory,
                        "created.bin"
                    );

                dbi.CreateDirectory(
                    remoteDirectory
                );

                if (dbi.FileExists(
                        remoteFile
                    ))
                {
                    throw new IOException(
                        "O arquivo de teste já existe antes do Drop."
                    );
                }
            }

            progress?.Invoke(
                "Pasta criada. MediaDevices desconectado."
            );

            Thread.Sleep(
                750
            );

            progress?.Invoke(
                "Executando Shell Drop de arquivo NOVO..."
            );

            var drop =
                _dropProvider
                    .DropLocalItemToStorageFolder(
                        "Switch",
                        "1: SD Card",
                        localFile,
                        "SwitchSaveAssistant-Probe",
                        probeFolderName
                    );

            progress?.Invoke(
                $"Drop retornou efeito " +
                $"0x{drop.AcceptedDropEffect:X8}."
            );

            progress?.Invoke(
                "Verificando arquivo remoto..."
            );

            string remoteSha;
            bool cleanupConfirmed;

            using (var dbi =
                   connectDbi())
            {
                if (!dbi.FileExists(
                        remoteFile
                    ))
                {
                    throw new IOException(
                        "O arquivo novo não apareceu no dispositivo."
                    );
                }

                dbi.DownloadFile(
                    remoteFile,
                    downloadedFile
                );

                var remoteBytes =
                    new FileInfo(
                        downloadedFile
                    ).Length;

                if (remoteBytes !=
                    payload.LongLength)
                {
                    throw new IOException(
                        $"Tamanho divergente. " +
                        $"Esperado={payload.LongLength}; " +
                        $"Atual={remoteBytes}."
                    );
                }

                remoteSha =
                    _sha256Service.CalculateFile(
                        downloadedFile
                    );

                if (!string.Equals(
                        sourceSha,
                        remoteSha,
                        StringComparison.OrdinalIgnoreCase
                    ))
                {
                    throw new IOException(
                        $"SHA-256 divergente. " +
                        $"Esperado={sourceSha}; " +
                        $"Atual={remoteSha}."
                    );
                }

                progress?.Invoke(
                    "Arquivo novo confirmado por tamanho + SHA-256."
                );

                progress?.Invoke(
                    "Removendo pasta remota descartável..."
                );

                dbi.DeleteDirectory(
                    remoteDirectory,
                    recursive: true
                );

                cleanupConfirmed =
                    !dbi.DirectoryExists(
                        remoteDirectory
                    );

                if (!cleanupConfirmed)
                {
                    throw new IOException(
                        "A limpeza remota não pôde ser confirmada."
                    );
                }
            }

            completed =
                true;

            return new ShellDropCreateProbeResult(
                remoteDirectory,
                payload.LongLength,
                sourceSha,
                remoteSha,
                drop.AcceptedDropEffect,
                cleanupConfirmed
            );
        }
        finally
        {
            if (!completed &&
                !string.IsNullOrWhiteSpace(
                    remoteDirectory
                ))
            {
                try
                {
                    Thread.Sleep(
                        750
                    );

                    using var cleanupDbi =
                        connectDbi();

                    if (cleanupDbi.DirectoryExists(
                            remoteDirectory
                        ))
                    {
                        progress?.Invoke(
                            "Limpando diretório remoto após falha..."
                        );

                        cleanupDbi.DeleteDirectory(
                            remoteDirectory,
                            recursive: true
                        );
                    }
                }
                catch
                {
                    // Não mascara o erro principal.
                }
            }

            if (Directory.Exists(
                    localRoot
                ))
            {
                try
                {
                    Directory.Delete(
                        localRoot,
                        recursive: true
                    );
                }
                catch
                {
                    // Local descartável.
                }
            }
        }
    }

    private static byte[] CreatePattern(
        int length,
        int seed
    )
    {
        var result =
            new byte[length];

        for (var index = 0;
             index < result.Length;
             index++)
        {
            result[index] =
                (byte)(
                    (
                        index * 37 +
                        seed
                    ) %
                    251
                );
        }

        return result;
    }

    private static string CombineRemotePath(
        params string[] parts
    )
    {
        if (parts.Length == 0)
        {
            throw new ArgumentException(
                "Nenhuma parte de caminho fornecida."
            );
        }

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
}
