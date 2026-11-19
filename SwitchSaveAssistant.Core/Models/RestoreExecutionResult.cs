namespace SwitchSaveAssistant.Core.Models;

public sealed record RestoreExecutionResult(
    RestoreExecutionStatus Status,
    string Message,
    bool RequiresRescan
)
{
    public bool Success =>
        Status == RestoreExecutionStatus.Completed;
}
