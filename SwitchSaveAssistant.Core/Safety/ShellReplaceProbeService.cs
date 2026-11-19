using System.Text;
using SwitchSaveAssistant.Core.Hashing;
using SwitchSaveAssistant.Core.Models;
using SwitchSaveAssistant.Core.Mtp;
using SwitchSaveAssistant.Core.Providers;

namespace SwitchSaveAssistant.Core.Safety;

public sealed class ShellReplaceProbeService
{
    private readonly Sha256Service _sha256Service =
        new();

    public ShellReplaceProbeResult Run(
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

        var directoryName =
            $"shell-replace-{probeId}";

        var localRoot =
            Directory
                .CreateTempSubdirectory(
                    "SwitchSaveAssistant-ShellReplace-"
                )
                .FullName;

        var sourceDirectory =
            Path.Combine(
                localRoot,
                directoryName
            );

        Directory.CreateDirectory(
            sourceDirectory
        );

        var sourceFile =
            Path.Combine(
                sourceDirectory,
                "target.bin"
            );

        var downloadedFile =
            Path.Combine(
                localRoot,
                "downloaded.bin"
            );

        var original =
            Encoding.UTF8.GetBytes(
                "SwitchSaveAssistant SHELL ORIGINAL\n" +
                $"ID={probeId}\n" +
                new string(
                    'A',
                    512
                )
            );

        var replacement =
            Encoding.UTF8.GetBytes(
                "SwitchSaveAssistant SHELL REPLACEMENT\n" +
                $"ID={probeId}\n" +
                new string(
                    'B',
                    16 * 1024
                )
            );

        File.WriteAllBytes(
            sourceFile,
            original
        );

        var originalSha =
            _sha256Service.CalculateFile(
                sourceFile
            );

        string replacementSha;

        string sdRoot;
        string remoteDirectory = string.Empty;
        string remoteFile = string.Empty;

        var completed = false;

        try
        {
            progress?.Invoke(
                "Preparando arquivo ORIGINAL via MediaDevices..."
            );

            using (var dbi =
                   connectDbi())
            {
                sdRoot =
                    dbi.GetDriveRootPath(
                        "1: SD Card"
                    );

                remoteDirectory =
                    CombineRemotePath(
                        sdRoot,
                        directoryName
                    );

                remoteFile =
                    CombineRemotePath(
                        remoteDirectory,
                        "target.bin"
                    );

                if (dbi.DirectoryExists(
                        remoteDirectory
                    ))
                {
                    throw new IOException(
                        $"Diretório remoto inesperadamente já existe: {remoteDirectory}"
                    );
                }

                dbi.CreateDirectory(
                    remoteDirectory
                );

                dbi.UploadNewFile(
                    sourceFile,
                    remoteFile
                );

                var initial =
                    ReadBack(
                        dbi,
                        remoteFile,
                        downloadedFile
                    );

                if (initial.Sha256 !=
                        originalSha ||
                    initial.Length !=
                        original.LongLength)
                {
                    throw new IOException(
                        "ORIGINAL remoto não foi confirmado."
                    );
                }
            }

            progress?.Invoke(
                "ORIGINAL confirmado. MediaDevices desconectado."
            );

            File.WriteAllBytes(
                sourceFile,
                replacement
            );

            replacementSha =
                _sha256Service.CalculateFile(
                    sourceFile
                );

            progress?.Invoke(
                "Windows Shell: substituindo ORIGINAL pela versão MAIOR..."
            );

            new ShellMtpCopyProvider()
                .CopyDirectoryToStorage(
                    "Switch",
                    "1: SD Card",
                    sourceDirectory
                );

            progress?.Invoke(
                "Shell terminou. Verificando versão MAIOR via MediaDevices..."
            );

            string grownSha;

            using (var dbi =
                   connectDbi())
            {
                var grown =
                    ReadBack(
                        dbi,
                        remoteFile,
                        downloadedFile
                    );

                grownSha =
                    grown.Sha256;

                if (grown.Sha256 !=
                        replacementSha ||
                    grown.Length !=
                        replacement.LongLength)
                {
                    throw new IOException(
                        "Substituição Shell de crescimento não foi confirmada."
                    );
                }
            }

            progress?.Invoke(
                "Crescimento confirmado por tamanho + SHA-256."
            );

            File.WriteAllBytes(
                sourceFile,
                original
            );

            progress?.Invoke(
                "Windows Shell: substituindo novamente pela versão MENOR..."
            );

            new ShellMtpCopyProvider()
                .CopyDirectoryToStorage(
                    "Switch",
                    "1: SD Card",
                    sourceDirectory
                );

            progress?.Invoke(
                "Shell terminou. Verificando versão MENOR via MediaDevices..."
            );

            string shrunkSha;
            bool cleanupConfirmed;

            using (var dbi =
                   connectDbi())
            {
                var shrunk =
                    ReadBack(
                        dbi,
                        remoteFile,
                        downloadedFile
                    );

                shrunkSha =
                    shrunk.Sha256;

                if (shrunk.Sha256 !=
                        originalSha ||
                    shrunk.Length !=
                        original.LongLength)
                {
                    throw new IOException(
                        "Substituição Shell de redução não foi confirmada."
                    );
                }

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
                        "Limpeza remota não foi confirmada."
                    );
                }
            }

            var result =
                new ShellReplaceProbeResult(
                    remoteDirectory,
                    original.LongLength,
                    replacement.LongLength,
                    originalSha,
                    replacementSha,
                    grownSha,
                    shrunkSha,
                    cleanupConfirmed
                );

            completed = true;

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
                    // Dá ao namespace Shell/MTP tempo
                    // para liberar a operação anterior.
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
                Directory.Delete(
                    localRoot,
                    recursive: true
                );
            }
        }
    }

    private (
        string Sha256,
        long Length
    ) ReadBack(
        DbiMtpClient dbi,
        string remoteFile,
        string localFile
    )
    {
        if (File.Exists(localFile))
        {
            File.Delete(
                localFile
            );
        }

        dbi.DownloadFile(
            remoteFile,
            localFile
        );

        return (
            _sha256Service.CalculateFile(
                localFile
            ),
            new FileInfo(
                localFile
            ).Length
        );
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
}
