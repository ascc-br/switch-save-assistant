namespace SwitchSaveAssistant.Core.Models;

public sealed record ShellDropReplaceProbeResult(
    string RemoteDirectory,
    long OriginalBytes,
    long ReplacementBytes,
    string OriginalSha256,
    string ReplacementSha256,
    string GrownSha256,
    string ShrunkSha256,
    uint FirstDropEffect,
    uint SecondDropEffect,
    bool CleanupConfirmed
);
