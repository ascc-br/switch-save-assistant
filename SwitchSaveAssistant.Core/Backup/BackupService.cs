using SwitchSaveAssistant.Core.Hashing;
using SwitchSaveAssistant.Core.Models;
using SwitchSaveAssistant.Core.Mtp;
using SwitchSaveAssistant.Core.Safety;

namespace SwitchSaveAssistant.Core.Backup;

public sealed class BackupService
{
    private readonly Sha256Service _sha256Service;
    private readonly ManifestService _manifestService;

    public BackupService()
    {
        _sha256Service = new Sha256Service();
        _manifestService = new ManifestService();
    }

    public BackupResult Backup(
        DbiMtpClient client,
        RemoteSave save,
        string destinationPath,
        Action<ManifestEntry>? onFileCompleted = null
    )
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(save);

        var payloadPath = Path.Combine(
            destinationPath,
            "payload"
        );

        Directory.CreateDirectory(
            payloadPath
        );

        var remoteFiles = client.EnumerateFiles(
            save.RemotePath
        );

        var manifest = new List<ManifestEntry>();
        ulong totalBytes = 0;

        foreach (var remoteFile in remoteFiles)
        {
            var localFile =
                PathSafetyService.ResolveChildPath(
                    payloadPath,
                    remoteFile.RelativePath
                );

            var localDirectory =
                Path.GetDirectoryName(
                    localFile
                );

            if (!string.IsNullOrWhiteSpace(
                    localDirectory
                ))
            {
                Directory.CreateDirectory(
                    localDirectory
                );
            }

            client.DownloadFile(
                remoteFile.RemotePath,
                localFile
            );

            var localInfo = new FileInfo(
                localFile
            );

            if ((ulong)localInfo.Length
                != remoteFile.LengthBytes)
            {
                throw new IOException(
                    $"Tamanho divergente em '{remoteFile.RelativePath}'. " +
                    $"Switch={remoteFile.LengthBytes} bytes, " +
                    $"PC={localInfo.Length} bytes."
                );
            }

            var sha256 = _sha256Service
                .CalculateFile(
                    localFile
                );

            var safeRelativePath = Path
                .GetRelativePath(
                    payloadPath,
                    localFile
                )
                .Replace('\\', '/');

            var entry = new ManifestEntry(
                safeRelativePath,
                remoteFile.LengthBytes,
                sha256
            );

            manifest.Add(
                entry
            );

            totalBytes +=
                remoteFile.LengthBytes;

            onFileCompleted?.Invoke(
                entry
            );
        }

        var manifestPath =
            _manifestService.Write(
                destinationPath,
                manifest
            );

        return new BackupResult(
            destinationPath,
            payloadPath,
            manifestPath,
            manifest.Count,
            totalBytes,
            manifest
        );
    }
}
