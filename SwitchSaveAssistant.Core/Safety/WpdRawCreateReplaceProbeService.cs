using SwitchSaveAssistant.Core.Hashing;
using SwitchSaveAssistant.Core.Models;
using SwitchSaveAssistant.Core.Mtp;

namespace SwitchSaveAssistant.Core.Safety;

public sealed class WpdRawCreateReplaceProbeService
{
    private readonly Sha256Service
        _sha256Service = new();

    public WpdRawCreateReplaceProbeResult Run(
        DbiMtpClient client,
        Action<string>? progress = null
    )
    {
        ArgumentNullException.ThrowIfNull(
            client
        );

        var probeId =
            DateTime.Now.ToString(
                "yyyyMMdd-HHmmss"
            )
            + "-"
            + Guid.NewGuid()
                .ToString("N")[..8];

        var sdRoot =
            client.GetDriveRootPath(
                "1: SD Card"
            );

        var remoteDirectory =
            CombineRemotePath(
                sdRoot,
                "SwitchSaveAssistant-Probe",
                $"raw-create-{probeId}"
            );

        var remoteFile =
            CombineRemotePath(
                remoteDirectory,
                "target.bin"
            );

        var localRoot =
            Directory
                .CreateTempSubdirectory(
                    "SwitchSaveAssistant-RawCreate-"
                )
                .FullName;

        var localFile =
            Path.Combine(
                localRoot,
                "target.bin"
            );

        var downloadedFile =
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
            localFile,
            original
        );

        var originalSha =
            _sha256Service.CalculateFile(
                localFile
            );

        File.WriteAllBytes(
            localFile,
            replacement
        );

        var replacementSha =
            _sha256Service.CalculateFile(
                localFile
            );

        File.WriteAllBytes(
            localFile,
            original
        );

        var completed =
            false;

        try
        {
            progress?.Invoke(
                "Criando área remota descartável..."
            );

            client.CreateDirectory(
                remoteDirectory
            );

            progress?.Invoke(
                "Criando target.bin ORIGINAL pelo caminho normal..."
            );

            client.UploadNewFile(
                localFile,
                remoteFile
            );

            VerifyRemote(
                client,
                remoteFile,
                downloadedFile,
                originalSha,
                original.LongLength,
                "ORIGINAL"
            );

            progress?.Invoke(
                "ORIGINAL confirmado por tamanho + SHA-256."
            );

            File.WriteAllBytes(
                localFile,
                replacement
            );

            progress?.Invoke(
                "Executando CreateObjectWithPropertiesAndData bruto " +
                "com o MESMO nome..."
            );

            client.RawCreateFileAllowExistingForProbe(
                localFile,
                remoteFile
            );

            var matchingAfterGrow =
                CountTargetFiles(
                    client,
                    remoteDirectory
                );

            progress?.Invoke(
                $"Objetos target.bin após crescimento: " +
                $"{matchingAfterGrow}"
            );

            if (matchingAfterGrow != 1)
            {
                throw new IOException(
                    "O raw-create não produziu uma substituição " +
                    $"1:1. Objetos encontrados: {matchingAfterGrow}."
                );
            }

            var grownSha =
                VerifyRemote(
                    client,
                    remoteFile,
                    downloadedFile,
                    replacementSha,
                    replacement.LongLength,
                    "VERSÃO MAIOR"
                );

            progress?.Invoke(
                "Crescimento confirmado por tamanho + SHA-256."
            );

            File.WriteAllBytes(
                localFile,
                original
            );

            progress?.Invoke(
                "Executando raw-create novamente com versão MENOR..."
            );

            client.RawCreateFileAllowExistingForProbe(
                localFile,
                remoteFile
            );

            var matchingAfterShrink =
                CountTargetFiles(
                    client,
                    remoteDirectory
                );

            progress?.Invoke(
                $"Objetos target.bin após redução: " +
                $"{matchingAfterShrink}"
            );

            if (matchingAfterShrink != 1)
            {
                throw new IOException(
                    "O segundo raw-create não produziu uma " +
                    $"substituição 1:1. Objetos encontrados: " +
                    $"{matchingAfterShrink}."
                );
            }

            var shrunkSha =
                VerifyRemote(
                    client,
                    remoteFile,
                    downloadedFile,
                    originalSha,
                    original.LongLength,
                    "VERSÃO MENOR"
                );

            progress?.Invoke(
                "Redução confirmada por tamanho + SHA-256."
            );

            progress?.Invoke(
                "Removendo área remota descartável..."
            );

            client.DeleteDirectory(
                remoteDirectory,
                recursive: true
            );

            var cleanupConfirmed =
                !client.DirectoryExists(
                    remoteDirectory
                );

            if (!cleanupConfirmed)
            {
                throw new IOException(
                    "A limpeza remota não pôde ser confirmada."
                );
            }

            completed =
                true;

            return new WpdRawCreateReplaceProbeResult(
                remoteDirectory,
                original.LongLength,
                replacement.LongLength,
                originalSha,
                replacementSha,
                grownSha,
                shrunkSha,
                matchingAfterGrow,
                matchingAfterShrink,
                cleanupConfirmed
            );
        }
        finally
        {
            if (!completed)
            {
                try
                {
                    if (client.DirectoryExists(
                            remoteDirectory
                        ))
                    {
                        progress?.Invoke(
                            "Limpando diretório remoto após falha..."
                        );

                        client.DeleteDirectory(
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

    private int CountTargetFiles(
        DbiMtpClient client,
        string remoteDirectory
    )
    {
        return client
            .EnumerateFiles(
                remoteDirectory
            )
            .Count(
                file =>
                    string.Equals(
                        file.RelativePath
                            .Replace(
                                '\\',
                                '/'
                            ),
                        "target.bin",
                        StringComparison.OrdinalIgnoreCase
                    )
            );
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

        var actualSha =
            _sha256Service.CalculateFile(
                downloadedFile
            );

        if (!string.Equals(
                actualSha,
                expectedSha256,
                StringComparison.OrdinalIgnoreCase
            ))
        {
            throw new IOException(
                $"{label}: SHA-256 divergente. " +
                $"Esperado={expectedSha256}; " +
                $"Atual={actualSha}."
            );
        }

        return actualSha;
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
        var result =
            parts[0]
                .TrimEnd(
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
