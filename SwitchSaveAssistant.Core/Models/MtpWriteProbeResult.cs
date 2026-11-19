namespace SwitchSaveAssistant.Core.Models;

public sealed record MtpWriteProbeResult(
    string RemoteDirectory,
    string RemoteFile,
    long Bytes,
    string SourceSha256,
    string DownloadedSha256,
    bool RemoteCleanupConfirmed
);
