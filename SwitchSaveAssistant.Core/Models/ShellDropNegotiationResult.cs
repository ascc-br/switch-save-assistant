namespace SwitchSaveAssistant.Core.Models;

public sealed record ShellDropNegotiationResult(
    bool DestinationDropTargetAvailable,
    bool SourceDataObjectAvailable,
    int DragEnterHResult,
    uint RequestedEffect,
    uint AcceptedEffect,
    int DragLeaveHResult,
    bool DropExecuted
);
