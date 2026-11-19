using System.Text;
using SwitchSaveAssistant.Core.Hashing;
using SwitchSaveAssistant.Core.Models;
using SwitchSaveAssistant.Core.Mtp;

namespace SwitchSaveAssistant.Core.Safety;

public sealed class MtpReplaceProbeService
{
    private readonly Sha256Service _sha256Service = new();

    public MtpReplaceProbeResult Run(
        DbiMtpClient client,
        Action<string>? progress = null
    )
    {
        ArgumentNullException.ThrowIfNull(
            client
        );

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

        var remoteDirectory =
            CombineRemotePath(
                sdRoot,
                "SwitchSaveAssistant-Probe",
                $"replace-{probeId}"
            );

        const string targetName =
            "target.bin";

        var stagedName =
            $"target.new-{probeId}.bin";

        var oldName =
            $"target.old-{probeId}.bin";

        var targetRemote =
            CombineRemotePath(
                remoteDirectory,
                targetName
            );

        var stagedRemote =
            CombineRemotePath(
                remoteDirectory,
                stagedName
            );

        var oldRemote =
            CombineRemotePath(
                remoteDirectory,
                oldName
            );

        var localDirectory =
            Directory
                .CreateTempSubdirectory(
                    "SwitchSaveAssistant-ReplaceProbe-"
                )
                .FullName;

        var originalLocal =
            Path.Combine(
                localDirectory,
                "original.bin"
            );

        var replacementLocal =
            Path.Combine(
                localDirectory,
                "replacement.bin"
            );

        var downloadedLocal =
            Path.Combine(
                localDirectory,
                "downloaded.bin"
            );

        var completed = false;

        try
        {
            var originalBytes =
                Encoding.UTF8.GetBytes(
                    "SwitchSaveAssistant ORIGINAL\n" +
                    $"ID={probeId}\n"
                );

            var replacementBytes =
                Encoding.UTF8.GetBytes(
                    "SwitchSaveAssistant REPLACEMENT\n" +
                    $"ID={probeId}\n"
                );

            File.WriteAllBytes(
                originalLocal,
                originalBytes
            );

            File.WriteAllBytes(
                replacementLocal,
                replacementBytes
            );

            var originalSha =
                _sha256Service.CalculateFile(
                    originalLocal
                );

            var replacementSha =
                _sha256Service.CalculateFile(
                    replacementLocal
                );

            progress?.Invoke(
                "Criando área remota descartável..."
            );

            client.CreateDirectory(
                remoteDirectory
            );

            progress?.Invoke(
                "Enviando versão ORIGINAL..."
            );

            client.UploadNewFile(
                originalLocal,
                targetRemote
            );

            VerifyRemoteHash(
                client,
                targetRemote,
                downloadedLocal,
                originalSha,
                "ORIGINAL"
            );

            progress?.Invoke(
                "ORIGINAL confirmado por SHA-256."
            );

            progress?.Invoke(
                "Enviando versão NOVA com nome temporário..."
            );

            client.UploadNewFile(
                replacementLocal,
                stagedRemote
            );

            VerifyRemoteHash(
                client,
                stagedRemote,
                downloadedLocal,
                replacementSha,
                "STAGED"
            );

            progress?.Invoke(
                "NOVA versão confirmada por SHA-256."
            );

            progress?.Invoke(
                "Renomeando original para cópia de rollback..."
            );

            client.Rename(
                targetRemote,
                oldName
            );

            if (!client.FileExists(oldRemote))
            {
                throw new IOException(
                    "Arquivo antigo não apareceu com o nome de rollback."
                );
            }

            progress?.Invoke(
                "Promovendo versão NOVA para o nome definitivo..."
            );

            client.Rename(
                stagedRemote,
                targetName
            );

            if (!client.FileExists(targetRemote))
            {
                throw new IOException(
                    "Arquivo novo não apareceu com o nome definitivo."
                );
            }

            progress?.Invoke(
                "Verificando versão definitiva..."
            );

            var finalSha =
                GetRemoteHash(
                    client,
                    targetRemote,
                    downloadedLocal
                );

            if (!string.Equals(
                    finalSha,
                    replacementSha,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                throw new IOException(
                    "SHA-256 final não corresponde à versão nova."
                );
            }

            progress?.Invoke(
                "Versão nova confirmada. Removendo rollback..."
            );

            client.DeleteFile(
                oldRemote
            );

            progress?.Invoke(
                "Limpando arquivo definitivo de teste..."
            );

            client.DeleteFile(
                targetRemote
            );

            client.DeleteDirectory(
                remoteDirectory,
                recursive: false
            );

            var cleanupConfirmed =
                !client.FileExists(targetRemote) &&
                !client.FileExists(stagedRemote) &&
                !client.FileExists(oldRemote) &&
                !client.DirectoryExists(remoteDirectory);

            if (!cleanupConfirmed)
            {
                throw new IOException(
                    "A limpeza do ensaio não pôde ser confirmada."
                );
            }

            completed = true;

            return new MtpReplaceProbeResult(
                remoteDirectory,
                originalBytes.LongLength,
                replacementBytes.LongLength,
                originalSha,
                replacementSha,
                finalSha,
                cleanupConfirmed
            );
        }
        finally
        {
            // Em caso de falha, a pasta é exclusiva deste probe.
            // Podemos removê-la recursivamente sem atingir outros dados.
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
                    // O erro original continua sendo o mais importante.
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

    private string GetRemoteHash(
        DbiMtpClient client,
        string remoteFile,
        string localTemporaryFile
    )
    {
        if (File.Exists(
                localTemporaryFile
            ))
        {
            File.Delete(
                localTemporaryFile
            );
        }

        client.DownloadFile(
            remoteFile,
            localTemporaryFile
        );

        return _sha256Service
            .CalculateFile(
                localTemporaryFile
            );
    }

    private void VerifyRemoteHash(
        DbiMtpClient client,
        string remoteFile,
        string localTemporaryFile,
        string expectedSha256,
        string label
    )
    {
        var actual =
            GetRemoteHash(
                client,
                remoteFile,
                localTemporaryFile
            );

        if (!string.Equals(
                actual,
                expectedSha256,
                StringComparison.OrdinalIgnoreCase
            ))
        {
            throw new IOException(
                $"SHA-256 divergente para {label}. " +
                $"Esperado={expectedSha256}, " +
                $"Atual={actual}."
            );
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
