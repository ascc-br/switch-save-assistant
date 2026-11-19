namespace SwitchSaveAssistant.Core.Models;

public sealed record ShellDropCapabilityResult(
    bool DestinationDropTargetAvailable,
    int DestinationDropTargetHResult,
    bool SourceDataObjectAvailable,
    int SourceDataObjectHResult
);
