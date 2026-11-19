using System.Text;
using SwitchSaveAssistant.Core.Hashing;
using SwitchSaveAssistant.Core.Models;
using SwitchSaveAssistant.Core.Mtp;

namespace SwitchSaveAssistant.Core.Safety;

public sealed class MtpWriteProbeService
{
    private readonly Sha256Service _sha256Service = new();

    public MtpWriteProbeResult Run(
        DbiMtpClient client,
        Action<string>? progress = null
    )
    {
        ArgumentNullException.ThrowIfNull(client);

        var sdRoot = client.GetDriveRootPath(
            "1: SD Card"
        );

        var probeId =
            DateTime.Now.ToString(
                "yyyyMMdd-HHmmss"
            )
            + "-"
            + Guid.NewGuid()
                .ToString("N")[..8];

        var remoteDirectory = CombineRemotePath(
            sdRoot,
            "SwitchSaveAssistant-Probe",
            probeId
        );

        var remoteFile = CombineRemotePath(
            remoteDirectory,
            "probe.bin"
        );

        var localDirectory =
            Directory
                .CreateTempSubdirectory(
                    "SwitchSaveAssistant-MtpProbe-"
                )
                .FullName;

        var sourceFile = Path.Combine(
            localDirectory,
            "source.bin"
        );

        var downloadedFile = Path.Combine(
            localDirectory,
            "downloaded.bin"
        );

        try
        {
            progress?.Invoke(
                $"Criando diretório remoto: {remoteDirectory}"
            );

            client.CreateDirectory(
                remoteDirectory
            );

            var content = Encoding.UTF8.GetBytes(
                $"SwitchSaveAssistant MTP write probe\n" +
                $"ID={probeId}\n" +
                $"UTC={DateTimeOffset.UtcNow:O}\n"
            );

            File.WriteAllBytes(
                sourceFile,
                content
            );

            var sourceSha256 =
                _sha256Service.CalculateFile(
                    sourceFile
                );

            progress?.Invoke(
                "Enviando arquivo de teste..."
            );

            client.UploadNewFile(
                sourceFile,
                remoteFile
            );

            if (!client.FileExists(remoteFile))
            {
                throw new IOException(
                    "O arquivo enviado não apareceu no dispositivo."
                );
            }

            progress?.Invoke(
                "Lendo o arquivo de volta..."
            );

            client.DownloadFile(
                remoteFile,
                downloadedFile
            );

            var downloadedSha256 =
                _sha256Service.CalculateFile(
                    downloadedFile
                );

            if (!string.Equals(
                    sourceSha256,
                    downloadedSha256,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                throw new IOException(
                    "SHA-256 divergente após upload/download MTP."
                );
            }

            progress?.Invoke(
                "Removendo arquivo remoto de teste..."
            );

            client.DeleteFile(
                remoteFile
            );

            progress?.Invoke(
                "Removendo diretório remoto de teste..."
            );

            client.DeleteDirectory(
                remoteDirectory,
                recursive: false
            );

            var cleanupConfirmed =
                !client.FileExists(remoteFile) &&
                !client.DirectoryExists(remoteDirectory);

            if (!cleanupConfirmed)
            {
                throw new IOException(
                    "A limpeza remota não pôde ser confirmada."
                );
            }

            return new MtpWriteProbeResult(
                remoteDirectory,
                remoteFile,
                content.LongLength,
                sourceSha256,
                downloadedSha256,
                cleanupConfirmed
            );
        }
        finally
        {
            if (Directory.Exists(localDirectory))
            {
                Directory.Delete(
                    localDirectory,
                    recursive: true
                );
            }
        }
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

        var result = parts[0]
            .TrimEnd('\\', '/');

        foreach (var part in parts.Skip(1))
        {
            result += "\\" +
                part.Trim('\\', '/');
        }

        return result;
    }
}
