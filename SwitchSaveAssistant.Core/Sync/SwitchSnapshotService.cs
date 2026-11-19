using SwitchSaveAssistant.Core.Hashing;
using SwitchSaveAssistant.Core.Models;
using SwitchSaveAssistant.Core.Mtp;

namespace SwitchSaveAssistant.Core.Sync;

public sealed class SwitchSnapshotService
{
    private readonly Sha256Service _sha256Service = new();

    public IReadOnlyDictionary<string, string> Create(
        DbiMtpClient client,
        RemoteSave save,
        Action<RemoteFile>? onFileStarted = null
    )
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(save);

        var result = new Dictionary<string, string>(
            StringComparer.Ordinal
        );

        var tempDirectory = Directory
            .CreateTempSubdirectory(
                "SwitchSaveAssistant-"
            )
            .FullName;

        try
        {
            var remoteFiles = client.EnumerateFiles(
                save.RemotePath
            );

            foreach (var remoteFile in remoteFiles)
            {
                onFileStarted?.Invoke(
                    remoteFile
                );

                var tempFile = Path.Combine(
                    tempDirectory,
                    $"{Guid.NewGuid():N}.bin"
                );

                client.DownloadFile(
                    remoteFile.RemotePath,
                    tempFile
                );

                var localInfo = new FileInfo(
                    tempFile
                );

                if ((ulong)localInfo.Length
                    != remoteFile.LengthBytes)
                {
                    throw new IOException(
                        $"Tamanho divergente em '{remoteFile.RelativePath}'. " +
                        $"Switch={remoteFile.LengthBytes}, " +
                        $"PC={localInfo.Length}."
                    );
                }

                var sha256 = _sha256Service
                    .CalculateFile(tempFile);

                var relativePath = remoteFile
                    .RelativePath
                    .Replace('\\', '/');

                if (!result.TryAdd(
                        relativePath,
                        sha256
                    ))
                {
                    throw new InvalidOperationException(
                        $"Arquivo remoto duplicado: '{relativePath}'."
                    );
                }

                File.Delete(tempFile);
            }

            return result;
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(
                    tempDirectory,
                    recursive: true
                );
            }
        }
    }
}
