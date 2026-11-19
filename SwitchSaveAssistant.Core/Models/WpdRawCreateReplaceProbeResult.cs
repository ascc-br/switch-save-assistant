namespace SwitchSaveAssistant.Core.Models;

public sealed record WpdRawCreateReplaceProbeResult(
    string RemoteDirectory,
    long OriginalBytes,
    long ReplacementBytes,
    string OriginalSha256,
    string ReplacementSha256,
    string AfterGrowSha256,
    string AfterShrinkSha256,
    int MatchingFilesAfterGrow,
    int MatchingFilesAfterShrink,
    bool CleanupConfirmed
);
