using SwitchSaveAssistant.Core.Hashing;
using SwitchSaveAssistant.Core.Models;
using SwitchSaveAssistant.Core.Mtp;
using SwitchSaveAssistant.Core.Providers;

namespace SwitchSaveAssistant.Core.Safety;

public sealed class ShellAsyncTreeDropProbeService
{
    private readonly Sha256Service
        _sha256Service = new();

    private readonly ShellAsyncMtpDropProvider
        _dropProvider = new();

    public ShellTreeDropProbeResult Run(
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
            $"tree-{probeId}";

        var localRoot =
            Directory
                .CreateTempSubdirectory(
                    "SwitchSaveAssistant-TreeProbe-"
                )
                .FullName;

        var localPayload =
            Path.Combine(
                localRoot,
                "payload"
            );

        var localNested =
            Path.Combine(
                localPayload,
                "nested"
            );

        var localDeeper =
            Path.Combine(
                localNested,
                "deeper"
            );

        var localFreshDirectory =
            Path.Combine(
                localPayload,
                "fresh-dir"
            );

        Directory.CreateDirectory(
            localDeeper
        );

        Directory.CreateDirectory(
            localFreshDirectory
        );

        /*
         * ESTADO FINAL desejado:
         *
         * payload/
         * ├── root-grow.bin           16.445 bytes
         * ├── nested/
         * │   ├── shrink.bin             570 bytes
         * │   └── deeper/
         * │       └── new.bin          8.197 bytes
         * └── fresh-dir/
         *     └── fresh.bin            4.099 bytes
         *
         * O remoto inicial terá:
         *
         * root-grow.bin = 570 bytes
         * shrink.bin    = 16.445 bytes
         *
         * Assim comprovamos crescimento + redução +
         * criação de arquivo + criação de diretório.
         */

        var finalRootGrow =
            Path.Combine(
                localPayload,
                "root-grow.bin"
            );

        var finalShrink =
            Path.Combine(
                localNested,
                "shrink.bin"
            );

        var finalNew =
            Path.Combine(
                localDeeper,
                "new.bin"
            );

        var finalFresh =
            Path.Combine(
                localFreshDirectory,
                "fresh.bin"
            );

        WritePattern(
            finalRootGrow,
            16_445,
            101
        );

        WritePattern(
            finalShrink,
            570,
            113
        );

        WritePattern(
            finalNew,
            8_197,
            127
        );

        WritePattern(
            finalFresh,
            4_099,
            139
        );

        var expected =
            BuildManifest(
                localPayload
            );

        var initialRootGrow =
            Path.Combine(
                localRoot,
                "initial-root-grow.bin"
            );

        var initialShrink =
            Path.Combine(
                localRoot,
                "initial-shrink.bin"
            );

        WritePattern(
            initialRootGrow,
            570,
            17
        );

        WritePattern(
            initialShrink,
            16_445,
            83
        );

        var initialRootGrowSha =
            _sha256Service.CalculateFile(
                initialRootGrow
            );

        var initialShrinkSha =
            _sha256Service.CalculateFile(
                initialShrink
            );

        var downloaded =
            Path.Combine(
                localRoot,
                "__downloaded.bin"
            );

        string remoteProbeRoot =
            string.Empty;

        string remotePayload =
            string.Empty;

        var completed =
            false;

        try
        {
            progress?.Invoke(
                "Preparando árvore remota ORIGINAL via MediaDevices..."
            );

            using (var dbi =
                   connectDbi())
            {
                var sdRoot =
                    dbi.GetDriveRootPath(
                        "1: SD Card"
                    );

                remoteProbeRoot =
                    CombineRemotePath(
                        sdRoot,
                        "SwitchSaveAssistant-Probe",
                        probeFolderName
                    );

                remotePayload =
                    CombineRemotePath(
                        remoteProbeRoot,
                        "payload"
                    );

                var remoteNested =
                    CombineRemotePath(
                        remotePayload,
                        "nested"
                    );

                var remoteDeeper =
                    CombineRemotePath(
                        remoteNested,
                        "deeper"
                    );

                dbi.CreateDirectory(
                    remoteDeeper
                );

                var remoteRootGrow =
                    CombineRemotePath(
                        remotePayload,
                        "root-grow.bin"
                    );

                var remoteShrink =
                    CombineRemotePath(
                        remoteNested,
                        "shrink.bin"
                    );

                dbi.UploadNewFile(
                    initialRootGrow,
                    remoteRootGrow
                );

                dbi.UploadNewFile(
                    initialShrink,
                    remoteShrink
                );

                VerifyOne(
                    dbi,
                    remoteRootGrow,
                    downloaded,
                    570,
                    initialRootGrowSha,
                    "ORIGINAL root-grow.bin"
                );

                VerifyOne(
                    dbi,
                    remoteShrink,
                    downloaded,
                    16_445,
                    initialShrinkSha,
                    "ORIGINAL nested/shrink.bin"
                );

                var initialFiles =
                    dbi.EnumerateFiles(
                        remotePayload
                    );

                if (initialFiles.Count != 2)
                {
                    throw new IOException(
                        "Estado remoto inicial inesperado. " +
                        $"Esperado=2 arquivos; Atual={initialFiles.Count}."
                    );
                }
            }

            progress?.Invoke(
                "Árvore ORIGINAL confirmada. MediaDevices desconectado."
            );

            progress?.Invoke(
                ""
            );

            progress?.Invoke(
                "Executando Shell Drop da pasta payload INTEIRA..."
            );

            progress?.Invoke(
                "Se o Windows perguntar, confirme SOMENTE o merge/substituição desta pasta de teste."
            );

            var drop =
                _dropProvider
                    .DropNewFileForProbe(
                        "Switch",
                        "1: SD Card",
                        localPayload,
                        new[]
                        {
                            "SwitchSaveAssistant-Probe",
                            probeFolderName
                        },
                        keepAliveMilliseconds:
                            60_000
                    );

            progress?.Invoke(
                $"Drop effect          : 0x{drop.DropEffect:X8}"
            );

            progress?.Invoke(
                $"Async capability     : " +
                $"{(drop.AsyncCapabilityAvailable ? "SIM" : "NÃO")}"
            );

            progress?.Invoke(
                $"Async mode           : " +
                $"{(drop.AsyncModeEnabled ? "SIM" : "NÃO")}"
            );

            progress?.Invoke(
                $"SetAsyncMode HR      : 0x{drop.SetAsyncModeHResult:X8}"
            );

            progress?.Invoke(
                $"GetAsyncMode HR      : 0x{drop.GetAsyncModeHResult:X8}"
            );

            progress?.Invoke(
                $"InOperation HR       : 0x{drop.InOperationHResult:X8}"
            );

            progress?.Invoke(
                $"InOperation observado: " +
                $"{(drop.WasAsyncOperationObserved ? "SIM" : "NÃO")}"
            );

            if ((drop.DropEffect & 0x00000001) == 0)
            {
                throw new IOException(
                    $"O Shell não confirmou DROPEFFECT_COPY. " +
                    $"Effect=0x{drop.DropEffect:X8}."
                );
            }

            progress?.Invoke(
                ""
            );

            progress?.Invoke(
                "Verificando árvore final via MediaDevices..."
            );

            IReadOnlyList<ManifestEntry>
                verifiedFiles;

            using (var dbi =
                   connectDbi())
            {
                verifiedFiles =
                    VerifyExactTree(
                        dbi,
                        remotePayload,
                        expected,
                        downloaded,
                        progress
                    );

                progress?.Invoke(
                    "Árvore final corresponde EXATAMENTE ao payload local."
                );

                progress?.Invoke(
                    "Removendo árvore remota descartável..."
                );

                dbi.DeleteDirectory(
                    remoteProbeRoot,
                    recursive: true
                );

                if (dbi.DirectoryExists(
                        remoteProbeRoot
                    ))
                {
                    throw new IOException(
                        "A limpeza remota não pôde ser confirmada."
                    );
                }
            }

            completed =
                true;

            var totalBytes =
                verifiedFiles.Aggregate(
                    0UL,
                    (
                        total,
                        file
                    ) =>
                        total +
                        file.LengthBytes
                );

            return new ShellTreeDropProbeResult(
                remoteProbeRoot,
                verifiedFiles.Count,
                totalBytes,
                drop.DropEffect,
                true,
                verifiedFiles
            );
        }
        finally
        {
            if (!completed &&
                !string.IsNullOrWhiteSpace(
                    remoteProbeRoot
                ))
            {
                try
                {
                    progress?.Invoke(
                        "Limpando árvore remota após falha..."
                    );

                    using var dbi =
                        connectDbi();

                    if (dbi.DirectoryExists(
                            remoteProbeRoot
                        ))
                    {
                        dbi.DeleteDirectory(
                            remoteProbeRoot,
                            recursive: true
                        );
                    }
                }
                catch
                {
                    // Não mascara a falha original.
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
                    // Diretório local descartável.
                }
            }
        }
    }

    private IReadOnlyList<ManifestEntry>
        VerifyExactTree(
            DbiMtpClient dbi,
            string remotePayload,
            IReadOnlyList<ManifestEntry> expected,
            string downloaded,
            Action<string>? progress
        )
    {
        var expectedByPath =
            expected.ToDictionary(
                entry =>
                    NormalizeRelativePath(
                        entry.RelativePath
                    ),
                StringComparer.OrdinalIgnoreCase
            );

        var remoteFiles =
            dbi.EnumerateFiles(
                remotePayload
            );

        var remoteByPath =
            remoteFiles.ToDictionary(
                file =>
                    NormalizeRelativePath(
                        file.RelativePath
                    ),
                StringComparer.OrdinalIgnoreCase
            );

        var missing =
            expectedByPath.Keys
                .Except(
                    remoteByPath.Keys,
                    StringComparer.OrdinalIgnoreCase
                )
                .OrderBy(
                    path => path,
                    StringComparer.OrdinalIgnoreCase
                )
                .ToArray();

        var unexpected =
            remoteByPath.Keys
                .Except(
                    expectedByPath.Keys,
                    StringComparer.OrdinalIgnoreCase
                )
                .OrderBy(
                    path => path,
                    StringComparer.OrdinalIgnoreCase
                )
                .ToArray();

        if (missing.Length > 0 ||
            unexpected.Length > 0)
        {
            throw new IOException(
                "Conjunto final de arquivos divergente. " +
                $"Ausentes=[{string.Join(", ", missing)}]; " +
                $"Inesperados=[{string.Join(", ", unexpected)}]."
            );
        }

        if (remoteByPath.Count !=
            expectedByPath.Count)
        {
            throw new IOException(
                "Quantidade final de arquivos divergente. " +
                $"Esperado={expectedByPath.Count}; " +
                $"Atual={remoteByPath.Count}."
            );
        }

        var verified =
            new List<ManifestEntry>();

        foreach (var expectedFile
                 in expected
                     .OrderBy(
                         file =>
                             file.RelativePath,
                         StringComparer.OrdinalIgnoreCase
                     ))
        {
            var relative =
                NormalizeRelativePath(
                    expectedFile.RelativePath
                );

            var remote =
                remoteByPath[relative];

            if (remote.LengthBytes !=
                expectedFile.LengthBytes)
            {
                throw new IOException(
                    $"{relative}: tamanho divergente. " +
                    $"Esperado={expectedFile.LengthBytes}; " +
                    $"Atual={remote.LengthBytes}."
                );
            }

            if (File.Exists(
                    downloaded
                ))
            {
                File.Delete(
                    downloaded
                );
            }

            dbi.DownloadFile(
                remote.RemotePath,
                downloaded
            );

            var actualSha =
                _sha256Service.CalculateFile(
                    downloaded
                );

            if (!string.Equals(
                    actualSha,
                    expectedFile.Sha256,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                throw new IOException(
                    $"{relative}: SHA-256 divergente. " +
                    $"Esperado={expectedFile.Sha256}; " +
                    $"Atual={actualSha}."
                );
            }

            progress?.Invoke(
                $"OK  {relative}  " +
                $"{expectedFile.LengthBytes:N0} bytes  " +
                $"{actualSha}"
            );

            verified.Add(
                new ManifestEntry(
                    relative,
                    expectedFile.LengthBytes,
                    actualSha
                )
            );
        }

        return verified;
    }

    private void VerifyOne(
        DbiMtpClient dbi,
        string remoteFile,
        string downloaded,
        long expectedBytes,
        string expectedSha,
        string label
    )
    {
        if (File.Exists(
                downloaded
            ))
        {
            File.Delete(
                downloaded
            );
        }

        dbi.DownloadFile(
            remoteFile,
            downloaded
        );

        var info =
            new FileInfo(
                downloaded
            );

        if (info.Length !=
            expectedBytes)
        {
            throw new IOException(
                $"{label}: tamanho divergente. " +
                $"Esperado={expectedBytes}; Atual={info.Length}."
            );
        }

        var actualSha =
            _sha256Service.CalculateFile(
                downloaded
            );

        if (!string.Equals(
                actualSha,
                expectedSha,
                StringComparison.OrdinalIgnoreCase
            ))
        {
            throw new IOException(
                $"{label}: SHA-256 divergente."
            );
        }
    }

    private IReadOnlyList<ManifestEntry>
        BuildManifest(
            string payloadRoot
        )
    {
        return Directory
            .EnumerateFiles(
                payloadRoot,
                "*",
                SearchOption.AllDirectories
            )
            .Select(
                path =>
                {
                    var relative =
                        NormalizeRelativePath(
                            Path.GetRelativePath(
                                payloadRoot,
                                path
                            )
                        );

                    var length =
                        checked(
                            (ulong)
                            new FileInfo(
                                path
                            ).Length
                        );

                    var sha =
                        _sha256Service
                            .CalculateFile(
                                path
                            );

                    return new ManifestEntry(
                        relative,
                        length,
                        sha
                    );
                }
            )
            .OrderBy(
                entry =>
                    entry.RelativePath,
                StringComparer.OrdinalIgnoreCase
            )
            .ToArray();
    }

    private static void WritePattern(
        string path,
        int length,
        int seed
    )
    {
        var bytes =
            new byte[length];

        for (var index = 0;
             index < bytes.Length;
             index++)
        {
            bytes[index] =
                (byte)(
                    (
                        index * 37 +
                        seed
                    ) %
                    251
                );
        }

        File.WriteAllBytes(
            path,
            bytes
        );
    }

    private static string NormalizeRelativePath(
        string path
    )
    {
        return path
            .Replace(
                '\\',
                '/'
            )
            .TrimStart(
                '/'
            );
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
