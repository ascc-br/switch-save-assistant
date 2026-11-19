using System.Text;
using SwitchSaveAssistant.Core.Hashing;
using SwitchSaveAssistant.Core.Models;
using SwitchSaveAssistant.Core.Mtp;

namespace SwitchSaveAssistant.Core.Safety;

public sealed class MtpOverwriteProbeService
{
    private readonly Sha256Service _sha256Service = new();

    public MtpOverwriteProbeResult Run(
        DbiMtpClient client,
        Action<string>? progress = null
    )
    {
        ArgumentNullException.ThrowIfNull(
            client
        );

        var sdRoot =
            client.GetDriveRootPath(
                "1: SD Card"
            );

        var probeId =
            DateTime.Now.ToString(
                "yyyyMMdd-HHmmss"
            )
            + "-"
            + Guid.NewGuid()
                .ToString("N")[..8];

        var remoteDirectory =
            CombineRemotePath(
                sdRoot,
                "SwitchSaveAssistant-Probe",
                $"overwrite-{probeId}"
            );

        var remoteFile =
            CombineRemotePath(
                remoteDirectory,
                "target.bin"
            );

        var localDirectory =
            Directory
                .CreateTempSubdirectory(
                    "SwitchSaveAssistant-OverwriteProbe-"
                )
                .FullName;

        var originalFile =
            Path.Combine(
                localDirectory,
                "original.bin"
            );

        var replacementFile =
            Path.Combine(
                localDirectory,
                "replacement.bin"
            );

        var downloadedFile =
            Path.Combine(
                localDirectory,
                "downloaded.bin"
            );

        var completed = false;

        try
        {
            var original =
                Encoding.UTF8.GetBytes(
                    "SwitchSaveAssistant ORIGINAL\n" +
                    $"ID={probeId}\n" +
                    new string('A', 257)
                );

            var replacement =
                Encoding.UTF8.GetBytes(
                    "SwitchSaveAssistant REPLACEMENT\n" +
                    $"ID={probeId}\n" +
                    new string('B', 8192)
                );

            File.WriteAllBytes(
                originalFile,
                original
            );

            File.WriteAllBytes(
                replacementFile,
                replacement
            );

            var originalSha =
                _sha256Service.CalculateFile(
                    originalFile
                );

            var replacementSha =
                _sha256Service.CalculateFile(
                    replacementFile
                );

            progress?.Invoke(
                "Criando diretório remoto descartável..."
            );

            client.CreateDirectory(
                remoteDirectory
            );

            progress?.Invoke(
                "Criando arquivo ORIGINAL..."
            );

            client.UploadNewFile(
                originalFile,
                remoteFile
            );

            var initial =
                ReadBack(
                    client,
                    remoteFile,
                    downloadedFile
                );

            if (initial.Sha256 != originalSha ||
                initial.Length != original.LongLength)
            {
                throw new IOException(
                    "A versão ORIGINAL não foi confirmada."
                );
            }

            progress?.Invoke(
                "ORIGINAL confirmado."
            );

            progress?.Invoke(
                "Sobrescrevendo o MESMO objeto com versão maior..."
            );

            client.OverwriteExistingFile(
                replacementFile,
                remoteFile
            );

            var grown =
                ReadBack(
                    client,
                    remoteFile,
                    downloadedFile
                );

            if (grown.Sha256 != replacementSha ||
                grown.Length != replacement.LongLength)
            {
                throw new IOException(
                    "Overwrite de crescimento não foi confirmado."
                );
            }

            progress?.Invoke(
                "Crescimento confirmado por tamanho + SHA-256."
            );

            progress?.Invoke(
                "Sobrescrevendo o MESMO objeto com versão menor..."
            );

            client.OverwriteExistingFile(
                originalFile,
                remoteFile
            );

            var shrunk =
                ReadBack(
                    client,
                    remoteFile,
                    downloadedFile
                );

            if (shrunk.Sha256 != originalSha ||
                shrunk.Length != original.LongLength)
            {
                throw new IOException(
                    "Overwrite de redução não foi confirmado."
                );
            }

            progress?.Invoke(
                "Redução confirmada por tamanho + SHA-256."
            );

            progress?.Invoke(
                "Removendo arquivo remoto de teste..."
            );

            client.DeleteFile(
                remoteFile
            );

            client.DeleteDirectory(
                remoteDirectory,
                recursive: false
            );

            var cleanupConfirmed =
                !client.FileExists(
                    remoteFile
                ) &&
                !client.DirectoryExists(
                    remoteDirectory
                );

            if (!cleanupConfirmed)
            {
                throw new IOException(
                    "Limpeza remota não confirmada."
                );
            }

            completed = true;

            return new MtpOverwriteProbeResult(
                remoteFile,
                original.LongLength,
                replacement.LongLength,
                originalSha,
                replacementSha,
                grown.Sha256,
                shrunk.Sha256,
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
                        client.DeleteDirectory(
                            remoteDirectory,
                            recursive: true
                        );
                    }
                }
                catch
                {
                    // Preserva o erro original.
                }
            }

            if (Directory.Exists(
                    localDirectory
                ))
            {
                Directory.Delete(
                    localDirectory,
                    recursive: true
                );
            }
        }
    }

    private (
        string Sha256,
        long Length
    ) ReadBack(
        DbiMtpClient client,
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

        client.DownloadFile(
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

        foreach (var part in parts.Skip(1))
        {
            result += "\\" +
                part.Trim(
                    '\\',
                    '/'
                );
        }

        return result;
    }
}
