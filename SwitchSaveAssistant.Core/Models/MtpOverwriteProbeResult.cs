namespace SwitchSaveAssistant.Core.Models;

public sealed record MtpOverwriteProbeResult(
    string RemoteFile,
    long OriginalBytes,
    long ReplacementBytes,
    string OriginalSha256,
    string ReplacementSha256,
    string AfterGrowSha256,
    string AfterShrinkSha256,
    bool CleanupConfirmed
);
