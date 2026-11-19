using SwitchSaveAssistant.Core.Hashing;
using SwitchSaveAssistant.Core.Models;
using SwitchSaveAssistant.Core.Mtp;
using SwitchSaveAssistant.Core.Providers;

namespace SwitchSaveAssistant.Core.Safety;

public sealed class ShellAsyncDropReplaceProbeService
{
    private readonly Sha256Service
        _sha256Service = new();

    private readonly ShellAsyncMtpDropProvider
        _dropProvider = new();

    public ShellDropReplaceProbeResult Run(
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
            $"drop-replace-{probeId}";

        var localRoot =
            Directory
                .CreateTempSubdirectory(
                    "SwitchSaveAssistant-SdDropReplace-"
                )
                .FullName;

        var localTarget =
            Path.Combine(
                localRoot,
                "target.bin"
            );

        var downloaded =
            Path.Combine(
                localRoot,
                "downloaded.bin"
            );

        var original =
            CreatePattern(
                570,
                17
            );

        var replacement =
            CreatePattern(
                16_445,
                83
            );

        File.WriteAllBytes(
            localTarget,
            original
        );

        var originalSha =
            _sha256Service.CalculateFile(
                localTarget
            );

        File.WriteAllBytes(
            localTarget,
            replacement
        );

        var replacementSha =
            _sha256Service.CalculateFile(
                localTarget
            );

        // Voltamos para ORIGINAL antes de preparar
        // o arquivo inicial no dispositivo.
        File.WriteAllBytes(
            localTarget,
            original
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
                "Preparando arquivo ORIGINAL via MediaDevices..."
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
                        "target.bin"
                    );

                dbi.CreateDirectory(
                    remoteDirectory
                );

                dbi.UploadNewFile(
                    localTarget,
                    remoteFile
                );

                VerifyRemote(
                    dbi,
                    remoteFile,
                    downloaded,
                    originalSha,
                    original.LongLength,
                    "ORIGINAL"
                );
            }

            progress?.Invoke(
                "ORIGINAL confirmado. MediaDevices desconectado."
            );

            Thread.Sleep(
                750
            );

            File.WriteAllBytes(
                localTarget,
                replacement
            );

            progress?.Invoke(
                "Shell Drop: substituindo ORIGINAL pela versão MAIOR..."
            );

            var firstDrop =
                _dropProvider
                    .DropNewFileForProbe(
                        "Switch",
                        "1: SD Card",
                        localTarget,
                        new[]
                        {
                            "SwitchSaveAssistant-Probe",
                            probeFolderName
                        }
                    );

            progress?.Invoke(
                $"Async capability #1: " +
                $"{(firstDrop.AsyncCapabilityAvailable ? "SIM" : "NÃO")}"
            );

            progress?.Invoke(
                $"Async mode #1      : " +
                $"{(firstDrop.AsyncModeEnabled ? "SIM" : "NÃO")}"
            );

            progress?.Invoke(
                $"SetAsyncMode #1 HR : 0x{firstDrop.SetAsyncModeHResult:X8}"
            );

            progress?.Invoke(
                $"GetAsyncMode #1 HR : 0x{firstDrop.GetAsyncModeHResult:X8}"
            );

            progress?.Invoke(
                $"InOperation #1 HR  : 0x{firstDrop.InOperationHResult:X8}"
            );

            progress?.Invoke(
                $"InOperation #1     : " +
                $"{(firstDrop.WasAsyncOperationObserved ? "OBSERVADO" : "NÃO OBSERVADO")}"
            );

            progress?.Invoke(
                $"Primeiro Drop retornou efeito " +
                $"0x{firstDrop.DropEffect:X8}."
            );

            progress?.Invoke(
                "Verificando versão MAIOR via MediaDevices..."
            );

            string grownSha;

            using (var dbi =
                   connectDbi())
            {
                grownSha =
                    VerifyRemote(
                        dbi,
                        remoteFile,
                        downloaded,
                        replacementSha,
                        replacement.LongLength,
                        "VERSÃO MAIOR"
                    );
            }

            progress?.Invoke(
                "Crescimento confirmado por tamanho + SHA-256."
            );

            Thread.Sleep(
                750
            );

            File.WriteAllBytes(
                localTarget,
                original
            );

            progress?.Invoke(
                "Shell Drop: substituindo novamente pela versão MENOR..."
            );

            var secondDrop =
                _dropProvider
                    .DropNewFileForProbe(
                        "Switch",
                        "1: SD Card",
                        localTarget,
                        new[]
                        {
                            "SwitchSaveAssistant-Probe",
                            probeFolderName
                        }
                    );

            progress?.Invoke(
                $"Async capability #2: " +
                $"{(secondDrop.AsyncCapabilityAvailable ? "SIM" : "NÃO")}"
            );

            progress?.Invoke(
                $"Async mode #2      : " +
                $"{(secondDrop.AsyncModeEnabled ? "SIM" : "NÃO")}"
            );

            progress?.Invoke(
                $"SetAsyncMode #2 HR : 0x{secondDrop.SetAsyncModeHResult:X8}"
            );

            progress?.Invoke(
                $"GetAsyncMode #2 HR : 0x{secondDrop.GetAsyncModeHResult:X8}"
            );

            progress?.Invoke(
                $"InOperation #2 HR  : 0x{secondDrop.InOperationHResult:X8}"
            );

            progress?.Invoke(
                $"InOperation #2     : " +
                $"{(secondDrop.WasAsyncOperationObserved ? "OBSERVADO" : "NÃO OBSERVADO")}"
            );

            progress?.Invoke(
                $"Segundo Drop retornou efeito " +
                $"0x{secondDrop.DropEffect:X8}."
            );

            progress?.Invoke(
                "Verificando versão MENOR via MediaDevices..."
            );

            string shrunkSha;
            bool cleanupConfirmed;

            using (var dbi =
                   connectDbi())
            {
                shrunkSha =
                    VerifyRemote(
                        dbi,
                        remoteFile,
                        downloaded,
                        originalSha,
                        original.LongLength,
                        "VERSÃO MENOR"
                    );

                progress?.Invoke(
                    "Redução confirmada por tamanho + SHA-256."
                );

                progress?.Invoke(
                    "Removendo diretório remoto descartável..."
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
                        "A limpeza da pasta remota não pôde ser confirmada."
                    );
                }
            }

            var result =
                new ShellDropReplaceProbeResult(
                    remoteDirectory,
                    original.LongLength,
                    replacement.LongLength,
                    originalSha,
                    replacementSha,
                    grownSha,
                    shrunkSha,
                    firstDrop.DropEffect,
                    secondDrop.DropEffect,
                    cleanupConfirmed
                );

            completed =
                true;

            return result;
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
                    // Nunca mascara o erro principal.
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

    private string VerifyRemote(
        DbiMtpClient client,
        string remoteFile,
        string downloadedFile,
        string expectedSha256,
        long expectedBytes,
        string label
    )
    {
        if (File.Exists(
                downloadedFile
            ))
        {
            File.Delete(
                downloadedFile
            );
        }

        client.DownloadFile(
            remoteFile,
            downloadedFile
        );

        var actualBytes =
            new FileInfo(
                downloadedFile
            ).Length;

        if (actualBytes !=
            expectedBytes)
        {
            throw new IOException(
                $"{label}: tamanho divergente. " +
                $"Esperado={expectedBytes}; " +
                $"Atual={actualBytes}."
            );
        }

        var actualSha256 =
            _sha256Service.CalculateFile(
                downloadedFile
            );

        if (!string.Equals(
                actualSha256,
                expectedSha256,
                StringComparison.OrdinalIgnoreCase
            ))
        {
            throw new IOException(
                $"{label}: SHA-256 divergente. " +
                $"Esperado={expectedSha256}; " +
                $"Atual={actualSha256}."
            );
        }

        return actualSha256;
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
                        index * 31 +
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
