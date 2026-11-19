namespace SwitchSaveAssistant.Core.Models;

public sealed record MtpReplaceProbeResult(
    string RemoteDirectory,
    long OriginalBytes,
    long ReplacementBytes,
    string OriginalSha256,
    string ReplacementSha256,
    string FinalSha256,
    bool CleanupConfirmed
);
