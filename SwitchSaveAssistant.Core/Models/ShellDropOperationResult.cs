namespace SwitchSaveAssistant.Core.Models;

public sealed record ShellDropOperationResult(
    int DragEnterHResult,
    uint RequestedEffect,
    uint AcceptedEnterEffect,
    int DropHResult,
    uint AcceptedDropEffect
);
