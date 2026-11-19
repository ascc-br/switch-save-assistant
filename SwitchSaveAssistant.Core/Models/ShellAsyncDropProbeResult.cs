namespace SwitchSaveAssistant.Core.Models;

public sealed record ShellAsyncDropProbeResult(
    bool AsyncCapabilityAvailable,
    int SetAsyncModeHResult,
    int GetAsyncModeHResult,
    bool AsyncModeEnabled,
    int DragEnterHResult,
    int DropHResult,
    uint DropEffect,
    int InOperationHResult,
    bool WasAsyncOperationObserved,
    bool RemoteFileAppeared,
    long RemoteBytes,
    string? RemoteSha256,
    bool CleanupConfirmed
);
