namespace SwitchSaveAssistant.Core.Models;

public sealed record ShellReplaceProbeResult(
    string RemoteDirectory,
    long OriginalBytes,
    long ReplacementBytes,
    string OriginalSha256,
    string ReplacementSha256,
    string AfterGrowSha256,
    string AfterShrinkSha256,
    bool CleanupConfirmed
);
