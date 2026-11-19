namespace SwitchSaveAssistant.Core.Models;

public sealed record ShellDropCreateProbeResult(
    string RemoteDirectory,
    long Bytes,
    string SourceSha256,
    string RemoteSha256,
    uint DropEffect,
    bool CleanupConfirmed
);
